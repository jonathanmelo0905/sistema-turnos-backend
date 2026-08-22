using System.Text.Json;

namespace AgendaApp.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción no manejada en {Method} {Path}", context.Request.Method, context.Request.Path);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                var payload = JsonSerializer.Serialize(new
                {
                    message = "Ocurrió un error interno inesperado. Intenta nuevamente más tarde."
                });

                await context.Response.WriteAsync(payload);
            }
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseAppExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
