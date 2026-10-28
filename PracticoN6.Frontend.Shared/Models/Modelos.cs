using System.ComponentModel.DataAnnotations;

namespace PracticoN6.Frontend.Shared.Models
{
    // Modelos que la app intercambia con la API REST. Los nombres de propiedad coinciden con el JSON
    // (la deserialización no distingue mayúsculas de minúsculas).

    public class LoginRespuesta
    {
        public string Token { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public DateTime ExpiraUtc { get; set; }
    }

    public class Perfil
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public int? ClienteId { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
    }

    public class Pagina<T>
    {
        public int PaginaActual { get; set; }
        public int TamanoPagina { get; set; }
        public int TotalElementos { get; set; }
        public int TotalPaginas { get; set; }
        public List<T> Elementos { get; set; } = new();
    }

    public class Categoria
    {
        public int CategoriaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class Producto
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public string? ImagenRuta { get; set; }
        public int CategoriaId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public bool Disponible { get; set; }
    }

    public class MedioPago
    {
        public int MedioPagoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool RequiereTarjeta { get; set; }
    }

    public class ItemPedido
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    /// <summary>Una compra: sus ítems, el medio de pago y el código de autorización del pago simulado.</summary>
    public class Pedido
    {
        public int VentaId { get; set; }
        public DateTime Fecha { get; set; }
        public int ClienteId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public bool Anulada { get; set; }
        public string? MedioPago { get; set; }
        public string? CodigoAutorizacion { get; set; }
        public List<ItemPedido> Items { get; set; } = new();
        public decimal Total => Items.Sum(i => i.Subtotal);
    }

    public class ItemCheckout
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }

    public class SolicitudCheckout
    {
        public List<ItemCheckout> Items { get; set; } = new();
        public int MedioPagoId { get; set; }
        public string? NumeroTarjeta { get; set; }
        public int? ClienteId { get; set; }
    }

    public class Cliente
    {
        public int ClienteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Apellido { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Direccion { get; set; }

        public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
    }

    public class Proveedor
    {
        public int ProveedorId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Direccion { get; set; }
    }

    // Venta y Compra tal como las devuelve la API (entidad con sus relaciones); se usan en los listados del personal.
    public class NombreRef
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Apellido { get; set; }
        public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
    }

    public class DetalleDocumento
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public NombreRef? Producto { get; set; }
    }

    public class PagoRef
    {
        public string CodigoAutorizacion { get; set; } = string.Empty;
        public NombreRef? MedioPago { get; set; }
    }

    public class Venta
    {
        public int VentaId { get; set; }
        public DateTime Fecha { get; set; }
        public bool Anulada { get; set; }
        public int ClienteId { get; set; }
        public NombreRef? Cliente { get; set; }
        public PagoRef? Pago { get; set; }
        public List<DetalleDocumento> Detalles { get; set; } = new();
        public decimal Total => Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);

        public Pedido APedido() => new()
        {
            VentaId = VentaId,
            Fecha = Fecha,
            ClienteId = ClienteId,
            Cliente = Cliente?.NombreCompleto ?? string.Empty,
            Anulada = Anulada,
            MedioPago = Pago?.MedioPago?.Nombre,
            CodigoAutorizacion = Pago?.CodigoAutorizacion,
            Items = Detalles.Select(d => new ItemPedido
            {
                ProductoId = d.ProductoId,
                Producto = d.Producto?.Nombre ?? string.Empty,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario
            }).ToList()
        };
    }

    public class Compra
    {
        public int CompraId { get; set; }
        public DateTime Fecha { get; set; }
        public bool Anulada { get; set; }
        public NombreRef? Proveedor { get; set; }
        public List<DetalleDocumento> Detalles { get; set; } = new();
        public decimal Total => Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
    }

    public class LineaCompra
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; } = 1;
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    public class Movimiento
    {
        public int MovimientoStockId { get; set; }
        public DateTime Fecha { get; set; }
        public int ProductoId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? Observacion { get; set; }
        public string? Usuario { get; set; }
        public int? CompraId { get; set; }
        public int? VentaId { get; set; }
    }

    public class UsuarioAdmin
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public class RolInfo
    {
        public int RolId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class RegistroAuditoria
    {
        public int RegistroAuditoriaId { get; set; }
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Entidad { get; set; } = string.Empty;
        public string? EntidadId { get; set; }
        public string? Detalle { get; set; }
        public string? DireccionIp { get; set; }
        public int CodigoRespuesta { get; set; }
    }

    public class RegistroError
    {
        public int RegistroErrorId { get; set; }
        public DateTime Fecha { get; set; }
        public string Origen { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string? TipoExcepcion { get; set; }
        public string? StackTrace { get; set; }
        public string? Ruta { get; set; }
        public string? Metodo { get; set; }
        public string? Usuario { get; set; }
    }

    public class ResultadoEnvio
    {
        public bool Enviado { get; set; }
        public string Modo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    // ---- Formularios (con las mismas reglas de validación que la API) ----

    public class FormularioLogin
    {
        [Required(ErrorMessage = "Ingrese su usuario.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese su contraseña.")]
        public string Password { get; set; } = string.Empty;
    }

    public class FormularioRegistro
    {
        [Required(ErrorMessage = "Elija un nombre de usuario.")]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "El usuario debe tener entre 4 y 50 caracteres.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese su correo electrónico.")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Elija una contraseña.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repita la contraseña.")]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarPassword { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        public string? Nombre { get; set; }
        [StringLength(100)]
        public string? Apellido { get; set; }
        [StringLength(20)]
        public string? Telefono { get; set; }
        [StringLength(200)]
        public string? Direccion { get; set; }
    }

    public class FormularioPerfil
    {
        [Required(ErrorMessage = "Ingrese su nombre.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;
        [StringLength(100)]
        public string? Apellido { get; set; }
        [StringLength(20)]
        public string? Telefono { get; set; }
        [StringLength(200)]
        public string? Direccion { get; set; }
    }

    public class FormularioProducto
    {
        [Required(ErrorMessage = "Ingrese el nombre del producto.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "La descripción admite hasta 500 caracteres.")]
        public string? Descripcion { get; set; }

        [Range(0.01, 100000000, ErrorMessage = "El precio debe ser mayor a cero.")]
        public decimal Precio { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
        public int Stock { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Elija una categoría.")]
        public int CategoriaId { get; set; }
    }

    public class FormularioCategoria
    {
        [Required(ErrorMessage = "Ingrese el nombre de la categoría.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 50 caracteres.")]
        public string Nombre { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Descripcion { get; set; }
    }

    public class FormularioProveedor
    {
        [Required(ErrorMessage = "Ingrese el nombre del proveedor.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;
        [StringLength(20, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre 7 y 20 caracteres.")]
        public string? Telefono { get; set; }
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
        public string? Email { get; set; }
        [StringLength(200, MinimumLength = 3, ErrorMessage = "La dirección debe tener entre 3 y 200 caracteres.")]
        public string? Direccion { get; set; }
    }

    public class FormularioUsuario
    {
        [Required(ErrorMessage = "Ingrese el nombre de usuario.")]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "El usuario debe tener entre 4 y 50 caracteres.")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese el correo electrónico.")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese una contraseña.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Elija un rol.")]
        public string Rol { get; set; } = string.Empty;
    }
}
