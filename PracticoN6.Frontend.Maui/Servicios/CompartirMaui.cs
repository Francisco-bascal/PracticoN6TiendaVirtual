using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Maui.Servicios;

/// <summary>Abre la hoja de compartir de Android con un texto.</summary>
public class CompartirMaui : ICompartir
{
    public async Task<bool> CompartirTextoAsync(string titulo, string texto)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = titulo,
            Text = texto
        });

        return true;
    }
}
