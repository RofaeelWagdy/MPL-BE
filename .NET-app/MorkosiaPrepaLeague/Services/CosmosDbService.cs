using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MorkosiaPrepaLeague.Models.Core;
using User = MorkosiaPrepaLeague.Models.Core.User;

namespace MorkosiaPrepaLeague.Services
{
    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container _leaguesContainer;
        private readonly Container _usersContainer;
        private readonly Container _transferWindowsContainer;
        private readonly Container _teamsContainer;
        private readonly Container _activityTypesContainer;
        private readonly Container _concreteActivitiesContainer;
        private readonly Container _attendanceRequestsContainer;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(IConfiguration configuration, ILogger<CosmosDbService> logger)
        {
            _logger = logger;
            var connectionString = configuration["CosmosDb:ConnectionString"];
            var databaseName = configuration["CosmosDb:DatabaseName"];
            var leaguesContainerName = "Leagues";
            var usersContainerName = "Users";
            var transferWindowsContainerName = "TransferWindows";
            var teamsContainerName = "Teams";
            var activityTypesContainerName = "ActivityTypes";
            var concreteActivitiesContainerName = "ConcreteActivities";
            var attendanceRequestsContainerName = "AttendanceRequests";

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName))
            {
                _logger.LogError("Cosmos DB ConnectionString or DatabaseName is not configured.");
                throw new InvalidOperationException("Cosmos DB configuration is missing.");
            }

            var cosmosClient = new CosmosClient(
                connectionString,
                new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    },
                }
            );

            var database = cosmosClient.GetDatabase(databaseName);
            _leaguesContainer = database.GetContainer(leaguesContainerName);
            _usersContainer = database.GetContainer(usersContainerName);
            _transferWindowsContainer = database.GetContainer(transferWindowsContainerName);
            _teamsContainer = database.GetContainer(teamsContainerName);
            _activityTypesContainer = database.GetContainer(activityTypesContainerName);
            _concreteActivitiesContainer = database.GetContainer(concreteActivitiesContainerName);
            _attendanceRequestsContainer = database.GetContainer(attendanceRequestsContainerName);

            _logger.LogInformation(
                "CosmosDbService initialized for database '{DbName}' and containers '{LeaguesContainerName}', '{UsersContainerName}', '{TransferWindowsContainerName}', '{TeamsContainerName}', '{ActivityTypesContainerName}', '{ConcreteActivitiesContainerName}'.",
                databaseName,
                leaguesContainerName,
                usersContainerName,
                transferWindowsContainerName,
                teamsContainerName,
                activityTypesContainerName,
                concreteActivitiesContainerName
            );
        }

        public async Task<League?> AddLeagueAsync(League league)
        {
            try
            {
                if (string.IsNullOrEmpty(league.Id))
                    league.Id = Guid.NewGuid().ToString();
                league.Ver = 1;

                ItemResponse<League> response = await _leaguesContainer.CreateItemAsync(
                    league,
                    new PartitionKey(league.Id)
                );
                _logger.LogInformation(
                    "Created league with ID: {LeagueId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    league.Id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning(
                    ex,
                    "Conflict creating league with ID: {LeagueId}. It might already exist.",
                    league.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error creating league with ID: {LeagueId}", league.Id);
                throw;
            }
        }

        public async Task<bool> LeagueNameExistsAsync(string name)
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.name) = LOWER(@name)"
                ).WithParameter("@name", name);

                var queryIterator = _leaguesContainer.GetItemQueryIterator<int>(query);
                if (queryIterator.HasMoreResults)
                {
                    FeedResponse<int> response = await queryIterator.ReadNextAsync();
                    _logger.LogDebug(
                        "League name check query for '{LeagueName}' returned count. Request Charge: {RequestCharge}",
                        name,
                        response.RequestCharge
                    );
                    int count = response.FirstOrDefault();
                    return count > 0;
                }
                _logger.LogWarning(
                    "League name check query for '{LeagueName}' returned no results.",
                    name
                );
                return false;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error checking if league name exists: {LeagueName}",
                    name
                );
                throw;
            }
        }

        public async Task<League?> GetLeagueByIdAsync(string id)
        {
            try
            {
                ItemResponse<League> response = await _leaguesContainer.ReadItemAsync<League>(
                    id,
                    new PartitionKey(id)
                );
                _logger.LogInformation(
                    "Read league with ID: {LeagueId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("League with ID: {LeagueId} not found.", id);
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error reading league with ID: {LeagueId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<League>> GetLeaguesByIdsAsync(IEnumerable<string> ids)
        {
            try
            {
                if (ids == null || !ids.Any())
                {
                    _logger.LogWarning(
                        "GetLeaguesByIdsAsync called with empty or null ids collection"
                    );
                    return Enumerable.Empty<League>();
                }

                var idsList = ids.ToList();
                var parameterizedQuery = new StringBuilder("SELECT * FROM c WHERE c.id IN (");

                for (int i = 0; i < idsList.Count; i++)
                {
                    parameterizedQuery.Append($"@id{i}");
                    if (i < idsList.Count - 1)
                    {
                        parameterizedQuery.Append(", ");
                    }
                }
                parameterizedQuery.Append(")");

                var queryDefinition = new QueryDefinition(parameterizedQuery.ToString());
                for (int i = 0; i < idsList.Count; i++)
                {
                    queryDefinition = queryDefinition.WithParameter($"@id{i}", idsList[i]);
                }

                _logger.LogInformation("Querying {Count} league IDs in batch", idsList.Count);

                var results = new List<League>();
                var queryIterator = _leaguesContainer.GetItemQueryIterator<League>(queryDefinition);

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    _logger.LogDebug(
                        "Batch leagues query returned {ResultCount} leagues. Request Charge: {RequestCharge}",
                        response.Count,
                        response.RequestCharge
                    );
                    results.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {ResultCount} leagues from {RequestCount} IDs",
                    results.Count,
                    idsList.Count
                );
                return results;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error retrieving batch of leagues");
                throw;
            }
        }

        public async Task<User?> AddUserAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.Id))
                    user.Id = Guid.NewGuid().ToString();
                user.Ver = 1;

                ItemResponse<User> response = await _usersContainer.CreateItemAsync(
                    user,
                    new PartitionKey(user.Id)
                );
                _logger.LogInformation(
                    "Created user with ID: {UserId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    user.Id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning(
                    ex,
                    "Conflict creating user with ID: {UserId}. It might already exist.",
                    user.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error creating user with ID: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.username) = LOWER(@username)"
                ).WithParameter("@username", username);

                var queryIterator = _usersContainer.GetItemQueryIterator<int>(query);
                if (queryIterator.HasMoreResults)
                {
                    FeedResponse<int> response = await queryIterator.ReadNextAsync();
                    _logger.LogDebug(
                        "Username check query for '{Username}' returned count. Request Charge: {RequestCharge}",
                        username,
                        response.RequestCharge
                    );
                    int count = response.FirstOrDefault();
                    return count > 0;
                }
                _logger.LogWarning(
                    "Username check query for '{Username}' returned no results.",
                    username
                );
                return false;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error checking if username exists: {Username}",
                    username
                );
                throw;
            }
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            try
            {
                ItemResponse<User> response = await _usersContainer.ReadItemAsync<User>(
                    id,
                    new PartitionKey(id)
                );
                _logger.LogInformation(
                    "Read user with ID: {UserId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User with ID: {UserId} not found.", id);
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error reading user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                _logger.LogInformation("Querying user by username: {Username}", username);

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE LOWER(c.username) = LOWER(@username)"
                ).WithParameter("@username", username);

                var queryIterator = _usersContainer.GetItemQueryIterator<User>(query);

                if (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    _logger.LogDebug(
                        "Username query for '{Username}' returned {Count} results. Request Charge: {RequestCharge}",
                        username,
                        response.Count,
                        response.RequestCharge
                    );

                    return response.FirstOrDefault();
                }

                _logger.LogWarning("No user found with username: {Username}", username);
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error querying user by username: {Username}",
                    username
                );
                throw;
            }
        }

        public async Task<User?> UpdateUserAsync(User user)
        {
            try
            {
                ItemResponse<User> response = await _usersContainer.ReplaceItemAsync(
                    user,
                    user.Id,
                    new PartitionKey(user.Id)
                );

                _logger.LogInformation(
                    "Updated user with ID: {UserId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    user.Id,
                    response.StatusCode,
                    response.RequestCharge
                );

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User with ID: {UserId} not found for update.", user.Id);
                return null;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                _logger.LogWarning(
                    "Optimistic concurrency violation updating user with ID: {UserId}. The user was modified by another operation.",
                    user.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error updating user with ID: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<IEnumerable<League>> GetAllLeaguesAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all leagues");

                var query = new QueryDefinition("SELECT * FROM c");
                var queryIterator = _leaguesContainer.GetItemQueryIterator<League>(query);

                var results = new List<League>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    _logger.LogDebug(
                        "Retrieved {Count} leagues. Request Charge: {RequestCharge}",
                        response.Count,
                        response.RequestCharge
                    );
                    results.AddRange(response);
                }

                _logger.LogInformation("Retrieved total of {Count} leagues", results.Count);
                return results;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error retrieving all leagues");
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetAllUsersForLeaguesAsync(
            IEnumerable<string> leagueIds
        )
        {
            try
            {
                _logger.LogInformation(
                    "Retrieving all users for {Count} leagues",
                    leagueIds.Count()
                );

                var accessibleLeagueIds = leagueIds.ToList();
                if (!accessibleLeagueIds.Any())
                {
                    _logger.LogWarning("No accessible leagues specified for user query");
                    return new List<User>();
                }

                var queryText = new StringBuilder("SELECT * FROM c WHERE ");
                var leagueConditions = new List<string>();

                for (int i = 0; i < accessibleLeagueIds.Count; i++)
                {
                    leagueConditions.Add(
                        $"ARRAY_CONTAINS(c.leaguesAdmin, @leagueId{i}) OR ARRAY_CONTAINS(c.leaguesMember, @leagueId{i})"
                    );
                }

                queryText.Append($"({string.Join(" OR ", leagueConditions)})");

                var queryDefinition = new QueryDefinition(queryText.ToString());
                for (int i = 0; i < accessibleLeagueIds.Count; i++)
                {
                    queryDefinition = queryDefinition.WithParameter(
                        $"@leagueId{i}",
                        accessibleLeagueIds[i]
                    );
                }

                var queryIterator = _usersContainer.GetItemQueryIterator<User>(queryDefinition);
                var users = new List<User>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    _logger.LogDebug(
                        "Retrieved {Count} users. Request Charge: {RequestCharge}",
                        response.Count,
                        response.RequestCharge
                    );
                    users.AddRange(response);
                }

                _logger.LogInformation("Successfully retrieved {Count} total users", users.Count);
                return users;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error retrieving users for leagues");
                throw;
            }
        }

        public async Task<bool> UpdateLeagueAsync(League league)
        {
            try
            {
                ItemResponse<League> response = await _leaguesContainer.ReplaceItemAsync(
                    league,
                    league.Id,
                    new PartitionKey(league.Id)
                );

                _logger.LogInformation(
                    "Updated league with ID: {LeagueId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    league.Id,
                    response.StatusCode,
                    response.RequestCharge
                );

                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("League with ID: {LeagueId} not found for update.", league.Id);
                return false;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                _logger.LogWarning(
                    "Optimistic concurrency violation updating league with ID: {LeagueId}. The league was modified by another operation.",
                    league.Id
                );
                return false;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos error updating league with ID: {LeagueId}", league.Id);
                throw;
            }
        }

        public async Task<IEnumerable<TransferWindow>> GetAllTransferWindowsAsync(string leagueId)
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.leagueId = @leagueId ORDER BY c.startDate ASC"
                ).WithParameter("@leagueId", leagueId);

                var queryIterator = _transferWindowsContainer.GetItemQueryIterator<TransferWindow>(
                    query
                );
                var results = new List<TransferWindow>();
                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    results.AddRange(response);
                }
                return results;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error getting all transfer windows for league {LeagueId}",
                    leagueId
                );
                throw;
            }
        }

        public async Task<TransferWindow?> GetCurrentTransferWindowAsync(string leagueId)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var query = new QueryDefinition(
                    @"SELECT * FROM c 
                      WHERE c.leagueId = @leagueId 
                      AND c.startDate <= @currentTime 
                      ORDER BY c.startDate DESC 
                      OFFSET 0 LIMIT 1"
                ).WithParameter("@leagueId", leagueId).WithParameter("@currentTime", currentTime);

                var queryIterator = _transferWindowsContainer.GetItemQueryIterator<TransferWindow>(
                    query
                );
                if (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    return response.FirstOrDefault();
                }
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving current transfer window for league {LeagueId}",
                    leagueId
                );
                throw;
            }
        }

        public async Task<TransferWindow?> GetTransferWindowByIdAsync(
            string leagueId,
            string windowId
        )
        {
            try
            {
                return await _transferWindowsContainer.ReadItemAsync<TransferWindow?>(
                    windowId,
                    new PartitionKey(leagueId)
                );
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<TransferWindow?> CreateTransferWindowAsync(TransferWindow transferWindow)
        {
            try
            {
                if (string.IsNullOrEmpty(transferWindow.Id))
                    transferWindow.Id = Guid.NewGuid().ToString();

                // Calculate window number
                var existingWindows = await GetAllTransferWindowsAsync(transferWindow.LeagueId);
                transferWindow.WindowNumber = existingWindows.Count() + 1;
                transferWindow.Ver = 1;

                var response = await _transferWindowsContainer.CreateItemAsync(
                    transferWindow,
                    new PartitionKey(transferWindow.LeagueId)
                );

                _logger.LogInformation(
                    "Created transfer window {WindowId} for league {LeagueId}",
                    transferWindow.Id,
                    transferWindow.LeagueId
                );

                // Initialize the ownership state for this transfer window
                try
                {
                    await InitializeLeagueOwnershipStateAsync(
                        transferWindow.LeagueId,
                        transferWindow.Id
                    );
                    _logger.LogInformation(
                        "Initialized ownership state for transfer window {WindowId} in league {LeagueId}",
                        transferWindow.Id,
                        transferWindow.LeagueId
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to initialize ownership state for transfer window {WindowId} in league {LeagueId}. This may be handled during first team submission.",
                        transferWindow.Id,
                        transferWindow.LeagueId
                    );
                    // Don't fail the transfer window creation if ownership state initialization fails
                    // It will be created on first team submission if needed
                }

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating transfer window for league {LeagueId}",
                    transferWindow.LeagueId
                );
                throw;
            }
        }

        public async Task<IEnumerable<Team>> GetAllTeamsInLeagueAsync(string leagueId)
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.leagueId = @leagueId"
                ).WithParameter("@leagueId", leagueId);

                var queryIterator = _teamsContainer.GetItemQueryIterator<Team>(query);
                var teams = new List<Team>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    teams.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {TeamCount} teams for league {LeagueId}",
                    teams.Count,
                    leagueId
                );
                return teams;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Error retrieving teams for league {LeagueId}", leagueId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetPlayerOwnershipCountsAsync(string leagueId)
        {
            try
            {
                // Get the current transfer window to only count teams from the active window
                var currentWindow = await GetCurrentTransferWindowAsync(leagueId);
                if (currentWindow == null)
                {
                    // No active transfer window, return empty counts
                    _logger.LogInformation(
                        "No active transfer window found for league {LeagueId}, returning empty ownership counts",
                        leagueId
                    );
                    return new Dictionary<string, int>();
                }

                var teams = await GetTeamsForTransferWindowAsync(leagueId, currentWindow.Id!);
                var ownershipCounts = new Dictionary<string, int>();

                foreach (var team in teams)
                {
                    foreach (var player in team.Players)
                    {
                        if (ownershipCounts.ContainsKey(player.PlayerId))
                        {
                            ownershipCounts[player.PlayerId]++;
                        }
                        else
                        {
                            ownershipCounts[player.PlayerId] = 1;
                        }
                    }
                }

                _logger.LogInformation(
                    "Calculated ownership counts for {PlayerCount} players in league {LeagueId} transfer window {TransferWindowId}",
                    ownershipCounts.Count,
                    leagueId,
                    currentWindow.Id
                );

                return ownershipCounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calculating player ownership counts for league {LeagueId}",
                    leagueId
                );
                throw;
            }
        }

        /// <summary>
        /// Gets player ownership counts by position for a specific league
        /// Returns a dictionary where the key is the player ID and the value is another dictionary
        /// containing position names as keys and ownership counts as values
        /// </summary>
        /// <param name="leagueId">The league ID to get ownership counts for</param>
        /// <returns>Dictionary of player ID -> Dictionary of position -> count</returns>
        public async Task<
            Dictionary<string, Dictionary<string, int>>
        > GetPlayerPositionOwnershipCountsAsync(string leagueId)
        {
            try
            {
                // Get the current transfer window to only count teams from the active window
                var currentWindow = await GetCurrentTransferWindowAsync(leagueId);
                if (currentWindow == null)
                {
                    // No active transfer window, return empty counts
                    _logger.LogInformation(
                        "No active transfer window found for league {LeagueId}, returning empty ownership counts",
                        leagueId
                    );
                    return new Dictionary<string, Dictionary<string, int>>();
                }

                var teams = await GetTeamsForTransferWindowAsync(leagueId, currentWindow.Id!);
                var positionOwnershipCounts = new Dictionary<string, Dictionary<string, int>>();

                foreach (var team in teams)
                {
                    foreach (var player in team.Players)
                    {
                        // Initialize player entry if it doesn't exist
                        if (!positionOwnershipCounts.ContainsKey(player.PlayerId))
                        {
                            positionOwnershipCounts[player.PlayerId] =
                                new Dictionary<string, int>();
                        }

                        // Increment count for this player-position combination
                        if (positionOwnershipCounts[player.PlayerId].ContainsKey(player.Position))
                        {
                            positionOwnershipCounts[player.PlayerId][player.Position]++;
                        }
                        else
                        {
                            positionOwnershipCounts[player.PlayerId][player.Position] = 1;
                        }
                    }
                }

                _logger.LogInformation(
                    "Calculated position-aware ownership counts for {PlayerCount} players in league {LeagueId} transfer window {TransferWindowId}",
                    positionOwnershipCounts.Count,
                    leagueId,
                    currentWindow.Id
                );

                return positionOwnershipCounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calculating position-aware player ownership counts for league {LeagueId}",
                    leagueId
                );
                throw;
            }
        }

        public async Task<Team?> SaveTeamAsync(Team team)
        {
            try
            {
                // Set the composite ID to ensure one team per manager per transfer window
                team.SetCompositeId();

                if (string.IsNullOrEmpty(team.Id))
                {
                    _logger.LogError(
                        "Failed to generate team ID for manager {ManagerUserId} in transfer window {TransferWindowId}",
                        team.ManagerUserId,
                        team.TransferWindowId
                    );
                    throw new InvalidOperationException(
                        "Unable to generate team ID. Manager ID and Transfer Window ID are required."
                    );
                }

                // Use UpsertItemAsync to either create new team or replace existing team for this manager/window combination
                var response = await _teamsContainer.UpsertItemAsync(
                    team,
                    new PartitionKey(team.LeagueId)
                );

                _logger.LogInformation(
                    "Successfully saved/replaced team {TeamId} for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                    team.Id,
                    team.ManagerUserId,
                    team.LeagueId,
                    team.TransferWindowId
                );

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error saving team {TeamId} for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                    team.Id,
                    team.ManagerUserId,
                    team.LeagueId,
                    team.TransferWindowId
                );
                throw;
            }
        }

        public async Task<IEnumerable<Team>> GetTeamsForTransferWindowAsync(
            string leagueId,
            string transferWindowId
        )
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.leagueId = @leagueId AND c.transferWindowId = @transferWindowId"
                )
                    .WithParameter("@leagueId", leagueId)
                    .WithParameter("@transferWindowId", transferWindowId);

                var queryIterator = _teamsContainer.GetItemQueryIterator<Team>(query);
                var teams = new List<Team>();

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    teams.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {TeamCount} teams for league {LeagueId} transfer window {TransferWindowId}",
                    teams.Count,
                    leagueId,
                    transferWindowId
                );
                return teams;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving teams for league {LeagueId} transfer window {TransferWindowId}",
                    leagueId,
                    transferWindowId
                );
                throw;
            }
        }

        public async Task<Team?> GetManagerTeamForTransferWindowAsync(
            string leagueId,
            string transferWindowId,
            string managerUserId
        )
        {
            try
            {
                // Use the composite key to directly get the manager's team
                var teamId = Team.GenerateTeamId(managerUserId, transferWindowId);

                var response = await _teamsContainer.ReadItemAsync<Team>(
                    teamId,
                    new PartitionKey(leagueId)
                );

                _logger.LogInformation(
                    "Retrieved team {TeamId} for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                    teamId,
                    managerUserId,
                    leagueId,
                    transferWindowId
                );

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "No team found for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                    managerUserId,
                    leagueId,
                    transferWindowId
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving team for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                    managerUserId,
                    leagueId,
                    transferWindowId
                );
                throw;
            }
        }

        // Optimistic Concurrency Control methods

        public async Task<(LeagueOwnershipState?, string?)> GetLeagueOwnershipStateWithETagAsync(
            string leagueId,
            string transferWindowId
        )
        {
            try
            {
                var ownershipStateId = $"ownership-{transferWindowId}";
                var response = await _teamsContainer.ReadItemAsync<LeagueOwnershipState>(
                    ownershipStateId,
                    new PartitionKey(leagueId)
                );

                _logger.LogInformation(
                    "Retrieved ownership state {OwnershipStateId} for league {LeagueId} transfer window {TransferWindowId}",
                    ownershipStateId,
                    leagueId,
                    transferWindowId
                );

                return (response.Resource, response.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "No ownership state found for league {LeagueId} transfer window {TransferWindowId}",
                    leagueId,
                    transferWindowId
                );
                return (null, null);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving ownership state for league {LeagueId} transfer window {TransferWindowId}",
                    leagueId,
                    transferWindowId
                );
                throw;
            }
        }

        public async Task<LeagueOwnershipState?> GetLeagueOwnershipStateAsync(
            string leagueId,
            string transferWindowId
        )
        {
            var (ownershipState, _) = await GetLeagueOwnershipStateWithETagAsync(
                leagueId,
                transferWindowId
            );
            return ownershipState;
        }

        public async Task<LeagueOwnershipState> InitializeLeagueOwnershipStateAsync(
            string leagueId,
            string transferWindowId
        )
        {
            try
            {
                var ownershipState = new LeagueOwnershipState
                {
                    Id = $"ownership-{transferWindowId}",
                    LeagueId = leagueId,
                    TransferWindowId = transferWindowId,
                    OwnershipCounts = new Dictionary<string, Dictionary<string, int>>(),
                    TotalTeamsCount = 0,
                    LastUpdated = DateTime.UtcNow,
                };

                var response = await _teamsContainer.CreateItemAsync(
                    ownershipState,
                    new PartitionKey(leagueId)
                );

                _logger.LogInformation(
                    "Initialized ownership state {OwnershipStateId} for league {LeagueId} transfer window {TransferWindowId}",
                    ownershipState.Id,
                    leagueId,
                    transferWindowId
                );

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogInformation(
                    "Ownership state already exists for league {LeagueId} transfer window {TransferWindowId}, retrieving existing",
                    leagueId,
                    transferWindowId
                );

                var existing = await GetLeagueOwnershipStateAsync(leagueId, transferWindowId);
                return existing
                    ?? throw new InvalidOperationException(
                        "Failed to retrieve existing ownership state after conflict"
                    );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error initializing ownership state for league {LeagueId} transfer window {TransferWindowId}",
                    leagueId,
                    transferWindowId
                );
                throw;
            }
        }

        public async Task<bool> SaveTeamWithOwnershipUpdateAsync(
            Team team,
            LeagueOwnershipState ownershipState,
            string expectedETag
        )
        {
            try
            {
                // Set the composite ID to ensure one team per manager per transfer window
                team.SetCompositeId();

                if (string.IsNullOrEmpty(team.Id))
                {
                    _logger.LogError(
                        "Failed to generate team ID for manager {ManagerUserId} in transfer window {TransferWindowId}",
                        team.ManagerUserId,
                        team.TransferWindowId
                    );
                    throw new InvalidOperationException(
                        "Unable to generate team ID. Manager ID and Transfer Window ID are required."
                    );
                }

                // Update ownership counts with the new team's players
                foreach (var player in team.Players)
                {
                    if (!ownershipState.OwnershipCounts.ContainsKey(player.PlayerId))
                    {
                        ownershipState.OwnershipCounts[player.PlayerId] =
                            new Dictionary<string, int>();
                    }

                    if (
                        !ownershipState
                            .OwnershipCounts[player.PlayerId]
                            .ContainsKey(player.Position)
                    )
                    {
                        ownershipState.OwnershipCounts[player.PlayerId][player.Position] = 0;
                    }

                    ownershipState.OwnershipCounts[player.PlayerId][player.Position]++;
                }

                ownershipState.TotalTeamsCount++;
                ownershipState.LastUpdated = DateTime.UtcNow;

                // Create transactional batch
                var batch = _teamsContainer.CreateTransactionalBatch(
                    new PartitionKey(team.LeagueId)
                );

                // Add team upsert operation
                batch.UpsertItem(team);

                // Add ownership state replace operation with ETag condition
                batch.ReplaceItem(
                    ownershipState.Id,
                    ownershipState,
                    new TransactionalBatchItemRequestOptions { IfMatchEtag = expectedETag }
                );

                // Execute the batch atomically
                var batchResponse = await batch.ExecuteAsync();

                if (batchResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Successfully saved team {TeamId} and updated ownership state atomically for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                        team.Id,
                        team.ManagerUserId,
                        team.LeagueId,
                        team.TransferWindowId
                    );
                    return true;
                }
                else
                {
                    _logger.LogWarning(
                        "Transactional batch failed with status {StatusCode} for team {TeamId} manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                        batchResponse.StatusCode,
                        team.Id,
                        team.ManagerUserId,
                        team.LeagueId,
                        team.TransferWindowId
                    );
                    return false;
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                _logger.LogInformation(
                    "Optimistic concurrency conflict detected for team {TeamId} manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId} - ETag mismatch",
                    team.Id,
                    team.ManagerUserId,
                    team.LeagueId,
                    team.TransferWindowId
                );
                return false;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error saving team {TeamId} with ownership update for manager {ManagerUserId} in league {LeagueId} transfer window {TransferWindowId}",
                    team.Id,
                    team.ManagerUserId,
                    team.LeagueId,
                    team.TransferWindowId
                );
                throw;
            }
        }

        // Activity Type methods
        public async Task<MplActivityType?> AddActivityTypeAsync(MplActivityType activityType)
        {
            try
            {
                if (string.IsNullOrEmpty(activityType.Id))
                    activityType.Id = Guid.NewGuid().ToString();
                activityType.Ver = 1;

                ItemResponse<MplActivityType> response =
                    await _activityTypesContainer.CreateItemAsync(
                        activityType,
                        new PartitionKey(activityType.LeagueId)
                    );
                _logger.LogInformation(
                    "Created activity type with ID: {ActivityTypeId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    activityType.Id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning(
                    ex,
                    "Conflict creating activity type with ID: {ActivityTypeId}. It might already exist.",
                    activityType.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error creating activity type with ID: {ActivityTypeId}",
                    activityType.Id
                );
                throw;
            }
        }

        public async Task<MplActivityType?> GetActivityTypeByIdAsync(string id)
        {
            try
            {
                // Since we don't know the league ID, we need to query by ID
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter(
                    "@id",
                    id
                );

                using FeedIterator<MplActivityType> iterator =
                    _activityTypesContainer.GetItemQueryIterator<MplActivityType>(query);

                while (iterator.HasMoreResults)
                {
                    FeedResponse<MplActivityType> response = await iterator.ReadNextAsync();
                    var activityType = response.FirstOrDefault();
                    if (activityType != null)
                    {
                        _logger.LogInformation(
                            "Retrieved activity type with ID: {ActivityTypeId}",
                            id
                        );
                        return activityType;
                    }
                }

                _logger.LogInformation("Activity type with ID: {ActivityTypeId} not found", id);
                return null;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Activity type with ID: {ActivityTypeId} not found", id);
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting activity type with ID: {ActivityTypeId}",
                    id
                );
                throw;
            }
        }

        public async Task<MplActivityType?> UpdateActivityTypeAsync(MplActivityType activityType)
        {
            try
            {
                ItemResponse<MplActivityType> response =
                    await _activityTypesContainer.ReplaceItemAsync(
                        activityType,
                        activityType.Id,
                        new PartitionKey(activityType.LeagueId)
                    );
                _logger.LogInformation(
                    "Updated activity type with ID: {ActivityTypeId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    activityType.Id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Activity type with ID: {ActivityTypeId} not found for update",
                    activityType.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error updating activity type with ID: {ActivityTypeId}",
                    activityType.Id
                );
                throw;
            }
        }

        public async Task<IEnumerable<MplActivityType>> GetActivityTypesForLeagueAsync(
            string leagueId
        )
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.leagueId = @leagueId"
                ).WithParameter("@leagueId", leagueId);

                var activityTypes = new List<MplActivityType>();
                using FeedIterator<MplActivityType> iterator =
                    _activityTypesContainer.GetItemQueryIterator<MplActivityType>(query);

                while (iterator.HasMoreResults)
                {
                    FeedResponse<MplActivityType> response = await iterator.ReadNextAsync();
                    activityTypes.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {Count} activity types for league: {LeagueId}",
                    activityTypes.Count,
                    leagueId
                );
                return activityTypes;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting activity types for league: {LeagueId}",
                    leagueId
                );
                throw;
            }
        }

        // Concrete Activity methods
        public async Task<ConcreteActivity?> AddConcreteActivityAsync(
            ConcreteActivity concreteActivity
        )
        {
            try
            {
                if (string.IsNullOrEmpty(concreteActivity.Id))
                    concreteActivity.Id = Guid.NewGuid().ToString();
                concreteActivity.Ver = 1;

                ItemResponse<ConcreteActivity> response =
                    await _concreteActivitiesContainer.CreateItemAsync(
                        concreteActivity,
                        new PartitionKey(concreteActivity.TransferWindowId)
                    );
                _logger.LogInformation(
                    "Created concrete activity with ID: {ConcreteActivityId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    concreteActivity.Id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning(
                    ex,
                    "Conflict creating concrete activity with ID: {ConcreteActivityId}. It might already exist.",
                    concreteActivity.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error creating concrete activity with ID: {ConcreteActivityId}",
                    concreteActivity.Id
                );
                throw;
            }
        }

        public async Task<ConcreteActivity?> GetConcreteActivityByIdAsync(
            string id,
            string transferWindowId
        )
        {
            try
            {
                ItemResponse<ConcreteActivity> response =
                    await _concreteActivitiesContainer.ReadItemAsync<ConcreteActivity>(
                        id,
                        new PartitionKey(transferWindowId)
                    );
                _logger.LogInformation(
                    "Retrieved concrete activity with ID: {ConcreteActivityId}",
                    id
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Concrete activity with ID: {ConcreteActivityId} not found in transfer window: {TransferWindowId}",
                    id,
                    transferWindowId
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting concrete activity with ID: {ConcreteActivityId} in transfer window: {TransferWindowId}",
                    id,
                    transferWindowId
                );
                throw;
            }
        }

        public async Task<ConcreteActivity?> UpdateConcreteActivityAsync(
            ConcreteActivity concreteActivity
        )
        {
            try
            {
                ItemResponse<ConcreteActivity> response =
                    await _concreteActivitiesContainer.ReplaceItemAsync(
                        concreteActivity,
                        concreteActivity.Id,
                        new PartitionKey(concreteActivity.TransferWindowId)
                    );
                _logger.LogInformation(
                    "Updated concrete activity with ID: {ConcreteActivityId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    concreteActivity.Id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Concrete activity with ID: {ConcreteActivityId} not found for update in transfer window: {TransferWindowId}",
                    concreteActivity.Id,
                    concreteActivity.TransferWindowId
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error updating concrete activity with ID: {ConcreteActivityId} in transfer window: {TransferWindowId}",
                    concreteActivity.Id,
                    concreteActivity.TransferWindowId
                );
                throw;
            }
        }

        public async Task<bool> DeleteConcreteActivityAsync(string id, string transferWindowId)
        {
            try
            {
                ItemResponse<ConcreteActivity> response =
                    await _concreteActivitiesContainer.DeleteItemAsync<ConcreteActivity>(
                        id,
                        new PartitionKey(transferWindowId)
                    );
                _logger.LogInformation(
                    "Deleted concrete activity with ID: {ConcreteActivityId}, Status: {StatusCode}, Request Charge: {RequestCharge}",
                    id,
                    response.StatusCode,
                    response.RequestCharge
                );
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Concrete activity with ID: {ConcreteActivityId} not found for deletion in transfer window: {TransferWindowId}",
                    id,
                    transferWindowId
                );
                return false;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error deleting concrete activity with ID: {ConcreteActivityId} in transfer window: {TransferWindowId}",
                    id,
                    transferWindowId
                );
                throw;
            }
        }

        public async Task<
            IEnumerable<ConcreteActivity>
        > GetConcreteActivitiesForTransferWindowAsync(
            string transferWindowId,
            DateOnly? from = null,
            DateOnly? to = null,
            string? activityTypeId = null
        )
        {
            try
            {
                var queryBuilder = new StringBuilder(
                    "SELECT * FROM c WHERE c.transferWindowId = @transferWindowId"
                );
                var parameters = new List<(string, object)>
                {
                    ("@transferWindowId", transferWindowId),
                };

                if (from.HasValue)
                {
                    queryBuilder.Append(" AND c.date >= @from");
                    parameters.Add(("@from", from.Value.ToString("yyyy-MM-dd")));
                }

                if (to.HasValue)
                {
                    queryBuilder.Append(" AND c.date <= @to");
                    parameters.Add(("@to", to.Value.ToString("yyyy-MM-dd")));
                }

                if (!string.IsNullOrEmpty(activityTypeId))
                {
                    queryBuilder.Append(" AND c.activityTypeId = @activityTypeId");
                    parameters.Add(("@activityTypeId", activityTypeId));
                }

                queryBuilder.Append(" ORDER BY c.date DESC");

                var queryDefinition = new QueryDefinition(queryBuilder.ToString());
                foreach (var (paramName, paramValue) in parameters)
                {
                    queryDefinition.WithParameter(paramName, paramValue);
                }

                var concreteActivities = new List<ConcreteActivity>();
                using FeedIterator<ConcreteActivity> iterator =
                    _concreteActivitiesContainer.GetItemQueryIterator<ConcreteActivity>(
                        queryDefinition
                    );

                while (iterator.HasMoreResults)
                {
                    FeedResponse<ConcreteActivity> response = await iterator.ReadNextAsync();
                    concreteActivities.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {Count} concrete activities for transfer window: {TransferWindowId}",
                    concreteActivities.Count,
                    transferWindowId
                );
                return concreteActivities;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting concrete activities for transfer window: {TransferWindowId}",
                    transferWindowId
                );
                throw;
            }
        }

        public async Task<bool> AddParticipantToConcreteActivityAsync(
            string activityId,
            string transferWindowId,
            string participantId
        )
        {
            try
            {
                var activity = await GetConcreteActivityByIdAsync(activityId, transferWindowId);
                if (activity == null)
                {
                    _logger.LogWarning(
                        "Concrete activity {ActivityId} not found in transfer window {TransferWindowId}",
                        activityId,
                        transferWindowId
                    );
                    return false;
                }

                if (activity.ParticipantIds.Contains(participantId))
                {
                    _logger.LogInformation(
                        "Participant {ParticipantId} already in concrete activity {ActivityId}",
                        participantId,
                        activityId
                    );
                    return true;
                }

                activity.ParticipantIds.Add(participantId);
                activity.UpdatedAt = DateTime.UtcNow;

                var result = await UpdateConcreteActivityAsync(activity);
                return result != null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error adding participant {ParticipantId} to concrete activity {ActivityId}",
                    participantId,
                    activityId
                );
                throw;
            }
        }

        public async Task<bool> RemoveParticipantFromConcreteActivityAsync(
            string activityId,
            string transferWindowId,
            string participantId
        )
        {
            try
            {
                var activity = await GetConcreteActivityByIdAsync(activityId, transferWindowId);
                if (activity == null)
                {
                    _logger.LogWarning(
                        "Concrete activity {ActivityId} not found in transfer window {TransferWindowId}",
                        activityId,
                        transferWindowId
                    );
                    return false;
                }

                if (!activity.ParticipantIds.Contains(participantId))
                {
                    _logger.LogInformation(
                        "Participant {ParticipantId} not in concrete activity {ActivityId}",
                        participantId,
                        activityId
                    );
                    return true;
                }

                activity.ParticipantIds.Remove(participantId);
                activity.UpdatedAt = DateTime.UtcNow;

                var result = await UpdateConcreteActivityAsync(activity);
                return result != null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Error removing participant {ParticipantId} from concrete activity {ActivityId}",
                    participantId,
                    activityId
                );
                throw;
            }
        }

        // Attendance Request methods
        public async Task<AttendanceRequest?> CreateAttendanceRequestAsync(
            AttendanceRequest request
        )
        {
            try
            {
                if (string.IsNullOrEmpty(request.Id))
                    request.Id = Guid.NewGuid().ToString();
                request.Ver = 1;

                ItemResponse<AttendanceRequest> response =
                    await _attendanceRequestsContainer.CreateItemAsync(
                        request,
                        new PartitionKey(request.LeagueId)
                    );
                _logger.LogInformation(
                    "Created attendance request with ID: {RequestId}, Status: {StatusCode}",
                    request.Id,
                    response.StatusCode
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogWarning(
                    ex,
                    "Conflict creating attendance request with ID: {RequestId}",
                    request.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error creating attendance request with ID: {RequestId}",
                    request.Id
                );
                throw;
            }
        }

        public async Task<AttendanceRequest?> GetAttendanceRequestByIdAsync(string id)
        {
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter(
                    "@id",
                    id
                );

                using FeedIterator<AttendanceRequest> iterator =
                    _attendanceRequestsContainer.GetItemQueryIterator<AttendanceRequest>(query);

                while (iterator.HasMoreResults)
                {
                    FeedResponse<AttendanceRequest> response = await iterator.ReadNextAsync();
                    var request = response.FirstOrDefault();
                    if (request != null)
                    {
                        return request;
                    }
                }
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting attendance request with ID: {RequestId}",
                    id
                );
                throw;
            }
        }

        public async Task<AttendanceRequest?> UpdateAttendanceRequestAsync(
            AttendanceRequest request
        )
        {
            try
            {
                ItemResponse<AttendanceRequest> response =
                    await _attendanceRequestsContainer.ReplaceItemAsync(
                        request,
                        request.Id,
                        new PartitionKey(request.LeagueId)
                    );
                _logger.LogInformation(
                    "Updated attendance request with ID: {RequestId}, Status: {StatusCode}",
                    request.Id,
                    response.StatusCode
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Attendance request with ID: {RequestId} not found for update",
                    request.Id
                );
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error updating attendance request with ID: {RequestId}",
                    request.Id
                );
                throw;
            }
        }

        public async Task<
            IEnumerable<AttendanceRequest>
        > GetPendingAttendanceRequestsForLeagueAsync(string leagueId)
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.leagueId = @leagueId AND c.status = @status ORDER BY c.requestedAt DESC"
                )
                    .WithParameter("@leagueId", leagueId)
                    .WithParameter("@status", 0); // 0 = Pending

                var requests = new List<AttendanceRequest>();
                using FeedIterator<AttendanceRequest> iterator =
                    _attendanceRequestsContainer.GetItemQueryIterator<AttendanceRequest>(query);

                while (iterator.HasMoreResults)
                {
                    FeedResponse<AttendanceRequest> response = await iterator.ReadNextAsync();
                    requests.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {Count} pending attendance requests for league: {LeagueId}",
                    requests.Count,
                    leagueId
                );
                return requests;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting pending attendance requests for league: {LeagueId}",
                    leagueId
                );
                throw;
            }
        }

        public async Task<IEnumerable<AttendanceRequest>> GetAttendanceRequestsForStudentAsync(
            string leagueId,
            string studentUserId
        )
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.leagueId = @leagueId AND c.studentUserId = @studentUserId ORDER BY c.requestedAt DESC"
                )
                    .WithParameter("@leagueId", leagueId)
                    .WithParameter("@studentUserId", studentUserId);

                var requests = new List<AttendanceRequest>();
                using FeedIterator<AttendanceRequest> iterator =
                    _attendanceRequestsContainer.GetItemQueryIterator<AttendanceRequest>(query);

                while (iterator.HasMoreResults)
                {
                    FeedResponse<AttendanceRequest> response = await iterator.ReadNextAsync();
                    requests.AddRange(response);
                }

                _logger.LogInformation(
                    "Retrieved {Count} attendance requests for student: {StudentUserId}",
                    requests.Count,
                    studentUserId
                );
                return requests;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    ex,
                    "Cosmos error getting attendance requests for student: {StudentUserId}",
                    studentUserId
                );
                throw;
            }
        }
    }
}
