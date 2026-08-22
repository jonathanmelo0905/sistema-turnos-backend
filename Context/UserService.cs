namespace AgendaApp.Context
{
    public class UserService
    {
        public Guid UserId { get; set; }
        public Guid ServiceId { get; set; }

        // 🔗 Relaciones
        public User User { get; set; } = null!;
        public Service Service { get; set; } = null!;
    }
}
