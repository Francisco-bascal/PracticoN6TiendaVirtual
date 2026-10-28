using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;
using PracticoN6.Frontend.Shared.Models;

namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>
    /// Sesión del usuario: inicio y cierre de sesión, restauración del token guardado en el dispositivo y roles.
    /// Es también el AuthenticationStateProvider, así que [Authorize(Roles = ...)] y AuthorizeView funcionan con él.
    /// </summary>
    public class SesionService : AuthenticationStateProvider
    {
        private const string ClaveSesion = "sesion";

        private static readonly ClaimsPrincipal Anonimo = new(new ClaimsIdentity());

        private readonly TiendaApi _tienda;
        private readonly ApiClient _api;
        private readonly CredencialesActuales _credenciales;
        private readonly IAlmacenamientoSeguro _almacenamiento;

        private Task? _restauracion;
        private LoginRespuesta? _sesion;

        /// <summary>Se dispara al iniciar o cerrar sesión (el carrito, por ejemplo, cambia con el usuario).</summary>
        public event Action? SesionCambio;

        /// <summary>Mensaje para mostrar en la pantalla de inicio de sesión (por ejemplo, "su sesión venció").</summary>
        public string? AvisoPendiente { get; set; }

        public SesionService(
            TiendaApi tienda,
            ApiClient api,
            CredencialesActuales credenciales,
            IAlmacenamientoSeguro almacenamiento)
        {
            _tienda = tienda;
            _api = api;
            _credenciales = credenciales;
            _almacenamiento = almacenamiento;

            _api.SesionExpirada += async () =>
            {
                AvisoPendiente = "Su sesión venció. Inicie sesión nuevamente.";
                await CerrarSesionAsync();
            };
        }

        public string? NombreUsuario => _sesion?.NombreUsuario;
        public string? Rol => _sesion?.Rol;
        public bool EstaAutenticado => _sesion != null;
        public bool EsAdministrador => Rol == Roles.Administrador;
        public bool EsPersonal => Rol is Roles.Administrador or Roles.Operador;

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            await AsegurarRestauradaAsync();
            return new AuthenticationState(CrearPrincipal());
        }

        /// <exception cref="ApiException">Credenciales inválidas o sin conexión.</exception>
        public async Task IniciarSesionAsync(string nombreUsuario, string password)
        {
            var respuesta = await _tienda.LoginAsync(nombreUsuario.Trim(), password);
            await EstablecerAsync(respuesta, guardar: true);
        }

        public async Task CerrarSesionAsync()
        {
            _sesion = null;
            _credenciales.Token = null;

            await _almacenamiento.EliminarAsync(ClaveSesion);

            SesionCambio?.Invoke();
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonimo)));
        }

        /// <summary>Espera a que se restaure (una sola vez) la sesión guardada: las páginas protegidas la necesitan antes de decidir.</summary>
        public Task AsegurarRestauradaAsync() => _restauracion ??= RestaurarAsync();

        private async Task RestaurarAsync()
        {
            try
            {
                string? json = await _almacenamiento.LeerAsync(ClaveSesion);
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                var guardada = JsonSerializer.Deserialize<LoginRespuesta>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

                if (guardada == null || guardada.ExpiraUtc <= DateTime.UtcNow.AddSeconds(30))
                {
                    await _almacenamiento.EliminarAsync(ClaveSesion);
                    return;
                }

                await EstablecerAsync(guardada, guardar: false);
            }
            catch (Exception)
            {
                // Un dato guardado ilegible (o el almacenamiento seguro no disponible) equivale a no tener sesión.
                _sesion = null;
                _credenciales.Token = null;
            }
        }

        private async Task EstablecerAsync(LoginRespuesta respuesta, bool guardar)
        {
            _sesion = respuesta;
            _credenciales.Token = respuesta.Token;

            if (guardar)
            {
                await _almacenamiento.GuardarAsync(ClaveSesion, JsonSerializer.Serialize(respuesta, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }

            SesionCambio?.Invoke();
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CrearPrincipal())));
        }

        private ClaimsPrincipal CrearPrincipal()
        {
            if (_sesion == null)
            {
                return Anonimo;
            }

            var identidad = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Name, _sesion.NombreUsuario),
                    new Claim(ClaimTypes.Role, _sesion.Rol)
                },
                authenticationType: "jwt");

            return new ClaimsPrincipal(identidad);
        }
    }
}
