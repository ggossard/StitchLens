using StitchLens.Data.Models;

namespace StitchLens.Core.Services;

public interface ITierConfigurationService {
    Task<TierConfiguration> GetConfigAsync(SubscriptionTier tier);
    Task<List<TierConfiguration>> GetAllConfigsAsync();
    Task<int> GetDownloadQuotaAsync(SubscriptionTier tier);
    Task<int> GetPatternLimitAsync(SubscriptionTier tier);
}