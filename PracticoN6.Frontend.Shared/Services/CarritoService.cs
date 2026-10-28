using System.Text.Json;
using PracticoN6.Frontend.Shared.Models;

namespace PracticoN6.Frontend.Shared.Services
{
    public class ItemCarrito
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public int Cantidad { get; set; }
        /// <summary>Stock que tenía el producto cuando se agregó o se vio por última vez: tope de la cantidad en el carrito.</summary>
        public int Stock { get; set; }
        public string? ImagenRuta { get; set; }

        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    /// <summary>
    /// Carrito de compras. Vive en el dispositivo y se guarda por usuario, así que sobrevive al cierre de la app.
    /// Los precios son solo informativos: al confirmar la compra la API vuelve a tomarlos del catálogo.
    /// </summary>
    public class CarritoService
    {
        private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

        private readonly IPreferencias _preferencias;
        private readonly SesionService _sesion;
        private readonly List<ItemCarrito> _items = new();
        private string? _usuario;

        public event Action? Cambio;

        public CarritoService(IPreferencias preferencias, SesionService sesion)
        {
            _preferencias = preferencias;
            _sesion = sesion;

            // El carrito pertenece al usuario: al cambiar la sesión se carga el del nuevo usuario (o se vacía la memoria).
            _sesion.SesionCambio += () => _ = CargarAsync();
        }

        public IReadOnlyList<ItemCarrito> Items => _items;
        public int Unidades => _items.Sum(i => i.Cantidad);
        public decimal Total => _items.Sum(i => i.Subtotal);
        public bool EstaVacio => _items.Count == 0;

        /// <summary>
        /// Carga el carrito guardado del usuario actual. Puede invocarse varias veces a la vez (al iniciar sesión se dispara
        /// desde dos lugares): por eso lee primero y recién después reemplaza el contenido, sin ningún await en el medio.
        /// </summary>
        public async Task CargarAsync()
        {
            string? usuario = _sesion.NombreUsuario;
            var guardados = new List<ItemCarrito>();

            if (usuario != null)
            {
                try
                {
                    string? json = await _preferencias.LeerAsync(Clave(usuario));

                    if (!string.IsNullOrEmpty(json))
                    {
                        guardados = JsonSerializer.Deserialize<List<ItemCarrito>>(json, OpcionesJson) ?? new List<ItemCarrito>();
                    }
                }
                catch (Exception)
                {
                    // Un carrito guardado ilegible se descarta: empieza vacío.
                }
            }

            _usuario = usuario;
            _items.Clear();
            _items.AddRange(guardados);

            Cambio?.Invoke();
        }

        /// <summary>Agrega unidades sin superar el stock. Devuelve cuántas se agregaron realmente (0 si ya está el máximo).</summary>
        public async Task<int> AgregarAsync(Producto producto, int cantidad = 1)
        {
            var item = _items.FirstOrDefault(i => i.ProductoId == producto.ProductoId);

            if (item == null)
            {
                item = new ItemCarrito { ProductoId = producto.ProductoId };
                _items.Add(item);
            }

            // Se actualizan los datos vigentes del producto.
            item.Nombre = producto.Nombre;
            item.PrecioUnitario = producto.Precio;
            item.Stock = producto.Stock;
            item.ImagenRuta = producto.ImagenRuta;

            int antes = item.Cantidad;
            item.Cantidad = Math.Min(item.Cantidad + Math.Max(cantidad, 0), Math.Max(producto.Stock, 0));

            if (item.Cantidad == 0)
            {
                _items.Remove(item);
            }

            await GuardarAsync();
            return item.Cantidad - antes;
        }

        public async Task CambiarCantidadAsync(int productoId, int cantidad)
        {
            var item = _items.FirstOrDefault(i => i.ProductoId == productoId);

            if (item == null)
            {
                return;
            }

            if (cantidad <= 0)
            {
                _items.Remove(item);
            }
            else
            {
                item.Cantidad = Math.Min(cantidad, Math.Max(item.Stock, 1));
            }

            await GuardarAsync();
        }

        public async Task QuitarAsync(int productoId)
        {
            _items.RemoveAll(i => i.ProductoId == productoId);
            await GuardarAsync();
        }

        public async Task VaciarAsync()
        {
            _items.Clear();
            await GuardarAsync();
        }

        public int CantidadDe(int productoId) => _items.FirstOrDefault(i => i.ProductoId == productoId)?.Cantidad ?? 0;

        private async Task GuardarAsync()
        {
            if (_usuario != null)
            {
                try
                {
                    await _preferencias.GuardarAsync(Clave(_usuario), JsonSerializer.Serialize(_items, OpcionesJson));
                }
                catch (Exception)
                {
                    // Si no se puede persistir, el carrito sigue funcionando en memoria.
                }
            }

            Cambio?.Invoke();
        }

        private static string Clave(string usuario) => $"carrito.{usuario.ToLowerInvariant()}";
    }
}
