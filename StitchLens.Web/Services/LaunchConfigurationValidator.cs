namespace StitchLens.Web.Services;

public static class LaunchConfigurationValidator {
    private static readonly string[] RequiredConfigurationKeys = {
        "ConnectionStrings:DefaultConnection",
        "Stripe:SecretKey",
        "Stripe:WebhookSecret"
    };

    public static void ValidateOrThrow(IConfiguration configuration, IWebHostEnvironment environment, ILogger logger) {
        var missingKeys = RequiredConfigurationKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToArray();

        if (missingKeys.Length > 0) {
            var missingKeyList = string.Join(", ", missingKeys);
            var message = $"Missing required configuration keys: {missingKeyList}";

            if (environment.IsDevelopment()) {
                logger.LogWarning("{Message}. Development mode allows startup for local iteration.", message);
            }
            else {
                throw new InvalidOperationException(message);
            }
        }

        var hasHobbyistAnnual = !string.IsNullOrWhiteSpace(configuration["Stripe:PriceIds:HobbyistAnnual"]);
        var hasCreatorAnnual = !string.IsNullOrWhiteSpace(configuration["Stripe:PriceIds:CreatorAnnual"]);

        if (!hasHobbyistAnnual || !hasCreatorAnnual) {
            logger.LogInformation(
                "Annual subscription checkout is disabled unless annual Stripe price IDs are configured. " +
                "HobbyistAnnualConfigured={HobbyistAnnualConfigured}, CreatorAnnualConfigured={CreatorAnnualConfigured}",
                hasHobbyistAnnual,
                hasCreatorAnnual);
        }
    }
}
