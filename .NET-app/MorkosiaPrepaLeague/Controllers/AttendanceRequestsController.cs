using Microsoft.AspNetCore.Mvc;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Requests;
using MorkosiaPrepaLeague.Services;

namespace MorkosiaPrepaLeague.Controllers
{
    [Route("api/v1/attendance-requests")]
    [ApiController]
    public class AttendanceRequestsController : ControllerBase
    {
        private readonly ICosmosDbService _cosmosDbService;

        public AttendanceRequestsController(ICosmosDbService cosmosDbService)
        {
            _cosmosDbService = cosmosDbService;
        }

        [HttpPost]
        public async Task<ActionResult<AttendanceRequest>> CreateAttendanceRequest(
            [FromBody] CreateAttendanceRequestRequest request)
        {
            try
            {
                // First check if already a participant (already attended)
                var concreteActivity = await _cosmosDbService.GetConcreteActivityByIdAsync(request.ActivityId, request.TransferWindowId);
                if (concreteActivity != null && concreteActivity.ParticipantIds.Contains(request.StudentUserId))
                {
                    return BadRequest("You have already attended this activity");
                }
                
                var existingRequests = await _cosmosDbService.GetAttendanceRequestsForStudentAsync(request.LeagueId, request.StudentUserId);
                var pendingRequest = existingRequests.FirstOrDefault(r => 
                    r.ActivityId == request.ActivityId && 
                    r.Status == AttendanceRequestStatus.Pending);
                
                if (pendingRequest != null)
                {
                    return BadRequest("You have already submitted a request for this activity");
                }
                
                var attendanceRequest = new AttendanceRequest
                {
                    LeagueId = request.LeagueId,
                    TransferWindowId = request.TransferWindowId,
                    ActivityId = request.ActivityId,
                    StudentUserId = request.StudentUserId,
                    StudentName = request.StudentName,
                    ActivityName = request.ActivityName,
                    ActivityDate = request.ActivityDate,
                    Status = AttendanceRequestStatus.Pending,
                    RequestedAt = DateTime.UtcNow
                };

                var result = await _cosmosDbService.CreateAttendanceRequestAsync(attendanceRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating attendance request: {ex.Message}");
            }
        }

        [HttpGet("league/{leagueId}/pending")]
        public async Task<ActionResult<IEnumerable<AttendanceRequest>>> GetPendingRequests(
            [FromRoute] string leagueId)
        {
            try
            {
                var requests = await _cosmosDbService.GetPendingAttendanceRequestsForLeagueAsync(leagueId);
                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving pending requests: {ex.Message}");
            }
        }

        [HttpGet("student/{studentUserId}")]
        public async Task<ActionResult<IEnumerable<AttendanceRequest>>> GetStudentRequests(
            [FromQuery] string leagueId,
            [FromRoute] string studentUserId)
        {
            try
            {
                var requests = await _cosmosDbService.GetAttendanceRequestsForStudentAsync(leagueId, studentUserId);
                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving student requests: {ex.Message}");
            }
        }

        [HttpPost("process")]
        public async Task<ActionResult<AttendanceRequest>> ProcessRequest(
            [FromBody] ProcessAttendanceRequestRequest request,
            [FromQuery] string processedByUserId)
        {
            try
            {
                var attendanceRequest = await _cosmosDbService.GetAttendanceRequestByIdAsync(request.RequestId);
                if (attendanceRequest == null)
                {
                    return NotFound("Attendance request not found");
                }

                if (attendanceRequest.Status != AttendanceRequestStatus.Pending)
                {
                    return BadRequest("Request has already been processed");
                }

                if (request.Approve)
                {
                    attendanceRequest.Status = AttendanceRequestStatus.Approved;
                    
                    var added = await _cosmosDbService.AddParticipantToConcreteActivityAsync(
                        attendanceRequest.ActivityId,
                        attendanceRequest.TransferWindowId,
                        attendanceRequest.StudentUserId);
                    
                    if (!added)
                    {
                        return BadRequest("Failed to add participant to activity");
                    }
                }
                else
                {
                    attendanceRequest.Status = AttendanceRequestStatus.Rejected;
                }

                attendanceRequest.ProcessedAt = DateTime.UtcNow;
                attendanceRequest.ProcessedByUserId = processedByUserId;

                var result = await _cosmosDbService.UpdateAttendanceRequestAsync(attendanceRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing attendance request: {ex.Message}");
            }
        }
    }
}