using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Maui.Servicios;

/// <summary>Preferencias no sensibles (dirección del servidor, carrito) en las preferencias de la aplicación.</summary>
public class PreferenciasMaui : IPreferencias
{
    public Task<string?> LeerAsync(string clave)
    {
        string? valor = Preferences.Default.Get<string?>(clave, null);
        return Task.FromResult(valor);
    }

    public Task GuardarAsync(string clave, string valor)
    {
        Preferences.Default.Set(clave, valor);
        return Task.CompletedTask;
    }

    public Task EliminarAsync(string clave)
    {
        Preferences.Default.Remove(clave);
        return Task.CompletedTask;
    }
}
