using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PracticoN5ServicioApiRest.Models
{
    public class MedioPago
    {
        [Key]
        public int MedioPagoId { get; set; }
        [Required]
        [Length(3, 50)]
        public string Nombre { get; set; } = string.Empty;
        // Los medios con tarjeta piden número de tarjeta en la simulación del pago.
        public bool RequiereTarjeta { get; set; }


        [JsonIgnore]
        [InverseProperty(nameof(Pago.MedioPago))]
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }
}
