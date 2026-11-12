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

    /// <summary>
    /// Off by default: closing the browser or restarting the app signs the user out.
    /// Delivery staff tick it once on their own phone so they are not asked every morning.
    /// </summary>
    [Display(Name = "Keep me signed in on this device")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
