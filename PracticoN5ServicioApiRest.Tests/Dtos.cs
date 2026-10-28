using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    public record LoginDto(string Token, string NombreUsuario, string Rol, DateTime ExpiraUtc);
    public record ProductoDto(int ProductoId, string Nombre, string? Descripcion, decimal Precio, int Stock, int CategoriaId, string Categoria, bool Disponible);
    public record PaginaDto<T>(int PaginaActual, int TamanoPagina, int TotalElementos, int TotalPaginas, List<T> Elementos);
    public record PedidoItemDto(int ProductoId, string Producto, int Cantidad, decimal PrecioUnitario, decimal Subtotal);
    public record PedidoDto(int VentaId, DateTime Fecha, int ClienteId, string Cliente, bool Anulada, string? MedioPago, string? CodigoAutorizacion, decimal Total, List<PedidoItemDto> Items);
    public record MovimientoDto(int MovimientoStockId, DateTime Fecha, int ProductoId, string Producto, int Cantidad, string Motivo, string? Observacion, string? Usuario, int? CompraId, int? VentaId);
    public record CategoriaDto(int CategoriaId, string Nombre);
    public record ClienteDto(int ClienteId, string Nombre, string? Apellido, string? Email);
    public record ProveedorDto(int ProveedorId, string Nombre);

    /// <summary>Operaciones de la API que se repiten en muchos tests.</summary>
    public static class Api
    {
        /// <summary>Inicia sesión y deja el token en el cliente.</summary>
        public static async Task IniciarSesionAsync(this HttpClient http, string usuario, string password)
        {
            var respuesta = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = usuario, password });
            respuesta.EnsureSuccessStatusCode();

            var login = await respuesta.Content.ReadFromJsonAsync<LoginDto>();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.Token);
        }

        public static async Task<T> LeerAsync<T>(this HttpResponseMessage respuesta)
        {
            Assert.True(respuesta.IsSuccessStatusCode, $"Se esperaba éxito pero fue {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
            return (await respuesta.Content.ReadFromJsonAsync<T>())!;
        }

        public static async Task<int> CategoriaIdAsync(this HttpClient http, string nombre = "Electrónica")
        {
            var categorias = await (await http.GetAsync("/api/Categorias")).LeerAsync<List<CategoriaDto>>();
            return categorias.First(c => c.Nombre == nombre).CategoriaId;
        }

        public static async Task<int> ProveedorIdAsync(this HttpClient http)
        {
            var pagina = await (await http.GetAsync("/api/Proveedores")).LeerAsync<PaginaDto<ProveedorDto>>();
            return pagina.Elementos.First().ProveedorId;
        }

        public static async Task<int> ClienteIdDemoAsync(this HttpClient http)
        {
            var pagina = await (await http.GetAsync("/api/Clientes?tamanoPagina=100")).LeerAsync<PaginaDto<ClienteDto>>();
            return pagina.Elementos.First(c => c.Email == "cliente@tienda.local").ClienteId;
        }

        /// <summary>Crea un producto con un nombre único (para que cada test trabaje con el suyo) y devuelve su id.</summary>
        public static async Task<int> CrearProductoAsync(this HttpClient http, decimal precio = 1000m, int stock = 10, string? nombre = null)
        {
            var respuesta = await http.PostAsJsonAsync("/api/Productos", new
            {
                nombre = nombre ?? "Producto " + Guid.NewGuid().ToString("N")[..8],
                descripcion = "Producto de prueba",
                precio,
                stock,
                categoriaId = await http.CategoriaIdAsync()
            });

            return (await respuesta.LeerAsync<ProductoDto>()).ProductoId;
        }

        public static async Task<ProductoDto> ObtenerProductoAsync(this HttpClient http, int id)
        {
            return await (await http.GetAsync($"/api/Productos/{id}")).LeerAsync<ProductoDto>();
        }

        public static async Task<List<MovimientoDto>> MovimientosAsync(this HttpClient http, int productoId)
        {
            var pagina = await (await http.GetAsync($"/api/MovimientosStock?productoId={productoId}&tamanoPagina=100")).LeerAsync<PaginaDto<MovimientoDto>>();
            return pagina.Elementos;
        }

        public static object CheckoutItems(int productoId, int cantidad) => new { productoId, cantidad };

        /// <summary>Mensaje de texto de una respuesta de error (la API responde texto plano o JSON con "mensaje").</summary>
        public static async Task<string> MensajeAsync(this HttpResponseMessage respuesta)
        {
            string cuerpo = await respuesta.Content.ReadAsStringAsync();

            try
            {
                using var json = JsonDocument.Parse(cuerpo);
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("mensaje", out var mensaje))
                {
                    return mensaje.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                // No es JSON: es el texto plano.
            }

            return cuerpo;
        }
    }
}
