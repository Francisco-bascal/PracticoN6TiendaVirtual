namespace PracticoN5ServicioApiRest.DTOs
{
    public class ResponseProductoDTO
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public string? ImagenRuta { get; set; }
        public int CategoriaId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        // Atajo para el catálogo: hay unidades para vender.
        public bool Disponible => Stock > 0;
    }
}
