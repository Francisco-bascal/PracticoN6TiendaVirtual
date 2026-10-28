using System.Net;
using System.Text;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Shared.Tests
{
    /// <summary>Almacenamiento en memoria que reemplaza al del dispositivo.</summary>
    public class AlmacenamientoEnMemoria : IAlmacenamientoSeguro, IPreferencias
    {
        public Dictionary<string, string> Datos { get; } = new();

        public virtual Task<string?> LeerAsync(string clave) => Task.FromResult(Datos.TryGetValue(clave, out var v) ? v : null);

        public Task GuardarAsync(string clave, string valor)
        {
            Datos[clave] = valor;
            return Task.CompletedTask;
        }

        public Task EliminarAsync(string clave)
        {
            Datos.Remove(clave);
            return Task.CompletedTask;
        }
    }

    /// <summary>Almacenamiento con lectura realmente asíncrona, como el localStorage del navegador (por JS interop).</summary>
    public class AlmacenamientoLento : AlmacenamientoEnMemoria
    {
        public override async Task<string?> LeerAsync(string clave)
        {
            await Task.Delay(15);
            return await base.LeerAsync(clave);
        }
    }

    /// <summary>Servidor falso: responde lo que cada test programa y registra las peticiones recibidas.</summary>
    public class ManejadorFalso : HttpMessageHandler
    {
        public List<HttpRequestMessage> Peticiones { get; } = new();
        /// <summary>Cuerpo de cada petición (la petición se descarta después de enviarla, así que se lee al recibirla).</summary>
        public List<string?> Cuerpos { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Respuesta { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public Exception? Falla { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

            if (Falla != null)
            {
                throw Falla;
            }

            return Respuesta(request);
        }

        public static HttpResponseMessage Json(string json, HttpStatusCode codigo = HttpStatusCode.OK)
            => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        public static HttpResponseMessage Texto(string texto, HttpStatusCode codigo)
            => new(codigo) { Content = new StringContent(texto, Encoding.UTF8, "text/plain") };
    }

    /// <summary>Arma el conjunto de servicios de la tienda con dobles para el almacenamiento y el servidor.</summary>
    public class Entorno
    {
        public AlmacenamientoEnMemoria Almacenamiento { get; }
        public ManejadorFalso Servidor { get; } = new();
        public CredencialesActuales Credenciales { get; } = new();
        public ServidorApi ServidorApi { get; }
        public ApiClient Api { get; }
        public TiendaApi Tienda { get; }
        public SesionService Sesion { get; }
        public CarritoService Carrito { get; }

        public Entorno(string urlPorDefecto = "http://servidor.test:5276", AlmacenamientoEnMemoria? almacenamiento = null)
        {
            Almacenamiento = almacenamiento ?? new AlmacenamientoEnMemoria();
            ServidorApi = new ServidorApi(Almacenamiento, new OpcionesTienda { UrlApiPorDefecto = urlPorDefecto });
            Api = new ApiClient(new HttpClient(Servidor), ServidorApi, Credenciales);
            Tienda = new TiendaApi(Api);
            Sesion = new SesionService(Tienda, Api, Credenciales, Almacenamiento);
            Carrito = new CarritoService(Almacenamiento, Sesion);
        }

        /// <summary>Respuesta de login de la API para un usuario y rol.</summary>
        public static string RespuestaLogin(string usuario, string rol, DateTime? expira = null)
        {
            string vence = (expira ?? DateTime.UtcNow.AddHours(1)).ToString("o");
            return $$"""{"token":"token-de-{{usuario}}","nombreUsuario":"{{usuario}}","rol":"{{rol}}","expiraUtc":"{{vence}}"}""";
        }

        public Task IniciarSesionComoAsync(string usuario, string rol = "Cliente")
        {
            Servidor.Respuesta = _ => ManejadorFalso.Json(RespuestaLogin(usuario, rol));
            return Sesion.IniciarSesionAsync(usuario, "clave");
        }
    }
}
