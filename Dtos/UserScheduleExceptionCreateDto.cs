using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{// DTO para agregar una excepción puntual
    public class UserScheduleExceptionCreateDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public DateTime Date { get; set; } // Fecha de la excepción

        [Required]
        public bool IsAvailable { get; set; } // Si el usuario está disponible o no ese día

        public string? StartTime { get; set; }
        public string? EndTime { get; set; }

        [MaxLength(150)]
        public string? Reason { get; set; } // Motivo opcional (enfermedad, descanso, evento, etc.)
    }
}
