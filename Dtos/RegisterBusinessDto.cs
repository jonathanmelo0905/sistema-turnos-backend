namespace AgendaApp.Dtos
{
    public class RegisterBusinessDto
    {
        // Business
        public string BusinessName { get; set; } = null!;
        public string BusinessEmail { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Address { get; set; }

        // Usuario Admin
        public string AdminName { get; set; } = null!;
        public string AdminEmail { get; set; } = null!;
        public string Password { get; set; } = null!;

    }
}
