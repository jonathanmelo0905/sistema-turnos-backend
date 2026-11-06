using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    // DTO para crear o actualizar un horario semanal del usuario
    public class UserScheduleDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public byte DayOfWeek { get; set; } // 1=Lunes, 7=Domingo

        [Required]
        public bool IsAvailable { get; set; } = true;

        // Obligatorias solo si el día es trabajado (lo valida SchedulesService)
        public string? StartTime { get; set; } // Hora de inicio
        public string? EndTime { get; set; } // Hora de fin

        public string? BreakStart { get; set; } // Inicio del descanso
        public string? BreakEnd { get; set; }   // Fin del descanso
    }
}
