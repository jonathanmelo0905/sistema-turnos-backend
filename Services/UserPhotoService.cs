using AgendaApp.Context;
using AgendaApp.Services.Almacen;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface IUserPhotoService
    {
        Task<ServiceResult<string>> SubirAsync(Guid userId, IFormFile archivo);
        Task<bool> QuitarAsync(Guid userId);
    }

    public class UserPhotoService : IUserPhotoService
    {
        public const long TamanoMaximo = 2 * 1024 * 1024;

        private readonly AgendaProDbContext _context;
        private readonly IAlmacenArchivos _almacen;

        public UserPhotoService(AgendaProDbContext context, IAlmacenArchivos almacen)
        {
            _context = context;
            _almacen = almacen;
        }

        public async Task<ServiceResult<string>> SubirAsync(Guid userId, IFormFile archivo)
        {
            await using var stream = archivo.OpenReadStream();
            var (error, extension) = await Imagenes.ValidarAsync(archivo, stream, TamanoMaximo);
            if (error != null)
                return ServiceResult<string>.BadRequest(error);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ServiceResult<string>.NotFound("Usuario no encontrado.");

            // Nombre con marca de tiempo: cada foto nueva tiene otra URL y no queda la vieja en caché.
            var ruta = $"fotos/{user.BusinessId}/{user.UserId}-{DateTime.UtcNow.Ticks}{extension}";
            var url = await _almacen.GuardarAsync(ruta, stream, archivo.ContentType);

            var anterior = user.PhotoUrl;
            user.PhotoUrl = url;
            await _context.SaveChangesAsync();
            if (anterior != null) await _almacen.BorrarAsync(anterior);

            return ServiceResult<string>.Ok(url);
        }

        public async Task<bool> QuitarAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return false;
            if (user.PhotoUrl != null)
            {
                await _almacen.BorrarAsync(user.PhotoUrl);
                user.PhotoUrl = null;
                await _context.SaveChangesAsync();
            }
            return true;
        }
    }
}
