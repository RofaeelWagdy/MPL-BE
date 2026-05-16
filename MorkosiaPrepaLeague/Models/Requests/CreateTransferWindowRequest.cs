using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public class CreateTransferWindowRequest
    {      
        /// <summary>
        /// Start date and time for the transfer window in UTC
        /// </summary>
        [Required]
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date and time for the transfer window in UTC
        /// </summary>
        [Required]
        public DateTime EndDate { get; set; }
    }
}
