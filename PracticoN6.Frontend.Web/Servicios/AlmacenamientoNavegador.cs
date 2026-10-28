using Microsoft.JSInterop;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Web.Servicios
{
    /// <summary>Implementa el almacenamiento de la app con el localStorage del navegador.</summary>
    public class AlmacenamientoNavegador : IAlmacenamientoSeguro, IPreferencias
    {
        private readonly IJSRuntime _js;

        public AlmacenamientoNavegador(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<string?> LeerAsync(string clave) => await _js.InvokeAsync<string?>("tiendaAlmacen.leer", clave);

        public async Task GuardarAsync(string clave, string valor) => await _js.InvokeVoidAsync("tiendaAlmacen.guardar", clave, valor);

        public async Task EliminarAsync(string clave) => await _js.InvokeVoidAsync("tiendaAlmacen.eliminar", clave);
    }
}
