using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Roles disponibles para asignar a los usuarios; solo el Administrador los consulta.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.Administrador)]
    public class RolesController : ControllerBase
    {
        private readonly UsuariosService _servicio;

        public RolesController(UsuariosService servicio)
        {
            _servicio = servicio;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(CancellationToken cancellationToken = default)
        {
            return Ok(await _servicio.ObtenerRolesAsync(cancellationToken));
        }
    }
}
