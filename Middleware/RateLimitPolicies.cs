namespace AgendaApp.Middleware
{
    // Nombres de las políticas de límite de peticiones (registradas en Program.cs).
    public static class RateLimitPolicies
    {
        public const string PublicoLectura = "publico-lectura";
        public const string PublicoEscritura = "publico-escritura";
    }
}
