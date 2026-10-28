namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>Dirección de la API. El usuario puede cambiarla desde la app (por ejemplo, para apuntar al IP de la PC desde un teléfono real).</summary>
    public class ServidorApi
    {
        private const string Clave = "servidor.url";

        private readonly IPreferencias _preferencias;
        private readonly OpcionesTienda _opciones;
        private string? _url;

        public ServidorApi(IPreferencias preferencias, OpcionesTienda opciones)
        {
            _preferencias = preferencias;
            _opciones = opciones;
        }

        public string UrlPorDefecto => _opciones.UrlApiPorDefecto;

        public async Task<string> ObtenerUrlAsync()
        {
            _url ??= await _preferencias.LeerAsync(Clave) ?? _opciones.UrlApiPorDefecto;
            return _url;
        }

        /// <exception cref="ArgumentException">La dirección no es una URL http o https válida.</exception>
        public async Task GuardarUrlAsync(string url)
        {
            string normalizada = Normalizar(url);

            await _preferencias.GuardarAsync(Clave, normalizada);
            _url = normalizada;
        }

        public async Task RestablecerAsync()
        {
            await _preferencias.EliminarAsync(Clave);
            _url = _opciones.UrlApiPorDefecto;
        }

        public static string Normalizar(string? url)
        {
            string texto = (url ?? string.Empty).Trim().TrimEnd('/');

            if (texto.Length > 0 && !texto.Contains("://"))
            {
                texto = "http://" + texto;
            }

            if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Ingrese una dirección válida, por ejemplo http://192.168.0.10:5276");
            }

            return texto;
        }
    }
}
