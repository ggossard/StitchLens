using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;
using Stripe.Checkout;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Controllers;
using DataSubscription = StitchLens.Data.Models.Subscription;

namespace StitchLens.Web.Tests.Controllers;

public class WebhookControllerTests {
    [Fact]
    public async Task HandleCheckoutCompleted_DoesNothing_WhenUserIdMetadataIsInvalid() {
        using var db = CreateDb();
        var controller = CreateController(db.Context);

        var stripeEvent = new Event {
            Data = new EventData {
                Object = new Session {
                    Id = "cs_invalid_user",
                    Metadata = new Dictionary<string, string> {
                        ["user_id"] = "not-an-int",
                        ["tier"] = "Hobbyist"
                    }
                }
            }
        };

        await InvokePrivateAsync(controller, "HandleCheckoutCompleted", stripeEvent, "{}");

        (await db.Context.Subscriptions.CountAsync()).Should().Be(0);
        (await db.Context.PaymentHistory.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandleCheckoutCompleted_DoesNothing_WhenTierMetadataIsInvalid() {
        using var db = CreateDb();
        var controller = CreateController(db.Context);

        var stripeEvent = new Event {
            Data = new EventData {
                Object = new Session {
                    Id = "cs_invalid_tier",
                    Metadata = new Dictionary<string, string> {
                        ["user_id"] = "123",
                        ["tier"] = "BogusTier"
                    }
                }
            }
        };

        await InvokePrivateAsync(controller, "HandleCheckoutCompleted", stripeEvent, "{}");

        (await db.Context.Subscriptions.CountAsync()).Should().Be(0);
        (await db.Context.PaymentHistory.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandleCheckoutCompleted_DoesNotCreateDuplicateSubscription_WhenStripeSubscriptionAlreadyExists() {
        using var db = CreateDb();

        var user = new User {
            Id = 42,
            UserName = "existing@example.com",
            Email = "existing@example.com"
        };

        db.Context.Users.Add(user);
        db.Context.Subscriptions.Add(new DataSubscription {
            UserId = user.Id,
            Tier = SubscriptionTier.Hobbyist,
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            PatternCreationQuota = 3,
            StripeSubscriptionId = "sub_existing",
            StartDate = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(25)
        });
        await db.Context.SaveChangesAsync();

        var controller = CreateController(db.Context);

        var stripeEvent = new Event {
            Data = new EventData {
                Object = new Session {
                    Id = "cs_existing_sub",
                    SubscriptionId = "sub_existing",
                    Metadata = new Dictionary<string, string> {
                        ["user_id"] = "42",
                        ["tier"] = "Hobbyist",
                        ["billing_cycle"] = "Monthly"
                    }
                }
            }
        };

        await InvokePrivateAsync(controller, "HandleCheckoutCompleted", stripeEvent, "{}");

        (await db.Context.Subscriptions.CountAsync(s => s.StripeSubscriptionId == "sub_existing")).Should().Be(1);
    }

    [Fact]
    public async Task HandleInvoicePaymentSucceeded_DoesNotCreateDuplicatePayment_WhenInvoiceAlreadyRecorded() {
        using var db = CreateDb();

        var user = new User {
            Id = 100,
            UserName = "invoiceok@example.com",
            Email = "invoiceok@example.com",
            PatternsCreatedThisMonth = 7
        };

        var subscription = new DataSubscription {
            Id = 200,
            UserId = user.Id,
            Tier = SubscriptionTier.Creator,
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            PatternCreationQuota = 30,
            StripeSubscriptionId = "sub_for_invoice",
            StartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        };

        db.Context.Users.Add(user);
        db.Context.Subscriptions.Add(subscription);
        db.Context.PaymentHistory.Add(new PaymentHistory {
            UserId = user.Id,
            SubscriptionId = subscription.Id,
            Type = PaymentType.SubscriptionRecurring,
            Amount = 12.95m,
            Currency = "USD",
            Status = PaymentStatus.Succeeded,
            StripeInvoiceId = "in_duplicate_success"
        });
        await db.Context.SaveChangesAsync();

        var controller = CreateController(db.Context);
        var stripeEvent = new Event {
            Data = new EventData {
                Object = new Invoice {
                    Id = "in_duplicate_success",
                    AmountPaid = 1295,
                    Currency = "usd"
                }
            }
        };

        var rawJson = """
        {
          "data": {
            "object": {
              "parent": {
                "subscription_details": {
                  "subscription": "sub_for_invoice"
                }
              },
              "payment_intent": "pi_123"
            }
          }
        }
        """;

        await InvokePrivateAsync(controller, "HandleInvoicePaymentSucceeded", stripeEvent, rawJson);

        (await db.Context.PaymentHistory.CountAsync(p => p.StripeInvoiceId == "in_duplicate_success")).Should().Be(1);

        var refreshedUser = await db.Context.Users.FindAsync(user.Id);
        refreshedUser.Should().NotBeNull();
        refreshedUser!.PatternsCreatedThisMonth.Should().Be(7);
    }

    [Fact]
    public async Task HandleInvoicePaymentFailed_DoesNotCreateDuplicateFailure_WhenInvoiceAlreadyRecorded() {
        using var db = CreateDb();

        var user = new User {
            Id = 110,
            UserName = "invoicefail@example.com",
            Email = "invoicefail@example.com"
        };

        var subscription = new DataSubscription {
            Id = 210,
            UserId = user.Id,
            Tier = SubscriptionTier.Hobbyist,
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            PatternCreationQuota = 3,
            StripeSubscriptionId = "sub_for_failed_invoice",
            StartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        };

        db.Context.Users.Add(user);
        db.Context.Subscriptions.Add(subscription);
        db.Context.PaymentHistory.Add(new PaymentHistory {
            UserId = user.Id,
            SubscriptionId = subscription.Id,
            Type = PaymentType.SubscriptionRecurring,
            Amount = 12.95m,
            Currency = "USD",
            Status = PaymentStatus.Failed,
            StripeInvoiceId = "in_duplicate_failed"
        });
        await db.Context.SaveChangesAsync();

        var controller = CreateController(db.Context);
        var stripeEvent = new Event {
            Data = new EventData {
                Object = new Invoice {
                    Id = "in_duplicate_failed",
                    AmountDue = 1295,
                    Currency = "usd"
                }
            }
        };

        var rawJson = """
        {
          "data": {
            "object": {
              "subscription": "sub_for_failed_invoice"
            }
          }
        }
        """;

        await InvokePrivateAsync(controller, "HandleInvoicePaymentFailed", stripeEvent, rawJson);

        (await db.Context.PaymentHistory.CountAsync(p => p.StripeInvoiceId == "in_duplicate_failed")).Should().Be(1);
    }

    private static WebhookController CreateController(StitchLensDbContext context) {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Stripe:WebhookSecret"] = "whsec_test"
            })
            .Build();

        return new WebhookController(
            context,
            new NoOpSubscriptionService(),
            configuration,
            NullLogger<WebhookController>.Instance);
    }

    private static async Task InvokePrivateAsync(WebhookController controller, string methodName, Event stripeEvent, string rawJson) {
        var method = typeof(WebhookController).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"Expected private method '{methodName}' to exist.");

        var result = method!.Invoke(controller, new object[] { stripeEvent, rawJson });
        result.Should().BeAssignableTo<Task>();
        await (Task)result!;
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

    private sealed class NoOpSubscriptionService : ISubscriptionService {
        public Task<DataSubscription> CreateSubscriptionAsync(int userId, SubscriptionTier tier, string stripePriceId, BillingCycle billingCycle = BillingCycle.Monthly)
            => throw new NotSupportedException();

        public Task<DataSubscription> CreateCustomSubscriptionAsync(int userId, decimal monthlyPrice, int patternCreationQuota, bool allowCommercialUse, string customTierName, string? customTierNotes = null)
            => throw new NotSupportedException();

        public Task CancelSubscriptionAsync(int subscriptionId, string reason)
            => throw new NotSupportedException();

        public Task<DataSubscription> UpgradeSubscriptionAsync(int currentSubscriptionId, SubscriptionTier newTier, string newStripePriceId, BillingCycle billingCycle = BillingCycle.Monthly)
            => throw new NotSupportedException();

        public Task<(bool CanDownload, string? Reason)> CanUserDownloadAsync(int userId)
            => throw new NotSupportedException();

        public Task RecordDownloadAsync(int userId, int projectId)
            => throw new NotSupportedException();

        public Task<DataSubscription?> GetActiveSubscriptionAsync(int userId)
            => throw new NotSupportedException();

        public Task<DataSubscription?> GetSubscriptionByIdAsync(int subscriptionId)
            => throw new NotSupportedException();

        public Task<List<DataSubscription>> GetUserSubscriptionsAsync(int userId)
            => throw new NotSupportedException();

        public Task<List<PaymentHistory>> GetPaymentHistoryAsync(int userId)
            => throw new NotSupportedException();
    }
}
