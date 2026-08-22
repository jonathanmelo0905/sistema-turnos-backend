using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class UserSchedule
    {
        [Key]
        public Guid ScheduleId { get; set; }
        public Guid UserId { get; set; }
        public byte DayOfWeek { get; set; }
        public bool IsAvailable { get; set; } = true;
        [Column(TypeName = "varchar(8)")]
        public string? StartTime { get; set; }
        [Column(TypeName = "varchar(8)")]
        public string? EndTime { get; set; }
        [Column(TypeName = "varchar(8)")]
        public string? BreakStart { get; set; }
        [Column(TypeName = "varchar(8)")]
        public string? BreakEnd { get; set; }

        // 🔗 Relaciones
        public User User { get; set; } = null!;
    }
}
