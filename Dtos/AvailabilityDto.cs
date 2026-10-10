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

        // Horas de inicio reservables (cada SlotStepMin) cuando se consulta con un servicio
        public List<DateTime> Starts { get; set; } = new();

        // Ausencia aprobada que afecta el día (para mostrarla en la agenda)
        public string? TimeOff { get; set; }
    }

    public class StaffAvailabilityDto
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = null!;
        public List<DateTime> Starts { get; set; } = new();
        public string? TimeOff { get; set; }
    }

    public class AvailabilityResponseDto
    {
        public Guid UserId { get; set; }
        public Guid? ServiceId { get; set; }
        public int? DurationMin { get; set; }
        public List<AvailabilityDayDto> Days { get; set; } = new();
    }
}
