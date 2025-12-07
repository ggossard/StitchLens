using StitchLens.Data.Models;

namespace StitchLens.Web.Models;

public class MyAccountViewModel {
    public string Email { get; set; } = string.Empty;
    public string PlanType { get; set; } = "Free";
    public UserType UserType { get; set; }
    public DateTime CreatedAt { get; set; }
    public int PatternCount { get; set; }
}