namespace AgendaApp.Dtos
{
    // Entrada del catálogo estático de permisos
    public class PermissionDto
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    // Permiso otorgado a un usuario, con auditoría (para vista de Owner sobre otro usuario)
    public class UserPermissionAuditDto
    {
        public string Code { get; set; } = string.Empty;
        public DateTime? GrantedAt { get; set; }
        public Guid? GrantedByUserId { get; set; }

        // true cuando el permiso es implícito por rol (Owner), no una fila otorgada explícitamente
        public bool Implicit { get; set; }
    }
}
