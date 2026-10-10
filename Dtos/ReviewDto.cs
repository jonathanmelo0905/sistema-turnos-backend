using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class ReviewDto
    {
        public Guid ReviewId { get; set; }
        public Guid AppointmentId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Reply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public bool Hidden { get; set; }
        public string? ClientName { get; set; }
        public Guid UserId { get; set; }
        public string? StaffName { get; set; }
        public string? ServiceName { get; set; }
        public DateTime AppointmentDate { get; set; }
    }

    public class ReviewListDto
    {
        public double Average { get; set; }
        public int Count { get; set; }
        public int[] Distribution { get; set; } = new int[5];   // [1★..5★]
        public List<ReviewDto> Items { get; set; } = new();
    }

    public class ReviewReplyDto
    {
        [MaxLength(500)]
        public string? Reply { get; set; }
    }

    // ---- Público ----
    public class PublicReviewDto
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string ClientName { get; set; } = null!;   // solo el primer nombre
        public string? ServiceName { get; set; }
        public string? StaffName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Reply { get; set; }
    }

    public class PublicReviewCreateDto
    {
        [Required]
        public string AccessToken { get; set; } = null!;

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }
    }
}
