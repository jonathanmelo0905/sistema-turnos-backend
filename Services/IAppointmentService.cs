using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IAppointmentService
    {
        Task<ServiceResult<List<AppointmentDto>>> GetAppointmentsAsync(Guid businessId);
        Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(AppointmentCreateDto dto);
        Task<ServiceResult<AppointmentDto>> UpdateAppointmentAsync(Guid id, AppointmentCreateDto dto);
        Task<ServiceResult<bool>> DeleteAppointmentAsync(Guid id);
    }
}
