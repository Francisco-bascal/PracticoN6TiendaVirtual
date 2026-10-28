using Microsoft.JSInterop;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Web.Servicios
{
    /// <summary>Comparte con la API Web Share del navegador y, si no existe, copia el texto al portapapeles.</summary>
    public class CompartirNavegador : ICompartir
    {
        private readonly IJSRuntime _js;

        public CompartirNavegador(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<bool> CompartirTextoAsync(string titulo, string texto)
            => await _js.InvokeAsync<bool>("tiendaCompartir", titulo, texto);
    }
}
