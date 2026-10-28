using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class DetallesCompraService
    {
        private readonly SistemaVentasDbContext _contexto;

        public DetallesCompraService(SistemaVentasDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<ICollection<DetalleCompra>> ObtenerPorCompraIdAsync(
            int compraId, 
            CancellationToken cancellationToken = default)
        {
            return await _contexto.DetallesCompra
                .AsNoTracking()
                .Include(d => d.Producto)
                .Where(d => d.CompraId == compraId)
                .ToListAsync(cancellationToken);
        }

        public async Task<ICollection<DetalleCompra>> ObtenerPorProductoIdAsync(
            int productoId, 
            CancellationToken cancellationToken = default)
        {
            return await _contexto.DetallesCompra
                .AsNoTracking()
                .Include(d => d.Compra)
                    .ThenInclude(c => c.Proveedor)
                .Where(d => d.ProductoId == productoId)
                .ToListAsync(cancellationToken);
        }

        public async Task<DetalleCompra?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _contexto.DetallesCompra
                .AsNoTracking()
                .Include(d => d.Producto)
                .Include(d => d.Compra)
                .FirstOrDefaultAsync(d => d.DetalleCompraId == id, cancellationToken);
        }
    }
}
