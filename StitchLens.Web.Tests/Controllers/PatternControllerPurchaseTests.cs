using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;
using Stripe.Checkout;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Controllers;
using StitchLens.Web.Services;

namespace StitchLens.Web.Tests.Controllers;

[Trait("Category", "LaunchCritical")]
public class PatternControllerPurchaseTests {
    [Fact]
    public async Task CompletePatternPurchase_CreatesPayment_ForValidPaidSession() {
        using var db = CreateDb();

        var user = new User { Id = 7, UserName = "buyer@example.com", Email = "buyer@example.com" };
        var project = new Project { Id = 11, UserId = user.Id, OriginalImagePath = "uploads/p.png", CreatedAt = DateTime.UtcNow };
        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();

        var fakeStripe = new FakeStripeCheckoutSessionService();
        fakeStripe.AddSession("sess_ok", new Session {
            Id = "sess_ok",
            PaymentStatus = "paid",
            AmountTotal = 595,
            Currency = "usd",
            CustomerId = "cus_123",
            PaymentIntent = new PaymentIntent { Id = "pi_one_time_1" },
            Metadata = new Dictionary<string, string> {
                ["purchase_type"] = "one_time_pattern",
                ["user_id"] = user.Id.ToString(),
                ["project_id"] = project.Id.ToString()
            }
        });

        var controller = CreateController(db.Context, fakeStripe, user.Id);

        var result = await controller.CompletePatternPurchase(project.Id, true, "sess_ok");

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Preview");

        var payments = await db.Context.PaymentHistory
            .Where(p => p.ProjectId == project.Id && p.UserId == user.Id)
            .ToListAsync();

        payments.Should().HaveCount(1);
        payments[0].Status.Should().Be(PaymentStatus.Succeeded);
        payments[0].Type.Should().Be(PaymentType.OneTimePattern);
        payments[0].StripePaymentIntentId.Should().Be("pi_one_time_1");

        var refreshedUser = await db.Context.Users.FindAsync(user.Id);
        refreshedUser.Should().NotBeNull();
        refreshedUser!.StripeCustomerId.Should().Be("cus_123");
    }

    [Fact]
    public async Task CompletePatternPurchase_DoesNotCreatePayment_WhenMetadataDoesNotMatchProject() {
        using var db = CreateDb();

        var user = new User { Id = 8, UserName = "buyer2@example.com", Email = "buyer2@example.com" };
        var project = new Project { Id = 22, UserId = user.Id, OriginalImagePath = "uploads/p2.png", CreatedAt = DateTime.UtcNow };
        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();

        var fakeStripe = new FakeStripeCheckoutSessionService();
        fakeStripe.AddSession("sess_bad_meta", new Session {
            Id = "sess_bad_meta",
            PaymentStatus = "paid",
            Metadata = new Dictionary<string, string> {
                ["purchase_type"] = "one_time_pattern",
                ["user_id"] = user.Id.ToString(),
                ["project_id"] = "999"
            }
        });

        var controller = CreateController(db.Context, fakeStripe, user.Id);

        var result = await controller.CompletePatternPurchase(project.Id, true, "sess_bad_meta");

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Preview");

        (await db.Context.PaymentHistory.CountAsync(p => p.ProjectId == project.Id && p.UserId == user.Id))
            .Should().Be(0);
    }

