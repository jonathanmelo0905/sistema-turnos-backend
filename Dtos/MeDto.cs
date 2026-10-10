namespace AgendaApp.Dtos
{
    // Sesión actual: datos del usuario + permisos efectivos (para armar el menú del front)
    public class MeDto
    {
        public Guid UserId { get; set; }
        public Guid BusinessId { get; set; }
        public string BusinessName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
    }
}
