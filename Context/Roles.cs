namespace AgendaApp.Context
{
    public static class Roles
    {
        public const string Owner = "Owner";
        public const string Encargado = "Encargado";
        public const string Colaborador = "Colaborador";

        // Operador de la plataforma: aprueba empresas desde /plataforma. No es un rol de empresa (no está en All)
        public const string PlatformAdmin = "PlatformAdmin";

        public static readonly IReadOnlyCollection<string> All = new[] { Owner, Encargado, Colaborador };

        public static bool IsValid(string? role) => role != null && All.Contains(role);
    }
}
