using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class ClientesService
    {
        private readonly SistemaVentasDbContext _contexto;

        public ClientesService(SistemaVentasDbContext contexto)
        {
            _contexto = contexto;
        }

        /// <summary>Devuelve los clientes paginados.</summary>
        /// <param name="pagina">Número de página (default 1).</param>
        /// <param name="tamanoPagina">Elementos por página (default 10, máx. 100).</param>
        public async Task<ResultadoPaginadoDto<Cliente>> ObtenerTodosAsync(
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
            var consulta = _contexto.Clientes
                .AsNoTracking()
                .OrderBy(c => c.Nombre)
                .ThenBy(c => c.ClienteId);

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

            return new ResultadoPaginadoDto<Cliente>
            {
                PaginaActual = pagina,
                TamanoPagina = tamanoPagina,
                TotalElementos = totalElementos,
                TotalPaginas = totalPaginas,
                Elementos = elementos
            };
        }

        public async Task<Cliente?> ObtenerPorIdAsync(
            int id, 
            bool incluirVentas = false, 
            CancellationToken cancellationToken = default)
        {
            var consulta = _contexto.Clientes.AsNoTracking();

            if (incluirVentas)
            {
                consulta = consulta
                    .Include(c => c.Ventas)
                        .ThenInclude(v => v.Detalles)
                            .ThenInclude(d => d.Producto);
            }

            return await consulta.FirstOrDefaultAsync(c => c.ClienteId == id, cancellationToken);
        }

        public async Task<Cliente> CrearAsync(Cliente cliente, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cliente.Nombre))
            {
                throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(cliente));
            }

            if (!string.IsNullOrWhiteSpace(cliente.Email))
            {
                bool emailDuplicado = await _contexto.Clientes
                    .AnyAsync(c => c.Email != null && c.Email.ToLower() == cliente.Email.ToLower(), cancellationToken);

                if (emailDuplicado)
                {
                    throw new InvalidOperationException($"Ya existe un cliente con el correo electrónico '{cliente.Email}'.");
                }
            }

            cliente.Ventas = new List<Venta>();
            _contexto.Clientes.Add(cliente);
            await _contexto.SaveChangesAsync(cancellationToken);

            return cliente;
        }

        public async Task<Cliente> ActualizarAsync(
            int id, 
            Cliente clienteActualizado, 
            CancellationToken cancellationToken = default)
        {
            var clienteExistente = await _contexto.Clientes
                .FirstOrDefaultAsync(c => c.ClienteId == id, cancellationToken);

            if (clienteExistente == null)
            {
                throw new KeyNotFoundException($"No se encontró el cliente con ID {id}.");
            }

            if (string.IsNullOrWhiteSpace(clienteActualizado.Nombre))
            {
                throw new ArgumentException("El nombre del cliente no puede estar vacío.", nameof(clienteActualizado));
            }

            if (!string.IsNullOrWhiteSpace(clienteActualizado.Email))
            {
                bool emailDuplicado = await _contexto.Clientes
                    .AnyAsync(c => c.ClienteId != id && c.Email != null && c.Email.ToLower() == clienteActualizado.Email.ToLower(), cancellationToken);

                if (emailDuplicado)
                {
                    throw new InvalidOperationException($"Ya existe otro cliente con el correo electrónico '{clienteActualizado.Email}'.");
                }
            }

            clienteExistente.Nombre = clienteActualizado.Nombre;
            clienteExistente.Apellido = clienteActualizado.Apellido;
            clienteExistente.Telefono = clienteActualizado.Telefono;
            clienteExistente.Email = clienteActualizado.Email;
            clienteExistente.Direccion = clienteActualizado.Direccion;

            await _contexto.SaveChangesAsync(cancellationToken);

            return clienteExistente;
        }

        /// <summary>Devuelve el perfil de cliente de un usuario; si todavía no tiene uno, lo crea con los datos de su cuenta.</summary>
        public async Task<Cliente> ObtenerOCrearPorUsuarioAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            var cliente = await _contexto.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

            if (cliente != null)
            {
                return cliente;
            }

            var usuario = await _contexto.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId, cancellationToken);

            if (usuario == null)
            {
                throw new KeyNotFoundException($"No se encontró el usuario con ID {usuarioId}.");
            }

            cliente = new Cliente
            {
                Nombre = usuario.NombreUsuario,
                Email = usuario.Email,
                UsuarioId = usuarioId
            };

            _contexto.Clientes.Add(cliente);
            await _contexto.SaveChangesAsync(cancellationToken);

            return cliente;
        }

        public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
        {
            var cliente = await _contexto.Clientes
                .FirstOrDefaultAsync(c => c.ClienteId == id, cancellationToken);

            if (cliente == null)
            {
                throw new KeyNotFoundException($"No se encontró el cliente con ID {id}.");
            }

            bool tieneVentas = await _contexto.Ventas
                .AnyAsync(v => v.ClienteId == id, cancellationToken);

            if (tieneVentas)
            {
                throw new InvalidOperationException("No se puede eliminar el cliente porque posee ventas registradas.");
            }

            _contexto.Clientes.Remove(cliente);
            await _contexto.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
