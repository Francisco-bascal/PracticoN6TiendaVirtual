using PracticoN6.Frontend.Shared.Models;

namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>Operaciones de la API REST de la tienda, agrupadas por recurso.</summary>
    public class TiendaApi
    {
        private readonly ApiClient _api;

        public TiendaApi(ApiClient api)
        {
            _api = api;
        }

        // ---- Cuenta ----

        public Task<LoginRespuesta> LoginAsync(string nombreUsuario, string password)
            => _api.PostAsync<LoginRespuesta>("api/Usuarios/login", new { nombreUsuario, password });

        public Task RegistrarseAsync(FormularioRegistro f)
            => _api.PostAsync("api/Usuarios/registro", new
            {
                f.NombreUsuario,
                f.Email,
                f.Password,
                f.Nombre,
                f.Apellido,
                f.Telefono,
                f.Direccion
            });

        public Task<Perfil> ObtenerPerfilAsync() => _api.GetAsync<Perfil>("api/Usuarios/yo");

        public Task<Perfil> ActualizarPerfilAsync(FormularioPerfil f) => _api.PutAsync<Perfil>("api/Usuarios/yo", f);

        // El endpoint de estado responde texto plano ("API funcionando"), no JSON.
        public async Task ProbarConexionAsync() => await _api.GetTextoAsync("api/Usuarios/test");

        // ---- Catálogo ----

        public Task<Pagina<Producto>> BuscarProductosAsync(
            string? busqueda,
            int? categoriaId,
            bool soloDisponibles,
            string orden,
            int pagina,
            int tamanoPagina = 12)
        {
            var parametros = new List<string>
            {
                $"pagina={pagina}",
                $"tamanoPagina={tamanoPagina}",
                $"orden={Uri.EscapeDataString(orden)}"
            };

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                parametros.Add($"busqueda={Uri.EscapeDataString(busqueda.Trim())}");
            }

            if (categoriaId.HasValue)
            {
                parametros.Add($"categoriaId={categoriaId.Value}");
            }

            if (soloDisponibles)
            {
                parametros.Add("soloDisponibles=true");
            }

            return _api.GetAsync<Pagina<Producto>>("api/Productos?" + string.Join("&", parametros));
        }

        public Task<Producto> ObtenerProductoAsync(int id) => _api.GetAsync<Producto>($"api/Productos/{id}");

        public Task<List<Categoria>> ObtenerCategoriasAsync() => _api.GetAsync<List<Categoria>>("api/Categorias");

        /// <summary>URL de la imagen del producto, o null si no tiene. Lleva el nombre del archivo para que el caché se renueve al cambiarla.</summary>
        public async Task<string?> UrlImagenAsync(Producto producto)
        {
            if (string.IsNullOrWhiteSpace(producto.ImagenRuta))
            {
                return null;
            }

            return await _api.UrlAbsolutaAsync($"api/Productos/{producto.ProductoId}/imagen?v={Uri.EscapeDataString(Path.GetFileName(producto.ImagenRuta))}");
        }

        // ---- Administración de productos y categorías ----

        public Task<Producto> CrearProductoAsync(FormularioProducto f) => _api.PostAsync<Producto>("api/Productos", f);

        /// <summary>Edita los datos del producto sin tocar su stock (el stock se ajusta aparte, con su movimiento).</summary>
        public async Task<Producto> ActualizarProductoAsync(int id, FormularioProducto f)
        {
            // La API reemplaza el stock con el valor enviado: se envía el vigente para no pisar ventas recientes.
            var actual = await ObtenerProductoAsync(id);

            return await _api.PutAsync<Producto>($"api/Productos/{id}", new
            {
                f.Nombre,
                f.Descripcion,
                f.Precio,
                actual.Stock,
                f.CategoriaId,
                actual.ImagenRuta
            });
        }

        public Task AjustarStockAsync(int id, int cambio, string? observacion)
        {
            string consulta = string.IsNullOrWhiteSpace(observacion) ? string.Empty : $"?observacion={Uri.EscapeDataString(observacion.Trim())}";
            return _api.PatchAsync($"api/Productos/{id}/stock{consulta}", cambio);
        }

        public Task<Producto> SubirImagenAsync(int id, Stream contenido, string nombreArchivo, string tipoMime)
            => _api.PostArchivoAsync<Producto>($"api/Productos/{id}/imagen", "archivo", nombreArchivo, contenido, tipoMime);

        public Task EliminarProductoAsync(int id) => _api.DeleteAsync($"api/Productos/{id}");

        public Task CrearCategoriaAsync(FormularioCategoria f) => _api.PostAsync("api/Categorias", f);

        public Task ActualizarCategoriaAsync(int id, FormularioCategoria f)
            => _api.PutAsync<Categoria>($"api/Categorias/{id}", new { categoriaId = id, f.Nombre, f.Descripcion });

        public Task EliminarCategoriaAsync(int id) => _api.DeleteAsync($"api/Categorias/{id}");

        // ---- Proveedores y clientes ----

        public Task<Pagina<Proveedor>> ObtenerProveedoresAsync(int pagina = 1, int tamanoPagina = 100)
            => _api.GetAsync<Pagina<Proveedor>>($"api/Proveedores?pagina={pagina}&tamanoPagina={tamanoPagina}");

        public Task CrearProveedorAsync(FormularioProveedor f) => _api.PostAsync("api/Proveedores", f);

        public Task ActualizarProveedorAsync(int id, FormularioProveedor f)
            => _api.PutAsync<Proveedor>($"api/Proveedores/{id}", new { proveedorId = id, f.Nombre, f.Telefono, f.Email, f.Direccion });

        public Task EliminarProveedorAsync(int id) => _api.DeleteAsync($"api/Proveedores/{id}");

        public Task<Pagina<Cliente>> ObtenerClientesAsync(int pagina = 1, int tamanoPagina = 100)
            => _api.GetAsync<Pagina<Cliente>>($"api/Clientes?pagina={pagina}&tamanoPagina={tamanoPagina}");

        // ---- Compras (entradas de mercadería) ----

        public Task<Pagina<Compra>> ObtenerComprasAsync(int pagina, int tamanoPagina = 10)
            => _api.GetAsync<Pagina<Compra>>($"api/Compras?pagina={pagina}&tamanoPagina={tamanoPagina}");

        public Task RegistrarCompraAsync(int proveedorId, IEnumerable<LineaCompra> lineas)
            => _api.PostAsync("api/Compras", new
            {
                proveedorId,
                detalles = lineas.Select(l => new { l.ProductoId, l.Cantidad, l.PrecioUnitario })
            });

        public Task AnularCompraAsync(int id) => _api.PostAsync($"api/Compras/{id}/anular");

        // ---- Ventas, carrito y pagos ----

        public Task<List<MedioPago>> ObtenerMediosPagoAsync() => _api.GetAsync<List<MedioPago>>("api/MediosPago");

        public Task<Pedido> ConfirmarCompraAsync(SolicitudCheckout solicitud) => _api.PostAsync<Pedido>("api/Ventas/checkout", solicitud);

        public Task<List<Pedido>> ObtenerMisPedidosAsync() => _api.GetAsync<List<Pedido>>("api/Ventas/mis-pedidos");

        public async Task<Pedido> ObtenerPedidoAsync(int ventaId)
            => (await _api.GetAsync<Venta>($"api/Ventas/{ventaId}")).APedido();

        public Task<Pagina<Venta>> ObtenerVentasAsync(int pagina, int tamanoPagina = 10)
            => _api.GetAsync<Pagina<Venta>>($"api/Ventas?pagina={pagina}&tamanoPagina={tamanoPagina}");

        public Task AnularVentaAsync(int id) => _api.PostAsync($"api/Ventas/{id}/anular");

        public Task<byte[]> ObtenerComprobanteAsync(int ventaId) => _api.GetBytesAsync($"api/Ventas/{ventaId}/comprobante");

        public Task<ResultadoEnvio> EnviarComprobanteAsync(int ventaId, string? destinatario)
            => _api.PostAsync<ResultadoEnvio>($"api/Ventas/{ventaId}/comprobante/enviar", new { destinatario });

        // ---- Stock y reportes ----

        public Task<Pagina<Movimiento>> ObtenerMovimientosAsync(DateTime? desde, DateTime? hasta, string? motivo, int pagina, int tamanoPagina = 20)
        {
            var parametros = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamanoPagina}" };

            if (desde.HasValue) parametros.Add($"desde={Formato.ParaApi(desde.Value)}");
            if (hasta.HasValue) parametros.Add($"hasta={Formato.ParaApi(hasta.Value)}");
            if (!string.IsNullOrWhiteSpace(motivo)) parametros.Add($"motivo={Uri.EscapeDataString(motivo)}");

            return _api.GetAsync<Pagina<Movimiento>>("api/MovimientosStock?" + string.Join("&", parametros));
        }

        public Task<byte[]> ObtenerReporteAsync(string tipo, DateTime? desde, DateTime? hasta)
            => _api.GetBytesAsync($"api/Reportes/{tipo}{ConsultaPeriodo(desde, hasta)}");

        public Task<ResultadoEnvio> EnviarReporteAsync(string tipo, string destinatario, DateTime? desde, DateTime? hasta)
            => _api.PostAsync<ResultadoEnvio>($"api/Reportes/{tipo}/enviar", new { destinatario, desde, hasta });

        private static string ConsultaPeriodo(DateTime? desde, DateTime? hasta)
        {
            var parametros = new List<string>();

            if (desde.HasValue) parametros.Add($"desde={Formato.ParaApi(desde.Value)}");
            if (hasta.HasValue) parametros.Add($"hasta={Formato.ParaApi(hasta.Value)}");

            return parametros.Count == 0 ? string.Empty : "?" + string.Join("&", parametros);
        }

        // ---- Usuarios, roles y registros ----

        public Task<List<UsuarioAdmin>> ObtenerUsuariosAsync() => _api.GetAsync<List<UsuarioAdmin>>("api/Usuarios");

        public Task<List<RolInfo>> ObtenerRolesAsync() => _api.GetAsync<List<RolInfo>>("api/Roles");

        public Task CrearUsuarioAsync(FormularioUsuario f) => _api.PostAsync("api/Usuarios", f);

        public Task ActualizarUsuarioAsync(UsuarioAdmin u, string rol, bool activo)
            => _api.PutAsync<UsuarioAdmin>($"api/Usuarios/{u.UsuarioId}", new { u.NombreUsuario, u.Email, rol, activo });

        public Task EliminarUsuarioAsync(int id) => _api.DeleteAsync($"api/Usuarios/{id}");

        public Task<Pagina<RegistroAuditoria>> ObtenerAuditoriaAsync(
            DateTime? desde, DateTime? hasta, string? usuario, string? entidad, int pagina, int tamanoPagina = 20)
        {
            var parametros = new List<string> { $"pagina={pagina}", $"tamanoPagina={tamanoPagina}" };

            if (desde.HasValue) parametros.Add($"desde={Formato.ParaApi(desde.Value)}");
            if (hasta.HasValue) parametros.Add($"hasta={Formato.ParaApi(hasta.Value)}");
            if (!string.IsNullOrWhiteSpace(usuario)) parametros.Add($"usuario={Uri.EscapeDataString(usuario.Trim())}");
            if (!string.IsNullOrWhiteSpace(entidad)) parametros.Add($"entidad={Uri.EscapeDataString(entidad.Trim())}");

            return _api.GetAsync<Pagina<RegistroAuditoria>>("api/Logs/auditoria?" + string.Join("&", parametros));
        }

        public Task<Pagina<RegistroError>> ObtenerErroresAsync(string? origen, int pagina, int tamanoPagina = 20)
        {
            string filtro = string.IsNullOrWhiteSpace(origen) ? string.Empty : $"&origen={Uri.EscapeDataString(origen)}";
            return _api.GetAsync<Pagina<RegistroError>>($"api/Logs/errores?pagina={pagina}&tamanoPagina={tamanoPagina}{filtro}");
        }
    }
}
