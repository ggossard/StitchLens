using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using StitchLens.Web.Models;
using StitchLens.Data.Models;

namespace StitchLens.Web.Controllers;

public class HomeController : Controller {
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<User> _userManager;

    public HomeController(
        ILogger<HomeController> logger,
        UserManager<User> userManager) {
        _logger = logger;
        _userManager = userManager;
    }

    public IActionResult Index() {
        return View();
    }

    public IActionResult Privacy() {
        return View();
    }

    public IActionResult Terms() {
        return View();
    }

    public async Task<IActionResult> Pricing() {
        var viewModel = new PricingViewModel {
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false
        };

        // Get current user's tier if authenticated
        if (viewModel.IsAuthenticated) {
            var user = await _userManager.GetUserAsync(User);
            viewModel.CurrentTier = user?.CurrentTier;
        }

        // Define pricing tiers
        viewModel.Tiers = new List<PricingTier>
        {
            new PricingTier
            {
                Name = "Pay As You Go",
                Description = "Perfect for trying out StitchLens",
                MonthlyPrice = 5.95m,
                PriceDisplay = "",
                Tier = SubscriptionTier.PayAsYouGo,
                IsPopular = false,
                Features = new List<string>
                {
                    "Sign Up & 1st one free",
                    "Community support"
                },
                ButtonText = viewModel.CurrentTier == SubscriptionTier.PayAsYouGo ? "Current Plan" : "Get Started Now",
                ButtonClass = "btn-outline-secondary",
                IsCurrent = viewModel.CurrentTier == SubscriptionTier.PayAsYouGo
            },
            new PricingTier
            {
                Name = "Hobbyist",
                Description = "Great for regular crafters",
                MonthlyPrice = 12.95m,
                PriceDisplay = "$12.95",
                Tier = SubscriptionTier.Hobbyist,
                IsPopular = true,
                Features = new List<string>
                {
                    "3 patterns created per month",
                    "Priority email support",
                    "Save unlimited patterns"
                },
                ButtonText = viewModel.CurrentTier == SubscriptionTier.Hobbyist ? "Current Plan" : "Subscribe Now",
                ButtonClass = "btn-primary",
                IsCurrent = viewModel.CurrentTier == SubscriptionTier.Hobbyist
            },
            new PricingTier
            {
                Name = "Creator",
                Description = "For professionals and Etsy sellers",
                MonthlyPrice = 35.95m,
                PriceDisplay = "$35.95",
                Tier = SubscriptionTier.Creator,
                IsPopular = false,
                Features = new List<string>
                {
                    "30 patterns created per month",
                    "Priority support (24hr response)",
                    "White-label options (coming soon)"
                },
                ButtonText = viewModel.CurrentTier == SubscriptionTier.Creator ? "Current Plan" : "Go Pro",
                ButtonClass = "btn-success",
                IsCurrent = viewModel.CurrentTier == SubscriptionTier.Creator
            },
            new PricingTier
            {
                Name = "Custom",
                Description = "Enterprise solutions for businesses",
                MonthlyPrice = 0,
                PriceDisplay = "Contact Us",
                Tier = SubscriptionTier.Custom,
                IsPopular = false,
                Features = new List<string>
                {
                    "Unlimited patterns created",
                    "Custom integrations",
                    "API access",
                    "White-label platform",
                    "SLA guarantee",
                    "Volume discounts",
                    "Custom features & development"
                },
                ButtonText = "Contact Sales",
                ButtonClass = "btn-outline-dark",
                IsCurrent = viewModel.CurrentTier == SubscriptionTier.Custom
            }
        };

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
