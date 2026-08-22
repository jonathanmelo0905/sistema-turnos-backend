namespace AgendaApp.Dtos
{
    public class UpdateUserDto
    {
        public string Name { get; set; } = null!;
        public string? Phone { get; set; }
        public string Role { get; set; } = null!;
    }
}
