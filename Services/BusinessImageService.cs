using AgendaApp.Context;
using AgendaApp.Services.Almacen;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    // Las dos imágenes de la empresa: logo (círculo) y portada (banner de la página pública).
    public enum BusinessImage { Logo, Cover }

    public interface IBusinessImageService
    {
        Task<ServiceResult<string>> SubirAsync(Guid businessId, BusinessImage tipo, IFormFile archivo);
        Task<bool> QuitarAsync(Guid businessId, BusinessImage tipo);
    }

    public class BusinessImageService : IBusinessImageService
    {
        public const long LogoMaximo = 2 * 1024 * 1024;
        public const long PortadaMaxima = 5 * 1024 * 1024;

        private readonly AgendaProDbContext _context;
        private readonly IAlmacenArchivos _almacen;

        public BusinessImageService(AgendaProDbContext context, IAlmacenArchivos almacen)
        {
            _context = context;
            _almacen = almacen;
        }

        public async Task<ServiceResult<string>> SubirAsync(Guid businessId, BusinessImage tipo, IFormFile archivo)
        {
            await using var stream = archivo.OpenReadStream();
            var (error, extension) = await Imagenes.ValidarAsync(archivo, stream, tipo == BusinessImage.Logo ? LogoMaximo : PortadaMaxima);
            if (error != null)
                return ServiceResult<string>.BadRequest(error);

            var business = await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == businessId);
            if (business == null)
                return ServiceResult<string>.NotFound("Empresa no encontrada.");

            // Nombre con marca de tiempo: cada imagen nueva tiene otra URL y no queda la vieja en caché.
            var carpeta = tipo == BusinessImage.Logo ? "logos" : "portadas";
            var url = await _almacen.GuardarAsync($"{carpeta}/{businessId}/{DateTime.UtcNow.Ticks}{extension}", stream, archivo.ContentType);

            var anterior = tipo == BusinessImage.Logo ? business.LogoUrl : business.CoverUrl;
            if (tipo == BusinessImage.Logo) business.LogoUrl = url; else business.CoverUrl = url;
            await _context.SaveChangesAsync();
            if (anterior != null) await _almacen.BorrarAsync(anterior);

            return ServiceResult<string>.Ok(url);
        }

        public async Task<bool> QuitarAsync(Guid businessId, BusinessImage tipo)
        {
            var business = await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == businessId);
            if (business == null) return false;

            var actual = tipo == BusinessImage.Logo ? business.LogoUrl : business.CoverUrl;
            if (actual != null)
            {
                await _almacen.BorrarAsync(actual);
                if (tipo == BusinessImage.Logo) business.LogoUrl = null; else business.CoverUrl = null;
                await _context.SaveChangesAsync();
            }
            return true;
        }
    }
}
