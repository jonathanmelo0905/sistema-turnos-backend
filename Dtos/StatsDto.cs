namespace AgendaApp.Dtos
{
    public class StatsTotalsDto
    {
        public int Appointments { get; set; }      // sin cancelados ni rechazados
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int NoShow { get; set; }
        public int Upcoming { get; set; }          // pendientes/confirmados aún por atender
        public double CancelRate { get; set; }     // % sobre todos los reservados
        public double NoShowRate { get; set; }     // % sobre los que debían atenderse
        public decimal Revenue { get; set; }       // atendidos
        public decimal Estimated { get; set; }     // por atender
        public decimal AvgTicket { get; set; }
        public int NewClients { get; set; }
        public int ReturningClients { get; set; }
        public double WebShare { get; set; }       // % reservados en línea
    }

    public class StatsDayDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public int Completed { get; set; }
        public decimal Revenue { get; set; }
    }

    public class StatsServiceDto
    {
        public Guid ServiceId { get; set; }
        public string Name { get; set; } = null!;
        public int Count { get; set; }
        public decimal Revenue { get; set; }
    }

    public class StatsStaffDto
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = null!;
        public int Count { get; set; }
        public int Completed { get; set; }
        public int NoShow { get; set; }
        public decimal Revenue { get; set; }
        public double OccupancyPct { get; set; }   // minutos reservados / minutos disponibles
        public double? Rating { get; set; }
        public int RatingCount { get; set; }
    }

    public class StatsHeatCellDto
    {
        public int DayOfWeek { get; set; }   // 1=Lunes … 7=Domingo
        public int Hour { get; set; }
        public int Count { get; set; }
        public bool Open { get; set; }       // la empresa atiende en esa hora
    }

    public class StatsDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public StatsTotalsDto Totals { get; set; } = new();
        public StatsTotalsDto Previous { get; set; } = new();
        public List<StatsDayDto> ByDay { get; set; } = new();
        public List<StatsServiceDto> ByService { get; set; } = new();
        public List<StatsStaffDto> ByStaff { get; set; } = new();
        public List<StatsHeatCellDto> Heatmap { get; set; } = new();
        public List<StatsHeatCellDto> SlowHours { get; set; } = new();
        public double RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public int[] RatingDistribution { get; set; } = new int[5];

        // Efectividad de Horas Valle en el periodo
        public StatsValleyDto Valley { get; set; } = new();
    }

    public class StatsValleyDto
    {
        public bool Enabled { get; set; }            // configuración actual de la empresa
        public int Bookings { get; set; }            // reservas con descuento hechas en el periodo (sin cancelar)
        public int PreviousBookings { get; set; }    // mismo dato en el periodo anterior
        public double SharePct { get; set; }         // % de los turnos del periodo
        public int Completed { get; set; }
        public int Upcoming { get; set; }            // reservados con descuento aún por atender
        public decimal Estimated { get; set; }       // lo que dejarán esos turnos
        public double NoShowRate { get; set; }       // % de no asistió entre los que debían atenderse
        public decimal Revenue { get; set; }         // ingresos de los atendidos con descuento
        public decimal DiscountGiven { get; set; }   // lo que se deja de cobrar en las reservas con descuento
        public double AvgDiscountPct { get; set; }
        public int NewClients { get; set; }          // su primer turno en la empresa fue en hora valle
        public double WebShare { get; set; }         // % reservados en línea
        public List<StatsHeatCellDto> TopSlots { get; set; } = new();     // franjas valle más tomadas
        public List<StatsServiceDto> TopServices { get; set; } = new();   // servicios más reservados con descuento
        public List<StatsDayDto> ByWeek { get; set; } = new();            // turnos valle por semana (Date = lunes)
    }
}
