using Microsoft.JSInterop;

namespace PracticoN6.Frontend.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

#if ANDROID
        // BlazorWebView sirve la interfaz desde una página https interna. Las imágenes del catálogo se piden a la API,
        // que en desarrollo es http (http://10.0.2.2:5276): sin este ajuste la vista web de Android las bloquea
        // como "contenido mixto". En producción conviene servir la API por https.
        blazorWebView.BlazorWebViewInitialized += (sender, e) =>
        {
            e.WebView.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
        };
#endif
    }

    // El botón "atrás" de Android navega hacia atrás dentro de la interfaz Blazor y solo cierra la app
    // cuando ya no hay pantalla anterior.
    protected override bool OnBackButtonPressed()
    {
        _ = VolverAsync();
        return true;
    }

    private async Task VolverAsync()
    {
        bool pudoVolver = false;

        await blazorWebView.TryDispatchAsync(async servicios =>
        {
            var js = servicios.GetRequiredService<IJSRuntime>();
            pudoVolver = await js.InvokeAsync<bool>("tiendaVolver");
        });

        if (!pudoVolver)
        {
            Application.Current?.Quit();
        }
    }
}
