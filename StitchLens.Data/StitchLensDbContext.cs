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

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        // CRITICAL: Call base.OnModelCreating first to set up Identity tables
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity => {
            entity.HasKey(e => e.Id);
            // Email index is already configured by Identity, but we can make it explicit
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);

            // One-to-One relationship with PartnerConfig
            entity.HasOne(e => e.PartnerConfig)
                .WithOne(p => p.User)
                .HasForeignKey<PartnerConfig>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Project configuration
        modelBuilder.Entity<Project>(entity => {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.WidthInches).HasPrecision(10, 2);
            entity.Property(e => e.HeightInches).HasPrecision(10, 2);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.YarnBrand)
                .WithMany()
                .HasForeignKey(e => e.YarnBrandId)
                .OnDelete(DeleteBehavior.SetNull);
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
    }
}