using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class ErrorLogService
    {
        private readonly SistemaVentasDbContext _contexto;
        private readonly ZonaHorariaNegocio _zonaHoraria;

        public ErrorLogService(SistemaVentasDbContext contexto, ZonaHorariaNegocio zonaHoraria)
        {
            _contexto = contexto;
            _zonaHoraria = zonaHoraria;
        }

        /// <summary>Guarda el error y devuelve su identificador, que se le informa al usuario para poder rastrearlo.</summary>
        public async Task<int> RegistrarAsync(RegistroError error, CancellationToken cancellationToken = default)
        {
            error.Fecha = DateTime.UtcNow;
            error.Mensaje = Recortar(error.Mensaje, 1000) ?? string.Empty;
            error.TipoExcepcion = Recortar(error.TipoExcepcion, 200);
            error.Ruta = Recortar(error.Ruta, 300);
            error.Usuario = Recortar(error.Usuario, 50);

            _contexto.RegistrosError.Add(error);
            await _contexto.SaveChangesAsync(cancellationToken);

            return error.RegistroErrorId;
        }

        public async Task<ResultadoPaginadoDto<RegistroError>> ObtenerAsync(
            DateTime? desde,
            DateTime? hasta,
            string? origen,
            int pagina = 1,
            int tamanoPagina = 20,
            CancellationToken cancellationToken = default)
        {
            Paginacion.Validar(pagina, tamanoPagina);

            var (desdeUtc, hastaUtc) = _zonaHoraria.RangoUtc(desde, hasta);
            var consulta = _contexto.RegistrosError.AsNoTracking().AsQueryable();

            if (desdeUtc.HasValue)
            {
                consulta = consulta.Where(r => r.Fecha >= desdeUtc.Value);
            }

            if (hastaUtc.HasValue)
            {
                consulta = consulta.Where(r => r.Fecha < hastaUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(origen))
            {
                consulta = consulta.Where(r => r.Origen == origen);
            }

            return await Paginacion.PaginarAsync(
                consulta.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.RegistroErrorId),
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
