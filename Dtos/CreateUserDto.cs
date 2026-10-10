namespace AgendaApp.Dtos
{
    public class CreateUserDto
    {
        // Se ignora: el negocio siempre sale del token de quien crea el usuario.
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string Password { get; set; } = null!;
        public string Role { get; set; } = AgendaApp.Context.Roles.Colaborador;
    }
}
