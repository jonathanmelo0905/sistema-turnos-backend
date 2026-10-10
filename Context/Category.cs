using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    // Agrupa los servicios del catálogo (ej. "Cortes", "Barba", "Color").
    public class Category
    {
        [Key]
        public Guid CategoryId { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required, MaxLength(80)]
        public string Name { get; set; } = null!;

        [MaxLength(200)]
        public string? Description { get; set; }

        // Borrado lógico: las categorías inactivas no se ofrecen al crear servicios.
        public bool Active { get; set; } = true;

        public Business Business { get; set; } = null!;
        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}
