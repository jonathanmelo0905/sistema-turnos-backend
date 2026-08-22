using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{// DTO para registrar una excepción puntual en el horario de un usuario
    public class ScheduleExceptionDto
    {
        [Required]
        public DateTime Date { get; set; }  // Fecha de la excepción (ej: festivo o día libre)

        public bool IsAvailable { get; set; } = false;  // Indica si ese día está disponible o no

        public string? StartTime { get; set; }  // Hora de inicio en caso de disponibilidad parcial
        public string? EndTime { get; set; }    // Hora de fin en caso de disponibilidad parcial

        public string? Reason { get; set; }       // Motivo (ej: Vacaciones, enfermedad, mantenimiento)
    }
}
