namespace AgendaApp.Services.Almacen
{
    // Dónde se guardan los archivos subidos (fotos del personal). Hoy en disco local;
    // al desplegar se cambia por un almacenamiento de objetos (S3, R2, Azure Blob)
    // registrando otra implementación en Program.cs según "Almacen:Tipo".
    public interface IAlmacenArchivos
    {
        // Guarda el archivo y devuelve la URL pública (relativa a la API o absoluta).
        Task<string> GuardarAsync(string ruta, Stream contenido, string contentType, CancellationToken ct = default);

        // Borra el archivo de una URL devuelta por GuardarAsync; ignora si no existe.
        Task BorrarAsync(string url, CancellationToken ct = default);
    }
}
