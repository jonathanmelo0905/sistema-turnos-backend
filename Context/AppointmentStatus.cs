namespace AgendaApp.Context
{
    // Ciclo de vida de un turno:
    //   Pending ─► Confirmed ─► InProgress ─► Completed
    //      │           │
    //      ├─► Cancelled, NoShow (desde Pending/Confirmed)
    //   PendingOutOfHours ─► Confirmed | RejectedOutOfHours (vía aprobación)
    public static class AppointmentStatus
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string NoShow = "NoShow";
        public const string Cancelled = "Cancelled";
        public const string PendingOutOfHours = "PendingOutOfHours";
        public const string RejectedOutOfHours = "RejectedOutOfHours";

        private static readonly Dictionary<string, string[]> Transitions = new()
        {
            [Pending] = new[] { Confirmed, InProgress, Cancelled, NoShow },
            [Confirmed] = new[] { InProgress, Cancelled, NoShow },
            [InProgress] = new[] { Completed },
            [PendingOutOfHours] = new[] { Cancelled }
        };

        public static bool CanMove(string from, string to) =>
            Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

        // Estados en los que el turno ya no se puede reprogramar ni cambiar
        public static bool IsClosed(string status) =>
            status is Completed or NoShow or Cancelled or RejectedOutOfHours;
    }
}
