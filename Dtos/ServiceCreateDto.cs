using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{// DTO para crear un nuevo servicio
    public class ServiceCreateDto
    {
        [Required]
        public Guid BusinessId { get; set; } // ID del negocio al que pertenece el servicio

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Nombre del servicio

        [MaxLength(250)]
        public string? Description { get; set; } // Descripción opcional del servicio

        [Required]
        public int DurationMin { get; set; } // Duración del servicio en minutos

        [Required]
        public decimal Price { get; set; } // Precio del servicio
    }
}
