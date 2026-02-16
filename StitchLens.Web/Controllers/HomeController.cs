using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using StitchLens.Core.Services;
using StitchLens.Web.Models;
using StitchLens.Data.Models;

namespace StitchLens.Web.Controllers;

public class HomeController : Controller {
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<User> _userManager;
    private readonly ITierConfigurationService _tierConfigurationService;

    public HomeController(
        ILogger<HomeController> logger,
        UserManager<User> userManager,
        ITierConfigurationService tierConfigurationService) {
        _logger = logger;
        _userManager = userManager;
        _tierConfigurationService = tierConfigurationService;
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

        var configs = await _tierConfigurationService.GetAllConfigsAsync();

        viewModel.Tiers = configs
            .Select(config => MapPricingTier(config, viewModel.CurrentTier))
            .ToList();

        return View(viewModel);
    }

    private static PricingTier MapPricingTier(TierConfiguration config, SubscriptionTier? currentTier) {
        var isCurrent = currentTier == config.Tier;

        return new PricingTier {
            Name = config.Name,
            Description = config.Description,
            MonthlyPrice = config.MonthlyPrice,
            PriceDisplay = config.Tier == SubscriptionTier.Custom ? "Contact Us" : config.MonthlyPrice.ToString("0.00"),
            Tier = config.Tier,
            IsPopular = config.Tier == SubscriptionTier.Hobbyist,
            Features = BuildFeatures(config),
            ButtonText = BuildButtonText(config.Tier, isCurrent),
            ButtonClass = BuildButtonClass(config.Tier),
            IsCurrent = isCurrent
        };
    }

    private static List<string> BuildFeatures(TierConfiguration config) {
        if (config.Tier == SubscriptionTier.Custom) {
            return new List<string> {
                "Unlimited patterns created",
                "Custom integrations",
                "API access",
                "White-label platform",
                "SLA guarantee",
                "Volume discounts",
                "Custom features & development"
            };
        }

        if (config.Tier == SubscriptionTier.PayAsYouGo) {
            return new List<string> {
                "Sign up and create your first pattern free",
                "Pay per additional pattern",
                "Community support"
            };
        }

        var features = new List<string> {
            $"{config.PatternCreationQuota} patterns created per month",
            config.PrioritySupport ? "Priority support" : "Email support",
            "Save unlimited patterns"
        };

        if (config.AllowCommercialUse) {
            features.Add("Commercial use license included");
        }

        return features;
    }

    private static string BuildButtonText(SubscriptionTier tier, bool isCurrent) {
        if (tier == SubscriptionTier.Custom) {
            return "Contact Sales";
        }

        if (isCurrent) {
            return "Current Plan";
        }

        return tier switch {
            SubscriptionTier.PayAsYouGo => "Get Started",
            SubscriptionTier.Hobbyist => "Subscribe Now",
            SubscriptionTier.Creator => "Go Pro",
            _ => "Get Started"
        };
    }

    private static string BuildButtonClass(SubscriptionTier tier) {
        return tier switch {
            SubscriptionTier.PayAsYouGo => "btn-outline-secondary",
            SubscriptionTier.Hobbyist => "btn-primary",
            SubscriptionTier.Creator => "btn-success",
            SubscriptionTier.Custom => "btn-outline-dark",
            _ => "btn-primary"
        };
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
