using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class TimeOffCreateDto
    {
        // Vacío = para uno mismo. Para otra persona se requiere collaborators.manage.
        public Guid? UserId { get; set; }

        [Required, MaxLength(20)]
        public string Type { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public string? StartTime { get; set; }
        public string? EndTime { get; set; }

        [MaxLength(250)]
        public string? Reason { get; set; }
    }

    public class TimeOffReviewDto
    {
        [MaxLength(250)]
        public string? Note { get; set; }
    }

    public class TimeOffConflictDto
    {
        public Guid AppointmentId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = null!;
        public string? ClientName { get; set; }
        public string? ClientPhone { get; set; }
        public string? ServiceName { get; set; }
    }

    public class TimeOffResponseDto
    {
        public Guid TimeOffId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string Type { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public int ConflictCount { get; set; }
    }
}
