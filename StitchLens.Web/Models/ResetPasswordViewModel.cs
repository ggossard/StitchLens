using System.ComponentModel.DataAnnotations;

namespace StitchLens.Web.Models;

public class ResetPasswordViewModel {
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "{0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
