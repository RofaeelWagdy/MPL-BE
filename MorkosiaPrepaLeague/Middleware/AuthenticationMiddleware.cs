using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Attributes;
using MorkosiaPrepaLeague.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace MorkosiaPrepaLeague.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<AuthenticationMiddleware> _logger;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly string? _expectedDevKey;
        private const string DevKeyHeaderName = "x-dev-key";

        public AuthenticationMiddleware(
            RequestDelegate next, 
            IConfiguration configuration, 
            ICosmosDbService cosmosDbService,
            ILogger<AuthenticationMiddleware> logger,
            IPasswordHasher<User> passwordHasher)
        {
            _next = next;
            _configuration = configuration;
            _cosmosDbService = cosmosDbService;
            _logger = logger;
            _passwordHasher = passwordHasher;

            string configKeyName;
            var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";

            if (environment == "Development")
            {
                configKeyName = "Development:DevKey";
            }
            else if (environment == "Production")
            {
                configKeyName = "Production:DevKey";
            }
            else
            {
                configKeyName = "Development:DevKey";
            }

            _expectedDevKey = configuration[configKeyName];
            _logger.LogInformation("AuthenticationMiddleware configured. Reading key from '{ConfigKey}'. Key found: {KeyFound}",
                configKeyName, !string.IsNullOrEmpty(_expectedDevKey));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            _logger.LogDebug("AuthenticationMiddleware processing request for path: {Path}", context.Request.Path);

            // Check for super admin (dev key)
            if (context.Request.Headers.TryGetValue(DevKeyHeaderName, out var providedKey) && 
                !string.IsNullOrEmpty(_expectedDevKey) && 
                providedKey == _expectedDevKey)
            {
                _logger.LogInformation("Super admin authenticated via dev key for path: {Path}", context.Request.Path);
                context.User = CreateClaimsPrincipal("superadmin", Role.SuperAdmin, null, null, null);
                await _next(context);
                return;
            }

            // Check for basic authentication
            if (context.Request.Headers.TryGetValue("Authorization", out var authorization) && 
                authorization.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var authValue = authorization.ToString()["Basic ".Length..].Trim();
                    var credentialBytes = Convert.FromBase64String(authValue);
                    var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

                    if (credentials.Length == 2)
                    {
                        var username = credentials[0];
                        var password = credentials[1];
                        
                        var user = await _cosmosDbService.GetUserByUsernameAsync(username);
                        
                        if (user != null)
                        {
                            // Verify password
                            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.HashedPassword, password);
                            
                            if (passwordVerificationResult == PasswordVerificationResult.Success)
                            {
                                // Determine user role
                                var role = Role.User;
                                
                                // If user has any leagues they admin, they are a LeagueAdmin
                                if (user.LeaguesAdmin != null && user.LeaguesAdmin.Count > 0)
                                {
                                    role = Role.LeagueAdmin;
                                }
                                
                                _logger.LogInformation("User {Username} authenticated successfully with role {Role}", username, role);
                                
                                context.User = CreateClaimsPrincipal(username, role, user.LeaguesAdmin, user.LeaguesMember, user.Id);
                                await _next(context);
                                return;
                            }
                            else
                            {
                                _logger.LogWarning("Invalid password for user: {Username}", username);
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                await context.Response.WriteAsync("Invalid username or password");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("User not found: {Username}", username);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing basic authentication");
                }
            }

            // If we got here, no authentication was provided or it was invalid
            // We'll still call next() and let the authorization middleware decide what to do based on the endpoint
            await _next(context);
        }

        private static ClaimsPrincipal CreateClaimsPrincipal(string username, Role role, IList<string>? adminLeagues, IList<string>? memberLeagues, string? userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role.ToString())
            };

            if (userId != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            if (adminLeagues != null)
            {
                foreach (var leagueId in adminLeagues)
                {
                    claims.Add(new Claim("AdminLeague", leagueId));
                }
            }

            if (memberLeagues != null)
            {
                foreach (var leagueId in memberLeagues)
                {
                    claims.Add(new Claim("MemberLeague", leagueId));
                }
            }

            var identity = new ClaimsIdentity(claims, "Custom");
            return new ClaimsPrincipal(identity);
        }
    }
}