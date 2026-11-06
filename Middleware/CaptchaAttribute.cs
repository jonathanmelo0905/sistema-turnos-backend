using AgendaApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgendaApp.Middleware
{
    // [Captcha] en un endpoint público: exige el token de Turnstile en la cabecera X-Captcha-Token.
    // Si falta o no es válido: 400 { message, code: "Captcha" } (el front reinicia el widget).
    public class CaptchaAttribute : TypeFilterAttribute
    {
        public CaptchaAttribute() : base(typeof(CaptchaFilter)) { }
    }

    // Filtro de recurso: corre antes de leer y validar el cuerpo, así un bot no llega ni a la validación
    public class CaptchaFilter : IAsyncResourceFilter
    {
        public const string Cabecera = "X-Captcha-Token";
        private readonly ITurnstileService _turnstile;

        public CaptchaFilter(ITurnstileService turnstile) => _turnstile = turnstile;

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var http = context.HttpContext;
            var token = http.Request.Headers[Cabecera].FirstOrDefault();
            if (!await _turnstile.ValidarAsync(token, http.Connection.RemoteIpAddress?.ToString()))
            {
                context.Result = new BadRequestObjectResult(new
                {
                    message = "No pudimos confirmar que no eres un robot. Completa la verificación e intenta de nuevo.",
                    code = "Captcha"
                });
                return;
            }
            await next();
        }
    }
}
