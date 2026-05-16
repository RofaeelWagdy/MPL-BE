using System;

namespace MorkosiaPrepaLeague.Models.Core
{
    public class AttendanceRequest : CosmosDocumentBase
    {
        public string DocumentType { get; } = "AttendanceRequest";

        public string LeagueId { get; set; } = string.Empty;

        public string TransferWindowId { get; set; } = string.Empty;

        public string ActivityId { get; set; } = string.Empty;

        public string StudentUserId { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public string ActivityName { get; set; } = string.Empty;

        public DateTime ActivityDate { get; set; }

        public AttendanceRequestStatus Status { get; set; } = AttendanceRequestStatus.Pending;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        public string? ProcessedByUserId { get; set; }
    }

    public enum AttendanceRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }
}