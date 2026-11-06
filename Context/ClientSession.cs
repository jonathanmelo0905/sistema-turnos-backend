namespace AgendaApp.Context
{
    // Sesión del cliente en un navegador (Fase 9 T8). Solo se guarda el hash SHA-256 del token;
    // vence a los 90 días sin uso y se renueva al usarla. "No soy yo" la borra.
    public class ClientSession
    {
        public Guid ClientSessionId { get; set; }
        public Guid ClientId { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string TokenHash { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public Client Client { get; set; } = null!;
    }
}
