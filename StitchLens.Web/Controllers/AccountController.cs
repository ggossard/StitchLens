using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Models;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;

namespace StitchLens.Web.Controllers;

public class AccountController : Controller {
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly StitchLensDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IConfiguration _configuration;
    private readonly ITierConfigurationService _tierConfigService;

    public AccountController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    StitchLensDbContext context,
    ISubscriptionService subscriptionService,
    IConfiguration configuration,
    ITierConfigurationService tierConfigService) {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _subscriptionService = subscriptionService;
        _configuration = configuration;
        _tierConfigService = tierConfigService;

        // Initialize Stripe
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    // GET: /Account/Register
    [HttpGet]
    public IActionResult Register() {
        return View();
    }

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model) {
        if (ModelState.IsValid) {
            var user = new User {
                UserName = model.Email,
                Email = model.Email,
                Nickname = string.IsNullOrWhiteSpace(model.Nickname) ? null : model.Nickname.Trim(),
                UserType = UserType.Customer,
                PlanType = "Free",
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded) {
                // Sign the user in
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Redirect to pattern upload or home
                return RedirectToAction("Upload", "Pattern");
            }

            // Add errors to ModelState
            foreach (var error in result.Errors) {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null) {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid) {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded) {
                // Redirect to returnUrl or home
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut) {
                ModelState.AddModelError(string.Empty, "Account is locked out.");
            }
            else {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
        }

        return View(model);
    }

    // GET: /Account/MyPatterns
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyPatterns() {
        var userId = int.Parse(_userManager.GetUserId(User)!);

        var projects = await _context.Projects
            .Include(p => p.YarnBrand)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(projects);
    }

    // GET: /Account/MyAccount
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyAccount() {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) {
            return RedirectToAction("Login");
        }

        user = await _context.Users
            .Include(u => u.ActiveSubscription)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (user == null) {
            return RedirectToAction("Login");
        }

        var model = new MyAccountViewModel {
            Email = user.Email!,
            PlanType = user.PlanType,
            UserType = user.UserType,
            CreatedAt = user.CreatedAt,
            PatternCount = await _context.Projects.CountAsync(p => p.UserId == user.Id)
        };

        return View(model);
    }

    // GET: /Account/Dashboard
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Dashboard() {
        var user = await _userManager.Users
            .Include(u => u.ActiveSubscription)
            .FirstOrDefaultAsync(u => u.Id == int.Parse(_userManager.GetUserId(User)!));

        if (user == null) {
            return RedirectToAction("Login");
        }

        // Reset monthly counter if new month
        if (user.LastPatternCreationDate.Month != DateTime.UtcNow.Month ||
            user.LastPatternCreationDate.Year != DateTime.UtcNow.Year) {
            user.PatternsCreatedThisMonth = 0;
            user.LastPatternCreationDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Reset daily counter if new day
        if (user.LastPatternDate.Date != DateTime.UtcNow.Date) {
            user.PatternsCreatedToday = 0;
            user.LastPatternDate = DateTime.UtcNow; 
            await _context.SaveChangesAsync();
        }

        // Get subscription history
        var subscriptions = await _context.Subscriptions
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .ToListAsync();

        // Get recent payments
        var payments = await _context.PaymentHistory
            .Where(p => p.UserId == user.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync();

        var model = new DashboardViewModel {
            User = user,
            CurrentTier = user.CurrentTier,
            PatternsCreatedThisMonth = user.PatternsCreatedThisMonth,
            PatternCreationQuota = user.ActiveSubscription?.PatternCreationQuota
                 ?? await _tierConfigService.GetPatternCreationQuotaAsync(user.CurrentTier),
            PatternsCreatedToday = user.PatternsCreatedToday,
            NextBillingDate = user.ActiveSubscription?.NextBillingDate,
            MonthlyPrice = user.ActiveSubscription?.MonthlyPrice ?? 0,
            BillingCycle = user.ActiveSubscription?.BillingCycle,
            HasActiveSubscription = user.ActiveSubscription != null &&
                                    user.ActiveSubscription.Status == SubscriptionStatus.Active,
            RecentSubscriptions = subscriptions,
            RecentPayments = payments
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNickname(string? nickname) {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) {
            return RedirectToAction("Login");
        }

        var normalizedNickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();

        if (!string.IsNullOrEmpty(normalizedNickname) && normalizedNickname.Length > 50) {
            TempData["Error"] = "Nickname cannot exceed 50 characters.";
            return RedirectToAction("Dashboard");
        }

        user.Nickname = normalizedNickname;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded) {
            TempData["Success"] = "Nickname updated.";
        }
        else {
            TempData["Error"] = "Unable to update nickname right now.";
        }

        return RedirectToAction("Dashboard");
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied() {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl) {
        if (Url.IsLocalUrl(returnUrl)) {
            return Redirect(returnUrl);
        }
        else {
            return RedirectToAction("Index", "Home");
        }
    }


    // External login challenge
    [HttpPost]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null) {
        // Request a redirect to the external login provider
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    // External login callback
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null) {
        returnUrl = returnUrl ?? Url.Content("~/");

        if (remoteError != null) {
            ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
            return RedirectToAction(nameof(Login), new { ReturnUrl = returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null) {
            ModelState.AddModelError(string.Empty, "Error loading external login information.");
            return RedirectToAction(nameof(Login), new { ReturnUrl = returnUrl });
        }

        // Sign in the user with this external login provider if the user already has a login
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (result.Succeeded) {
            // User already has an account with this external login
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut) {
            return RedirectToAction(nameof(Login));
        }
        else {
            // User doesn't have an account yet - create one
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(email)) {
                ModelState.AddModelError(string.Empty, "Email not received from external provider.");
                return RedirectToAction(nameof(Login));
            }

            // Check if a user with this email already exists
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null) {
                // User exists with this email - link this external login to their account
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded) {
                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in addLoginResult.Errors) {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return RedirectToAction(nameof(Login));
            }

            // Create a new user
            var user = new User {
                UserName = email,
                Email = email,
                EmailConfirmed = true, // Trust the external provider
                UserType = UserType.Customer,
                PlanType = "Free",
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded) {
                createResult = await _userManager.AddLoginAsync(user, info);
                if (createResult.Succeeded) {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
            }

            foreach (var error in createResult.Errors) {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return RedirectToAction(nameof(Login));
        }
    }

    // GET: /Account/Subscribe?tier=Hobbyist&billingCycle=Monthly
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Subscribe(SubscriptionTier tier, BillingCycle billingCycle = BillingCycle.Monthly) {
        // Validate tier
        if (tier == SubscriptionTier.PayAsYouGo || tier == SubscriptionTier.Custom) {
            TempData["Error"] = "Please select a valid subscription plan.";
            return RedirectToAction("Pricing", "Home");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) {
            return RedirectToAction("Login");
        }

        // Check if already subscribed to this tier and cycle
        if (user.CurrentTier == tier && user.ActiveSubscription?.BillingCycle == billingCycle) {
            TempData["Info"] = "You're already subscribed to this plan!";
            return RedirectToAction("Dashboard");
        }

        var tierConfig = await _tierConfigService.GetConfigAsync(tier);
        var priceId = billingCycle == BillingCycle.Annual
            ? tierConfig.StripeAnnualPriceId
            : tierConfig.StripeMonthlyPriceId;

        if (string.IsNullOrEmpty(priceId)) {
            TempData["Error"] = billingCycle == BillingCycle.Annual
                ? "Annual billing is not configured for this plan yet."
                : "Invalid subscription plan configuration.";
            return RedirectToAction("Pricing", "Home", new { billingCycle });
        }

        try {
            // Create Stripe Checkout Session
            var options = new SessionCreateOptions {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "subscription",
                CustomerEmail = user.Email,
                ClientReferenceId = user.Id.ToString(),
                LineItems = new List<SessionLineItemOptions>
                {
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            },
                SuccessUrl = Url.Action("SubscribeSuccess", "Account", null, Request.Scheme) + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = Url.Action("Pricing", "Home", new { billingCycle }, Request.Scheme),
                Metadata = new Dictionary<string, string>
                {
                { "user_id", user.Id.ToString() },
                { "tier", tier.ToString() },
                { "billing_cycle", billingCycle.ToString() }
            }
            };

            // If user has Stripe customer ID, use it
            if (!string.IsNullOrEmpty(user.StripeCustomerId)) {
                options.Customer = user.StripeCustomerId;
                options.CustomerEmail = null; // Don't set email if using existing customer
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            // Redirect to Stripe Checkout
            return Redirect(session.Url);
        }
        catch (StripeException ex) {
            TempData["Error"] = $"Payment system error: {ex.Message}";
            return RedirectToAction("Pricing", "Home", new { billingCycle });
        }
    }

    // GET: /Account/SubscribeSuccess?session_id=...
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SubscribeSuccess(string session_id) {
        if (string.IsNullOrEmpty(session_id)) {
            TempData["Error"] = "Invalid session.";
            return RedirectToAction("Pricing");
        }

        try {
            // Retrieve session from Stripe
            var service = new SessionService();
            var session = await service.GetAsync(session_id);

            if (session.PaymentStatus == "paid") {
                var tierName = session.Metadata["tier"];
                TempData["Success"] = $"Welcome to {tierName} plan! Your subscription is now active.";
            }
            else {
                TempData["Warning"] = "Payment is processing. Your subscription will be active shortly.";
            }

            return RedirectToAction("Dashboard");
        }
        catch (StripeException ex) {
            TempData["Error"] = $"Error verifying payment: {ex.Message}";
            return RedirectToAction("Dashboard");
        }
    }

    // POST: /Account/CancelSubscription
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSubscription() {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) {
            return RedirectToAction("Login");
        }

        if (user.ActiveSubscriptionId == null) {
            TempData["Error"] = "No active subscription found.";
            return RedirectToAction("Dashboard");
        }

        try {
            await _subscriptionService.CancelSubscriptionAsync(
                user.ActiveSubscriptionId.Value,
                "User requested cancellation");

            TempData["Success"] = "Your subscription has been cancelled. You'll have access until the end of your billing period.";
        }
        catch (Exception ex) {
            TempData["Error"] = $"Error canceling subscription: {ex.Message}";
        }

        return RedirectToAction("Dashboard");
    }

}
