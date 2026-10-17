namespace AgendaApp.Services.Almacen
{
    // Guarda en wwwroot/archivos/… y se sirve con UseStaticFiles en /archivos/….
    public class AlmacenLocal : IAlmacenArchivos
    {
        private const string Prefijo = "/archivos/";
        private readonly string _raiz;

        public AlmacenLocal(IWebHostEnvironment env)
        {
            _raiz = Path.Combine(env.ContentRootPath, "wwwroot", "archivos");
        }

        public async Task<string> GuardarAsync(string ruta, Stream contenido, string contentType, CancellationToken ct = default)
        {
            var destino = RutaFisica(ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            await using var archivo = File.Create(destino);
            await contenido.CopyToAsync(archivo, ct);
            return Prefijo + ruta.Replace(Path.DirectorySeparatorChar, '/');
        }

        public Task BorrarAsync(string url, CancellationToken ct = default)
        {
            if (url.StartsWith(Prefijo))
            {
                var destino = RutaFisica(url[Prefijo.Length..]);
                if (File.Exists(destino)) File.Delete(destino);
            }
            return Task.CompletedTask;
        }

        // Evita que una ruta con ".." salga de la carpeta raíz.
        private string RutaFisica(string ruta)
        {
            var completa = Path.GetFullPath(Path.Combine(_raiz, ruta));
            if (!completa.StartsWith(Path.GetFullPath(_raiz)))
                throw new InvalidOperationException("Ruta de archivo inválida.");
            return completa;
        }
    }
}
