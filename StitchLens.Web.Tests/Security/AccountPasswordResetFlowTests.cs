using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StitchLens.Data.Models;
using StitchLens.Web.Controllers;
using StitchLens.Web.Models;
using StitchLens.Web.Services;

namespace StitchLens.Web.Tests.Security;

[Trait("Category", "LaunchCritical")]
public class AccountPasswordResetFlowTests {
    [Fact]
    public async Task ForgotPassword_SendsResetEmail_WhenConfirmedUserExists() {
        var userManager = CreateUserManagerMock();
        var signInManager = CreateSignInManagerMock(userManager.Object);
        var emailSender = new Mock<IEmailSenderService>();

        var user = new User { Id = 15, Email = "recover@example.com", UserName = "recover@example.com" };
        userManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(m => m.IsEmailConfirmedAsync(user)).ReturnsAsync(true);
        userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token-123");
        emailSender.Setup(m => m.SendPasswordResetEmailAsync(user.Email, It.IsAny<string>())).ReturnsAsync(true);

        var controller = CreateController(userManager.Object, signInManager.Object, emailSender.Object);
        controller.Url = CreateUrlHelper();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.ControllerContext.HttpContext.Request.Scheme = "https";

        var result = await controller.ForgotPassword(new ForgotPasswordViewModel { Email = user.Email });

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be(nameof(AccountController.ForgotPasswordConfirmation));
        emailSender.Verify(m => m.SendPasswordResetEmailAsync(user.Email, It.Is<string>(s => s.Contains("ResetPassword"))), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotSendEmail_WhenUserMissing() {
        var userManager = CreateUserManagerMock();
        var signInManager = CreateSignInManagerMock(userManager.Object);
        var emailSender = new Mock<IEmailSenderService>();

        userManager.Setup(m => m.FindByEmailAsync("missing@example.com")).ReturnsAsync((User?)null);

        var controller = CreateController(userManager.Object, signInManager.Object, emailSender.Object);

        var result = await controller.ForgotPassword(new ForgotPasswordViewModel { Email = "missing@example.com" });

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be(nameof(AccountController.ForgotPasswordConfirmation));
        emailSender.Verify(m => m.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_RedirectsToConfirmation_WhenResetSucceeds() {
        var userManager = CreateUserManagerMock();
        var signInManager = CreateSignInManagerMock(userManager.Object);
        var emailSender = new Mock<IEmailSenderService>();

        var user = new User { Id = 18, Email = "reset@example.com", UserName = "reset@example.com" };
        userManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(m => m.ResetPasswordAsync(user, "decoded-token", "NewPass123!"))
            .ReturnsAsync(IdentityResult.Success);

        var controller = CreateController(userManager.Object, signInManager.Object, emailSender.Object);

        var result = await controller.ResetPassword(new ResetPasswordViewModel {
            Email = user.Email,
            Token = "decoded-token",
            Password = "NewPass123!",
            ConfirmPassword = "NewPass123!"
        });

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be(nameof(AccountController.ResetPasswordConfirmation));
    }

    private static AccountController CreateController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IEmailSenderService emailSender) {
        return new AccountController(
            userManager,
            signInManager,
            null!,
            null!,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            null!,
            emailSender,
            Mock.Of<ILogger<AccountController>>());
    }

    private static IUrlHelper CreateUrlHelper() {
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns("https://localhost/Account/ResetPassword?token=test&email=recover@example.com");
        return urlHelper.Object;
    }

    private static Mock<UserManager<User>> CreateUserManagerMock() {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new Mock<ILogger<UserManager<User>>>().Object);
    }

    private static Mock<SignInManager<User>> CreateSignInManagerMock(UserManager<User> userManager) {
        return new Mock<SignInManager<User>>(
            userManager,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<User>>().Object,
            Options.Create(new IdentityOptions()),
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<User>>().Object);
    }
}
