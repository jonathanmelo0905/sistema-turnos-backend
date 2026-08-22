namespace AgendaApp.Dtos
{
    public class ResultadoService
    {
        public bool Status { get; set; }
        public required string Mensaje { get; set; }
        public object? Data { get; set; }
    }
}
