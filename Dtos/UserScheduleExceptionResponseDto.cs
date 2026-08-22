namespace AgendaApp.Dtos
{
    // DTO para mostrar una excepción
    public class UserScheduleExceptionResponseDto
    {
        public Guid ExceptionId { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public bool IsAvailable { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Reason { get; set; }
    }
}
