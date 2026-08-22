namespace AgendaApp.Services
{
    public interface IPermissionCache
    {
        Task<HashSet<string>> GetPermissionCodesAsync(Guid userId, Func<Guid, Task<HashSet<string>>> loader);
        void Invalidate(Guid userId);
    }
}
