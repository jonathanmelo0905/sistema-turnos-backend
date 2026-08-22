using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class Client
    {
        public Guid ClientId { get; set; }
        public Guid BusinessId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }


        // 👇 Relación uno-a-muchos con Appointment
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        // 🔗 Relaciones
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
    }
}
