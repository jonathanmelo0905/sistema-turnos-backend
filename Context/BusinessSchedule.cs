using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class BusinessSchedule
    {
        [Key]
        public Guid ScheduleId { get; set; }
        public Guid BusinessId { get; set; }
        public byte DayOfWeek { get; set; } // 1 = Lunes, 7 = Domingo
        public bool IsOpen { get; set; } = true;
        [Column(TypeName = "varchar(8)")]
        public string? OpenTime { get; set; }
        [Column(TypeName = "varchar(8)")]
        public string? CloseTime { get; set; }

        // 🔗 Relaciones
        public Business Business { get; set; } = null!;
    }
}
