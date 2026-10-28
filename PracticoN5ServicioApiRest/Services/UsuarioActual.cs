using PracticoN5ServicioApiRest.Models;
using System.Security.Claims;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>Identidad del usuario que realiza la petición actual, leída de los claims del token JWT.</summary>
    public class UsuarioActual
    {
        private readonly IHttpContextAccessor _accessor;

        public UsuarioActual(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        private ClaimsPrincipal? Usuario => _accessor.HttpContext?.User;

        public int? Id
        {
            get
            {
                string? valor = Usuario?.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(valor, out int id) ? id : null;
            }
        }

        public string? Nombre => Usuario?.FindFirstValue(ClaimTypes.Name);

        public bool EsPersonal =>
            Usuario != null
            && (Usuario.IsInRole(NombresRol.Administrador) || Usuario.IsInRole(NombresRol.Operador));
    }
}
