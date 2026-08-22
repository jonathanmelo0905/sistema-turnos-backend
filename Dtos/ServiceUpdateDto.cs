using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class ServiceUpdateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        [Required]
        public int DurationMin { get; set; }

        [Required]
        public decimal Price { get; set; }

        public bool Active { get; set; } = true; // Campo que indica si está activo o no
    }
}
