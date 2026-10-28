using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Infraestructura;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Auditoría y registro de errores. La consulta es solo para el Administrador;
    // cualquier usuario autenticado puede informar un error ocurrido en la aplicación móvil.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly AuditoriaService _auditoria;
        private readonly ErrorLogService _errores;
        private readonly UsuarioActual _usuarioActual;

        public LogsController(AuditoriaService auditoria, ErrorLogService errores, UsuarioActual usuarioActual)
        {
            _auditoria = auditoria;
            _errores = errores;
            _usuarioActual = usuarioActual;
        }

        /// <summary>Registros de auditoría, más recientes primero. Filtros opcionales: desde, hasta, usuario, entidad y accion.</summary>
        [Authorize(Roles = NombresRol.Administrador)]
        [HttpGet("auditoria")]
        public async Task<IActionResult> ObtenerAuditoria(
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null,
            [FromQuery] string? usuario = null,
            [FromQuery] string? entidad = null,
            [FromQuery] string? accion = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _auditoria.ObtenerAsync(desde, hasta, usuario, entidad, accion, pagina, tamanoPagina, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Errores registrados, más recientes primero. Filtros opcionales: desde, hasta y origen ("API" o "App").</summary>
        [Authorize(Roles = NombresRol.Administrador)]
        [HttpGet("errores")]
        public async Task<IActionResult> ObtenerErrores(
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null,
            [FromQuery] string? origen = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _errores.ObtenerAsync(desde, hasta, origen, pagina, tamanoPagina, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>La aplicación móvil informa un error capturado del lado del cliente.</summary>
        [NoAuditar]
        [EnableRateLimiting("errores")]
        [HttpPost("errores")]
        public async Task<IActionResult> InformarError(
            [FromBody] ErrorClienteDTO error,
            CancellationToken cancellationToken = default)
        {
            int id = await _errores.RegistrarAsync(
                new RegistroError
                {
                    Origen = "App",
                    Mensaje = error.Mensaje,
                    TipoExcepcion = error.TipoExcepcion,
                    StackTrace = error.StackTrace,
                    Ruta = error.Ruta,
                    Usuario = _usuarioActual.Nombre
                },
                cancellationToken);

            return Ok(new { codigoError = id });
        }
    }
}
