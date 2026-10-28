using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Infraestructura
{
    /// <summary>
    /// Registra en la auditoría quién hizo qué: toda acción que modifica datos (POST, PUT, PATCH, DELETE),
    /// los inicios de sesión y las acciones de lectura marcadas con [Auditar]. Nunca guarda el cuerpo de la petición
    /// (contiene contraseñas y datos de pago): solo la ruta, el usuario, la acción y el resultado.
    /// </summary>
    public class AuditoriaFilter : IAsyncActionFilter
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditoriaFilter> _logger;

        public AuditoriaFilter(IServiceScopeFactory scopeFactory, ILogger<AuditoriaFilter> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var ejecutada = await next();

            var http = context.HttpContext;
            var metadatos = context.ActionDescriptor.EndpointMetadata;

            bool esLectura = HttpMethods.IsGet(http.Request.Method)
                || HttpMethods.IsHead(http.Request.Method)
                || HttpMethods.IsOptions(http.Request.Method);

            if (metadatos.OfType<NoAuditarAttribute>().Any()
                || (esLectura && !metadatos.OfType<AuditarAttribute>().Any()))
            {
                return;
            }

            try
            {
                await RegistrarAsync(context, ejecutada);
            }
            catch (Exception ex)
            {
                // Un fallo al auditar no debe hacer fallar la operación que ya se realizó.
                _logger.LogError(ex, "No se pudo registrar la auditoría de {Ruta}.", http.Request.Path);
            }
        }

        private async Task RegistrarAsync(ActionExecutingContext context, ActionExecutedContext ejecutada)
        {
            var http = context.HttpContext;
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;

            int codigo = ejecutada.Exception != null && !ejecutada.ExceptionHandled
                ? StatusCodes.Status500InternalServerError
                : (ejecutada.Result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;

            string usuario = http.User.Identity?.Name ?? "anónimo";
            string accion = descriptor?.ActionName ?? http.Request.Method;
            string? detalle = $"{http.Request.Method} {http.Request.Path}{http.Request.QueryString}";

            // En el login y el registro todavía no hay token: el usuario sale del cuerpo (solo el nombre, nunca la contraseña).
            if (context.ActionArguments.Values.OfType<LoginRequestDTO>().FirstOrDefault() is { } login)
            {
                usuario = login.NombreUsuario;
                detalle = codigo == StatusCodes.Status200OK ? "Inicio de sesión exitoso" : "Inicio de sesión fallido";
            }
            else if (context.ActionArguments.Values.OfType<RegistrarUsuarioDTO>().FirstOrDefault() is { } registro)
            {
                usuario = registro.NombreUsuario;
                detalle = "Registro de usuario nuevo";
            }

            // Scope (y DbContext) propio: guardar la auditoría no debe arrastrar cambios pendientes de la operación auditada,
            // por ejemplo los de una transacción que se revirtió.
            using var scope = _scopeFactory.CreateScope();
            var auditoria = scope.ServiceProvider.GetRequiredService<AuditoriaService>();

            await auditoria.RegistrarAsync(
                usuario,
                accion,
                descriptor?.ControllerName ?? "(desconocida)",
                ObtenerEntidadId(context, ejecutada),
                detalle,
                http.Connection.RemoteIpAddress?.ToString(),
                codigo,
                http.RequestAborted);
        }

        // Id del recurso afectado: el de la ruta (PUT/DELETE/anular) o el del recurso recién creado (POST).
        private static string? ObtenerEntidadId(ActionExecutingContext context, ActionExecutedContext ejecutada)
        {
            if (context.RouteData.Values.TryGetValue("id", out var idRuta) && idRuta != null)
            {
                return idRuta.ToString();
            }

            if (ejecutada.Result is CreatedAtActionResult creado
                && creado.RouteValues != null
                && creado.RouteValues.TryGetValue("id", out var idCreado))
            {
                return idCreado?.ToString();
            }

            return null;
        }
    }
}