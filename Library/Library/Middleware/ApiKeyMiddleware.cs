//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Http;
//using Library.Data;
//using Microsoft.EntityFrameworkCore;

//namespace Library.Middleware
//{
//    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
//    public class ApiKeyMiddleware
//    {
//        private readonly RequestDelegate _next;

//        public ApiKeyMiddleware(RequestDelegate next)
//        {
//            _next = next;
//        }

//        public async Task Invoke(HttpContext context, AppDbContext dbContext)
//        {
//            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
//            {
//                context.Response.StatusCode = 401; // Unauthorized
//                await context.Response.WriteAsync("API Key is missing.");
//                return;
//            }

//            var apiKey = await dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Key == extractedApiKey && k.IsActive);
//            if (apiKey == null)
//            {
//                context.Response.StatusCode = 403; // Forbidden
//                await context.Response.WriteAsync("Invalid API Key.");
//                return;
//            }

//            await _next(context);
//        }
//    }


//    // Extension method used to add the middleware to the HTTP request pipeline.
//    public static class ApiKeyMiddlewareExtensions
//    {
//        public static IApplicationBuilder UseApiKeyMiddleware(this IApplicationBuilder builder)
//        {
//            return builder.UseMiddleware<ApiKeyMiddleware>();
//        }
//    }
//}
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Library.Data;
using Microsoft.EntityFrameworkCore;

namespace Library.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, AppDbContext dbContext)
        {
            var path = context.Request.Path.Value;

            // Bypass API key validation for Swagger and its resources
            if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("API Key is missing.");
                return;
            }
            string extractedApi = extractedApiKey.ToString();

            var apiKey = await dbContext.ApiKeys
                .FirstOrDefaultAsync(k => k.Key == extractedApi && k.IsActive);

            if (apiKey == null)
            {
                context.Response.StatusCode = 403; // Forbidden
                await context.Response.WriteAsync("Invalid API Key.");
                return;
            }

            await _next(context);
        }
    }
}
