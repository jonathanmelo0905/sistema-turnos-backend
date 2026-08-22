using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class Appointment
    {
        [Key]
        public Guid AppointmentId { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public DateTime DateTimeStart { get; set; }

        [Required]
        public DateTime DateTimeEnd { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid serviceId {  get; set; }

        // 🔗 Relaciones
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;

        [ForeignKey("ClientId")]
        public Client Client { get; set; } = null!;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [ForeignKey("serviceId")]
        public Service Service { get; set; } = null!;
    }
}
