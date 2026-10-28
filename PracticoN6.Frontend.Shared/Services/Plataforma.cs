namespace PracticoN6.Frontend.Shared.Services
{
    // Capacidades que dependen del dispositivo. La app móvil (MAUI) y el host web las implementan cada una a su manera;
    // el resto de la aplicación solo conoce estas interfaces.

    /// <summary>Almacenamiento cifrado para datos sensibles (el token de sesión). MAUI: SecureStorage.</summary>
    public interface IAlmacenamientoSeguro
    {
        Task<string?> LeerAsync(string clave);
        Task GuardarAsync(string clave, string valor);
        Task EliminarAsync(string clave);
    }

    /// <summary>Almacenamiento simple de preferencias (dirección del servidor, carrito). MAUI: Preferences.</summary>
    public interface IPreferencias
    {
        Task<string?> LeerAsync(string clave);
        Task GuardarAsync(string clave, string valor);
        Task EliminarAsync(string clave);
    }

    /// <summary>Entrega un archivo (por ejemplo un PDF) al usuario. MAUI: hoja de compartir de Android. Web: descarga.</summary>
    public interface IGestorArchivos
    {
        Task GuardarYCompartirAsync(string nombreArchivo, byte[] contenido, string tipoMime);
    }

    /// <summary>Estado de la red del dispositivo. MAUI: Connectivity. Navegador: navigator.onLine.</summary>
    public interface IConectividad
    {
        /// <summary>false cuando el dispositivo no tiene ninguna red (modo avión, sin datos ni wifi).</summary>
        bool HayConexion { get; }

        /// <summary>Se dispara cada vez que la red aparece o desaparece.</summary>
        event Action<bool>? Cambio;

        /// <summary>Empieza a escuchar los cambios de red (en el navegador necesita JS interop, por eso es asíncrono). Se puede invocar varias veces.</summary>
        Task IniciarAsync();
    }

    /// <summary>Compartir texto con otras aplicaciones. MAUI: hoja de compartir de Android. Navegador: Web Share o portapapeles.</summary>
    public interface ICompartir
    {
        /// <returns>true si se abrió la hoja de compartir; false si el texto solo se copió al portapapeles.</returns>
        Task<bool> CompartirTextoAsync(string titulo, string texto);
    }

    public class OpcionesTienda
    {
        /// <summary>Dirección de la API que se usa mientras el usuario no configure otra.</summary>
        public string UrlApiPorDefecto { get; set; } = "http://localhost:5276";
    }
}
