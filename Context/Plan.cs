using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class Plan
    {
        [Key]
        public int PlanId { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = null!; // Free, Pro, Premium

        [Required]
        public int MaxUsers { get; set; }

        [Required]
        public int MaxAppointments { get; set; }

        [Required]
        public decimal Price { get; set; }

        // 🔗 Relación con Businesses
        public ICollection<Business> Businesses { get; set; } = new List<Business>();
    }
}
