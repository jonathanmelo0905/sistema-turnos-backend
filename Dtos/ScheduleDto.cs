using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    // DTO que representa el horario semanal de un usuario
    public class ScheduleDto
    {
        [Required]
        public int DayOfWeek { get; set; }   // Día de la semana (1=Lunes ... 7=Domingo)

        [Required]
        public string? StartTime { get; set; }  // Hora de inicio de la jornada

        [Required]
        public string? EndTime { get; set; }    // Hora de fin de la jornada

        public bool IsAvailable { get; set; } = true;  // Indica si ese día está disponible

        public string? BreakStart { get; set; }   // Hora de inicio del descanso (opcional)
        public string? BreakEnd { get; set; }     // Hora de fin del descanso (opcional)
    }
}
