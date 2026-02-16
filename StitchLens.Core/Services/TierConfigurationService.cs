using Microsoft.EntityFrameworkCore;
using StitchLens.Data;
using StitchLens.Data.Models;

namespace StitchLens.Core.Services;

public class TierConfigurationService : ITierConfigurationService {
    private readonly StitchLensDbContext _context;

    public TierConfigurationService(StitchLensDbContext context) {
        _context = context;
    }

    public async Task<TierConfiguration> GetConfigAsync(SubscriptionTier tier) {
        var config = await _context.TierConfigurations
            .FirstOrDefaultAsync(t => t.Tier == tier);

        if (config == null) {
            throw new InvalidOperationException($"Tier configuration not found for: {tier}");
        }

        return config;
    }

    public async Task<List<TierConfiguration>> GetAllConfigsAsync() {
        return await _context.TierConfigurations
            .OrderBy(t => t.MonthlyPrice)
            .ToListAsync();
    }

    public async Task<int> GetPatternCreationQuotaAsync(SubscriptionTier tier) {
        var config = await GetConfigAsync(tier);
        return config.PatternCreationQuota;
    }

    public async Task<int> GetPatternLimitAsync(SubscriptionTier tier) {
        var config = await GetConfigAsync(tier);
        return config.PatternCreationDailyLimit;
    }
}
