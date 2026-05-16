using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MorkosiaPrepaLeague.Models;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Attributes;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MorkosiaPrepaLeague.Middleware
{
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthorizationMiddleware> _logger;
        private const string TargetPathPrefix = "/api";
        
        private static readonly string[] PublicPaths = new[]
        {
            "/api/users/register",
        };

        public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(TargetPathPrefix) || IsPublicPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Get endpoint and check if it has a MinRole attribute
            var endpoint = context.GetEndpoint();
            var minRoleAttribute = endpoint?.Metadata.GetMetadata<MinRoleAttribute>();
            var requiredRole = minRoleAttribute?.Required ?? Role.User; // Default to User level if not specified

            _logger.LogDebug("Path {Path} requires role: {RequiredRole}", context.Request.Path, requiredRole);

            // Get the user's role from claims
            var userRoleClaim = context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRoleClaim) || !Enum.TryParse<Role>(userRoleClaim, out var userRole))
            {
                userRole = Role.Public;
            }

            _logger.LogDebug("User has role: {UserRole}", userRole);

            // Check if the user has sufficient permission
            if (userRole >= requiredRole)
            {
                _logger.LogInformation("User with role {UserRole} authorized for {Path} requiring {RequiredRole}", 
                    userRole, context.Request.Path, requiredRole);
                await _next(context);
            }
            else
            {
                _logger.LogWarning("Authorization failed for {Path}. Required: {RequiredRole}, User has: {UserRole}", 
                    context.Request.Path, requiredRole, userRole);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: Insufficient permissions");
            }
        }

        private bool IsPublicPath(PathString path)
        {
            return PublicPaths.Any(p => path.Equals(p, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}