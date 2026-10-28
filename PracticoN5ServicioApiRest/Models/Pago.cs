using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PracticoN5ServicioApiRest.Models
{
    /// <summary>Pago simulado (aprobado) asociado a una venta. No se guarda ningún dato de tarjeta.</summary>
    public class Pago
    {
        [Key]
        public int PagoId { get; set; }
        [Required]
        [Precision(18,2)]
        [Range(0.01, double.MaxValue)]
        public decimal Monto { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        [Required]
        [MaxLength(40)]
        public string CodigoAutorizacion { get; set; } = string.Empty;


        [ForeignKey(nameof(Venta))]
        public int VentaId { get; set; }
        [JsonIgnore]
        public Venta Venta { get; set; } = null!;


        [ForeignKey(nameof(MedioPago))]
        public int MedioPagoId { get; set; }
        public MedioPago MedioPago { get; set; } = null!;
    }
}
