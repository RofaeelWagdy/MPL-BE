using Microsoft.AspNetCore.Builder;
using MorkosiaPrepaLeague.Middleware;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Attributes;

namespace Microsoft.AspNetCore.Builder
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>();
        }

        public static IApplicationBuilder UseCustomAuthorization(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthorizationMiddleware>();
        }
    }
}