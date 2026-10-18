using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgendaApp.Middleware
{
    // Los controladores responden errores con texto (BadRequest("...")); los fronts piden JSON y fallaban
    // al leerlo ("Unexpected token ... is not valid JSON"). Toda respuesta de error con texto sale como { message }.
    public class MensajeErrorFilter : IAlwaysRunResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is ObjectResult { Value: string texto } r && (r.StatusCode ?? 200) >= 400)
            {
                r.Value = new { message = texto };
                r.DeclaredType = null;
                r.ContentTypes.Clear();
            }
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}
