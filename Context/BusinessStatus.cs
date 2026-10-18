namespace AgendaApp.Context
{
    // Estado de la empresa en la plataforma: las registradas desde /registro esperan aprobación del operador
    public static class BusinessStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }
}
