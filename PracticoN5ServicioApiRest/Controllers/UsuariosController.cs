using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;
using System.Security.Claims;

namespace PracticoN5ServicioApiRest.Controllers
{
    // "test", "login" y "registro" son públicas ([AllowAnonymous]); "yo" es de cualquier usuario autenticado;
    // el resto de la gestión de usuarios es solo para el Administrador.
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuariosService _servicio;

        public UsuariosController(UsuariosService servicio)
        {
            _servicio = servicio;
        }

        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult Test()
        {
            return Ok("API funcionando");
        }

        /// <summary>Autentica credenciales y devuelve el token JWT a usar como Bearer en los [Authorize].</summary>
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request, CancellationToken cancellationToken = default)
        {
            var respuesta = await _servicio.LoginAsync(
                request.NombreUsuario,
                request.Password,
                cancellationToken);

            // null = credenciales inválidas (usuario inexistente, contraseña incorrecta o usuario desactivado) -> 401.
            if (respuesta == null)
            {
                return Unauthorized("Nombre de usuario o contraseña incorrectos.");
            }

            return Ok(respuesta);
        }

        /// <summary>Registra un usuario nuevo y le asigna automáticamente el rol Cliente.</summary>
        [AllowAnonymous]
        [HttpPost("registro")]
        public async Task<IActionResult> Registrar(
            [FromBody] RegistrarUsuarioDTO request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var usuarioCreado = await _servicio.RegistrarClienteAsync(request, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId),
                    new { id = usuarioCreado.UsuarioId },
                    MapearARespuesta(usuarioCreado));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        /// <summary>Datos y perfil de cliente del usuario autenticado.</summary>
        [Authorize]
        [HttpGet("yo")]
        public async Task<IActionResult> ObtenerPerfil(CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _servicio.ObtenerPerfilAsync(ObtenerIdUsuarioActual(), cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [Authorize]
        [HttpPut("yo")]
        public async Task<IActionResult> ActualizarPerfil(
            [FromBody] ActualizarPerfilDTO request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _servicio.ActualizarPerfilAsync(ObtenerIdUsuarioActual(), request, cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(CancellationToken cancellationToken = default)
        {
            var usuarios = await _servicio.ObtenerTodosAsync(cancellationToken);
            return Ok(usuarios.Select(MapearARespuesta).ToList());
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            var usuario = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
            if (usuario == null)
            {
                return NotFound($"No se encontró el usuario con ID {id}.");
            }

            return Ok(MapearARespuesta(usuario));
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpGet("usuario/{nombreUsuario}")]
        public async Task<IActionResult> ObtenerPorNombreUsuario(
            string nombreUsuario,
            CancellationToken cancellationToken = default)
        {
            var usuario = await _servicio.ObtenerPorNombreUsuarioAsync(nombreUsuario, cancellationToken);
            if (usuario == null)
            {
                return NotFound($"No se encontró el usuario con nombre '{nombreUsuario}'.");
            }

            return Ok(MapearARespuesta(usuario));
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpPost]
        public async Task<IActionResult> Crear(
            [FromBody] CrearUsuarioDTO request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var usuarioCreado = await _servicio.CrearAsync(request, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId),
                    new { id = usuarioCreado.UsuarioId },
                    MapearARespuesta(usuarioCreado));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Actualizar(
            int id,
            [FromBody] ActualizarUsuarioDTO request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var usuarioActualizado = await _servicio.ActualizarAsync(
                    id,
                    request,
                    ObtenerIdUsuarioActual(),
                    cancellationToken);
                return Ok(MapearARespuesta(usuarioActualizado));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _servicio.EliminarAsync(id, ObtenerIdUsuarioActual(), cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        private int ObtenerIdUsuarioActual()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        //Conversión de la entidad de usuario a un DTO que solo expone información no sensible
        private static UsuarioResponseDTO MapearARespuesta(Usuario usuario)
        {
            return new UsuarioResponseDTO
            {
                UsuarioId = usuario.UsuarioId,
                NombreUsuario = usuario.NombreUsuario,
                Email = usuario.Email,
                Rol = usuario.Rol.Nombre,
                Activo = usuario.Activo
            };
        }
    }
}
