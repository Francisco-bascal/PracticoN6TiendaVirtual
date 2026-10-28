using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Maui.Servicios;

/// <summary>Estado de la red a través de Connectivity (requiere el permiso ACCESS_NETWORK_STATE, ya declarado en el manifiesto).</summary>
public class ConectividadMaui : IConectividad
{
    private bool _escuchando;

    // Solo "sin ninguna red" cuenta como sin conexión: la API puede estar en la red local aunque no haya internet.
    public bool HayConexion => Connectivity.Current.NetworkAccess != NetworkAccess.None;

    public event Action<bool>? Cambio;

    public Task IniciarAsync()
    {
        if (!_escuchando)
        {
            _escuchando = true;
            Connectivity.Current.ConnectivityChanged += (sender, e) => Cambio?.Invoke(e.NetworkAccess != NetworkAccess.None);
        }

        return Task.CompletedTask;
    }
}
