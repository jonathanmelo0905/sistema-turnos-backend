namespace AgendaApp.Dtos
{
    public enum AvailabilityReason
    {
        Available,
        ServiceNotFound,
        ServiceNotProvided,
        UserNotFound,
        OutOfSchedule,        // único motivo elegible para la vía de flexibilidad (CU-05)
        OverlapsAppointment,  // rechazo duro siempre, sin excepción
        InPast,               // el horario ya pasó
        TimeOff               // el colaborador tiene una ausencia aprobada
    }
}
