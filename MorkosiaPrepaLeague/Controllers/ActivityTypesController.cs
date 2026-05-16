using Microsoft.AspNetCore.Mvc;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Requests;
using MorkosiaPrepaLeague.Models.Attributes;
using MorkosiaPrepaLeague.Services;
using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Controllers
{
    [ApiController]
    [Route("api/activitytypes")]
    [MinRole(Role.LeagueAdmin)]
    public class ActivityTypesController : ControllerBase
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<ActivityTypesController> _logger;

        public ActivityTypesController(ICosmosDbService cosmosDbService, ILogger<ActivityTypesController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Gets all activity types for a specific league (accessible to all authenticated users)
        /// </summary>
        [HttpGet("league/{leagueId}")]
        [MinRole(Role.User)]
        public async Task<IActionResult> GetActivityTypesForLeague([Required] string leagueId)
        {
            try
            {
                var activityTypes = await _cosmosDbService.GetActivityTypesForLeagueAsync(leagueId);
                
                _logger.LogInformation("Retrieved {Count} activity types for league {LeagueId}", 
                    activityTypes.Count(), leagueId);

                return Ok(activityTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving activity types for league {LeagueId}", leagueId);
                return StatusCode(500, "An error occurred while retrieving activity types");
            }
        }

        /// <summary>
        /// Creates a new activity type for a specific league
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateActivityType([FromBody] CreateActivityTypeRequest request)
        {
            try
            {
                var activityType = new MplActivityType
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = request.Name.Trim(),
                    DefaultPoints = request.DefaultPoints,
                    LeagueId = request.LeagueId,
                    LinkedPositions = request.LinkedPositions?.Distinct().ToList() ?? new List<string>()
                };

                var createdActivityType = await _cosmosDbService.AddActivityTypeAsync(activityType);
                
                if (createdActivityType == null)
                {
                    return BadRequest("Failed to create activity type");
                }
                
                _logger.LogInformation("Created activity type {ActivityTypeName} for league {LeagueId}", 
                    activityType.Name, activityType.LeagueId);

                return CreatedAtAction(nameof(GetActivityTypesForLeague), 
                    new { leagueId = activityType.LeagueId }, createdActivityType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating activity type {ActivityTypeName} for league {LeagueId}", 
                    request.Name, request.LeagueId);
                return StatusCode(500, "An error occurred while creating the activity type");
            }
        }

        /// <summary>
        /// Updates an existing activity type
        /// </summary>
        [HttpPut("{activityTypeId}")]
        public async Task<IActionResult> UpdateActivityType(
            [Required] string activityTypeId, 
            [FromBody] UpdateActivityTypeRequest request)
        {
            try
            {
                // First, get the existing activity type
                var existingActivityType = await _cosmosDbService.GetActivityTypeByIdAsync(activityTypeId);

                if (existingActivityType == null)
                {
                    return NotFound($"Activity type with ID {activityTypeId} not found");
                }

                // Update the properties
                existingActivityType.Name = request.Name.Trim();
                existingActivityType.DefaultPoints = request.DefaultPoints;
                existingActivityType.LinkedPositions = request.LinkedPositions?.Distinct().ToList() ?? new List<string>();

                var updatedActivityType = await _cosmosDbService.UpdateActivityTypeAsync(existingActivityType);
                
                if (updatedActivityType == null)
                {
                    return BadRequest("Failed to update activity type");
                }
                
                _logger.LogInformation("Updated activity type {ActivityTypeId} for league {LeagueId}", 
                    activityTypeId, existingActivityType.LeagueId);

                return Ok(updatedActivityType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating activity type {ActivityTypeId}", activityTypeId);
                return StatusCode(500, "An error occurred while updating the activity type");
            }
        }
    }
}
