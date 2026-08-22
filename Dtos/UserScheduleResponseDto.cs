namespace AgendaApp.Dtos
{
    // DTO para mostrar un horario existente
    public class UserScheduleResponseDto
    {
        public Guid ScheduleId { get; set; }
        public Guid UserId { get; set; }
        public byte DayOfWeek { get; set; }
        public bool IsAvailable { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? BreakStart { get; set; }
        public string? BreakEnd { get; set; }
    }
}
