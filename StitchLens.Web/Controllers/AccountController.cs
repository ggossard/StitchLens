using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Models;
using System.Security.Claims;   

namespace StitchLens.Web.Controllers;

public class AccountController : Controller {
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly StitchLensDbContext _context;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        StitchLensDbContext context) {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
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

        var model = new MyAccountViewModel {
            Email = user.Email!,
            PlanType = user.PlanType,
            UserType = user.UserType,
            CreatedAt = user.CreatedAt,
            PatternCount = await _context.Projects.CountAsync(p => p.UserId == user.Id)
        };

        return View(model);
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


}