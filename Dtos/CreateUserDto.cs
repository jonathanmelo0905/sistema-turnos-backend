namespace AgendaApp.Dtos
{
    public class CreateUserDto
    {
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!; // ⚠️ en controller lo conviertes a Hash
        public string Role { get; set; } = AgendaApp.Context.Roles.Encargado;
    }
}
