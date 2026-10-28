using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class CategoriasService
    {
        private readonly SistemaVentasDbContext _contexto;

        public CategoriasService(SistemaVentasDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<ICollection<CategoriaProducto>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
        {
            return await _contexto.Categorias
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<CategoriaProducto?> ObtenerPorIdAsync(
            int id, 
            bool incluirProductos = false, 
            CancellationToken cancellationToken = default)
        {
            var consulta = _contexto.Categorias.AsNoTracking();

            if (incluirProductos)
            {
                consulta = consulta.Include(c => c.Productos);
            }

            return await consulta.FirstOrDefaultAsync(c => c.CategoriaId == id, cancellationToken);
        }

        public async Task<CategoriaProducto> CrearAsync(
            CategoriaProducto categoria, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(categoria.Nombre))
            {
                throw new ArgumentException("El nombre de la categoría es obligatorio.", nameof(categoria));
            }

            bool nombreDuplicado = await _contexto.Categorias
                .AnyAsync(c => c.Nombre.ToLower() == categoria.Nombre.ToLower(), cancellationToken);

            if (nombreDuplicado)
            {
                throw new InvalidOperationException($"Ya existe una categoría con el nombre '{categoria.Nombre}'.");
            }

            categoria.Productos = new List<Producto>();
            _contexto.Categorias.Add(categoria);
            await _contexto.SaveChangesAsync(cancellationToken);

            return categoria;
        }

        public async Task<CategoriaProducto> ActualizarAsync(
            int id, 
            CategoriaProducto categoriaActualizada, 
            CancellationToken cancellationToken = default)
        {
            var categoriaExistente = await _contexto.Categorias
                .FirstOrDefaultAsync(c => c.CategoriaId == id, cancellationToken);

            if (categoriaExistente == null)
            {
                throw new KeyNotFoundException($"No se encontró la categoría con ID {id}.");
            }

            if (string.IsNullOrWhiteSpace(categoriaActualizada.Nombre))
            {
                throw new ArgumentException("El nombre de la categoría no puede estar vacío.", nameof(categoriaActualizada));
            }

            bool nombreDuplicado = await _contexto.Categorias
                .AnyAsync(c => c.CategoriaId != id && c.Nombre.ToLower() == categoriaActualizada.Nombre.ToLower(), cancellationToken);

            if (nombreDuplicado)
            {
                throw new InvalidOperationException($"Ya existe otra categoría con el nombre '{categoriaActualizada.Nombre}'.");
            }

            categoriaExistente.Nombre = categoriaActualizada.Nombre;
            categoriaExistente.Descripcion = categoriaActualizada.Descripcion;

            await _contexto.SaveChangesAsync(cancellationToken);

            return categoriaExistente;
        }

        public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
        {
            var categoria = await _contexto.Categorias
                .FirstOrDefaultAsync(c => c.CategoriaId == id, cancellationToken);

            if (categoria == null)
            {
                throw new KeyNotFoundException($"No se encontró la categoría con ID {id}.");
            }

            bool tieneProductos = await _contexto.Productos
                .AnyAsync(p => p.CategoriaId == id, cancellationToken);

            if (tieneProductos)
            {
                throw new InvalidOperationException("No se puede eliminar la categoría porque contiene productos asociados.");
            }

            _contexto.Categorias.Remove(categoria);
            await _contexto.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
