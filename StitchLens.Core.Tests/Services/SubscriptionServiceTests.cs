using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;

namespace StitchLens.Core.Tests.Services;

public class SubscriptionServiceTests {
    [Fact]
    public async Task CanUserDownloadAsync_AllowsPayAsYouGoUser() {
        using var db = CreateDb();
        db.Context.Users.Add(new User {
            Id = 1,
            UserName = "paygo@example.com",
            Email = "paygo@example.com",
            CurrentTier = SubscriptionTier.PayAsYouGo,
            LastPatternDate = DateTime.UtcNow.AddDays(-1),
            LastPatternCreationDate = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var service = new SubscriptionService(db.Context);
        var result = await service.CanUserDownloadAsync(1);

        result.CanDownload.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public async Task CanUserDownloadAsync_BlocksWhenNoActiveSubscription() {
        using var db = CreateDb();
        db.Context.Users.Add(new User {
            Id = 2,
            UserName = "nosub@example.com",
            Email = "nosub@example.com",
            CurrentTier = SubscriptionTier.Hobbyist,
            LastPatternDate = DateTime.UtcNow.AddDays(-1),
            LastPatternCreationDate = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var service = new SubscriptionService(db.Context);
        var result = await service.CanUserDownloadAsync(2);

        result.CanDownload.Should().BeFalse();
        result.Reason.Should().Be("No active subscription");
    }

    [Fact]
    public async Task CanUserDownloadAsync_BlocksWhenSubscriptionIsNotActive() {
        using var db = CreateDb();
        var user = new User {
            Id = 3,
            UserName = "pastdue@example.com",
            Email = "pastdue@example.com",
            CurrentTier = SubscriptionTier.Creator,
            LastPatternDate = DateTime.UtcNow.AddDays(-1),
            LastPatternCreationDate = DateTime.UtcNow
        };

        var subscription = new Subscription {
            Id = 30,
            UserId = user.Id,
            Tier = SubscriptionTier.Creator,
            Status = SubscriptionStatus.PastDue,
            PatternCreationQuota = 30,
            StartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        };

        db.Context.Users.Add(user);
        db.Context.Subscriptions.Add(subscription);
        await db.Context.SaveChangesAsync();

        user.ActiveSubscriptionId = subscription.Id;
        await db.Context.SaveChangesAsync();

        var service = new SubscriptionService(db.Context);
        var result = await service.CanUserDownloadAsync(3);

        result.CanDownload.Should().BeFalse();
        result.Reason.Should().Be("Subscription is not active");
    }

    [Fact]
    public async Task CanUserDownloadAsync_BlocksWhenMonthlyQuotaReached() {
        using var db = CreateDb();
        var user = new User {
            Id = 4,
            UserName = "quota@example.com",
            Email = "quota@example.com",
            CurrentTier = SubscriptionTier.Hobbyist,
            PatternsCreatedThisMonth = 3,
            LastPatternCreationDate = DateTime.UtcNow,
            LastPatternDate = DateTime.UtcNow.AddDays(-1)
        };

        var subscription = new Subscription {
            Id = 40,
            UserId = user.Id,
            Tier = SubscriptionTier.Hobbyist,
            Status = SubscriptionStatus.Active,
            PatternCreationQuota = 3,
            StartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        };

        db.Context.Users.Add(user);
        db.Context.Subscriptions.Add(subscription);
        await db.Context.SaveChangesAsync();

        user.ActiveSubscriptionId = subscription.Id;
        await db.Context.SaveChangesAsync();

        var service = new SubscriptionService(db.Context);
        var result = await service.CanUserDownloadAsync(4);

        result.CanDownload.Should().BeFalse();
        result.Reason.Should().StartWith("Monthly quota of 3 patterns reached");
    }

    [Fact]
    public async Task CanUserDownloadAsync_ResetsMonthlyCountWhenMonthChanged() {
        using var db = CreateDb();
        var user = new User {
            Id = 5,
            UserName = "reset@example.com",
            Email = "reset@example.com",
            CurrentTier = SubscriptionTier.Hobbyist,
            PatternsCreatedThisMonth = 9,
            LastPatternCreationDate = DateTime.UtcNow.AddMonths(-1),
            LastPatternDate = DateTime.UtcNow.AddDays(-1)
        };

        var subscription = new Subscription {
            Id = 50,
            UserId = user.Id,
            Tier = SubscriptionTier.Hobbyist,
            Status = SubscriptionStatus.Active,
            PatternCreationQuota = 10,
            StartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        };

        db.Context.Users.Add(user);
        db.Context.Subscriptions.Add(subscription);
        await db.Context.SaveChangesAsync();

        user.ActiveSubscriptionId = subscription.Id;
        await db.Context.SaveChangesAsync();

        var service = new SubscriptionService(db.Context);
        var result = await service.CanUserDownloadAsync(5);

        result.CanDownload.Should().BeTrue();

        var updatedUser = await db.Context.Users.FindAsync(5);
        updatedUser.Should().NotBeNull();
        updatedUser!.PatternsCreatedThisMonth.Should().Be(0);
    }

    [Fact]
    public async Task CanUserDownloadAsync_BlocksWhenDailyLimitReached() {
        using var db = CreateDb();
        db.Context.Users.Add(new User {
            Id = 6,
            UserName = "dailylimit@example.com",
            Email = "dailylimit@example.com",
            CurrentTier = SubscriptionTier.PayAsYouGo,
            PatternsCreatedToday = 20,
            LastPatternDate = DateTime.UtcNow,
            LastPatternCreationDate = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var service = new SubscriptionService(db.Context);
        var result = await service.CanUserDownloadAsync(6);

        result.CanDownload.Should().BeFalse();
        result.Reason.Should().Contain("Daily limit of 20 patterns reached");
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
