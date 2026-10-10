namespace AgendaApp.Dtos
{
    public class UpdateUserDto
    {
        public string Name { get; set; } = null!;
        public string? Phone { get; set; }
        public string Role { get; set; } = null!;
        public bool Active { get; set; } = true;

        // Opcional: el dueño asigna una contraseña nueva (mínimo 6 caracteres)
        public string? NewPassword { get; set; }
    }
}
