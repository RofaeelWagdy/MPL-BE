using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public class UserRegistrationRequest : UserBaseRequest
    {
        [Required]
        [MinLength(3)]
        [MaxLength(20)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public override string? FullName { get; set; } = string.Empty;
        
        [Required]
        public override string? Class { get; set; } = string.Empty;
        
        [Required]
        public override string? Password { get; set; } = string.Empty;
    }
}