using Microsoft.Extensions.Logging;
using PracticoN6.Frontend.Maui.Servicios;
using PracticoN6.Frontend.Shared.Extensions;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Maui;

public static class MauiProgram
{
    // Dirección de la API en desarrollo. En el emulador de Android, 10.0.2.2 es la computadora que lo aloja
    // (localhost sería el propio emulador). Desde la app se puede cambiar en la pantalla "Servidor".

    private static readonly string UrlApiDesarrollo = DeviceInfo.DeviceType == DeviceType.Virtual
        ? "http://10.0.2.2:5276"   // emulador: 10.0.2.2 es la PC anfitriona
        : "http://localhost:5276"; // teléfono físico con "adb reverse"

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Interfaz y lógica de la tienda (proyecto compartido).
        builder.Services.AddTiendaShared(opciones => opciones.UrlApiPorDefecto = UrlApiDesarrollo);

        // Lo que depende del dispositivo.
        builder.Services.AddSingleton<IAlmacenamientoSeguro, AlmacenamientoSeguroMaui>();
        builder.Services.AddSingleton<IPreferencias, PreferenciasMaui>();
        builder.Services.AddSingleton<IGestorArchivos, GestorArchivosMaui>();
        builder.Services.AddSingleton<IConectividad, ConectividadMaui>();
        builder.Services.AddSingleton<ICompartir, CompartirMaui>();

        return builder.Build();
    }
}