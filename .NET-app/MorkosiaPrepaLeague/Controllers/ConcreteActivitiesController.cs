using Microsoft.AspNetCore.Mvc;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Requests;
using MorkosiaPrepaLeague.Services;
using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Controllers
{
        [Route("api/v1/transfer-windows/{transferWindowId}/concrete-activities")]
        [ApiController]
        public class ConcreteActivitiesController : ControllerBase
        {
            private readonly ICosmosDbService _cosmosDbService;

            public ConcreteActivitiesController(ICosmosDbService cosmosDbService)
            {
                _cosmosDbService = cosmosDbService;
            }

            [HttpGet]
            public async Task<ActionResult<IEnumerable<ConcreteActivity>>> GetConcreteActivities(
                [FromRoute] string transferWindowId,
                [FromQuery] DateOnly? from = null,
                [FromQuery] DateOnly? to = null,
                [FromQuery] string? activityTypeId = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 50)
            {
                try
                {
                    if (pageSize > 100)
                        pageSize = 100;

                    var activities = await _cosmosDbService.GetConcreteActivitiesForTransferWindowAsync(transferWindowId, from, to, activityTypeId);
                    
                    // Apply pagination
                    var pagedActivities = activities
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                    
                    return Ok(pagedActivities);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error retrieving concrete activities: {ex.Message}");
                }
            }

            [HttpPost]
            public async Task<ActionResult<ConcreteActivity>> CreateConcreteActivity(
                [FromRoute] string transferWindowId,
                [FromBody] CreateConcreteActivityRequest request)
            {
                try
                {
                    if (request.TransferWindowId != transferWindowId)
                        return BadRequest("Transfer Window ID in route must match Transfer Window ID in request body");

                    // Verify the activity type exists
                    var activityType = await _cosmosDbService.GetActivityTypeByIdAsync(request.ActivityTypeId);
                    if (activityType == null)
                        return BadRequest("Activity type not found");

                    var concreteActivity = new ConcreteActivity
                    {
                        Id = Guid.NewGuid().ToString(),
                        TransferWindowId = transferWindowId,
                        ActivityTypeId = request.ActivityTypeId,
                        Date = request.Date,
                        OverridePoints = request.OverridePoints,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var result = await _cosmosDbService.AddConcreteActivityAsync(concreteActivity);
                    return CreatedAtAction(nameof(CreateConcreteActivity), new { transferWindowId, id = concreteActivity.Id }, result);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error creating concrete activity: {ex.Message}");
                }
            }

            [HttpPost("{activityId}")]
            public async Task<ActionResult<ConcreteActivity>> UpdateConcreteActivity(
                [FromRoute] string transferWindowId,
                [FromRoute] string activityId,
                [FromBody] UpdateConcreteActivityRequest request)
            {
                try
                {
                    if (request.Id != activityId)
                        return BadRequest("Activity ID in route must match Activity ID in request body");

                    var existingActivity = await _cosmosDbService.GetConcreteActivityByIdAsync(activityId, transferWindowId);
                    if (existingActivity == null)
                        return NotFound("Concrete activity not found");

                    existingActivity.Date = request.Date;
                    existingActivity.OverridePoints = request.OverridePoints;
                    existingActivity.UpdatedAt = DateTime.UtcNow;

                    var result = await _cosmosDbService.UpdateConcreteActivityAsync(existingActivity);
                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error updating concrete activity: {ex.Message}");
                }
            }

            [HttpDelete("{activityId}")]
            public async Task<ActionResult> DeleteConcreteActivity(
                [FromRoute] string transferWindowId,
                [FromRoute] string activityId)
            {
                try
                {
                    var existingActivity = await _cosmosDbService.GetConcreteActivityByIdAsync(activityId, transferWindowId);
                    if (existingActivity == null)
                        return NotFound("Concrete activity not found");

                    // TODO: Check if any participations exist for this activity
                    // If participations exist, prevent deletion

                    var deleted = await _cosmosDbService.DeleteConcreteActivityAsync(activityId, transferWindowId);
                    if (!deleted)
                        return StatusCode(500, "Failed to delete concrete activity");
                        
                    return NoContent();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error deleting concrete activity: {ex.Message}");
                }
            }

            [HttpPost("{activityId}/participants")]
            public async Task<ActionResult> AddParticipant(
                [FromRoute] string transferWindowId,
                [FromRoute] string activityId,
                [FromBody] AddParticipantRequest request)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(request.ParticipantId))
                        return BadRequest("ParticipantId is required");

                    var success = await _cosmosDbService.AddParticipantToConcreteActivityAsync(activityId, transferWindowId, request.ParticipantId);
                    if (!success)
                        return NotFound("Concrete activity not found");

                    return Ok();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error adding participant: {ex.Message}");
                }
            }

            [HttpDelete("{activityId}/participants/{participantId}")]
            public async Task<ActionResult> RemoveParticipant(
                [FromRoute] string transferWindowId,
                [FromRoute] string activityId,
                [FromRoute] string participantId)
            {
                try
                {
                    var success = await _cosmosDbService.RemoveParticipantFromConcreteActivityAsync(activityId, transferWindowId, participantId);
                    if (!success)
                        return NotFound("Concrete activity not found");

                    return Ok();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error removing participant: {ex.Message}");
                }
            }
        }
}
