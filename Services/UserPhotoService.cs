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
            if (archivo.Length == 0)
                return ServiceResult<string>.BadRequest("El archivo está vacío.");
            if (archivo.Length > TamanoMaximo)
                return ServiceResult<string>.BadRequest("La foto no puede pesar más de 2 MB.");

            // El tipo se decide por la firma del archivo, no por lo que diga el navegador.
            await using var stream = archivo.OpenReadStream();
            var extension = await DetectarImagenAsync(stream);
            if (extension == null)
                return ServiceResult<string>.BadRequest("Solo se aceptan fotos JPG, PNG o WEBP.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ServiceResult<string>.NotFound("Usuario no encontrado.");

            // Nombre con marca de tiempo: cada foto nueva tiene otra URL y no queda la vieja en caché.
            var ruta = $"fotos/{user.BusinessId}/{user.UserId}-{DateTime.UtcNow.Ticks}{extension}";
            stream.Position = 0;
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

        private static async Task<string?> DetectarImagenAsync(Stream stream)
        {
            var cabecera = new byte[12];
            var leidos = await stream.ReadAsync(cabecera);
            if (leidos < 12) return null;
            if (cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF) return ".jpg";
            if (cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47) return ".png";
            if (cabecera[0] == 'R' && cabecera[1] == 'I' && cabecera[2] == 'F' && cabecera[3] == 'F'
                && cabecera[8] == 'W' && cabecera[9] == 'E' && cabecera[10] == 'B' && cabecera[11] == 'P') return ".webp";
            return null;
        }
    }
}
