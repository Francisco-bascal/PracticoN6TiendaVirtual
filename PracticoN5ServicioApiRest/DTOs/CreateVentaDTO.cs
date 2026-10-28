namespace PracticoN5ServicioApiRest.DTOs
{
    public class CreateVentaDTO
    {
        public DateTime Fecha { get; set; }
        public int ClienteId { get; set; }
        public ICollection<CreateDetalleVentaDTO> Detalles { get; set; } = new List<CreateDetalleVentaDTO>();
    }
}