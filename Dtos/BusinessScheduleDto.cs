using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class BusinessScheduleDayDto
    {
        [Required, Range(1, 7)]
        public byte DayOfWeek { get; set; } // 1=Lunes ... 7=Domingo

        public bool IsOpen { get; set; } = true;

        public string? OpenTime { get; set; }  // "HH:mm"
        public string? CloseTime { get; set; } // "HH:mm"
    }

    // Reemplazo completo del bloque de 7 días
    public class BusinessScheduleUpdateDto
    {
        [Required, MinLength(7), MaxLength(7)]
        public List<BusinessScheduleDayDto> Days { get; set; } = new();
    }

    public class BusinessScheduleResponseDto
    {
        public Guid BusinessId { get; set; }
        public List<BusinessScheduleDayDto> Days { get; set; } = new();
    }
}
