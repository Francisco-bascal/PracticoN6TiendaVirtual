using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    public class Venta
    {
        [Key]
        public int VentaId { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        // Las ventas anuladas no se borran: se conservan para la auditoría y quedan fuera de los reportes.
        public bool Anulada { get; set; }
        public DateTime? FechaAnulacion { get; set; }



        [Required]
        [ForeignKey(nameof(Cliente))]
        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;

        // Usuario que registró la venta (el propio cliente al comprar desde la app, o el personal en una venta de mostrador).
        [ForeignKey(nameof(Usuario))]
        public int? UsuarioId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Usuario? Usuario { get; set; }

        [InverseProperty(nameof(DetalleVenta.Venta))]
        public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();

        [InverseProperty(nameof(Models.Pago.Venta))]
        public Pago? Pago { get; set; }
    }
}
