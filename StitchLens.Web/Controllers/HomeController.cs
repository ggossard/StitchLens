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
                Name = "Free",
                Description = "Perfect for trying out StitchLens",
                MonthlyPrice = 0,
                PriceDisplay = "Free",
                Tier = SubscriptionTier.Free,
                IsPopular = false,
                Features = new List<string>
                {
                    "3 pattern downloads per month",
                    "Basic color matching",
                    "Standard resolution PDFs",
                    "DMC yarn colors only",
                    "Community support"
                },
                ButtonText = viewModel.CurrentTier == SubscriptionTier.Free ? "Current Plan" : "Get Started Free",
                ButtonClass = "btn-outline-secondary",
                IsCurrent = viewModel.CurrentTier == SubscriptionTier.Free
            },
            new PricingTier
            {
                Name = "Hobbyist",
                Description = "Great for regular crafters",
                MonthlyPrice = 12.99m,
                PriceDisplay = "$12.99",
                Tier = SubscriptionTier.Hobbyist,
                IsPopular = true,
                Features = new List<string>
                {
                    "10 pattern downloads per month",
                    "Advanced color matching",
                    "High resolution PDFs",
                    "All yarn brands (DMC, Appleton, Paternayan)",
                    "Priority email support",
                    "No daily creation limits",
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
                MonthlyPrice = 49.99m,
                PriceDisplay = "$49.99",
                Tier = SubscriptionTier.Creator,
                IsPopular = false,
                Features = new List<string>
                {
                    "100 pattern downloads per month",
                    "Commercial use license included",
                    "Ultra-high resolution PDFs",
                    "All yarn brands + custom palettes",
                    "Priority support (24hr response)",
                    "Batch processing (coming soon)",
                    "API access (coming soon)",
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
                    "Unlimited pattern downloads",
                    "Full commercial license",
                    "Dedicated account manager",
                    "Custom integrations",
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