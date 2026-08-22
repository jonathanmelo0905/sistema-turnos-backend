using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class UserScheduleException
    {
        [Key]
        public Guid ExceptionId { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public bool IsAvailable { get; set; } = false;
        [Column(TypeName = "varchar(8)")]
        public string? StartTime { get; set; }
        [Column(TypeName = "varchar(8)")]
        public string? EndTime { get; set; }
        public string? Reason { get; set; }

        // 🔗 Relaciones
        public User User { get; set; } = null!;
    }
}
