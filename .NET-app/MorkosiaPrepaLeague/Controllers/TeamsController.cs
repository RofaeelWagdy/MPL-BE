using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MorkosiaPrepaLeague.Models.Core;
using MorkosiaPrepaLeague.Models.Attributes;
using MorkosiaPrepaLeague.Models.Requests;
using MorkosiaPrepaLeague.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

namespace MorkosiaPrepaLeague.Controllers
{
    [ApiController]
    [Route("api/teams")]
    [MinRole(Role.User)]
    public class TeamsController : ControllerBase
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<TeamsController> _logger;

        public TeamsController(ICosmosDbService cosmosDbService, ILogger<TeamsController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Get all available players (members) in a league for team selection
        /// </summary>
        /// <param name="leagueId">The ID of the league</param>
        /// <returns>List of all members in the league as available players, with position-aware ownership caps</returns>
        [HttpGet("available-players/{leagueId}")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAvailablePlayers(string leagueId)
        {
            _logger.LogInformation("Getting available players for league {LeagueId}", leagueId);

            try
            {
                // Get the current user's ID from claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                // First, verify the league exists
                var league = await _cosmosDbService.GetLeagueByIdAsync(leagueId);
                if (league == null)
                {
                    _logger.LogWarning("League {LeagueId} not found", leagueId);
                    return NotFound(new { message = $"League with ID '{leagueId}' not found." });
                }

                // Get all users who are members of this league
                var allUsers = await _cosmosDbService.GetAllUsersForLeaguesAsync(new[] { leagueId });
                
                // Get current position-aware ownership counts for all players
                var positionOwnershipCounts = await _cosmosDbService.GetPlayerPositionOwnershipCountsAsync(leagueId);
                
                // If we have a current user, get their current team to provide more accurate availability
                // by simulating the removal of their current selections
                if (!string.IsNullOrEmpty(userId))
                {
                    var adjustedOwnershipCounts = await GetAdjustedOwnershipCounts(league.Id, userId, positionOwnershipCounts);
                    positionOwnershipCounts = adjustedOwnershipCounts;
                }
                
                // Get available positions from league configuration
                var availablePositions = league.TeamPositions?.Select(tp => tp.Name).ToList() ?? new List<string> { "PLAYER" };
                
                // Transform users into available players with position-aware information
                var availablePlayers = allUsers.Select(user => new
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Class = user.Class,
                    Price = league.GetPlayerPrice(user.Id!),
                    // Show availability for each position
                    Positions = availablePositions.Select(position => new
                    {
                        Position = position,
                        IsAvailable = IsPlayerAvailableForPosition(league, user.Id!, position, positionOwnershipCounts),
                        CurrentOwnership = GetCurrentOwnershipForPosition(user.Id!, position, positionOwnershipCounts),
                        OwnershipCap = league.GetEffectiveOwnershipCap(user.Id!, position)
                    }).ToList()
                }).ToList();

                _logger.LogInformation("Found {PlayerCount} players with position-aware availability in league {LeagueId}", 
                    availablePlayers.Count, leagueId);

                return Ok(availablePlayers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available players for league {LeagueId}", leagueId);
                return StatusCode(500, new { message = "An error occurred while retrieving available players." });
            }
        }

        /// <summary>
        /// Get the current user's team for a specific transfer window (or most recent if not specified)
        /// </summary>
        [HttpGet("my-team/{leagueId}")]
        [ProducesResponseType(typeof(Team), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyTeam(string leagueId, [FromQuery] string? windowId = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return BadRequest(new { message = "Unable to determine user ID" });

                TransferWindow? targetWindow;
                if (!string.IsNullOrEmpty(windowId))
                {
                    targetWindow = await _cosmosDbService.GetTransferWindowByIdAsync(leagueId, windowId);
                }
                else
                {
                    targetWindow = await _cosmosDbService.GetCurrentTransferWindowAsync(leagueId);
                }

                if (targetWindow == null)
                    return NotFound(new { message = "No transfer window found" });

                var team = await _cosmosDbService.GetManagerTeamForTransferWindowAsync(leagueId, targetWindow.Id!, userId);
                if (team == null)
                    return NotFound(new { message = "No team found for this window" });

                return Ok(team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team for user in league {LeagueId}", leagueId);
                return StatusCode(500, new { message = "An error occurred while retrieving your team." });
            }
        }

        /// <summary>
        /// Get team scores for a league based on activity participation and position matching
        /// </summary>
        /// <param name="leagueId">The ID of the league</param>
        /// <param name="windowId">Optional transfer window ID to filter scores for a specific week</param>
        /// <returns>Team scores with per-player, per-activity breakdown</returns>
        [HttpGet("scores/{leagueId}")]
        [ProducesResponseType(typeof(TeamScoresResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTeamScores(string leagueId, [FromQuery] string? windowId = null)
        {
            _logger.LogInformation("Getting team scores for league {LeagueId}, window {WindowId}", leagueId, windowId ?? "latest");

            try
            {
                var league = await _cosmosDbService.GetLeagueByIdAsync(leagueId);
                if (league == null)
                {
                    _logger.LogWarning("League {LeagueId} not found", leagueId);
                    return NotFound(new { message = $"League with ID '{leagueId}' not found." });
                }

                var targetWindow = await _cosmosDbService.GetCurrentTransferWindowAsync(leagueId);
                if (!string.IsNullOrEmpty(windowId))
                {
                    var specificWindow = await _cosmosDbService.GetTransferWindowByIdAsync(leagueId, windowId);
                    if (specificWindow != null) targetWindow = specificWindow;
                }
                if (targetWindow == null)
                {
                    _logger.LogWarning("No transfer windows found for league {LeagueId}", leagueId);
                    return NotFound(new { message = "No transfer windows found." });
                }

                var teams = new List<Team>();
                if (!string.IsNullOrEmpty(windowId))
                {
                    var specificWindow = await _cosmosDbService.GetTransferWindowByIdAsync(leagueId, windowId);
                    if (specificWindow != null)
                    {
                        var windowTeams = await _cosmosDbService.GetTeamsForTransferWindowAsync(leagueId, specificWindow.Id!);
                        teams.AddRange(windowTeams);
                    }
                }
                else
                {
                    var allWindows = await _cosmosDbService.GetAllTransferWindowsAsync(leagueId);
                    foreach (var w in allWindows)
                    {
                        var windowTeams = await _cosmosDbService.GetTeamsForTransferWindowAsync(leagueId, w.Id!);
                        teams.AddRange(windowTeams);
                    }
                }
                var activities = new List<ConcreteActivity>();
                if (!string.IsNullOrEmpty(windowId))
                {
                    var windowActivities = await _cosmosDbService.GetConcreteActivitiesForTransferWindowAsync(windowId);
                    activities.AddRange(windowActivities);
                }
                else
                {
                    var allWindows = await _cosmosDbService.GetAllTransferWindowsAsync(leagueId);
                    foreach (var w in allWindows)
                    {
                        var windowActivities = await _cosmosDbService.GetConcreteActivitiesForTransferWindowAsync(w.Id!);
                        activities.AddRange(windowActivities);
                    }
                }
                var activityTypes = await _cosmosDbService.GetActivityTypesForLeagueAsync(leagueId);
                var allUsers = await _cosmosDbService.GetAllUsersForLeaguesAsync(new[] { leagueId });

                var activityTypeLookup = activityTypes.ToDictionary(at => at.Id!, at => at);
                var userLookup = allUsers.ToDictionary(u => u.Id!, u => u);

                var windows = await _cosmosDbService.GetAllTransferWindowsAsync(leagueId);
                var windowLookup = windows.ToDictionary(w => w.Id!, w => w);
                var sortedWindows = windows.OrderBy(w => w.StartDate).ToList();

                var teamScoresList = new List<TeamScore>();

                foreach (var team in teams)
                {
                    var teamScore = new TeamScore
                    {
                        TeamId = team.Id!,
                        TransferWindowId = team.TransferWindowId,
                        ManagerUserId = team.ManagerUserId,
                        ManagerName = userLookup.TryGetValue(team.ManagerUserId, out var managerUser)
                            ? (managerUser.FullName ?? managerUser.Username)
                            : team.ManagerUserId
                    };

                    var playerScoresDict = new Dictionary<string, PlayerScore>();

                    foreach (var player in team.Players)
                    {
                        var playerName = userLookup.TryGetValue(player.PlayerId, out var playerUser)
                            ? (playerUser.FullName ?? playerUser.Username)
                            : player.PlayerId;

                        playerScoresDict[player.PlayerId] = new PlayerScore
                        {
                            PlayerId = player.PlayerId,
                            PlayerName = playerName,
                            Position = player.Position
                        };
                    }

                    foreach (var activity in activities)
                    {
                        if (activity.TransferWindowId != team.TransferWindowId) continue;
                        if (!activityTypeLookup.TryGetValue(activity.ActivityTypeId, out var activityType))
                            continue;

                        var activityName = activityType.Name;
                        var linkedPositions = activityType.LinkedPositions ?? new List<string>();
                        var points = activity.OverridePoints ?? activityType.DefaultPoints;
                        var activityDateStr = activity.Date.ToString("yyyy-MM-dd");
                        var participants = activity.ParticipantIds ?? new List<string>();

                        foreach (var playerScore in playerScoresDict.Values)
                        {
                            var participated = participants.Contains(playerScore.PlayerId);
                            var qualifies = participated && linkedPositions.Contains(playerScore.Position);
                            var earnedPoints = qualifies ? points : 0;

                            playerScore.Activities.Add(new PlayerActivityScore
                            {
                                ActivityId = activity.Id!,
                                ActivityName = activityName,
                                Date = activityDateStr,
                                Points = earnedPoints,
                                PotentialPoints = points,
                                Qualifies = qualifies,
                                Participated = participated
                            });
                        }
                    }

                    teamScore.Players = playerScoresDict.Values.ToList();
                    teamScore.TotalScore = teamScore.Players.Sum(p => p.Activities.Sum(a => a.Points));
                    teamScore.WeekScore = teamScore.TotalScore;
                    teamScoresList.Add(teamScore);
                }

                var response = new TeamScoresResponse { Teams = teamScoresList };

                _logger.LogInformation("Calculated scores for {TeamCount} teams in league {LeagueId}", teamScoresList.Count, leagueId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating team scores for league {LeagueId}", leagueId);
                return StatusCode(500, new { message = "An error occurred while calculating team scores." });
            }
        }

        /// <summary>
        /// Checks if a player is available for selection in a specific position based on ownership caps
        /// </summary>
        /// <param name="league">The league</param>
        /// <param name="playerId">The player's user ID</param>
        /// <param name="position">The position to check</param>
        /// <param name="positionOwnershipCounts">Current position-aware ownership counts for all players</param>
        /// <returns>True if the player is available for the position, false if they've reached their ownership cap</returns>
        private bool IsPlayerAvailableForPosition(League league, string playerId, string position, 
            Dictionary<string, Dictionary<string, int>> positionOwnershipCounts)
        {
            // Get current ownership count for this player-position combination
            var currentOwnership = GetCurrentOwnershipForPosition(playerId, position, positionOwnershipCounts);
            
            // Get the ownership cap for this player-position combination
            var ownershipCap = league.GetEffectiveOwnershipCap(playerId, position);
            
            // If no ownership cap is set, player is always available
            if (ownershipCap == null)
            {
                return true;
            }
            
            // Player is available if they haven't reached their ownership cap for this position
            return currentOwnership < ownershipCap.Value;
        }

        /// <summary>
        /// Gets the current ownership count for a player in a specific position
        /// </summary>
        /// <param name="playerId">The player's user ID</param>
        /// <param name="position">The position to check</param>
        /// <param name="positionOwnershipCounts">Current position-aware ownership counts for all players</param>
        /// <returns>The current ownership count for the player-position combination</returns>
        private int GetCurrentOwnershipForPosition(string playerId, string position, 
            Dictionary<string, Dictionary<string, int>> positionOwnershipCounts)
        {
            if (positionOwnershipCounts.ContainsKey(playerId) && 
                positionOwnershipCounts[playerId].ContainsKey(position))
            {
                return positionOwnershipCounts[playerId][position];
            }
            
            return 0;
        }

        /// <summary>
        /// Pick a team for the current transfer window
        /// </summary>
        /// <param name="request">The team selection request</param>
        /// <returns>The saved team</returns>
        [HttpPost("pick-team")]
        [ProducesResponseType(typeof(Team), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PickTeam([FromBody] PickTeamRequest request)
        {
            _logger.LogInformation("User attempting to pick team for league {LeagueId}", request.LeagueId);

            try
            {
                // Get the current user's ID from claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Unable to determine user ID from authentication token");
                    return BadRequest(new { message = "Unable to determine user ID from authentication token" });
                }

                // Validate that user can pick a team from this league (must be member, not admin/superadmin)
                if (!AuthorizationUtilities.CanUserPickTeamFromLeague(User, request.LeagueId))
                {
                    _logger.LogWarning("User {UserId} is not authorized to pick a team from league {LeagueId}", userId, request.LeagueId);
                    return Forbid("You are not authorized to pick a team from this league. Only league members can pick teams.");
                }

                // Check if the league exists
                var league = await _cosmosDbService.GetLeagueByIdAsync(request.LeagueId);
                if (league == null)
                {
                    _logger.LogWarning("League {LeagueId} not found", request.LeagueId);
                    return NotFound(new { message = $"League with ID '{request.LeagueId}' not found" });
                }
                
                // Check if there's an active transfer window
                var currentWindow = await _cosmosDbService.GetCurrentTransferWindowAsync(request.LeagueId);
                if (currentWindow == null)
                {
                    _logger.LogWarning("No active transfer window found for league {LeagueId}", request.LeagueId);
                    return BadRequest(new { message = "No active transfer window for this league" });
                }

                // Create the Team entity from the request
                var team = new Team
                {
                    LeagueId = request.LeagueId,
                    ManagerUserId = userId,
                    TransferWindowId = currentWindow.Id!,
                    Players = request.Players.Select(p => new PlayerTeamPosition
                    {
                        PlayerId = p.PlayerId,
                        Position = p.Position
                    }).ToList()
                };

                // Validate positional structure
                var positionValidationResult = ValidateTeamPositionalStructure(team, league);
                if (!positionValidationResult.IsValid)
                {
                    _logger.LogWarning("Positional structure validation failed for user {UserId} in league {LeagueId}: {ValidationMessage}",
                        userId, request.LeagueId, positionValidationResult.ErrorMessage);
                    return BadRequest(new { message = positionValidationResult.ErrorMessage });
                }

                // Validate manager ownership - ensure the team's ManagerUserId matches the authenticated user
                if (team.ManagerUserId != userId)
                {
                    _logger.LogWarning("Manager ownership validation failed for user {UserId} in league {LeagueId}: Manager ownership is invalid",
                        userId, request.LeagueId);
                    return BadRequest(new { message = "Manager ownership is invalid. Please try again." });
                }

                // Validate player validity - ensure all selected players exist and are members of the league
                var playerValidityResult = await ValidatePlayerValidity(team, league);
                if (!playerValidityResult.IsValid)
                {
                    _logger.LogWarning("Player validity validation failed for user {UserId} in league {LeagueId}: {ValidationMessage}",
                        userId, request.LeagueId, playerValidityResult.ErrorMessage);
                    return BadRequest(new { message = playerValidityResult.ErrorMessage });
                }

                // Validate budget - check if total cost of selected players exceeds manager's budget
                var managerBudget = league.GetEffectiveBudget(userId);
                if (managerBudget.HasValue)
                {
                    var totalCost = 0;
                    foreach (var player in team.Players)
                    {
                        var playerPrice = league.GetPlayerPrice(player.PlayerId);
                        totalCost += playerPrice;
                    }

                    if (totalCost > managerBudget.Value)
                    {
                        _logger.LogWarning("User {UserId} attempted to select team with total cost {TotalCost} exceeding budget {Budget} in league {LeagueId}",
                            userId, totalCost, managerBudget.Value, request.LeagueId);
                        return BadRequest(new
                        {
                            message = $"Team total cost ({totalCost:N0}) exceeds your budget ({managerBudget.Value:N0})",
                            totalCost = totalCost,
                            budget = managerBudget.Value,
                            exceeded = totalCost - managerBudget.Value
                        });
                    }

                    _logger.LogInformation("Budget validation passed for user {UserId}: total cost {TotalCost} within budget {Budget}",
                        userId, totalCost, managerBudget.Value);
                }
                else
                {
                    return StatusCode(500, new { message = "No budget configured for this league" });
                }

                // Validate player ownership caps using optimistic concurrency control
                const int maxRetries = 3;
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        // Get or initialize the ownership state for this transfer window
                        var (ownershipState, expectedETag) = await _cosmosDbService.GetLeagueOwnershipStateWithETagAsync(league.Id!, currentWindow.Id!);
                        if (ownershipState == null)
                        {
                            ownershipState = await _cosmosDbService.InitializeLeagueOwnershipStateAsync(league.Id!, currentWindow.Id!);
                            // Get the ETag for the newly created state
                            (ownershipState, expectedETag) = await _cosmosDbService.GetLeagueOwnershipStateWithETagAsync(league.Id!, currentWindow.Id!);
                            
                            if (ownershipState == null)
                            {
                                _logger.LogError("Failed to initialize ownership state for user {UserId} in league {LeagueId} transfer window {TransferWindowId}", 
                                    userId, request.LeagueId, currentWindow.Id);
                                return StatusCode(500, new { message = "Failed to initialize ownership tracking for this transfer window" });
                            }
                        }

                        // Validate ownership caps against the current state
                        var ownershipValidationResult = ValidatePlayerOwnershipCapsAgainstState(team, league, ownershipState, userId);
                        if (!ownershipValidationResult.IsValid)
                        {
                            _logger.LogWarning("Ownership cap validation failed for user {UserId} in league {LeagueId}: {ValidationMessage}",
                                userId, request.LeagueId, ownershipValidationResult.ErrorMessage);
                            return BadRequest(new { message = ownershipValidationResult.ErrorMessage });
                        }

                        // Attempt to save the team and update ownership state atomically
                        var saveSuccess = await _cosmosDbService.SaveTeamWithOwnershipUpdateAsync(team, ownershipState, expectedETag ?? string.Empty);
                        
                        if (saveSuccess)
                        {
                            _logger.LogInformation("Successfully saved team {TeamId} for user {UserId} in league {LeagueId} with atomic ownership update", 
                                team.Id, userId, request.LeagueId);
                            return Ok(team);
                        }
                        else
                        {
                            _logger.LogInformation("Optimistic concurrency conflict detected for user {UserId} in league {LeagueId}, retry {Retry}/{MaxRetries}", 
                                userId, request.LeagueId, retry + 1, maxRetries);
                            
                            if (retry == maxRetries - 1)
                            {
                                _logger.LogWarning("Maximum retries exceeded for user {UserId} in league {LeagueId}", userId, request.LeagueId);
                                return Conflict(new { message = "Another user modified the team ownership while you were making your selection. Please try again." });
                            }
                            
                            // Wait a bit before retrying to reduce contention
                            await Task.Delay(100 + (retry * 50));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during atomic team save attempt {Retry} for user {UserId} in league {LeagueId}", retry + 1, userId, request.LeagueId);
                        
                        if (retry == maxRetries - 1)
                        {
                            throw;
                        }
                    }
                }

                // This should never be reached due to the loop structure above
                return StatusCode(500, new { message = "Unexpected error during team save operation" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error picking team for league {LeagueId}", request.LeagueId);
                return StatusCode(500, new { message = "An error occurred while picking the team" });
            }
        }

        /// <summary>
        /// Validates that a team's positional structure matches the league's requirements
        /// </summary>
        /// <param name="team">The team to validate</param>
        /// <param name="league">The league with position requirements</param>
        /// <returns>A validation result indicating success or failure with error message</returns>
        private (bool IsValid, string ErrorMessage) ValidateTeamPositionalStructure(Team team, League league)
        {
            // Check if league has defined team positions
            if (league.TeamPositions == null || !league.TeamPositions.Any())
            {
                return (false, "League does not have a defined team structure");
            }

            // Check for duplicate players in the team
            var playerIds = team.Players.Select(p => p.PlayerId).ToList();
            var duplicatePlayerIds = playerIds.GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatePlayerIds.Any())
            {
                return (false, $"Player(s) selected multiple times: {string.Join(", ", duplicatePlayerIds)}");
            }

            // Group players by position to count them
            var playersByPosition = team.Players.GroupBy(p => p.Position)
                .ToDictionary(g => g.Key, g => g.Count());

            // Validate each required position
            foreach (var requiredPosition in league.TeamPositions)
            {
                var actualCount = playersByPosition.GetValueOrDefault(requiredPosition.Name, 0);
                
                if (actualCount != requiredPosition.Count)
                {
                    return (false, $"Position {requiredPosition.Name} requires {requiredPosition.Count} player(s), but {actualCount} were selected");
                }
            }

            // Check for players assigned to positions not defined in the league
            var validPositions = league.TeamPositions.Select(p => p.Name).ToHashSet();
            var invalidPositions = playersByPosition.Keys.Where(pos => !validPositions.Contains(pos)).ToList();
            
            if (invalidPositions.Any())
            {
                return (false, $"Invalid position(s): {string.Join(", ", invalidPositions)}");
            }

            // Verify total team size matches expected size
            var expectedTotalSize = league.GetTotalTeamSize();
            var actualTotalSize = team.Players.Count;
            
            if (expectedTotalSize.HasValue && actualTotalSize != expectedTotalSize.Value)
            {
                return (false, $"Team must have exactly {expectedTotalSize.Value} players, but {actualTotalSize} were selected");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates that all players in the team are valid members of the league
        /// </summary>
        /// <param name="team">The team with selected players</param>
        /// <param name="league">The league to validate against</param>
        /// <returns>A validation result indicating success or failure with error message</returns>
        private async Task<(bool IsValid, string ErrorMessage)> ValidatePlayerValidity(Team team, League league)
        {
            // Get all users who are members of this league
            var allUsers = await _cosmosDbService.GetAllUsersForLeaguesAsync(new[] { league.Id });
            var allUserIds = allUsers.Select(u => u.Id).ToHashSet();

            // Check each player in the team
            foreach (var player in team.Players)
            {
                // Player must be a member of the league
                if (!allUserIds.Contains(player.PlayerId))
                {
                    var playerUser = await _cosmosDbService.GetUserByIdAsync(player.PlayerId);
                    var playerName = playerUser?.FullName ?? playerUser?.Username ?? player.PlayerId;

                    return (false, $"Player {playerName} is not a valid member of the league.");
                }
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates that a team's player ownership selections don't exceed ownership caps using the provided ownership state
        /// </summary>
        /// <param name="team">The team to validate</param>
        /// <param name="league">The league with ownership cap rules</param>
        /// <param name="ownershipState">The current ownership state for validation</param>
        /// <param name="managerUserId">The user ID of the team manager</param>
        /// <returns>A validation result indicating success or failure with error message</returns>
        private (bool IsValid, string ErrorMessage) ValidatePlayerOwnershipCapsAgainstState(Team team, League league, LeagueOwnershipState ownershipState, string managerUserId)
        {
            // Create a working copy of ownership counts to simulate the new team's impact
            var workingOwnershipCounts = new Dictionary<string, Dictionary<string, int>>();
            
            // Deep copy the current ownership state
            foreach (var playerEntry in ownershipState.OwnershipCounts)
            {
                workingOwnershipCounts[playerEntry.Key] = new Dictionary<string, int>(playerEntry.Value);
            }
            
            // Check each player in the new team selection
            foreach (var player in team.Players)
            {
                // Initialize player entry if it doesn't exist
                if (!workingOwnershipCounts.ContainsKey(player.PlayerId))
                {
                    workingOwnershipCounts[player.PlayerId] = new Dictionary<string, int>();
                }
                
                // Initialize position entry if it doesn't exist
                if (!workingOwnershipCounts[player.PlayerId].ContainsKey(player.Position))
                {
                    workingOwnershipCounts[player.PlayerId][player.Position] = 0;
                }
                
                // Get the ownership cap for this player-position combination
                var ownershipCap = league.GetEffectiveOwnershipCap(player.PlayerId, player.Position);
                var currentOwnership = workingOwnershipCounts[player.PlayerId][player.Position];
                
                // Check if adding this player would exceed the cap
                if (currentOwnership >= ownershipCap)
                {
                    return (false, $"Player {player.PlayerId} cannot be selected for position {player.Position}. " +
                                  $"Current ownership: {currentOwnership}/{ownershipCap}, cap would be exceeded.");
                }
                
                // Simulate adding this player for subsequent validations in the same team
                workingOwnershipCounts[player.PlayerId][player.Position]++;
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Gets ownership counts adjusted for the current manager's existing team
        /// This simulates removing the manager's current team from ownership counts
        /// to show accurate availability when they're replacing their team
        /// </summary>
        /// <param name="leagueId">The league ID</param>
        /// <param name="managerUserId">The manager's user ID</param>
        /// <param name="currentOwnershipCounts">The current ownership counts</param>
        /// <returns>Adjusted ownership counts with the manager's current team removed</returns>
        private async Task<Dictionary<string, Dictionary<string, int>>> GetAdjustedOwnershipCounts(
            string leagueId, string managerUserId, Dictionary<string, Dictionary<string, int>> currentOwnershipCounts)
        {
            // Create a copy of the ownership counts to avoid modifying the original
            var adjustedCounts = new Dictionary<string, Dictionary<string, int>>();
            foreach (var playerEntry in currentOwnershipCounts)
            {
                adjustedCounts[playerEntry.Key] = new Dictionary<string, int>(playerEntry.Value);
            }
            
            // Get the manager's current team to subtract from ownership counts using the direct lookup method
            var currentTransferWindow = await _cosmosDbService.GetCurrentTransferWindowAsync(leagueId);
            if (currentTransferWindow != null)
            {
                var currentManagerTeam = await _cosmosDbService.GetManagerTeamForTransferWindowAsync(
                    leagueId, currentTransferWindow.Id!, managerUserId);
                
                if (currentManagerTeam != null)
                {
                    foreach (var player in currentManagerTeam.Players)
                    {
                        if (adjustedCounts.ContainsKey(player.PlayerId) && 
                            adjustedCounts[player.PlayerId].ContainsKey(player.Position))
                        {
                            adjustedCounts[player.PlayerId][player.Position]--;
                            
                            // Remove the entry if count reaches zero to keep dictionary clean
                            if (adjustedCounts[player.PlayerId][player.Position] <= 0)
                            {
                                adjustedCounts[player.PlayerId].Remove(player.Position);
                            }
                        }
                    }
                }
            }
            
            return adjustedCounts;
        }
    }
}
