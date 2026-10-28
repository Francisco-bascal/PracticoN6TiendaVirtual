using Microsoft.JSInterop;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Web.Servicios
{
    /// <summary>Estado de la red del navegador (navigator.onLine y los eventos online/offline).</summary>
    public class ConectividadNavegador : IConectividad, IDisposable
    {
        private readonly IJSRuntime _js;
        private DotNetObjectReference<ConectividadNavegador>? _referencia;

        public ConectividadNavegador(IJSRuntime js)
        {
            _js = js;
        }

        public bool HayConexion { get; private set; } = true;

        public event Action<bool>? Cambio;

        public async Task IniciarAsync()
        {
            if (_referencia != null)
            {
                return;
            }

            _referencia = DotNetObjectReference.Create(this);
            HayConexion = await _js.InvokeAsync<bool>("tiendaConectividad.suscribir", _referencia);
        }

        [JSInvokable]
        public void Notificar(bool hayConexion)
        {
            HayConexion = hayConexion;
            Cambio?.Invoke(hayConexion);
        }

        public void Dispose() => _referencia?.Dispose();
    }
}
