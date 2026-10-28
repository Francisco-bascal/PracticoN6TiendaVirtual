using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Infraestructura
{
    /// <summary>
    /// Último recurso ante una excepción no controlada: la guarda en la tabla de errores (con ruta, usuario y
    /// stack trace) y responde un 500 en JSON con el código del error para que el usuario pueda informarlo.
    /// </summary>
    public class ManejoErroresMiddleware
    {
        private readonly RequestDelegate _siguiente;
        private readonly ILogger<ManejoErroresMiddleware> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public ManejoErroresMiddleware(
            RequestDelegate siguiente,
            ILogger<ManejoErroresMiddleware> logger,
            IServiceScopeFactory scopeFactory)
        {
            _siguiente = siguiente;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext contexto)
        {
            try
            {
                await _siguiente(contexto);
            }
            catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
            {
                // El cliente cortó la petición (por ejemplo, cerró la app): no es un error del servidor.
                contexto.Response.StatusCode = 499;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);

                int? codigoError = await RegistrarAsync(contexto, ex);

                if (contexto.Response.HasStarted)
                {
                    throw;
                }

                // Una falla al entregar el correo es un problema del servicio externo, no un error interno.
                bool esCorreo = ex is EnvioCorreoException;

                contexto.Response.StatusCode = esCorreo ? StatusCodes.Status502BadGateway : StatusCodes.Status500InternalServerError;

                await contexto.Response.WriteAsJsonAsync(new
                {
                    mensaje = esCorreo
                        ? ex.Message
                        : "Ocurrió un error inesperado. Si el problema persiste, informe el código de error.",
                    codigoError
                });
            }
        }

        private async Task<int?> RegistrarAsync(HttpContext contexto, Exception ex)
        {
            try
            {
                // Scope propio: el DbContext de la petición puede haber quedado en un estado inválido por el error.
                using var scope = _scopeFactory.CreateScope();
                var servicio = scope.ServiceProvider.GetRequiredService<ErrorLogService>();

                return await servicio.RegistrarAsync(new RegistroError
                {
                    Origen = "API",
                    Mensaje = ex.Message,
                    TipoExcepcion = ex.GetType().FullName,
                    StackTrace = ex.ToString(),
                    Ruta = contexto.Request.Path,
                    Metodo = contexto.Request.Method,
                    Usuario = contexto.User.Identity?.Name
                });
            }
            catch (Exception falla)
            {
                // Si ni siquiera se puede guardar el error, al menos queda en el log del servidor.
                _logger.LogError(falla, "No se pudo guardar el error en la base de datos.");
                return null;
            }
        }
    }
}
