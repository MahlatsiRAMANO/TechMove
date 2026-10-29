using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechMove.Web.Services.Api;

namespace TechMove.Web.Infrastructure;

public static class ModelStateExtensions
{
    /// <summary>
    /// Copies the API's validation errors onto the form. Field names from the API
    /// (e.g. "SignedAgreement", "EndDate") are matched to the view model's fields when they exist;
    /// everything else is shown in the summary at the top of the form.
    /// </summary>
    public static void AddApiErrors(this ModelStateDictionary modelState, ApiValidationException ex, params string[] knownFields)
    {
        if (ex.Errors.Count == 0)
        {
            modelState.AddModelError(string.Empty, ex.Message);
            return;
        }

        foreach (var (field, messages) in ex.Errors)
        {
            var key = knownFields.FirstOrDefault(f => string.Equals(f, field, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            foreach (var message in messages)
            {
                modelState.AddModelError(key, message);
            }
        }
    }
}
