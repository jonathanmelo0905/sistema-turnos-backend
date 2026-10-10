using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IAppointmentService
    {
        Task<List<AppointmentDto>> ListAsync(Guid businessId, DateTime from, DateTime to, Guid? userId, string? status);
        Task<AppointmentDetailDto?> GetDetailAsync(Guid businessId, Guid id);

        // Reserva: el fin se calcula con la duración del servicio; el precio queda congelado
        Task<ServiceResult<AppointmentDto>> BookAsync(Guid businessId, Guid? byUserId, string source, AppointmentBookDto dto);

        // Reprogramar (otra hora) o ceder (otro colaborador), revalidando disponibilidad
        Task<ServiceResult<AppointmentDto>> RescheduleAsync(Guid businessId, Guid id, Guid byUserId, AppointmentRescheduleDto dto);

        Task<ServiceResult<AppointmentDto>> ChangeStatusAsync(Guid businessId, Guid id, Guid byUserId, AppointmentStatusDto dto);

        // Compatibilidad con el contrato anterior (POST /api/appointments con fin explícito)
        Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(AppointmentCreateDto dto);

        // callerCanActOnBehalf: true si el caller es Owner o tiene el permiso flexibility.approve
        Task<ServiceResult<AppointmentDto>> ApproveOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf);
        Task<ServiceResult<AppointmentDto>> RejectOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf);
    }
}
