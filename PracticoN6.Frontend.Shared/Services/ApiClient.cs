using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>Error devuelto por la API (o imposibilidad de conectarse), con un mensaje listo para mostrarle al usuario.</summary>
    public class ApiException : Exception
    {
        /// <summary>Código HTTP; 0 si no hubo respuesta del servidor.</summary>
        public int Codigo { get; }

        public ApiException(int codigo, string mensaje) : base(mensaje)
        {
            Codigo = codigo;
        }

        public bool EsConflicto => Codigo == (int)HttpStatusCode.Conflict;
        public bool EsPagoRechazado => Codigo == (int)HttpStatusCode.PaymentRequired;
    }

    /// <summary>Token de la sesión actual, compartido entre el cliente HTTP y el servicio de sesión.</summary>
    public class CredencialesActuales
    {
        public string? Token { get; set; }
    }

    /// <summary>Cliente HTTP de la API: agrega el token, convierte los errores en <see cref="ApiException"/> y avisa cuando la sesión vence.</summary>
    public class ApiClient
    {
        private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly ServidorApi _servidor;
        private readonly CredencialesActuales _credenciales;

        /// <summary>Se dispara cuando la API responde 401 a una petición autenticada (token vencido o inválido).</summary>
        public event Func<Task>? SesionExpirada;

        public ApiClient(HttpClient http, ServidorApi servidor, CredencialesActuales credenciales)
        {
            _http = http;
            _servidor = servidor;
            _credenciales = credenciales;
        }

        public Task<T> GetAsync<T>(string ruta, CancellationToken cancellationToken = default)
            => LeerAsync<T>(HttpMethod.Get, ruta, null, cancellationToken);

        /// <summary>Para los endpoints que responden texto plano (no JSON), como el de estado de la API.</summary>
        public async Task<string> GetTextoAsync(string ruta, CancellationToken cancellationToken = default)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, null, cancellationToken);
            return await respuesta.Content.ReadAsStringAsync(cancellationToken);
        }

        public Task<T> PostAsync<T>(string ruta, object? cuerpo = null, CancellationToken cancellationToken = default)
            => LeerAsync<T>(HttpMethod.Post, ruta, Contenido(cuerpo), cancellationToken);

        public async Task PostAsync(string ruta, object? cuerpo = null, CancellationToken cancellationToken = default)
        {
            using var _ = await EnviarAsync(HttpMethod.Post, ruta, Contenido(cuerpo), cancellationToken);
        }

        public Task<T> PutAsync<T>(string ruta, object cuerpo, CancellationToken cancellationToken = default)
            => LeerAsync<T>(HttpMethod.Put, ruta, Contenido(cuerpo), cancellationToken);

        public async Task PatchAsync(string ruta, object cuerpo, CancellationToken cancellationToken = default)
        {
            using var _ = await EnviarAsync(HttpMethod.Patch, ruta, Contenido(cuerpo), cancellationToken);
        }

        public async Task DeleteAsync(string ruta, CancellationToken cancellationToken = default)
        {
            using var _ = await EnviarAsync(HttpMethod.Delete, ruta, null, cancellationToken);
        }

        public async Task<byte[]> GetBytesAsync(string ruta, CancellationToken cancellationToken = default)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, null, cancellationToken);
            return await respuesta.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public Task<T> PostArchivoAsync<T>(
            string ruta,
            string campo,
            string nombreArchivo,
            Stream contenido,
            string tipoMime,
            CancellationToken cancellationToken = default)
        {
            var archivo = new StreamContent(contenido);
            archivo.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(tipoMime) ? "application/octet-stream" : tipoMime);

            var formulario = new MultipartFormDataContent { { archivo, campo, nombreArchivo } };

            return LeerAsync<T>(HttpMethod.Post, ruta, formulario, cancellationToken);
        }

        /// <summary>URL absoluta de un recurso de la API (por ejemplo, la imagen de un producto para una etiqueta img).</summary>
        public async Task<string> UrlAbsolutaAsync(string ruta)
        {
            return (await _servidor.ObtenerUrlAsync()).TrimEnd('/') + "/" + ruta.TrimStart('/');
        }

        private static HttpContent? Contenido(object? cuerpo)
            => cuerpo == null ? null : JsonContent.Create(cuerpo, options: OpcionesJson);

        private async Task<T> LeerAsync<T>(HttpMethod metodo, string ruta, HttpContent? contenido, CancellationToken cancellationToken)
        {
            using var respuesta = await EnviarAsync(metodo, ruta, contenido, cancellationToken);

            try
            {
                var valor = await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson, cancellationToken);
                return valor ?? throw new ApiException((int)respuesta.StatusCode, "El servidor devolvió una respuesta vacía.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                // JsonException: el cuerpo no tiene la forma esperada. NotSupportedException: no es JSON (por ejemplo, texto plano).
                throw new ApiException((int)respuesta.StatusCode, "El servidor devolvió una respuesta que la aplicación no pudo interpretar.");
            }
        }

        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, HttpContent? contenido, CancellationToken cancellationToken)
        {
            using var peticion = new HttpRequestMessage(metodo, await UrlAbsolutaAsync(ruta)) { Content = contenido };

            string? token = _credenciales.Token;
            if (!string.IsNullOrEmpty(token))
            {
                peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpResponseMessage respuesta;

            try
            {
                respuesta = await _http.SendAsync(peticion, cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new ApiException(0, "No se pudo conectar con el servidor. Revise su conexión y la dirección configurada.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ApiException(0, "El servidor tardó demasiado en responder. Intente nuevamente.");
            }

            if (respuesta.IsSuccessStatusCode)
            {
                return respuesta;
            }

            string mensaje = await ExtraerMensajeAsync(respuesta);
            int codigo = (int)respuesta.StatusCode;
            respuesta.Dispose();

            // 401 con token: la sesión venció o el usuario fue desactivado. (Sin token es un login fallido: no hay sesión que cerrar.)
            if (codigo == (int)HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(token) && SesionExpirada != null)
            {
                await SesionExpirada.Invoke();
            }

            throw new ApiException(codigo, mensaje);
        }

        // La API responde de varias formas: texto plano, { "mensaje": ... } o ProblemDetails con errores de validación.
        private static async Task<string> ExtraerMensajeAsync(HttpResponseMessage respuesta)
        {
            string cuerpo = string.Empty;

            try
            {
                cuerpo = (await respuesta.Content.ReadAsStringAsync()).Trim();
            }
            catch (Exception)
            {
                // Sin cuerpo legible: se usa el mensaje genérico de abajo.
            }

            if (cuerpo.StartsWith('{'))
            {
                try
                {
                    using var json = JsonDocument.Parse(cuerpo);
                    var raiz = json.RootElement;

                    if (raiz.TryGetProperty("mensaje", out var mensaje) && mensaje.ValueKind == JsonValueKind.String)
                    {
                        return mensaje.GetString()!;
                    }

                    if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
                    {
                        var detalles = errores.EnumerateObject()
                            .SelectMany(e => e.Value.EnumerateArray().Select(m => m.GetString()))
                            .Where(m => !string.IsNullOrWhiteSpace(m));

                        return string.Join(" ", detalles);
                    }
                }
                catch (JsonException)
                {
                    // No era un JSON esperado: se cae al texto plano.
                }
            }

            if (cuerpo.Length > 0 && cuerpo.Length < 400 && !cuerpo.StartsWith('<'))
            {
                return cuerpo.Trim('"');
            }

            return respuesta.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Debe iniciar sesión para continuar.",
                HttpStatusCode.Forbidden => "No tiene permisos para realizar esta acción.",
                HttpStatusCode.NotFound => "No se encontró lo que buscaba.",
                HttpStatusCode.TooManyRequests => "Demasiados intentos. Espere un momento e intente nuevamente.",
                >= HttpStatusCode.InternalServerError => "El servidor tuvo un problema. Intente nuevamente en unos minutos.",
                _ => $"La operación no pudo completarse (error {(int)respuesta.StatusCode})."
            };
        }
    }
}
