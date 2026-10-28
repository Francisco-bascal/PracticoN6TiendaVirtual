namespace PracticoN5ServicioApiRest.DTOs
{
    public class CreateDetalleCompraDTO
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }
}