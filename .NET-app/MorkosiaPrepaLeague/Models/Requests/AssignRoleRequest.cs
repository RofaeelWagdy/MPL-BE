using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public class AssignRoleRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string LeagueId { get; set; } = string.Empty;
        
        [Required]
        [RegularExpression("^(admin|member)$", ErrorMessage = "Role must be either 'admin' or 'member'")]
        public string Role { get; set; } = string.Empty;
    }
}