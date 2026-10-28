using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Maui.Servicios;

/// <summary>
/// Datos sensibles (el token de sesión) en el almacenamiento cifrado del dispositivo
/// (Android Keystore a través de SecureStorage).
/// </summary>
public class AlmacenamientoSeguroMaui : IAlmacenamientoSeguro
{
    public async Task<string?> LeerAsync(string clave)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(clave);
        }
        catch (Exception)
        {
            // Si el dispositivo no puede descifrar el valor (por ejemplo, tras restaurar una copia de seguridad),
            // se lo descarta: el usuario simplemente vuelve a iniciar sesión.
            SecureStorage.Default.Remove(clave);
            return null;
        }
    }

    public Task GuardarAsync(string clave, string valor) => SecureStorage.Default.SetAsync(clave, valor);

    public Task EliminarAsync(string clave)
    {
        SecureStorage.Default.Remove(clave);
        return Task.CompletedTask;
    }
}
