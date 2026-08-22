using AgendaApp.Context;

namespace AgendaApp.Dtos
{
    public class PlansDto
    {
        public int PlanId { get; set; }
        public required string Name { get; set; }
        public int MaxUsers { get; set; }
        public int MaxAppointments { get; set; }
        public decimal Price { get; set; }
        //public ICollection<Business> Businesses { get; set; } = new List<Business>();
    }
}
