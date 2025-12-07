using Microsoft.AspNetCore.Identity;

namespace StitchLens.Data.Models;

public class User : IdentityUser<int>  // <int> means Id is int, not string
{
    // ASP.NET Identity provides: Id, Email, PasswordHash, EmailConfirmed, etc.

    // Your custom fields
    public UserType UserType { get; set; } = UserType.Customer;
    public string PlanType { get; set; } = "Free"; // Free, Premium, B2B_Basic, B2B_Pro
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();

    // For B2B partners (null for regular customers)
    public PartnerConfig? PartnerConfig { get; set; }
}