namespace AgendaApp.Services
{
    // Festivos de Colombia (Ley 51 de 1983, "ley Emiliani"), calculados sin servicios externos:
    // - fijos: no se mueven;
    // - Emiliani: si no caen en lunes, pasan al lunes siguiente;
    // - de Pascua: Jueves y Viernes Santo fijos; Ascensión, Corpus Christi y Sagrado Corazón en lunes.
    public static class FestivosColombia
    {
        public record Festivo(DateTime Fecha, string Nombre);

        public static List<Festivo> DelAnio(int anio)
        {
            var pascua = DomingoDePascua(anio);
            var lista = new List<Festivo>
            {
                new(new DateTime(anio, 1, 1), "Año Nuevo"),
                new(Lunes(new DateTime(anio, 1, 6)), "Reyes Magos"),
                new(Lunes(new DateTime(anio, 3, 19)), "San José"),
                new(pascua.AddDays(-3), "Jueves Santo"),
                new(pascua.AddDays(-2), "Viernes Santo"),
                new(new DateTime(anio, 5, 1), "Día del Trabajo"),
                new(pascua.AddDays(43), "Ascensión del Señor"),
                new(pascua.AddDays(64), "Corpus Christi"),
                new(pascua.AddDays(71), "Sagrado Corazón"),
                new(Lunes(new DateTime(anio, 6, 29)), "San Pedro y San Pablo"),
                new(new DateTime(anio, 7, 20), "Día de la Independencia"),
                new(new DateTime(anio, 8, 7), "Batalla de Boyacá"),
                new(Lunes(new DateTime(anio, 8, 15)), "Asunción de la Virgen"),
                new(Lunes(new DateTime(anio, 10, 12)), "Día de la Raza"),
                new(Lunes(new DateTime(anio, 11, 1)), "Todos los Santos"),
                new(Lunes(new DateTime(anio, 11, 11)), "Independencia de Cartagena"),
                new(new DateTime(anio, 12, 8), "Inmaculada Concepción"),
                new(new DateTime(anio, 12, 25), "Navidad")
            };
            return lista.OrderBy(f => f.Fecha).ToList();
        }

        // Festivos entre dos fechas (inclusive)
        public static List<Festivo> Entre(DateTime desde, DateTime hasta) =>
            Enumerable.Range(desde.Year, hasta.Year - desde.Year + 1)
                .SelectMany(DelAnio)
                .Where(f => f.Fecha >= desde.Date && f.Fecha <= hasta.Date)
                .ToList();

        public static Festivo? De(DateTime fecha) => DelAnio(fecha.Year).FirstOrDefault(f => f.Fecha == fecha.Date);

        private static DateTime Lunes(DateTime d) =>
            d.DayOfWeek == DayOfWeek.Monday ? d : d.AddDays(((int)DayOfWeek.Monday - (int)d.DayOfWeek + 7) % 7);

        // Algoritmo anónimo gregoriano (Meeus/Jones/Butcher)
        private static DateTime DomingoDePascua(int y)
        {
            int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451, mes = (h + l - 7 * m + 114) / 31, dia = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(y, mes, dia);
        }
    }
}
