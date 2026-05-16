// MorkosiaPrepaLeague/Controllers/LeaguesController.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Requests;
using MorkosiaPrepaLeague.Models.Attributes;
using MorkosiaPrepaLeague.Services;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Linq;

namespace MorkosiaPrepaLeague.Controllers
{
    [ApiController]
    [Route("api/leagues")]
    [MinRole(Role.User)]
    public class LeaguesController : ControllerBase
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<LeaguesController> _logger;

        public LeaguesController(ICosmosDbService cosmosDbService, ILogger<LeaguesController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        [HttpPost] // Route: POST api/leagues
        [MinRole(Role.SuperAdmin)]
        [ProducesResponseType(typeof(League), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateLeague([FromBody] CreateLeagueRequest request)
        {
            _logger.LogInformation("Attempting to create league with Name: {LeagueName}, Type: {LeagueType}", request.Name, request.Type);

            bool nameExists = await _cosmosDbService.LeagueNameExistsAsync(request.Name);
            if (nameExists)
            {
                _logger.LogWarning("League name '{LeagueName}' already exists. Returning Conflict.", request.Name);
                return Conflict(new { message = $"A league with the name '{request.Name}' already exists." });
            }

            var newLeague = new League
            {
                Name = request.Name,
                Type = request.Type,
            };

            try
            {
                var createdLeague = await _cosmosDbService.AddLeagueAsync(newLeague);

                if (createdLeague == null)
                {
                     _logger.LogError("Failed to create league '{LeagueName}' in Cosmos DB, AddLeagueAsync returned null.", request.Name);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save the league.");
                }

                _logger.LogInformation("Successfully created league with ID: {LeagueId}", createdLeague.Id);
                return CreatedAtAction(nameof(GetLeagueById), new { leagueId = createdLeague.Id }, createdLeague);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating league '{LeagueName}'.", request.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        [HttpGet("{leagueId}")] // Route: GET api/leagues/{leagueId}
        [MinRole(Role.User)]
        [ProducesResponseType(typeof(League), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLeagueById(string leagueId)
        {
            _logger.LogInformation("Attempting to get league with ID: {LeagueId}", leagueId);
            try
            {
                var league = await _cosmosDbService.GetLeagueByIdAsync(leagueId);

                if (league == null)
                {
                    _logger.LogWarning("League with ID: {LeagueId} not found.", leagueId);
                    return NotFound(new { message = $"League with ID '{leagueId}' not found." });
                }

                _logger.LogInformation("Successfully retrieved league with ID: {LeagueId}", leagueId);
                return Ok(league);
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "An unexpected error occurred while getting league with ID: {LeagueId}", leagueId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        [HttpPut("{leagueId}/activitypoints-config")]
        [MinRole(Role.LeagueAdmin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateActivityPointsLeagueConfiguration(string leagueId, [FromBody] Models.Requests.ActivityPointsLeagueConfigUpdateDto configDto)
        {
            _logger.LogInformation("Attempting to update ActivityPoints configuration for league ID: {LeagueId}", leagueId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: Invalid model state for league ID {LeagueId}.", leagueId);
                return BadRequest(ModelState);
            }

            var league = await _cosmosDbService.GetLeagueByIdAsync(leagueId);
            if (league == null)
            {
                _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: League with ID {LeagueId} not found.", leagueId);
                return NotFound(new ProblemDetails { Title = "Not Found", Detail = $"League with ID '{leagueId}' not found.", Status = StatusCodes.Status404NotFound });
            }           
            
            if (league.Type != "ActivityPoints")
            {
                _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: League ID {LeagueId} is type {LeagueType}, not ActivityPoints.", leagueId, league.Type);
                return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "This configuration is only applicable to ActivityPoints leagues.", Status = StatusCodes.Status400BadRequest });
            }

            if (!AuthorizationUtilities.IsUserAdminForLeague(User, leagueId))
            {
                _logger.LogWarning("User does not have permission to update configuration for league '{LeagueId}'", leagueId);
                return Forbid();
            }

            // Apply updates from DTO. Only update if the DTO property has a value.
            bool updated = false;
            
            // Handle team positions
            if (configDto.TeamPositions != null)
            {
                // Validate TeamPositions values (each position must have a name and positive count)
                if (configDto.TeamPositions.Any(p => string.IsNullOrWhiteSpace(p.Name) || p.Count <= 0))
                {
                    _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: TeamPositions for league {LeagueId} contains invalid data.", leagueId);
                    return BadRequest(new ProblemDetails { 
                        Title = "Bad Request", 
                        Detail = "All positions must have a name and a positive player count.", 
                        Status = StatusCodes.Status400BadRequest 
                    });
                }
                
                // Also verify there are no duplicate position names
                var positionNameGroups = configDto.TeamPositions.GroupBy(p => p.Name.ToLowerInvariant());
                if (positionNameGroups.Any(g => g.Count() > 1))
                {
                    _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: TeamPositions for league {LeagueId} contains duplicate position names.", leagueId);
                    return BadRequest(new ProblemDetails { 
                        Title = "Bad Request", 
                        Detail = "Position names must be unique (case insensitive).", 
                        Status = StatusCodes.Status400BadRequest 
                    });
                }
                
                // Set the positions
                league.TeamPositions = configDto.TeamPositions;
                _logger.LogInformation("Updated team positions for league {LeagueId}, total size: {TotalSize}", 
                    leagueId, configDto.TeamPositions.Sum(p => p.Count));
                
                updated = true;
            }
            
            if (configDto.InitialBudget.HasValue)
            {
                league.InitialBudget = configDto.InitialBudget.Value;
                updated = true;
            }
            
            if (configDto.DefaultPlayerPrice.HasValue)
            {
                league.DefaultPlayerPrice = configDto.DefaultPlayerPrice.Value;
                updated = true;
            }
            
            if (configDto.PlayerPriceOverrides != null)
            {
                // Validate PlayerPriceOverrides values (non-negative integers)
                if (configDto.PlayerPriceOverrides.Any(kvp => kvp.Value < 0))
                {
                    _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: PlayerPriceOverrides for league {LeagueId} contains negative prices.", leagueId);
                    return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "All prices in PlayerPriceOverrides must be non-negative integers.", Status = StatusCodes.Status400BadRequest });
                }
                league.PlayerPriceOverrides = configDto.PlayerPriceOverrides;
                updated = true;
            }
            
            if (configDto.BudgetOverrides != null)
            {
                // Validate BudgetOverrides values (positive integers)
                if (configDto.BudgetOverrides.Any(kvp => kvp.Value <= 0))
                {
                    _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: BudgetOverrides for league {LeagueId} contains non-positive budget values.", leagueId);
                    return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "All budget values in BudgetOverrides must be positive integers.", Status = StatusCodes.Status400BadRequest });
                }
                league.BudgetOverrides = configDto.BudgetOverrides;
                updated = true;
            }

            if (configDto.DefaultOwnershipCap.HasValue)
            {
                league.DefaultOwnershipCap = configDto.DefaultOwnershipCap.Value;
                updated = true;
            }

            if (configDto.MemberPositionOwnershipCaps != null)
            {
                // Validate MemberPositionOwnershipCaps structure and values
                foreach (var memberCaps in configDto.MemberPositionOwnershipCaps)
                {
                    if (string.IsNullOrWhiteSpace(memberCaps.Key))
                    {
                        _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: MemberPositionOwnershipCaps for league {LeagueId} contains empty member user ID.", leagueId);
                        return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Member user ID cannot be empty in MemberPositionOwnershipCaps.", Status = StatusCodes.Status400BadRequest });
                    }

                    if (memberCaps.Value == null || !memberCaps.Value.Any())
                    {
                        _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: MemberPositionOwnershipCaps for league {LeagueId} contains member {MemberId} with null or empty position caps.", leagueId, memberCaps.Key);
                        return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Member position caps cannot be null or empty in MemberPositionOwnershipCaps.", Status = StatusCodes.Status400BadRequest });
                    }

                    foreach (var positionCap in memberCaps.Value)
                    {
                        if (string.IsNullOrWhiteSpace(positionCap.Key))
                        {
                            _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: MemberPositionOwnershipCaps for league {LeagueId} contains empty position name for member {MemberId}.", leagueId, memberCaps.Key);
                            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Position name cannot be empty in MemberPositionOwnershipCaps.", Status = StatusCodes.Status400BadRequest });
                        }

                        if (positionCap.Value < 0)
                        {
                            _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: MemberPositionOwnershipCaps for league {LeagueId} contains negative cap value for member {MemberId} position {Position}.", leagueId, memberCaps.Key, positionCap.Key);
                            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Ownership cap values must be non-negative in MemberPositionOwnershipCaps.", Status = StatusCodes.Status400BadRequest });
                        }

                        // Optional: Validate that position names match defined team positions (if TeamPositions is configured)
                        if (league.TeamPositions != null && league.TeamPositions.Any())
                        {
                            var validPositions = league.TeamPositions.Select(p => p.Name).ToList();
                            if (!validPositions.Contains(positionCap.Key, StringComparer.OrdinalIgnoreCase))
                            {
                                _logger.LogWarning("UpdateActivityPointsLeagueConfiguration: MemberPositionOwnershipCaps for league {LeagueId} contains invalid position name '{Position}' for member {MemberId}.", leagueId, positionCap.Key, memberCaps.Key);
                                return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = $"Position '{positionCap.Key}' is not defined in the league's team structure.", Status = StatusCodes.Status400BadRequest });
                            }
                        }
                    }
                }

                league.MemberPositionOwnershipCaps = configDto.MemberPositionOwnershipCaps;
                _logger.LogInformation("Updated member position ownership caps for league {LeagueId}, configured {MemberCount} members with position-specific caps.", 
                    leagueId, configDto.MemberPositionOwnershipCaps.Count);
                updated = true;
            }

            if (updated)
            {
                var success = await _cosmosDbService.UpdateLeagueAsync(league);
                if (!success)
                {
                    _logger.LogError("UpdateActivityPointsLeagueConfiguration: Failed to update configuration for league ID {LeagueId} in database.", leagueId);
                    return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails { Title = "Internal Server Error", Detail = "Failed to update league configuration.", Status = StatusCodes.Status500InternalServerError });
                }
                _logger.LogInformation("UpdateActivityPointsLeagueConfiguration: Configuration updated successfully for league ID {LeagueId}.", leagueId);
            }
            else
            {
                _logger.LogInformation("UpdateActivityPointsLeagueConfiguration: No changes detected in the request for league ID {LeagueId}.", leagueId);
            }
            
            return NoContent();
        }
    }
}
