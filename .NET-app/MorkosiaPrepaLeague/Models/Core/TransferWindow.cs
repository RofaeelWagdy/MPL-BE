using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MorkosiaPrepaLeague.Models.Core
{
    public class TransferWindow : CosmosDocumentBase
    {
        [Required]
        public string LeagueId { get; set; } = string.Empty;
        
        /// <summary>
        /// Start date and time of the transfer window in UTC
        /// </summary>
        [Required]
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date and time of the transfer window in UTC
        /// </summary>
        [Required]
        public DateTime EndDate { get; set; }
        
        public int WindowNumber { get; set; }
       
        public string CreatedByAdminId { get; set; } = string.Empty;
    }
}
