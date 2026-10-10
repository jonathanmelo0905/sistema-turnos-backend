using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    // Reseña opcional del cliente sobre una atención (una por turno atendido).
    public class Review
    {
        [Key]
        public Guid ReviewId { get; set; }

        public Guid BusinessId { get; set; }
        public Guid AppointmentId { get; set; }
        public Guid ClientId { get; set; }

        // Quién atendió y qué servicio (para promedios por colaborador y servicio)
        public Guid UserId { get; set; }
        public Guid ServiceId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Respuesta pública de la empresa
        [MaxLength(500)]
        public string? Reply { get; set; }

        public DateTime? RepliedAt { get; set; }
        public Guid? RepliedByUserId { get; set; }

        // Oculta de la página pública (ofensiva, spam); sigue contando en estadísticas internas
        public bool Hidden { get; set; }

        public Appointment Appointment { get; set; } = null!;
        public Client Client { get; set; } = null!;
    }
}
