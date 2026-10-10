using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class ValleyConfigDto
    {
        public bool Enabled { get; set; }

        [Required, RegularExpression("^(Percent|Count)$", ErrorMessage = "El criterio debe ser Percent o Count.")]
        public string Mode { get; set; } = "Percent";

        [Range(0, 1000, ErrorMessage = "El umbral debe estar entre 0 y 1000.")]
        public decimal Threshold { get; set; }

        [Range(1, 90, ErrorMessage = "El descuento mínimo debe estar entre 1 % y 90 %.")]
        public int DiscountMin { get; set; }

        [Range(1, 90, ErrorMessage = "El descuento máximo debe estar entre 1 % y 90 %.")]
        public int DiscountMax { get; set; }

        [Range(1, 100, ErrorMessage = "El solape mínimo debe estar entre 1 % y 100 %.")]
        public int MinOverlapPct { get; set; }

        public bool Proportional { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // Una franja día × hora con su demanda de los últimos 30 días
    public class ValleyCellDto
    {
        public int DayOfWeek { get; set; }   // 1 = lunes … 7 = domingo
        public int Hour { get; set; }
        public int Count { get; set; }
        public double SharePct { get; set; } // % del total de citas del periodo
        public bool IsValley { get; set; }
        public int Pct { get; set; }         // descuento de la franja (0 si no es valle)
    }

    public class ValleyStateDto
    {
        public ValleyConfigDto Config { get; set; } = new();
        public int Total { get; set; }            // citas de los últimos 30 días
        public int MinData { get; set; }          // mínimo de citas para calcular valles
        public bool EnoughData { get; set; }
        public int Days { get; set; }
        public List<ValleyCellDto> Cells { get; set; } = new();   // solo franjas abiertas
    }

    // Descuento de un servicio que empieza a una hora concreta
    public class ValleyQuoteDto
    {
        public DateTime Start { get; set; }
        public int Pct { get; set; }          // descuento final que se cobra
        public int BasePct { get; set; }      // descuento de las horas valle tocadas (ponderado por minutos)
        public int OverlapPct { get; set; }   // % de la duración del servicio dentro de hora valle
        public bool Full { get; set; }        // true: solape ≥ mínimo; false: proporcional
    }

    // Lo que ve el cliente en la página pública
    public class PublicValleyDto
    {
        public int MinOverlapPct { get; set; }
        public bool Proportional { get; set; }
        public int DiscountMax { get; set; }
        public int Days { get; set; }
        public List<ValleyCellDto> Windows { get; set; } = new();
    }
}