    [Fact]
    public async Task CompletePatternPurchase_DoesNotDuplicatePayment_WhenPaymentIntentAlreadyRecorded() {
        using var db = CreateDb();

        var user = new User { Id = 9, UserName = "buyer3@example.com", Email = "buyer3@example.com" };
        var project = new Project { Id = 33, UserId = user.Id, OriginalImagePath = "uploads/p3.png", CreatedAt = DateTime.UtcNow };
        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        db.Context.PaymentHistory.Add(new PaymentHistory {
            UserId = user.Id,
            ProjectId = project.Id,
            Type = PaymentType.OneTimePattern,
            Amount = 5.95m,
            Currency = "USD",
            Status = PaymentStatus.Succeeded,
            StripePaymentIntentId = "pi_already",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var fakeStripe = new FakeStripeCheckoutSessionService();
        fakeStripe.AddSession("sess_dup_pi", new Session {
            Id = "sess_dup_pi",
            PaymentStatus = "paid",
            AmountTotal = 595,
            Currency = "usd",
            PaymentIntent = new PaymentIntent { Id = "pi_already" },
            Metadata = new Dictionary<string, string> {
                ["purchase_type"] = "one_time_pattern",
                ["user_id"] = user.Id.ToString(),
                ["project_id"] = project.Id.ToString()
            }
        });

        var controller = CreateController(db.Context, fakeStripe, user.Id);

        var result = await controller.CompletePatternPurchase(project.Id, true, "sess_dup_pi");

        result.Should().BeOfType<RedirectToActionResult>();
        var payments = await db.Context.PaymentHistory
            .Where(p => p.UserId == user.Id && p.ProjectId == project.Id)
            .ToListAsync();

        payments.Should().HaveCount(1);
        payments[0].StripePaymentIntentId.Should().Be("pi_already");
    }

    [Fact]
    public async Task CompletePatternPurchase_DoesNotCreatePayment_WhenSessionNotPaid() {
        using var db = CreateDb();

        var user = new User { Id = 10, UserName = "buyer4@example.com", Email = "buyer4@example.com" };
        var project = new Project { Id = 44, UserId = user.Id, OriginalImagePath = "uploads/p4.png", CreatedAt = DateTime.UtcNow };
        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();

        var fakeStripe = new FakeStripeCheckoutSessionService();
        fakeStripe.AddSession("sess_unpaid", new Session {
            Id = "sess_unpaid",
            PaymentStatus = "unpaid",
            Metadata = new Dictionary<string, string> {
                ["purchase_type"] = "one_time_pattern",
                ["user_id"] = user.Id.ToString(),
                ["project_id"] = project.Id.ToString()
            }
        });

        var controller = CreateController(db.Context, fakeStripe, user.Id);
        var result = await controller.CompletePatternPurchase(project.Id, true, "sess_unpaid");

        result.Should().BeOfType<RedirectToActionResult>();
        (await db.Context.PaymentHistory.CountAsync(p => p.ProjectId == project.Id && p.UserId == user.Id))
            .Should().Be(0);
    }

    [Fact]
    public async Task CompletePatternPurchase_DoesNotCreatePayment_WhenPurchaseTypeIsInvalid() {
        using var db = CreateDb();

        var user = new User { Id = 12, UserName = "buyer5@example.com", Email = "buyer5@example.com" };
        var project = new Project { Id = 55, UserId = user.Id, OriginalImagePath = "uploads/p5.png", CreatedAt = DateTime.UtcNow };
        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();

        var fakeStripe = new FakeStripeCheckoutSessionService();
        fakeStripe.AddSession("sess_bad_type", new Session {
            Id = "sess_bad_type",
            PaymentStatus = "paid",
            Metadata = new Dictionary<string, string> {
                ["purchase_type"] = "subscription",
                ["user_id"] = user.Id.ToString(),
                ["project_id"] = project.Id.ToString()
            }
        });

        var controller = CreateController(db.Context, fakeStripe, user.Id);
        var result = await controller.CompletePatternPurchase(project.Id, true, "sess_bad_type");

        result.Should().BeOfType<RedirectToActionResult>();
        (await db.Context.PaymentHistory.CountAsync(p => p.ProjectId == project.Id && p.UserId == user.Id))
            .Should().Be(0);
    }

    private static PatternController CreateController(
        StitchLensDbContext context,
        IStripeCheckoutSessionService stripeCheckoutSessionService,
        int userId) {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            "test"));

        var controller = new PatternController(
            context,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            stripeCheckoutSessionService,
            NullLogger<PatternController>.Instance) {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private static TestDb CreateDb() {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<StitchLensDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new StitchLensDbContext(options);
        context.Database.EnsureCreated();

        return new TestDb(context, connection);
    }

    private sealed class TestDb : IDisposable {
        public StitchLensDbContext Context { get; }
        private readonly SqliteConnection _connection;

        public TestDb(StitchLensDbContext context, SqliteConnection connection) {
            Context = context;
            _connection = connection;
        }

        public void Dispose() {
            Context.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider {
        public IDictionary<string, object> LoadTempData(HttpContext context) {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) {
        }
    }

    private sealed class FakeStripeCheckoutSessionService : IStripeCheckoutSessionService {
        private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

        public void AddSession(string sessionId, Session session) {
            _sessions[sessionId] = session;
        }

        public Task<Session> CreateAsync(SessionCreateOptions options) {
            throw new NotSupportedException();
        }

        public Task<Session> GetAsync(string sessionId) {
            if (_sessions.TryGetValue(sessionId, out var session)) {
                return Task.FromResult(session);
            }

            throw new StripeException($"Session not found: {sessionId}");
        }
    }
}
