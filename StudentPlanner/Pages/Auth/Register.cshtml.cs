using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentPlanner.Data;

namespace StudentPlanner.Pages.Auth;

public sealed class RegisterModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = new ApplicationUser { UserName = Input.Email.Trim(), Email = Input.Email.Trim(), FullName = Input.FullName.Trim() };
        var result = await users.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect("/");
        }
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail"
                ? "An account with this email already exists."
                : error.Description);
        return Page();
    }

    public sealed class RegisterInput
    {
        [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 10)] public string Password { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
    }
}
