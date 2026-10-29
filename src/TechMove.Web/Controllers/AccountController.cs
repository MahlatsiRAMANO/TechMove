using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Web.Infrastructure;
using TechMove.Web.Services.Api;

namespace TechMove.Web.Controllers;

public class LoginViewModel
{
    [Required]
    public string? Username { get; set; }

    [Required, DataType(DataType.Password)]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, dashes or underscores only.")]
    public string? Username { get; set; }

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }
}

/// <summary>
/// Sign-in and sign-up. Credentials go to the API (/api/auth/login, /api/auth/register). The JWT it
/// returns is kept in an encrypted, HTTP-only cookie and attached to every later API call by BearerTokenHandler.
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAuthApi _auth;

    public AccountController(IAuthApi auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        LoginResult? result;
        try
        {
            result = await _auth.LoginAsync(vm.Username!, vm.Password!, ct);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(vm);
        }

        if (result is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(vm);
        }

        await SignInAsync(result);
        TempData["Success"] = $"Welcome, {result.Username}.";
        return Url.IsLocalUrl(vm.ReturnUrl) ? LocalRedirect(vm.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    /// <summary>Self sign-up creates a Customer account. An Admin then links it to a client (or promotes it).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        try
        {
            var result = await _auth.RegisterAsync(vm.Username!, vm.Password!, ct);
            await SignInAsync(result);
            TempData["Success"] = $"Account '{result.Username}' created. An administrator will link it to your company.";
            return RedirectToAction("Index", "Home");
        }
        catch (ApiValidationException ex)
        {
            ModelState.AddApiErrors(ex, nameof(RegisterViewModel.Username), nameof(RegisterViewModel.Password));
            return View(vm);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(vm);
        }
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private Task SignInAsync(LoginResult result)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.Username),
            new(ClaimTypes.Role, result.Role),
            new(BearerTokenHandler.TokenClaim, result.Token)
        };

        if (result.ClientId.HasValue)
        {
            claims.Add(new Claim(UserRoles.ClientIdClaim, result.ClientId.Value.ToString()));
        }

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = result.ExpiresAtUtc });
    }
}
