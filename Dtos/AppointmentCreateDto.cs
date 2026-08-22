namespace AgendaApp.Dtos
{
    public class AppointmentCreateDto
    {
        public Guid BusinessId { get; set; }
        public Guid ClientId { get; set; }
        public Guid UserId { get; set; }
        public DateTime DateTimeStart { get; set; }
        public DateTime DateTimeEnd { get; set; }
        public Guid serviceId { get; set; }
    }
}
