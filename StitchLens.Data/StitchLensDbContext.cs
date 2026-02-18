using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StitchLens.Data.Models;

namespace StitchLens.Data;

// Change from DbContext to IdentityDbContext<User, IdentityRole<int>, int>
public class StitchLensDbContext : IdentityDbContext<User, IdentityRole<int>, int> {
    public StitchLensDbContext(DbContextOptions<StitchLensDbContext> options)
        : base(options) {
    }

    // Note: Users DbSet is inherited from IdentityDbContext, but we can still reference it
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<YarnBrand> YarnBrands => Set<YarnBrand>();
    public DbSet<YarnColor> YarnColors => Set<YarnColor>();
    public DbSet<PartnerConfig> PartnerConfigs => Set<PartnerConfig>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PaymentHistory> PaymentHistory => Set<PaymentHistory>();
    public DbSet<TierConfiguration> TierConfigurations => Set<TierConfiguration>();  // ADD THIS
    public DbSet<WebhookEventLog> WebhookEventLogs => Set<WebhookEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        // CRITICAL: Call base.OnModelCreating first to set up Identity tables
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity => {
            entity.HasKey(e => e.Id);
            // Email index is already configured by Identity, but we can make it explicit
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);

            // Subscription-related properties
            entity.Property(e => e.CurrentTier)
                .HasConversion<int>();

            entity.HasOne(e => e.ActiveSubscription)
                .WithOne()
                .HasForeignKey<User>(e => e.ActiveSubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            // One-to-One relationship with PartnerConfig
            entity.HasOne(e => e.PartnerConfig)
                .WithOne(p => p.User)
                .HasForeignKey<PartnerConfig>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.WidthInches).HasPrecision(10, 2);
            entity.Property(e => e.HeightInches).HasPrecision(10, 2);
            entity.Property(e => e.CraftType).HasConversion<int>();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull)  // CHANGED from Cascade
                .IsRequired(false);  // ADDED - makes relationship optional

            entity.HasOne(e => e.YarnBrand)
                .WithMany()
                .HasForeignKey(e => e.YarnBrandId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);  // ADDED - makes relationship optional
        });

        // PartnerConfig configuration
        modelBuilder.Entity<PartnerConfig>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.WebsiteDomain).HasMaxLength(200);
            entity.Property(e => e.ApiKey).HasMaxLength(100);
            entity.Property(e => e.MonthlyFee).HasPrecision(10, 2);

            // Ensure ApiKey is unique
            entity.HasIndex(e => e.ApiKey).IsUnique();
            // Ensure one config per user
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // YarnBrand configuration
        modelBuilder.Entity<YarnBrand>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.CraftType).HasConversion<int>();
            entity.Property(e => e.YardsPerStitch).HasPrecision(10, 3);
        });

        // YarnColor configuration
        modelBuilder.Entity<YarnColor>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.HexColor).HasMaxLength(7);

            entity.HasOne(e => e.YarnBrand)
                .WithMany(b => b.Colors)
                .HasForeignKey(e => e.YarnBrandId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity => {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Tier)
                .HasConversion<int>();

            entity.Property(e => e.BillingCycle)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.MonthlyPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.CustomTierName)
                .HasMaxLength(100);

            entity.Property(e => e.CustomTierNotes)
                .HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.StripeSubscriptionId)
                .IsUnique()
                .HasFilter("[StripeSubscriptionId] IS NOT NULL");
        });

        // PaymentHistory configuration
        modelBuilder.Entity<PaymentHistory>(entity => {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.Amount)
                .HasPrecision(10, 2);

            entity.Property(e => e.RefundAmount)
                .HasPrecision(10, 2);

            entity.Property(e => e.Currency)
                .HasMaxLength(3);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.StripePaymentIntentId)
                .IsUnique()
                .HasFilter("[StripePaymentIntentId] IS NOT NULL");
        });

        // TierConfiguration configuration
        modelBuilder.Entity<TierConfiguration>(entity => {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Tier)
                .HasConversion<int>();

            entity.Property(e => e.Name)
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.MonthlyPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.AnnualPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.PerPatternPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.StripeMonthlyPriceId)
                .HasMaxLength(100);

            entity.Property(e => e.StripeAnnualPriceId)
                .HasMaxLength(100);

            entity.Property(e => e.StripePerPatternPriceId)
                .HasMaxLength(100);

            // Ensure each tier has only one config
            entity.HasIndex(e => e.Tier)
                .IsUnique();
        });

        // WebhookEventLog configuration
        modelBuilder.Entity<WebhookEventLog>(entity => {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.EventType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.LastError)
                .HasMaxLength(1000);

            entity.HasIndex(e => e.EventId)
                .IsUnique();
        });
    }
}
