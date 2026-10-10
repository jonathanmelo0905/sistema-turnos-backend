namespace AgendaApp.Dtos
{
    public class ClientsDto
    {
        public Guid ClientId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }
}

namespace AgendaApp.Dtos
{
    public class ClientSearchDto
    {
        public Guid ClientId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int Visits { get; set; }
        public DateTime? LastVisit { get; set; }
    }
}
