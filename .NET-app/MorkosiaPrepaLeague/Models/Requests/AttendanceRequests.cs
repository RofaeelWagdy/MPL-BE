namespace MorkosiaPrepaLeague.Models.Requests
{
    public class CreateAttendanceRequestRequest
    {
        public string LeagueId { get; set; } = string.Empty;
        public string TransferWindowId { get; set; } = string.Empty;
        public string ActivityId { get; set; } = string.Empty;
        public string StudentUserId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public DateTime ActivityDate { get; set; }
    }

    public class ProcessAttendanceRequestRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Approve { get; set; }
    }
}