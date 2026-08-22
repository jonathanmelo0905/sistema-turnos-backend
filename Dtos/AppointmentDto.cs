namespace AgendaApp.Dtos
{
    public class AppointmentDto
    {
        public Guid AppointmentId { get; set; }

        public Guid BusinessId { get; set; }
        public string? BusinessName { get; set; }

        public Guid ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientPhone { get; set; }

        public Guid UserId { get; set; }
        public string? UserName { get; set; }

        public DateTime DateTimeStart { get; set; }
        public DateTime DateTimeEnd { get; set; }

        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public Guid ServiceId { get; set; }
    }
}
