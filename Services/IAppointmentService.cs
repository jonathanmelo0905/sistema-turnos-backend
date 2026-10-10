using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IAppointmentService
    {
        Task<ServiceResult<List<AppointmentDto>>> GetAppointmentsAsync(Guid businessId);
        Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(AppointmentCreateDto dto);
        Task<ServiceResult<AppointmentDto>> UpdateAppointmentAsync(Guid id, AppointmentCreateDto dto);
        Task<ServiceResult<bool>> DeleteAppointmentAsync(Guid id);

        // callerCanActOnBehalf: true si el caller es Owner o tiene el permiso flexibility.approve
        // (se resuelve en el controller, que tiene acceso a los claims del JWT).
        Task<ServiceResult<AppointmentDto>> ApproveOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf);
        Task<ServiceResult<AppointmentDto>> RejectOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf);
    }
}
