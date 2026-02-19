namespace StitchLens.Web.Services;

public interface IEmailSenderService {
    Task<bool> SendPasswordResetEmailAsync(string email, string resetLink);
}
