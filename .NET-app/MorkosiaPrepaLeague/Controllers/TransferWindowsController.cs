using Microsoft.AspNetCore.Mvc;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Attributes;
using MorkosiaPrepaLeague.Services;
using MorkosiaPrepaLeague.Models.Requests;
using System.Security.Claims;

namespace MorkosiaPrepaLeague.Controllers
{
    [ApiController]
    [Route("api/leagues/{leagueId}/transferwindows")]
    [MinRole(Role.User)]
    public class TransferWindowsController : ControllerBase
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<TransferWindowsController> _logger;

        public TransferWindowsController(ICosmosDbService cosmosDbService, ILogger<TransferWindowsController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the most recent transfer window whose start date has passed
        /// </summary>
        /// <param name="leagueId">The league ID</param>
        /// <returns>Transfer window details with isActive flag</returns>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentTransferWindow(string leagueId)
        {
            try
            {
                _logger.LogInformation("Getting current transfer window for league {LeagueId}", leagueId);

                var league = await _cosmosDbService.GetLeagueByIdAsync(leagueId);
                if (league == null)
                {
                    _logger.LogWarning("League {LeagueId} not found", leagueId);
                    return NotFound(new { message = "League not found" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _cosmosDbService.GetUserByIdAsync(userId);
                    if (user != null)
                    {
                        bool hasAccess = AuthorizationUtilities.IsUserAdminForLeague(User, leagueId) || 
                                        user.LeaguesMember.Contains(leagueId);
                        
                        if (!hasAccess)
                        {
                            _logger.LogWarning("User {UserId} attempted to access transfer window for league {LeagueId} without access", userId, leagueId);
                            return Forbid("You do not have access to this league");
                        }
                    }
                }

                var currentWindow = await _cosmosDbService.GetCurrentTransferWindowAsync(leagueId);
                var now = DateTime.UtcNow;
                
                if (currentWindow == null)
                {
                    _logger.LogInformation("No transfer window found for league {LeagueId}", leagueId);
                    return Ok(new { 
                        message = "No transfer windows found", 
                        isActive = false,
                        leagueId = leagueId,
                        currentTimeUtc = now
                    });
                }

                var isActive = now >= currentWindow.StartDate && now <= currentWindow.EndDate;

                var response = new
                {
                    transferWindow = new
                    {
                        id = currentWindow.Id,
                        leagueId = currentWindow.LeagueId,
                        startDateUtc = currentWindow.StartDate,
                        endDateUtc = currentWindow.EndDate,
                        windowNumber = currentWindow.WindowNumber,
                        createdByAdminId = currentWindow.CreatedByAdminId,
                        version = currentWindow.Ver
                    },
                    isActive = isActive,
                    currentTimeUtc = now,
                };

                _logger.LogInformation("Found transfer window {WindowId} for league {LeagueId}, isActive={IsActive}", 
                    currentWindow.Id, leagueId, isActive);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current transfer window for league {LeagueId}", leagueId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets all transfer windows for a league
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllTransferWindows(string leagueId)
        {
            try
            {
                var windows = await _cosmosDbService.GetAllTransferWindowsAsync(leagueId);
                return Ok(windows.Select(w => new
                {
                    id = w.Id,
                    leagueId = w.LeagueId,
                    startDate = w.StartDate,
                    endDate = w.EndDate,
                    windowNumber = w.WindowNumber,
                    createdByAdminId = w.CreatedByAdminId,
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all transfer windows for league {LeagueId}", leagueId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Creates a new transfer window for a league
        /// </summary>
        /// <param name="leagueId">The league ID</param>
        /// <param name="request">Transfer window creation details</param>
        /// <returns>Created transfer window details</returns>
        [HttpPost("create")]
        [MinRole(Role.LeagueAdmin)]
        public async Task<IActionResult> CreateTransferWindow(string leagueId, [FromBody] CreateTransferWindowRequest request)
        {
            try
            {
                _logger.LogInformation("Creating transfer window for league {LeagueId}", leagueId);

                // Validate request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Verify league exists
                var league = await _cosmosDbService.GetLeagueByIdAsync(leagueId);
                if (league == null)
                {
                    _logger.LogWarning("League {LeagueId} not found", leagueId);
                    return NotFound(new { message = "League not found" });
                }

                // Check if user is admin for this specific league
                if (!AuthorizationUtilities.IsUserAdminForLeague(User, leagueId))
                {
                    _logger.LogWarning("User attempted to create transfer window for league {LeagueId} without admin access", leagueId);
                    return Forbid("You do not have admin access to this league");
                }

                // Validate dates
                if (request.EndDate <= request.StartDate)
                {
                    return BadRequest(new { message = "End date must be after start date" });
                }

                if (request.StartDate <= DateTime.UtcNow)
                {
                    return BadRequest(new { message = "Start date must be in the future" });
                }

                // Check if there's already an active transfer window
                var now = DateTime.UtcNow;
                var latestStartedWindow = await _cosmosDbService.GetCurrentTransferWindowAsync(leagueId);
                if (latestStartedWindow != null && latestStartedWindow.EndDate >= now)
                {
                    return BadRequest(new { message = "Cannot create transfer window: there is already an active transfer window" });
                }

                // Get current user ID from claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }

                // Create transfer window
                var transferWindow = new TransferWindow
                {
                    LeagueId = leagueId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    CreatedByAdminId = userId
                };

                var created = await _cosmosDbService.CreateTransferWindowAsync(transferWindow);
                if (created == null)
                {
                    _logger.LogError("Failed to create transfer window for league {LeagueId}", leagueId);
                    return StatusCode(500, new { message = "Failed to create transfer window" });
                }

                var response = new
                {
                    id = created.Id,
                    leagueId = created.LeagueId,
                    startDateUtc = created.StartDate,
                    endDateUtc = created.EndDate,
                    windowNumber = created.WindowNumber,
                    createdByAdminId = created.CreatedByAdminId,
                    version = created.Ver,
                    createdAt = DateTime.UtcNow
                };

                _logger.LogInformation("Successfully created transfer window {WindowId} for league {LeagueId}", 
                    created.Id, leagueId);

                return CreatedAtAction(
                    nameof(GetCurrentTransferWindow),
                    new { leagueId },
                    response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transfer window for league {LeagueId}", leagueId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
