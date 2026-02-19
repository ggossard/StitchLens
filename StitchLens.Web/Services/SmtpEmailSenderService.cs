using System.Net;
using System.Net.Mail;

namespace StitchLens.Web.Services;

public class SmtpEmailSenderService : IEmailSenderService {
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SmtpEmailSenderService> _logger;

    public SmtpEmailSenderService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SmtpEmailSenderService> logger) {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink) {
        var smtpHost = _configuration["Email:Smtp:Host"];
        var smtpPortValue = _configuration["Email:Smtp:Port"];
        var smtpUser = _configuration["Email:Smtp:Username"];
        var smtpPassword = _configuration["Email:Smtp:Password"];
        var fromEmail = _configuration["Email:Smtp:FromEmail"];
        var fromName = _configuration["Email:Smtp:FromName"] ?? "StitchLens";

        if (string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(smtpPortValue) ||
            string.IsNullOrWhiteSpace(fromEmail)) {
            if (_environment.IsDevelopment()) {
                _logger.LogInformation(
                    "Development email fallback: password reset link for {Email}: {ResetLink}",
                    email,
                    resetLink);
                return true;
            }

            _logger.LogError("SMTP configuration missing. Unable to send password reset email.");
            return false;
        }

        if (!int.TryParse(smtpPortValue, out var smtpPort)) {
            _logger.LogError("Invalid SMTP port configuration. Port={Port}", smtpPortValue);
            return false;
        }

        var useSsl = bool.TryParse(_configuration["Email:Smtp:UseSsl"], out var parsedUseSsl)
            ? parsedUseSsl
            : true;

        using var message = new MailMessage {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Reset your StitchLens password",
            Body = BuildPasswordResetBody(resetLink),
            IsBodyHtml = true
        };

        message.To.Add(email);

        using var client = new SmtpClient(smtpHost, smtpPort) {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPassword)) {
            client.Credentials = new NetworkCredential(smtpUser, smtpPassword);
        }

        try {
            await client.SendMailAsync(message);
            _logger.LogInformation("Password reset email sent successfully. Email={Email}", email);
            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to send password reset email. Email={Email}", email);
            return false;
        }
    }

    private static string BuildPasswordResetBody(string resetLink) {
        return $"""
        <p>Hello,</p>
        <p>We received a request to reset your StitchLens password.</p>
        <p><a href=\"{resetLink}\">Click here to reset your password</a></p>
        <p>If you didn't request this, you can safely ignore this email.</p>
        <p>This link expires according to your account security policy.</p>
        """;
    }
}
