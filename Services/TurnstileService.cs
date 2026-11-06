using System.Text.Json.Serialization;

namespace AgendaApp.Services
{
    // Captcha Cloudflare Turnstile (Fase 9 T5). Valida el token del widget contra siteverify.
    // Sin Turnstile:SecretKey el captcha queda desactivado (se avisa en el log al arrancar).
    // Claves de prueba de Cloudflare: secreto 1x0000000000000000000000000000000AA (siempre aprueba)
    // y 2x0000000000000000000000000000000AA (siempre rechaza).
    public interface ITurnstileService
    {
        bool Activo { get; }
        Task<bool> ValidarAsync(string? token, string? ip);
    }

    public class TurnstileService : ITurnstileService
    {
        private const string Url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        private readonly HttpClient _http;
        private readonly ILogger<TurnstileService> _logger;
        private readonly string? _secreto;

        public TurnstileService(HttpClient http, IConfiguration config, ILogger<TurnstileService> logger)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(8);
            _logger = logger;
            _secreto = config["Turnstile:SecretKey"];
        }

        public bool Activo => !string.IsNullOrWhiteSpace(_secreto);

        public async Task<bool> ValidarAsync(string? token, string? ip)
        {
            if (!Activo) return true;
            if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;

            var campos = new Dictionary<string, string> { ["secret"] = _secreto!, ["response"] = token };
            if (!string.IsNullOrEmpty(ip)) campos["remoteip"] = ip;
            try
            {
                using var r = await _http.PostAsync(Url, new FormUrlEncodedContent(campos));
                var res = await r.Content.ReadFromJsonAsync<Respuesta>();
                if (res?.Success != true)
                    _logger.LogInformation("Captcha rechazado: {Errores}.", string.Join(", ", res?.ErrorCodes ?? Array.Empty<string>()));
                return res?.Success == true;
            }
            catch (Exception ex)
            {
                // Sin respuesta de Cloudflare no se deja pasar (falla cerrado)
                _logger.LogWarning(ex, "No se pudo validar el captcha con Cloudflare.");
                return false;
            }
        }

        private class Respuesta
        {
            [JsonPropertyName("success")] public bool Success { get; set; }
            [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
        }
    }
}
