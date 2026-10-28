namespace PracticoN5ServicioApiRest.DTOs
{
    public class MovimientoStockDTO
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
}
