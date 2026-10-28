using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Shared.Components
{
    /// <summary>
    /// ErrorBoundary que, además de mostrar un mensaje en lugar de la pantalla que falló, deja el error
    /// registrado (log local y registro de errores de la API).
    /// </summary>
    public class BoundaryConRegistro : ErrorBoundary
    {
        [Inject] private ClienteErrores Errores { get; set; } = default!;
        [Inject] private NavigationManager Navegacion { get; set; } = default!;

        protected override async Task OnErrorAsync(Exception exception)
        {
            await Errores.InformarAsync(exception, "/" + Navegacion.ToBaseRelativePath(Navegacion.Uri));
        }
    }
}
