using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;
using Stripe.Checkout;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Services;
using DataSubscription = StitchLens.Data.Models.Subscription;

namespace StitchLens.Web.Tests.Services;

public class StripeWebhookProcessorTests {
    [Fact]
    public async Task HandleCheckoutCompletedAsync_DoesNothing_WhenUserIdMetadataIsInvalid() {
        using var db = CreateDb();
        var processor = CreateProcessor(db.Context);

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

        await processor.HandleCheckoutCompletedAsync(stripeEvent, "{}");

        (await db.Context.Subscriptions.CountAsync()).Should().Be(0);
        (await db.Context.PaymentHistory.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandleCheckoutCompletedAsync_DoesNothing_WhenTierMetadataIsInvalid() {
        using var db = CreateDb();
        var processor = CreateProcessor(db.Context);

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

        await processor.HandleCheckoutCompletedAsync(stripeEvent, "{}");

        (await db.Context.Subscriptions.CountAsync()).Should().Be(0);
        (await db.Context.PaymentHistory.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandleCheckoutCompletedAsync_DoesNotCreateDuplicateSubscription_WhenStripeSubscriptionAlreadyExists() {
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

        var processor = CreateProcessor(db.Context);

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

        await processor.HandleCheckoutCompletedAsync(stripeEvent, "{}");

        (await db.Context.Subscriptions.CountAsync(s => s.StripeSubscriptionId == "sub_existing")).Should().Be(1);
    }

    [Fact]
    public async Task HandleInvoicePaymentSucceededAsync_DoesNotCreateDuplicatePayment_WhenInvoiceAlreadyRecorded() {
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

        var processor = CreateProcessor(db.Context);
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

        await processor.HandleInvoicePaymentSucceededAsync(stripeEvent, rawJson);

        (await db.Context.PaymentHistory.CountAsync(p => p.StripeInvoiceId == "in_duplicate_success")).Should().Be(1);

        var refreshedUser = await db.Context.Users.FindAsync(user.Id);
        refreshedUser.Should().NotBeNull();
        refreshedUser!.PatternsCreatedThisMonth.Should().Be(7);
    }

    [Fact]
    public async Task HandleInvoicePaymentFailedAsync_DoesNotCreateDuplicateFailure_WhenInvoiceAlreadyRecorded() {
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

        var processor = CreateProcessor(db.Context);
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

        await processor.HandleInvoicePaymentFailedAsync(stripeEvent, rawJson);

        (await db.Context.PaymentHistory.CountAsync(p => p.StripeInvoiceId == "in_duplicate_failed")).Should().Be(1);
    }

    private static StripeWebhookProcessor CreateProcessor(StitchLensDbContext context) {
        return new StripeWebhookProcessor(context, NullLogger<StripeWebhookProcessor>.Instance);
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
}
