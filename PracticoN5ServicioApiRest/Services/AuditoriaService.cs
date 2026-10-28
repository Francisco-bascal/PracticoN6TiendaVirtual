using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class AuditoriaService
    {
        private readonly SistemaVentasDbContext _contexto;
        private readonly ZonaHorariaNegocio _zonaHoraria;

        public AuditoriaService(SistemaVentasDbContext contexto, ZonaHorariaNegocio zonaHoraria)
        {
            _contexto = contexto;
            _zonaHoraria = zonaHoraria;
        }

        public async Task RegistrarAsync(
            string usuario,
            string accion,
            string entidad,
            string? entidadId,
            string? detalle,
            string? direccionIp,
            int codigoRespuesta,
            CancellationToken cancellationToken = default)
        {
            _contexto.RegistrosAuditoria.Add(new RegistroAuditoria
            {
                Fecha = DateTime.UtcNow,
                Usuario = Recortar(usuario, 50) ?? "anónimo",
                Accion = Recortar(accion, 60) ?? string.Empty,
                Entidad = Recortar(entidad, 60) ?? string.Empty,
                EntidadId = Recortar(entidadId, 40),
                Detalle = Recortar(detalle, 500),
                DireccionIp = Recortar(direccionIp, 45),
                CodigoRespuesta = codigoRespuesta
            });

            await _contexto.SaveChangesAsync(cancellationToken);
        }

        public async Task<ResultadoPaginadoDto<RegistroAuditoria>> ObtenerAsync(
            DateTime? desde,
            DateTime? hasta,
            string? usuario,
            string? entidad,
            string? accion,
            int pagina = 1,
            int tamanoPagina = 20,
            CancellationToken cancellationToken = default)
        {
            Paginacion.Validar(pagina, tamanoPagina);

            var (desdeUtc, hastaUtc) = _zonaHoraria.RangoUtc(desde, hasta);
            var consulta = _contexto.RegistrosAuditoria.AsNoTracking().AsQueryable();

            if (desdeUtc.HasValue)
            {
                consulta = consulta.Where(r => r.Fecha >= desdeUtc.Value);
            }

            if (hastaUtc.HasValue)
            {
                consulta = consulta.Where(r => r.Fecha < hastaUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(usuario))
            {
                consulta = consulta.Where(r => r.Usuario == usuario);
            }

            if (!string.IsNullOrWhiteSpace(entidad))
            {
                consulta = consulta.Where(r => r.Entidad == entidad);
            }

            if (!string.IsNullOrWhiteSpace(accion))
            {
                consulta = consulta.Where(r => r.Accion == accion);
            }

            return await Paginacion.PaginarAsync(
                consulta.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.RegistroAuditoriaId),
                pagina,
                tamanoPagina,
                cancellationToken);
        }

        private static string? Recortar(string? texto, int maximo)
        {
            if (texto == null || texto.Length <= maximo)
            {
                return texto;
            }

            return texto[..maximo];
        }
    }
}
