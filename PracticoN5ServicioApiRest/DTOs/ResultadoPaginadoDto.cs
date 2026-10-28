namespace PracticoN5ServicioApiRest.DTOs
{
    public class ResultadoPaginadoDto<T>
    {
        public int PaginaActual { get; set; }
        public int TamanoPagina { get; set; }
        public int TotalElementos { get; set; }
        public int TotalPaginas { get; set; }
        public ICollection<T> Elementos { get; set; } = new List<T>();
    }
}
