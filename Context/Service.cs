using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class Service
    {
        [Key] 
        public Guid ServiceId { get; set; }
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMin { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; } = true;
        public Guid? CategoryId { get; set; }

        public Category? Category { get; set; }

        // 🔗 Relaciones
        public Business Business { get; set; } = null!;
        public ICollection<UserService> UserServices { get; set; } = new List<UserService>();
    }
}
