using System.Net;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Shared.Tests
{
    public class ApiClientTests
    {
        [Fact]
        public async Task ArmaLaUrlCombinandoLaDireccionDelServidorConLaRuta()
        {
            var entorno = new Entorno("http://servidor.test:5276/");
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("\"ok\"");

            await entorno.Api.GetAsync<string>("/api/Usuarios/test");

            Assert.Equal("http://servidor.test:5276/api/Usuarios/test", entorno.Servidor.Peticiones.Single().RequestUri!.ToString());
        }

        [Fact]
        public async Task EnviaElTokenSoloSiHaySesion()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("\"ok\"");

            await entorno.Api.GetAsync<string>("api/x");
            entorno.Credenciales.Token = "abc.def.ghi";
            await entorno.Api.GetAsync<string>("api/x");

            Assert.Null(entorno.Servidor.Peticiones[0].Headers.Authorization);
            Assert.Equal("Bearer", entorno.Servidor.Peticiones[1].Headers.Authorization!.Scheme);
            Assert.Equal("abc.def.ghi", entorno.Servidor.Peticiones[1].Headers.Authorization!.Parameter);
        }

        [Theory]
        [InlineData("Stock insuficiente para el producto 'X'.", HttpStatusCode.Conflict, "Stock insuficiente para el producto 'X'.")]
        [InlineData("\"Nombre de usuario o contraseña incorrectos.\"", HttpStatusCode.Unauthorized, "Nombre de usuario o contraseña incorrectos.")]
        public async Task ElMensajeDeUnErrorEnTextoPlanoLlegaAlUsuario(string cuerpo, HttpStatusCode codigo, string esperado)
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto(cuerpo, codigo);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Equal((int)codigo, ex.Codigo);
            Assert.Equal(esperado, ex.Message);
        }

        [Fact]
        public async Task ElMensajeDeUnErrorJsonConMensajeSeExtrae()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("""{"mensaje":"No tiene permisos para acceder a este recurso."}""", HttpStatusCode.Forbidden);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Equal("No tiene permisos para acceder a este recurso.", ex.Message);
        }

        [Fact]
        public async Task LosErroresDeValidacionDeAspNetSeUnenEnUnSoloMensaje()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json(
                """{"title":"One or more validation errors occurred.","errors":{"Email":["El correo electrónico no es válido."],"Password":["La contraseña es muy corta."]}}""",
                HttpStatusCode.BadRequest);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.PostAsync("api/x", new { }));

            Assert.Equal("El correo electrónico no es válido. La contraseña es muy corta.", ex.Message);
        }

        [Fact]
        public async Task UnErrorSinCuerpoUsaUnMensajeGenerico()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Equal(500, ex.Codigo);
            Assert.Contains("problema", ex.Message);
        }

        [Fact]
        public async Task UnaPaginaHtmlDeError_NoSeMuestraAlUsuario()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto("<html><body>Bad gateway</body></html>", HttpStatusCode.BadGateway);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.DoesNotContain("<html>", ex.Message);
        }

        [Fact]
        public async Task SinConexion_SeInformaConCodigoCeroYUnMensajeComprensible()
        {
            var entorno = new Entorno();
            entorno.Servidor.Falla = new HttpRequestException("Connection refused");

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Equal(0, ex.Codigo);
            Assert.Contains("No se pudo conectar", ex.Message);
        }

        [Fact]
        public async Task UnTiempoDeEsperaAgotado_SeInformaSinCancelarLaOperacion()
        {
            var entorno = new Entorno();
            entorno.Servidor.Falla = new TaskCanceledException("timeout");

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Equal(0, ex.Codigo);
            Assert.Contains("tardó demasiado", ex.Message);
        }

        [Fact]
        public async Task UnaRespuesta401ConSesion_AvisaQueLaSesionVencio()
        {
            var entorno = new Entorno();
            entorno.Credenciales.Token = "vencido";
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("""{"mensaje":"Debe iniciar sesión para acceder a este recurso."}""", HttpStatusCode.Unauthorized);
            int avisos = 0;
            entorno.Api.SesionExpirada += () => { avisos++; return Task.CompletedTask; };

            await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Equal(1, avisos);
        }

        [Fact]
        public async Task UnLoginFallido_NoCuentaComoSesionVencida()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto("Nombre de usuario o contraseña incorrectos.", HttpStatusCode.Unauthorized);
            int avisos = 0;
            entorno.Api.SesionExpirada += () => { avisos++; return Task.CompletedTask; };

            await Assert.ThrowsAsync<ApiException>(() => entorno.Tienda.LoginAsync("x", "y"));

            Assert.Equal(0, avisos);
        }

        [Fact]
        public async Task UnaRespuestaQueNoEsElJsonEsperado_SeInformaSinRomperLaApp()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto("esto no es json", HttpStatusCode.OK);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<List<int>>("api/x"));

            Assert.Contains("interpretar", ex.Message);
        }

        [Fact]
        public async Task ProbarLaConexion_FuncionaConLaRespuestaDeTextoPlanoDelEndpointDeEstado()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto("API funcionando", HttpStatusCode.OK);

            await entorno.Tienda.ProbarConexionAsync();

            Assert.Equal("http://servidor.test:5276/api/Usuarios/test", entorno.Servidor.Peticiones.Single().RequestUri!.ToString());
        }

        [Fact]
        public async Task ProbarLaConexion_InformaElErrorSiElServidorNoResponde()
        {
            var entorno = new Entorno();
            entorno.Servidor.Falla = new HttpRequestException("Connection refused");

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Tienda.ProbarConexionAsync());

            Assert.Contains("No se pudo conectar", ex.Message);
        }

        [Fact]
        public async Task UnaRespuestaDeTextoPlanoDondeSeEsperabaJson_NoRompeLaAppConUnaExcepcionInesperada()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto("API funcionando", HttpStatusCode.OK);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Api.GetAsync<string>("api/x"));

            Assert.Contains("interpretar", ex.Message);
        }

        [Fact]
        public async Task LaDireccionDelServidorSePuedeCambiarEnCaliente()
        {
            var entorno = new Entorno("http://uno.test");
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("\"ok\"");

            await entorno.Api.GetAsync<string>("api/x");
            await entorno.ServidorApi.GuardarUrlAsync("http://dos.test:81");
            await entorno.Api.GetAsync<string>("api/x");

            Assert.Equal("http://uno.test/api/x", entorno.Servidor.Peticiones[0].RequestUri!.ToString());
            Assert.Equal("http://dos.test:81/api/x", entorno.Servidor.Peticiones[1].RequestUri!.ToString());
        }

        [Fact]
        public async Task ElCuerpoDeUnPostSeEnviaComoJsonEnCamelCase()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("{}");

            await entorno.Api.PostAsync("api/x", new { NombreUsuario = "ana", Cantidad = 2 });

            string cuerpo = entorno.Servidor.Cuerpos.Single()!;
            Assert.Equal("""{"nombreUsuario":"ana","cantidad":2}""", cuerpo);
        }

        [Fact]
        public async Task ElAjusteDeStockEnviaElNumeroPlanoYLaObservacionEnLaConsulta()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => new HttpResponseMessage(HttpStatusCode.OK);

            await entorno.Tienda.AjustarStockAsync(7, -3, "Rotura en depósito");

            var peticion = entorno.Servidor.Peticiones.Single();
            Assert.Equal(HttpMethod.Patch, peticion.Method);
            Assert.Equal("http://servidor.test:5276/api/Productos/7/stock?observacion=Rotura%20en%20dep%C3%B3sito", peticion.RequestUri!.AbsoluteUri);
            Assert.Equal("-3", entorno.Servidor.Cuerpos.Single());
        }

        [Fact]
        public async Task LaBusquedaDelCatalogoEscapaLosParametros()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("""{"paginaActual":1,"tamanoPagina":12,"totalElementos":0,"totalPaginas":0,"elementos":[]}""");

            await entorno.Tienda.BuscarProductosAsync("café & té", 4, true, "precio_asc", 2);

            string consulta = entorno.Servidor.Peticiones.Single().RequestUri!.Query;
            Assert.Contains("busqueda=caf%C3%A9%20%26%20t%C3%A9", consulta);
            Assert.Contains("categoriaId=4", consulta);
            Assert.Contains("soloDisponibles=true", consulta);
            Assert.Contains("orden=precio_asc", consulta);
            Assert.Contains("pagina=2", consulta);
        }

        [Fact]
        public async Task LaEdicionDeUnProductoConservaElStockVigenteDelServidor()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = peticion => peticion.Method == HttpMethod.Get
                ? ManejadorFalso.Json("""{"productoId":5,"nombre":"X","precio":10,"stock":8,"imagenRuta":"Uploads/a.png","categoriaId":1,"categoria":"C","disponible":true}""")
                : ManejadorFalso.Json("""{"productoId":5,"nombre":"Nuevo","precio":20,"stock":8,"categoriaId":1,"categoria":"C","disponible":true}""");

            await entorno.Tienda.ActualizarProductoAsync(5, new Models.FormularioProducto { Nombre = "Nuevo", Precio = 20, Stock = 999, CategoriaId = 1 });

            string cuerpo = entorno.Servidor.Cuerpos[1]!;
            // El stock del formulario (999) se ignora: se envía el que tiene el servidor (8) y la imagen actual se conserva.
            Assert.Contains("\"stock\":8", cuerpo);
            Assert.Contains("\"imagenRuta\":\"Uploads/a.png\"", cuerpo);
        }
    }
}
