namespace AgendaApp.Context
{
    // Estado de la empresa en la plataforma: las registradas desde /registro esperan aprobación del operador
    public static class BusinessStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        // Empresa interna donde viven los operadores (PlatformAdmin); nunca sale en listas ni en la web
        public const string Internal = "Internal";

        public static bool EsRevisable(string? s) => s == Pending || s == Approved || s == Rejected;
    }
}
