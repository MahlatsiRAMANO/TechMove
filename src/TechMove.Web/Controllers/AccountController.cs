using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

/// <summary>
/// Sign-in: the username/password go to the API (POST /api/auth/login). The JWT it returns is kept
/// in an encrypted, HTTP-only cookie and attached to every later API call by BearerTokenHandler.
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

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, result.Username),
            new Claim(ClaimTypes.Role, result.Role),
            new Claim(BearerTokenHandler.TokenClaim, result.Token)
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = result.ExpiresAtUtc });

        TempData["Success"] = $"Welcome, {result.Username}.";
        return Url.IsLocalUrl(vm.ReturnUrl) ? LocalRedirect(vm.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
