using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class CategoryResponseDto
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool Active { get; set; }
        public int ServiceCount { get; set; }
    }

    public class CategorySaveDto
    {
        [Required, MinLength(2), MaxLength(80)]
        public string Name { get; set; } = null!;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool Active { get; set; } = true;
    }
}
