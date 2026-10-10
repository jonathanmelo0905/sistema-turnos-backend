using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    // Horas Valle: descuento automático en las franjas día × hora con menos reservas de los últimos 30 días.
    // Una fila por empresa; sin fila = desactivado con los valores por defecto.
    public class ValleyConfig
    {
        [Key]
        public Guid BusinessId { get; set; }

        public bool Enabled { get; set; }

        // Percent: la franja es valle si tiene ≤ Threshold % de las citas; Count: si tiene ≤ Threshold citas
        [Required, MaxLength(10)]
        public string Mode { get; set; } = ValleyModes.Percent;

        [Column(TypeName = "decimal(6,2)")]
        public decimal Threshold { get; set; } = 2;

        // Rango del descuento: el máximo para franjas sin citas, el mínimo justo en el umbral
        public int DiscountMin { get; set; } = 10;
        public int DiscountMax { get; set; } = 25;

        // % mínimo de la duración del servicio que debe caer en hora valle
        public int MinOverlapPct { get; set; } = 80;

        // Bajo el mínimo: descuento proporcional al solape en vez de ninguno
        public bool Proportional { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
    }

    public static class ValleyModes
    {
        public const string Percent = "Percent";
        public const string Count = "Count";
    }
}
