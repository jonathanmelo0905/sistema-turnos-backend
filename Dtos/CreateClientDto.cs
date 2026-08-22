namespace AgendaApp.Dtos
{
    public class CreateClientDto
    {
        public Guid BusinessId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }
}
