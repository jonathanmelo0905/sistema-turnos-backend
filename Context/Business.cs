using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class Business
    {
        [Key]
        public Guid BusinessId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Email { get; set; } = null!;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [Required]
        public int PlanId { get; set; } = 1;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Relación con Users
        public ICollection<User> Users { get; set; } = new List<User>();


        // 🔗 Relaciones
        //public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<BusinessSchedule> BusinessSchedules { get; set; } = new List<BusinessSchedule>();
    }
}
