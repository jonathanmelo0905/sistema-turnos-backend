using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class ScheduleDayDto
    {
        [Range(1, 7)]
        public byte DayOfWeek { get; set; } // 1=Lunes ... 7=Domingo
        public bool IsAvailable { get; set; }
        public string? StartTime { get; set; }  // "HH:mm"
        public string? EndTime { get; set; }
        public string? BreakStart { get; set; }
        public string? BreakEnd { get; set; }
    }

    // Reemplazo completo de la semana del colaborador (7 días)
    public class WeeklyScheduleDto
    {
        [Required, MinLength(7), MaxLength(7)]
        public List<ScheduleDayDto> Days { get; set; } = new();
    }
}
