namespace MorkosiaPrepaLeague.Models.Requests
{
    /// <summary>
    /// DTO for updating user information. All properties are optional to allow partial updates.
    /// Inherits validation rules from UserBaseRequest.
    /// </summary>
    public class UserUpdateRequest : UserBaseRequest
    {
        // All properties are inherited from UserBaseRequest
        // They are nullable by default, allowing partial updates
    }
}
