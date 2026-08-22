namespace AgendaApp.Dtos
{
    public class AvailabilitySlotDto
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class AvailabilityDayDto
    {
        public DateTime Date { get; set; }
        public List<AvailabilitySlotDto> Slots { get; set; } = new();
    }

    public class AvailabilityResponseDto
    {
        public Guid UserId { get; set; }
        public Guid? ServiceId { get; set; }
        public int? DurationMin { get; set; }
        public List<AvailabilityDayDto> Days { get; set; } = new();
    }
}
