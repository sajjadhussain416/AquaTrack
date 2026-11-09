using System.ComponentModel.DataAnnotations;

namespace AquaTrack.Web.Models.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter {0}.")]
    [Display(Name = "Mobile number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}
