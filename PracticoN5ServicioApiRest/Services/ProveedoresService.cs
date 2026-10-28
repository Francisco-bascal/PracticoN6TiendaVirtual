using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class ProveedoresService
    {
        private readonly SistemaVentasDbContext _contexto;

        public ProveedoresService(SistemaVentasDbContext contexto)
        {
            _contexto = contexto;
        }

        /// <summary>Devuelve los proveedores paginados.</summary>
        /// <param name="pagina">Número de página (default 1).</param>
        /// <param name="tamanoPagina">Elementos por página (default 10, máx. 100).</param>
        public async Task<ResultadoPaginadoDto<Proveedor>> ObtenerTodosAsync(
            int pagina = 1, 
            int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            // Validación de los parámetros de paginación.
            if (pagina <= 0)
            {
                throw new ArgumentException("El número de página debe ser mayor o igual a 1.", nameof(pagina));
            }

            if (tamanoPagina <= 0)
            {
                throw new ArgumentException("El tamaño de página debe ser mayor a 0.", nameof(tamanoPagina));
            }

            if (tamanoPagina > 100)
            {
                throw new ArgumentException("El tamaño de página no puede superar el límite máximo de 100 elementos.", nameof(tamanoPagina));
            }

            // AsNoTracking: consulta de solo lectura, sin seguimiento de cambios.
            var consulta = _contexto.Proveedores
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .ThenBy(p => p.ProveedorId);

            // Total de elementos (para calcular el total de páginas).
            int totalElementos = await consulta.CountAsync(cancellationToken);

            // Total de páginas, redondeando hacia arriba.
            int totalPaginas = (int)Math.Ceiling(totalElementos / (double)tamanoPagina);

            // Skip: salta las páginas anteriores. Take: toma la página actual.
            // ToListAsync: materializa la consulta.
            var elementos = await consulta
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync(cancellationToken);

            return new ResultadoPaginadoDto<Proveedor>
            {
                PaginaActual = pagina,
                TamanoPagina = tamanoPagina,
                TotalElementos = totalElementos,
                TotalPaginas = totalPaginas,
                Elementos = elementos
            };
        }

        public async Task<Proveedor?> ObtenerPorIdAsync(
            int id, 
            bool incluirCompras = false, 
            CancellationToken cancellationToken = default)
        {
            var consulta = _contexto.Proveedores.AsNoTracking();

            if (incluirCompras)
            {
                consulta = consulta
                    .Include(p => p.Compras)
                        .ThenInclude(c => c.Detalles)
                            .ThenInclude(d => d.Producto);
            }

            return await consulta.FirstOrDefaultAsync(p => p.ProveedorId == id, cancellationToken);
        }

        public async Task<Proveedor> CrearAsync(Proveedor proveedor, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(proveedor.Nombre))
            {
                throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(proveedor));
            }

            if (!string.IsNullOrWhiteSpace(proveedor.Email))
            {
                bool emailDuplicado = await _contexto.Proveedores
                    .AnyAsync(p => p.Email != null && p.Email.ToLower() == proveedor.Email.ToLower(), cancellationToken);

                if (emailDuplicado)
                {
                    throw new InvalidOperationException($"Ya existe un proveedor con el correo electrónico '{proveedor.Email}'.");
                }
            }

            proveedor.Compras = new List<Compra>();
            _contexto.Proveedores.Add(proveedor);
            await _contexto.SaveChangesAsync(cancellationToken);

            return proveedor;
        }

        public async Task<Proveedor> ActualizarAsync(
            int id, 
            Proveedor proveedorActualizado, 
            CancellationToken cancellationToken = default)
        {
            var proveedorExistente = await _contexto.Proveedores
                .FirstOrDefaultAsync(p => p.ProveedorId == id, cancellationToken);

            if (proveedorExistente == null)
            {
                throw new KeyNotFoundException($"No se encontró el proveedor con ID {id}.");
            }

            if (string.IsNullOrWhiteSpace(proveedorActualizado.Nombre))
            {
                throw new ArgumentException("El nombre del proveedor no puede estar vacío.", nameof(proveedorActualizado));
            }

            if (!string.IsNullOrWhiteSpace(proveedorActualizado.Email))
            {
                bool emailDuplicado = await _contexto.Proveedores
                    .AnyAsync(p => p.ProveedorId != id && p.Email != null && p.Email.ToLower() == proveedorActualizado.Email.ToLower(), cancellationToken);

                if (emailDuplicado)
                {
                    throw new InvalidOperationException($"Ya existe otro proveedor con el correo electrónico '{proveedorActualizado.Email}'.");
                }
            }

            proveedorExistente.Nombre = proveedorActualizado.Nombre;
            proveedorExistente.Telefono = proveedorActualizado.Telefono;
            proveedorExistente.Email = proveedorActualizado.Email;
            proveedorExistente.Direccion = proveedorActualizado.Direccion;

            await _contexto.SaveChangesAsync(cancellationToken);

            return proveedorExistente;
        }

        public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
        {
            var proveedor = await _contexto.Proveedores
                .FirstOrDefaultAsync(p => p.ProveedorId == id, cancellationToken);

            if (proveedor == null)
            {
                throw new KeyNotFoundException($"No se encontró el proveedor con ID {id}.");
            }

            bool tieneCompras = await _contexto.Compras
                .AnyAsync(c => c.ProveedorId == id, cancellationToken);

            if (tieneCompras)
            {
                throw new InvalidOperationException("No se puede eliminar el proveedor porque posee compras registradas.");
            }

            _contexto.Proveedores.Remove(proveedor);
            await _contexto.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
