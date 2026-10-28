namespace PracticoN5ServicioApiRest.DTOs
{
    public class CreateCompraDTO
    {
        public DateTime Fecha { get; set; }
        public int ProveedorId { get; set; }
        public ICollection<CreateDetalleCompraDTO> Detalles { get; set; } = new List<CreateDetalleCompraDTO>();
    }
}