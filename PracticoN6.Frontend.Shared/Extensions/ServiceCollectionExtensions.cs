using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra los servicios de la tienda. Cada host debe registrar además sus implementaciones de
        /// <see cref="IAlmacenamientoSeguro"/>, <see cref="IPreferencias"/> e <see cref="IGestorArchivos"/>.
        /// </summary>
        public static IServiceCollection AddTiendaShared(this IServiceCollection servicios, Action<OpcionesTienda>? configurar = null)
        {
            var opciones = new OpcionesTienda();
            configurar?.Invoke(opciones);

            servicios.AddSingleton(opciones);
            servicios.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

            servicios.AddScoped<CredencialesActuales>();
            servicios.AddScoped<ServidorApi>();
            servicios.AddScoped<ApiClient>();
            servicios.AddScoped<TiendaApi>();
            servicios.AddScoped<SesionService>();
            servicios.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<SesionService>());
            servicios.AddScoped<CarritoService>();
            servicios.AddScoped<ClienteErrores>();
            servicios.AddScoped<NotificacionesService>();

            servicios.AddAuthorizationCore();

            return servicios;
        }
    }
}
