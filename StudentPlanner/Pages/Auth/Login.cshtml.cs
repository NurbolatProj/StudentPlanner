using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentPlanner.Data;

namespace StudentPlanner.Pages.Auth;

public sealed class LoginModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();

    public void OnGet(string? returnUrl = null) => Input.ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return LocalRedirect(Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl! : "/");
        if (result.IsLockedOut) ModelState.AddModelError(string.Empty, "This account is temporarily locked. Try again later.");
        else ModelState.AddModelError(string.Empty, "Invalid sign-in attempt.");
        return Page();
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
