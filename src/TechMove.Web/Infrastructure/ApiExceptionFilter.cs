using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TechMove.Web.Services.Api;

namespace TechMove.Web.Infrastructure;

/// <summary>
/// Safety net for API failures that a controller did not handle itself:
/// expired token → sign out and go to the login page; API down → friendly error page.
/// </summary>
public sealed class ApiExceptionFilter : IAsyncExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempData;
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ITempDataDictionaryFactory tempData, ILogger<ApiExceptionFilter> logger)
    {
        _tempData = tempData;
        _logger = logger;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiUnauthorizedException { IsForbidden: false }:
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _tempData.GetTempData(context.HttpContext)["Error"] = context.Exception.Message;
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString });
                context.ExceptionHandled = true;
                break;

            case ApiUnauthorizedException:
                _tempData.GetTempData(context.HttpContext)["Error"] = context.Exception.Message;
                context.Result = new RedirectToActionResult("Index", "Home", null);
                context.ExceptionHandled = true;
                break;

            case ApiNotFoundException:
                context.Result = new NotFoundResult();
                context.ExceptionHandled = true;
                break;

            case ApiException api:
                _logger.LogWarning(api, "TechMove API call failed");
                context.Result = new ViewResult
                {
                    ViewName = "ApiUnavailable",
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                    ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
                    {
                        ["Message"] = api.Message
                    }
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
