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

        // Precio del servicio al momento de reservar (las estadísticas no cambian si luego cambia el catálogo)
        [Column(TypeName = "decimal(10,2)")]
        public decimal? Price { get; set; }

        // Horas Valle: precio de lista sin descuento y % aplicado (Price ya trae el descuento)
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ListPrice { get; set; }

        public int? ValleyDiscountPct { get; set; }

        [MaxLength(300)]
        public string? Notes { get; set; }

        [MaxLength(250)]
        public string? CancelReason { get; set; }

        // Mostrador (lo crea el personal) o Web (lo reserva el cliente)
        [MaxLength(20)]
        public string Source { get; set; } = "Mostrador";

        public Guid? CreatedByUserId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Hay que avisarle al cliente (cambio, cesión, cancelación o ausencia del colaborador)
        public bool NeedsContact { get; set; }

        [MaxLength(200)]
        public string? ContactReason { get; set; }

        public DateTime? ContactedAt { get; set; }

        // Recordatorio del día anterior: cuándo y por dónde se avisó ("WhatsApp" | "Email")
        public DateTime? ReminderSentAt { get; set; }

        [MaxLength(20)]
        public string? ReminderChannel { get; set; }

        public ICollection<AppointmentHistory> History { get; set; } = new List<AppointmentHistory>();

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
