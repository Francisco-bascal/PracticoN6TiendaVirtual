using Microsoft.Extensions.Logging;

namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>
    /// Registro de errores de la aplicación: los deja en el log local y, si hay sesión, informa a la API
    /// (POST api/Logs/errores) para que queden en el registro de errores que consulta el administrador.
    /// </summary>
    public class ClienteErrores
    {
        private readonly ApiClient _api;
        private readonly CredencialesActuales _credenciales;
        private readonly ILogger<ClienteErrores> _logger;

        public ClienteErrores(ApiClient api, CredencialesActuales credenciales, ILogger<ClienteErrores> logger)
        {
            _api = api;
            _credenciales = credenciales;
            _logger = logger;
        }

        public async Task InformarAsync(Exception excepcion, string? ruta = null)
        {
            _logger.LogError(excepcion, "Error no controlado en la aplicación ({Ruta})", ruta);

            // Los errores esperados (validaciones, conflictos, falta de conexión) ya se le muestran al usuario: no se informan.
            if (excepcion is ApiException { Codigo: < 500 } || string.IsNullOrEmpty(_credenciales.Token))
            {
                return;
            }

            try
            {
                await _api.PostAsync("api/Logs/errores", new
                {
                    mensaje = excepcion.Message,
                    tipoExcepcion = excepcion.GetType().FullName,
                    stackTrace = excepcion.ToString(),
                    ruta
                });
            }
            catch (Exception)
            {
                // Informar el error no puede generar otro error: si falla, queda solo el log local.
            }
        }
    }
}
