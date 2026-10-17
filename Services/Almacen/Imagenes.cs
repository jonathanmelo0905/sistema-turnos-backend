namespace AgendaApp.Services.Almacen
{
    // Validación común de imágenes subidas (fotos del personal, logo y portada de la empresa).
    public static class Imagenes
    {
        // El tipo se decide por la firma del archivo, no por lo que diga el navegador.
        // Devuelve la extensión (".jpg", ".png", ".webp") o null si no es una imagen aceptada.
        public static async Task<string?> DetectarAsync(Stream stream)
        {
            var cabecera = new byte[12];
            var leidos = await stream.ReadAsync(cabecera);
            stream.Position = 0;
            if (leidos < 12) return null;
            if (cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF) return ".jpg";
            if (cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47) return ".png";
            if (cabecera[0] == 'R' && cabecera[1] == 'I' && cabecera[2] == 'F' && cabecera[3] == 'F'
                && cabecera[8] == 'W' && cabecera[9] == 'E' && cabecera[10] == 'B' && cabecera[11] == 'P') return ".webp";
            return null;
        }

        // Error legible o null si el archivo sirve (tamaño y tipo).
        public static async Task<(string? Error, string? Extension)> ValidarAsync(IFormFile archivo, Stream stream, long tamanoMaximo)
        {
            if (archivo.Length == 0) return ("El archivo está vacío.", null);
            if (archivo.Length > tamanoMaximo) return ($"La imagen no puede pesar más de {tamanoMaximo / (1024 * 1024)} MB.", null);
            var extension = await DetectarAsync(stream);
            return extension == null ? ("Solo se aceptan imágenes JPG, PNG o WEBP.", null) : (null, extension);
        }
    }
}
