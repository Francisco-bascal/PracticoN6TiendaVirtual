using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    public class Compra
    {
        [Key]
        public int CompraId { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        // Las compras anuladas no se borran: se conservan para la auditoría y quedan fuera de los reportes.
        public bool Anulada { get; set; }
        public DateTime? FechaAnulacion { get; set; }
        
        
        [Required]
        [ForeignKey(nameof(Proveedor))]
        public int ProveedorId { get; set; }
        public Proveedor Proveedor { get; set; } = null!;

        // Usuario que registró la entrada de mercadería.
        [ForeignKey(nameof(Usuario))]
        public int? UsuarioId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Usuario? Usuario { get; set; }


        [InverseProperty(nameof(DetalleCompra.Compra))]
        public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
    }
}
