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
public class AccountLoginRememberMeTests {
    [Fact]
    public async Task Login_ForwardsRememberMeTrue_ToPasswordSignIn() {
        var userManager = CreateUserManagerMock();
        var signInManager = CreateSignInManagerMock(userManager.Object);

        bool? capturedRememberMe = null;
        signInManager
            .Setup(m => m.PasswordSignInAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Callback<string, string, bool, bool>((_, _, isPersistent, _) => capturedRememberMe = isPersistent)
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = CreateController(userManager.Object, signInManager.Object);
        controller.Url = CreateUrlHelper();

        var result = await controller.Login(new LoginViewModel {
            Email = "test@example.com",
            Password = "Pass123!",
            RememberMe = true
        });

        result.Should().BeOfType<RedirectToActionResult>();
        capturedRememberMe.Should().BeTrue();
    }

    [Fact]
    public async Task Login_ForwardsRememberMeFalse_ToPasswordSignIn() {
        var userManager = CreateUserManagerMock();
        var signInManager = CreateSignInManagerMock(userManager.Object);

        bool? capturedRememberMe = null;
        signInManager
            .Setup(m => m.PasswordSignInAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Callback<string, string, bool, bool>((_, _, isPersistent, _) => capturedRememberMe = isPersistent)
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = CreateController(userManager.Object, signInManager.Object);
        controller.Url = CreateUrlHelper();

        var result = await controller.Login(new LoginViewModel {
            Email = "test@example.com",
            Password = "Pass123!",
            RememberMe = false
        });

        result.Should().BeOfType<RedirectToActionResult>();
        capturedRememberMe.Should().BeFalse();
    }

    private static AccountController CreateController(UserManager<User> userManager, SignInManager<User> signInManager) {
        return new AccountController(
            userManager,
            signInManager,
            null!,
            null!,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            null!,
            Mock.Of<IEmailSenderService>(),
            Mock.Of<ILogger<AccountController>>());
    }

    private static IUrlHelper CreateUrlHelper() {
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string?>())).Returns(false);
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
