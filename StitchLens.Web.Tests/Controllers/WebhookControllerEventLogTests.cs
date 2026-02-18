using System.Reflection;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Controllers;
using StitchLens.Web.Services;

namespace StitchLens.Web.Tests.Controllers;

public class WebhookControllerEventLogTests {
    [Fact]
    public async Task TryStartWebhookProcessingAsync_ReturnsNull_ForDuplicateProcessedEvent() {
        using var db = CreateDb();
        var controller = CreateController(db.Context);
        var stripeEvent = new Event { Id = "evt_dup_1", Type = "test.event" };

        var firstLog = await InvokeTryStartWebhookProcessingAsync(controller, stripeEvent);
        firstLog.Should().NotBeNull();

        await InvokeMarkWebhookProcessedAsync(controller, firstLog!.Id);

        var duplicateLog = await InvokeTryStartWebhookProcessingAsync(controller, stripeEvent);
        duplicateLog.Should().BeNull();

        var rows = await db.Context.WebhookEventLogs.Where(e => e.EventId == "evt_dup_1").ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Status.Should().Be(WebhookEventStatus.Processed);
    }

    [Fact]
    public async Task MarkWebhookFailedAsync_UpdatesStatusAndError() {
        using var db = CreateDb();
        var controller = CreateController(db.Context);
        var stripeEvent = new Event { Id = "evt_fail_1", Type = "checkout.session.completed" };

        var eventLog = await InvokeTryStartWebhookProcessingAsync(controller, stripeEvent);
        eventLog.Should().NotBeNull();

        await InvokeMarkWebhookFailedAsync(controller, eventLog!.Id, "Simulated processing failure");

        var updated = await db.Context.WebhookEventLogs.SingleAsync(e => e.EventId == "evt_fail_1");
        updated.Status.Should().Be(WebhookEventStatus.Failed);
        updated.LastError.Should().Be("Simulated processing failure");
    }

    [Fact]
    public async Task TryStartWebhookProcessingAsync_ReturnsExistingLogForRetry_WhenStatusIsFailed() {
        using var db = CreateDb();
        var controller = CreateController(db.Context);
        var stripeEvent = new Event { Id = "evt_retry_1", Type = "checkout.session.completed" };

        var firstLog = await InvokeTryStartWebhookProcessingAsync(controller, stripeEvent);
        firstLog.Should().NotBeNull();

        await InvokeMarkWebhookFailedAsync(controller, firstLog!.Id, "Transient failure");

        var retryLog = await InvokeTryStartWebhookProcessingAsync(controller, stripeEvent);
        retryLog.Should().NotBeNull();
        retryLog!.Id.Should().Be(firstLog.Id);
        retryLog.Status.Should().Be(WebhookEventStatus.Processing);

        var rows = await db.Context.WebhookEventLogs.Where(e => e.EventId == "evt_retry_1").ToListAsync();
        rows.Should().HaveCount(1);
    }

    private static WebhookController CreateController(StitchLensDbContext context) {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Stripe:WebhookSecret"] = "whsec_test"
            })
            .Build();

        return new WebhookController(context, new StubStripeWebhookProcessor(), config, NullLogger<WebhookController>.Instance);
    }

    private static async Task<WebhookEventLog?> InvokeTryStartWebhookProcessingAsync(WebhookController controller, Event stripeEvent) {
        var method = typeof(WebhookController).GetMethod("TryStartWebhookProcessingAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var result = method!.Invoke(controller, new object[] { stripeEvent });
        result.Should().BeAssignableTo<Task<WebhookEventLog?>>();
        return await (Task<WebhookEventLog?>)result!;
    }

    private static async Task InvokeMarkWebhookProcessedAsync(WebhookController controller, int eventLogId) {
        var method = typeof(WebhookController).GetMethod("MarkWebhookProcessedAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var result = method!.Invoke(controller, new object[] { eventLogId });
        result.Should().BeAssignableTo<Task>();
        await (Task)result!;
    }

    private static async Task InvokeMarkWebhookFailedAsync(WebhookController controller, int eventLogId, string errorMessage) {
        var method = typeof(WebhookController).GetMethod("MarkWebhookFailedAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var result = method!.Invoke(controller, new object[] { eventLogId, errorMessage });
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

    private sealed class StubStripeWebhookProcessor : IStripeWebhookProcessor {
        public Task HandleCheckoutCompletedAsync(Event stripeEvent, string rawJson) => Task.CompletedTask;
        public Task HandleSubscriptionUpdatedAsync(Event stripeEvent, string rawJson) => Task.CompletedTask;
        public Task HandleSubscriptionDeletedAsync(Event stripeEvent, string rawJson) => Task.CompletedTask;
        public Task HandleInvoicePaymentSucceededAsync(Event stripeEvent, string rawJson) => Task.CompletedTask;
        public Task HandleInvoicePaymentFailedAsync(Event stripeEvent, string rawJson) => Task.CompletedTask;
    }
}
