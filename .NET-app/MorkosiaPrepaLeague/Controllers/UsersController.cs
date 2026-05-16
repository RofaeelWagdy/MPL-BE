using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MorkosiaPrepaLeague.Models;
using MorkosiaPrepaLeague.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Requests;
using MorkosiaPrepaLeague.Models.Attributes;

namespace MorkosiaPrepaLeague.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<UsersController> _logger;
        private readonly IPasswordHasher<User> _passwordHasher;

        public UsersController(ICosmosDbService cosmosDbService, ILogger<UsersController> logger, IPasswordHasher<User> passwordHasher)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
            _passwordHasher = passwordHasher;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RegisterUser([FromBody] UserRegistrationRequest request)
        {
            _logger.LogInformation("Attempting to register user with Username: {Username}", request.Username);

            bool usernameExists = await _cosmosDbService.UsernameExistsAsync(request.Username);
            if (usernameExists)
            {
                _logger.LogWarning("Username '{Username}' already exists. Returning Conflict.", request.Username);
                return Conflict(new { message = $"A user with the username '{request.Username}' already exists." });
            }

            var newUser = new User
            {
                Username = request.Username,
                FullName = request.FullName,
                Class = request.Class,
                LeaguesAdmin = new List<string>(),
                LeaguesMember = new List<string>(),
                CreatedAt = DateTime.UtcNow
            };

            newUser.HashedPassword = _passwordHasher.HashPassword(newUser, request.Password);

            try
            {
                var createdUser = await _cosmosDbService.AddUserAsync(newUser);

                if (createdUser == null)
                {
                    _logger.LogError("Failed to register user '{Username}' in Cosmos DB, AddUserAsync returned null.", request.Username);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save the user.");
                }
                
                _logger.LogInformation("Successfully registered user with ID: {UserId}", createdUser.Id);
                return CreatedAtAction(nameof(GetUserById), new { userId = createdUser.Id }, createdUser); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while registering user '{Username}'.", request.Username);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        [HttpGet("me")]
        [MinRole(Role.User)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "Unable to determine user ID" });

            var user = await _cosmosDbService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }

        [HttpGet("all")]
        [MinRole(Role.LeagueAdmin)]
        [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUsers()
        {
            _logger.LogInformation("Fetching all accessible users");

            try
            {
                var allLeagues = await _cosmosDbService.GetAllLeaguesAsync();
                var allLeagueIds = allLeagues.Select(l => l.Id).ToList();
                var accessibleLeagueIds = AuthorizationUtilities.GetAdminAccessibleLeagueIds(User, allLeagueIds);
                
                _logger.LogInformation("User accessing {LeagueCount} leagues", accessibleLeagueIds.Count());
                
                var users = await _cosmosDbService.GetAllUsersForLeaguesAsync(accessibleLeagueIds);
                
                _logger.LogInformation("Successfully retrieved {Count} users", users.Count());
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving users");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while processing your request.");
            }
        }

        [HttpGet("{userId}")]
        [MinRole(Role.User)]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserById(string userId)
        {
            _logger.LogInformation("Attempting to get user with ID: {UserId}", userId);
            try
            {
                var user = await _cosmosDbService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID: {UserId} not found.", userId);
                    return NotFound(new { message = $"User with ID '{userId}' not found." });
                }
                _logger.LogInformation("Successfully retrieved user with ID: {UserId}", userId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting user with ID: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }
        
        [HttpPost("assign-role")]
        [MinRole(Role.LeagueAdmin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            _logger.LogInformation("Attempting to assign role '{Role}' for league '{LeagueId}' to user '{UserId}'", 
                request.Role, request.LeagueId, request.UserId);
            
            try
            {
                var league = await _cosmosDbService.GetLeagueByIdAsync(request.LeagueId);
                if (league == null)
                {
                    _logger.LogWarning("League with ID '{LeagueId}' not found.", request.LeagueId);
                    return NotFound(new { message = $"League with ID '{request.LeagueId}' not found." });
                }
                
                var user = await _cosmosDbService.GetUserByIdAsync(request.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID '{UserId}' not found.", request.UserId);
                    return NotFound(new { message = $"User with ID '{request.UserId}' not found." });
                }
                  if (!AuthorizationUtilities.IsUserAdminForLeague(User, request.LeagueId))
                {
                    _logger.LogWarning("User does not have permission to assign roles for league '{LeagueId}'", request.LeagueId);
                    return Forbid();
                }
                
                user.LeaguesAdmin ??= new List<string>();
                user.LeaguesMember ??= new List<string>();
                
                if (request.Role.ToLower() == "admin")
                {
                    if (!user.LeaguesAdmin.Contains(request.LeagueId))
                    {
                        user.LeaguesAdmin.Add(request.LeagueId);
                        
                        if (user.LeaguesMember.Contains(request.LeagueId))
                        {
                            user.LeaguesMember.Remove(request.LeagueId);
                        }
                    }
                }
                else if (request.Role.ToLower() == "member")
                {
                    if (!user.LeaguesMember.Contains(request.LeagueId))
                    {
                        user.LeaguesMember.Add(request.LeagueId);
                        
                        if (user.LeaguesAdmin.Contains(request.LeagueId))
                        {
                            user.LeaguesAdmin.Remove(request.LeagueId);
                        }
                    }
                }
                
                var updatedUser = await _cosmosDbService.UpdateUserAsync(user);
                if (updatedUser == null)
                {
                    _logger.LogError("Failed to update user '{UserId}' in Cosmos DB", request.UserId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update the user.");
                }
                
                _logger.LogInformation("Successfully assigned role '{Role}' for league '{LeagueId}' to user '{UserId}'", 
                    request.Role, request.LeagueId, request.UserId);
                
                return Ok(new 
                { 
                    message = $"Successfully assigned role '{request.Role}' for league '{request.LeagueId}' to user '{request.UserId}'",
                    userId = updatedUser.Id,
                    leagueId = request.LeagueId,
                    role = request.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while assigning role '{Role}' for league '{LeagueId}' to user '{UserId}'", 
                    request.Role, request.LeagueId, request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        [HttpPost("remove-role")]
        [MinRole(Role.LeagueAdmin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleRequest request)
        {
            _logger.LogInformation("Attempting to remove role '{Role}' for league '{LeagueId}' from user '{UserId}'", 
                request.Role, request.LeagueId, request.UserId);
            
            try
            {
                var league = await _cosmosDbService.GetLeagueByIdAsync(request.LeagueId);
                if (league == null)
                {
                    _logger.LogWarning("League with ID '{LeagueId}' not found.", request.LeagueId);
                    return NotFound(new { message = $"League with ID '{request.LeagueId}' not found." });
                }
                
                var user = await _cosmosDbService.GetUserByIdAsync(request.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID '{UserId}' not found.", request.UserId);
                    return NotFound(new { message = $"User with ID '{request.UserId}' not found." });
                }
                  if (!AuthorizationUtilities.IsUserAdminForLeague(User, request.LeagueId))
                {
                    _logger.LogWarning("User does not have permission to remove roles for league '{LeagueId}'", request.LeagueId);
                    return Forbid();
                }
                
                user.LeaguesAdmin ??= new List<string>();
                user.LeaguesMember ??= new List<string>();
                
                bool roleRemoved = false;
                
                if (request.Role.ToLower() == "admin")
                {
                    if (user.LeaguesAdmin.Contains(request.LeagueId))
                    {
                        user.LeaguesAdmin.Remove(request.LeagueId);
                        roleRemoved = true;
                    }
                }
                else if (request.Role.ToLower() == "member")
                {
                    if (user.LeaguesMember.Contains(request.LeagueId))
                    {
                        user.LeaguesMember.Remove(request.LeagueId);
                        roleRemoved = true;
                    }
                }
                
                if (!roleRemoved)
                {
                    _logger.LogInformation("User '{UserId}' does not have role '{Role}' for league '{LeagueId}' to remove", 
                        request.UserId, request.Role, request.LeagueId);
                    return Ok(new 
                    { 
                        message = $"User '{request.UserId}' does not have role '{request.Role}' for league '{request.LeagueId}' to remove",
                        userId = user.Id,
                        leagueId = request.LeagueId,
                        role = request.Role,
                        removed = false
                    });
                }
                
                var updatedUser = await _cosmosDbService.UpdateUserAsync(user);
                if (updatedUser == null)
                {
                    _logger.LogError("Failed to update user '{UserId}' in Cosmos DB", request.UserId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update the user.");
                }
                
                _logger.LogInformation("Successfully removed role '{Role}' for league '{LeagueId}' from user '{UserId}'", 
                    request.Role, request.LeagueId, request.UserId);
                
                return Ok(new 
                { 
                    message = $"Successfully removed role '{request.Role}' for league '{request.LeagueId}' from user '{request.UserId}'",
                    userId = updatedUser.Id,
                    leagueId = request.LeagueId,
                    role = request.Role,
                    removed = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while removing role '{Role}' for league '{LeagueId}' from user '{UserId}'", 
                    request.Role, request.LeagueId, request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(new { message = "Authentication required" });
            }

            try
            {
                var user = await _cosmosDbService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID: {UserId} not found.", userId);
                    return NotFound(new { message = $"User with ID '{userId}' not found." });
                }

                _logger.LogInformation("Successfully retrieved user with ID: {UserId}", userId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting user with ID: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }
      
        [HttpPut("{userId}")]
        [MinRole(Role.User)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UserUpdateRequest updateRequest)
        {
            _logger.LogInformation("Attempting to update user with ID: {UserId}", userId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("UpdateUser: Invalid model state for user ID {UserId}.", userId);
                return BadRequest(ModelState);
            }

            var user = await _cosmosDbService.GetUserByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("UpdateUser: User with ID {UserId} not found.", userId);
                return NotFound(new ProblemDetails { Title = "Not Found", Detail = $"User with ID '{userId}' not found.", Status = StatusCodes.Status404NotFound });
            }

            // Check permissions: Users can update their own profile, SuperAdmins can update any user, 
            // LeagueAdmins can update users who are members of their leagues
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!AuthorizationUtilities.CanUserManageTargetUser(User, currentUserId, userId, user.LeaguesMember))
            {
                _logger.LogWarning("User '{CurrentUserId}' does not have permission to update user '{UserId}'", currentUserId, userId);
                return Forbid();
            }
            
            _logger.LogInformation("User '{CurrentUserId}' updating user '{UserId}'", currentUserId, userId);

            // Apply updates from DTO. Only update if the DTO property has a value.
            bool updated = false;

            if (!string.IsNullOrWhiteSpace(updateRequest.FullName))
            {
                user.FullName = updateRequest.FullName;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(updateRequest.Class))
            {
                user.Class = updateRequest.Class;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(updateRequest.Password))
            {
                user.HashedPassword = _passwordHasher.HashPassword(user, updateRequest.Password);
                updated = true;
            }

            if (updated)
            {
                var updatedUser = await _cosmosDbService.UpdateUserAsync(user);
                if (updatedUser == null)
                {
                    _logger.LogError("UpdateUser: Failed to update user ID {UserId} in database.", userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Internal Server Error", Detail = "Failed to update user.", Status = StatusCodes.Status500InternalServerError });
                }
                _logger.LogInformation("UpdateUser: User updated successfully for user ID {UserId}.", userId);
            }
            else
            {
                _logger.LogInformation("UpdateUser: No changes detected in the request for user ID {UserId}.", userId);
            }

            return NoContent();
        }
    }
}
