using Microsoft.JSInterop;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Web.Servicios
{
    /// <summary>En el navegador un archivo se entrega como descarga.</summary>
    public class GestorArchivosNavegador : IGestorArchivos
    {
        private readonly IJSRuntime _js;

        public GestorArchivosNavegador(IJSRuntime js)
        {
            _js = js;
        }

        public async Task GuardarYCompartirAsync(string nombreArchivo, byte[] contenido, string tipoMime)
        {
            await _js.InvokeVoidAsync("tiendaDescargar", nombreArchivo, tipoMime, Convert.ToBase64String(contenido));
        }
    }
}
