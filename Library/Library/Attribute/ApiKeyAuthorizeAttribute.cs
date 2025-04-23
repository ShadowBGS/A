using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Library.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _requiredApiKey;

    public ApiKeyAuthorizeAttribute(string requiredApiKey)
    {
        _requiredApiKey = requiredApiKey;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult("API Key is missing.");
            return;
        }

        string apiKey = extractedApiKey.ToString();

        var apiKeyRecord = await dbContext.ApiKeys
            .Where(k => k.Key == apiKey && k.IsActive)
            .FirstOrDefaultAsync();

        // Instead of ForbidResult(), return a 403 response manually
        if (apiKeyRecord == null || apiKey != _requiredApiKey)
        {
            context.Result = new ObjectResult("Forbidden: Invalid API Key.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
