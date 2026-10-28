using System.Net;
using System.Net.Http.Headers;

namespace PracticoN5ServicioApiRest.Tests
{
    public class CatalogoTests : IClassFixture<TiendaFactory>
    {
        // PNG de 1x1 píxel.
        private static readonly byte[] PngMinimo = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private readonly TiendaFactory _fabrica;

        public CatalogoTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        private async Task<PaginaDto<ProductoDto>> ConsultarAsync(string consulta)
        {
            var cliente = await _fabrica.ClienteDemoAsync();
            return await (await cliente.GetAsync("/api/Productos?" + consulta)).LeerAsync<PaginaDto<ProductoDto>>();
        }

        [Fact]
        public async Task LaBusqueda_FiltraPorNombreOPorDescripcion()
        {
            var porNombre = await ConsultarAsync("busqueda=auricul");
            var porDescripcion = await ConsultarAsync("busqueda=autonomía");

            Assert.Contains(porNombre.Elementos, p => p.Nombre == "Auriculares Bluetooth");
            Assert.All(porNombre.Elementos, p => Assert.Contains("auricul", (p.Nombre + p.Descripcion).ToLowerInvariant()));
            Assert.Contains(porDescripcion.Elementos, p => p.Nombre == "Parlante portátil");
        }

        [Fact]
        public async Task ElFiltroDeCategoria_DevuelveSoloLosProductosDeEsaCategoria()
        {
            var cliente = await _fabrica.ClienteDemoAsync();
            int deportes = await cliente.CategoriaIdAsync("Deportes");

            var pagina = await ConsultarAsync($"categoriaId={deportes}&tamanoPagina=100");

            Assert.NotEmpty(pagina.Elementos);
            Assert.All(pagina.Elementos, p => Assert.Equal("Deportes", p.Categoria));
        }

        [Fact]
        public async Task SoloDisponibles_OcultaLosProductosSinStock()
        {
            var todos = await ConsultarAsync("tamanoPagina=100");
            var disponibles = await ConsultarAsync("soloDisponibles=true&tamanoPagina=100");

            Assert.Contains(todos.Elementos, p => p.Stock == 0 && !p.Disponible);
            Assert.DoesNotContain(disponibles.Elementos, p => p.Stock == 0);
            Assert.All(disponibles.Elementos, p => Assert.True(p.Disponible));
        }

        [Fact]
        public async Task ElOrdenPorPrecio_FuncionaEnAmbosSentidos()
        {
            var ascendente = await ConsultarAsync("orden=precio_asc&tamanoPagina=100");
            var descendente = await ConsultarAsync("orden=precio_desc&tamanoPagina=100");

            Assert.Equal(ascendente.Elementos.Select(p => p.Precio).OrderBy(x => x), ascendente.Elementos.Select(p => p.Precio));
            Assert.Equal(descendente.Elementos.Select(p => p.Precio).OrderByDescending(x => x), descendente.Elementos.Select(p => p.Precio));
        }

        [Fact]
        public async Task LaPaginacion_DividePorTamanoYNoRepiteElementos()
        {
            var primera = await ConsultarAsync("tamanoPagina=5&pagina=1");
            var segunda = await ConsultarAsync("tamanoPagina=5&pagina=2");

            Assert.Equal(5, primera.Elementos.Count);
            Assert.Equal((int)Math.Ceiling(primera.TotalElementos / 5.0), primera.TotalPaginas);
            Assert.Empty(primera.Elementos.Select(p => p.ProductoId).Intersect(segunda.Elementos.Select(p => p.ProductoId)));
        }

        [Theory]
        [InlineData("pagina=0")]
        [InlineData("tamanoPagina=0")]
        [InlineData("tamanoPagina=101")]
        public async Task LosParametrosDePaginacionInvalidos_Devuelven400(string consulta)
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var respuesta = await cliente.GetAsync("/api/Productos?" + consulta);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task CadaProductoInformaSuCategoriaYSuDisponibilidad()
        {
            var pagina = await ConsultarAsync("busqueda=Pelota");

            var pelota = Assert.Single(pagina.Elementos);
            Assert.Equal("Deportes", pelota.Categoria);
            Assert.True(pelota.Disponible);
            Assert.True(pelota.Precio > 0);
        }

        [Fact]
        public async Task LaImagenDelCatalogo_NoRequiereToken()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync();

            var contenido = new MultipartFormDataContent();
            var archivo = new ByteArrayContent(PngMinimo);
            archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            contenido.Add(archivo, "archivo", "foto.png");
            var carga = await admin.PostAsync($"/api/Productos/{id}/imagen", contenido);
            Assert.Equal(HttpStatusCode.OK, carga.StatusCode);

            // Una etiqueta <img> no puede mandar el header Authorization: la imagen debe ser pública.
            var anonimo = _fabrica.CreateClient();
            var imagen = await anonimo.GetAsync($"/api/Productos/{id}/imagen");

            Assert.Equal(HttpStatusCode.OK, imagen.StatusCode);
            Assert.Equal("image/png", imagen.Content.Headers.ContentType?.MediaType);
            Assert.Equal(PngMinimo, await imagen.Content.ReadAsByteArrayAsync());
        }

        [Fact]
        public async Task UnProductoSinImagen_Devuelve404SinPedirToken()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync();

            var respuesta = await _fabrica.CreateClient().GetAsync($"/api/Productos/{id}/imagen");

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }

        [Fact]
        public async Task SubirUnArchivoQueNoEsImagen_Devuelve400()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync();

            var contenido = new MultipartFormDataContent();
            contenido.Add(new ByteArrayContent("no soy una imagen"u8.ToArray()), "archivo", "virus.exe");

            var respuesta = await admin.PostAsync($"/api/Productos/{id}/imagen", contenido);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
    }
}
