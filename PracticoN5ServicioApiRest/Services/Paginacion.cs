using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.DTOs;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>Validación y armado de resultados paginados, compartido por los servicios nuevos.</summary>
    public static class Paginacion
    {
        public const int TamanoMaximo = 100;

        public static void Validar(int pagina, int tamanoPagina)
        {
            if (pagina <= 0)
            {
                throw new ArgumentException("El número de página debe ser mayor o igual a 1.", nameof(pagina));
            }

            if (tamanoPagina <= 0)
            {
                throw new ArgumentException("El tamaño de página debe ser mayor a 0.", nameof(tamanoPagina));
            }

            if (tamanoPagina > TamanoMaximo)
            {
                throw new ArgumentException($"El tamaño de página no puede superar el límite máximo de {TamanoMaximo} elementos.", nameof(tamanoPagina));
            }
        }

        /// <summary>La consulta ya debe venir ordenada.</summary>
        public static async Task<ResultadoPaginadoDto<T>> PaginarAsync<T>(
            IQueryable<T> consulta,
            int pagina,
            int tamanoPagina,
            CancellationToken cancellationToken)
        {
            Validar(pagina, tamanoPagina);

            int total = await consulta.CountAsync(cancellationToken);

            var elementos = await consulta
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync(cancellationToken);

            return new ResultadoPaginadoDto<T>
            {
                PaginaActual = pagina,
                TamanoPagina = tamanoPagina,
                TotalElementos = total,
                TotalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina),
                Elementos = elementos
            };
        }
    }
}
