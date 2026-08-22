using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IPlanService
    {
        Task<List<PlansDto>> GetPlansAsync();
    }
}
