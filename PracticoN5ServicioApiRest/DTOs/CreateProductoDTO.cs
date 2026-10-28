namespace PracticoN5ServicioApiRest.DTOs
{
    public class CreateProductoDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public string? ImagenRuta { get; set; }
        public int CategoriaId { get; set; }
    }
}