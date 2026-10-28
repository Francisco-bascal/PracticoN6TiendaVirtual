using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Shared.Tests
{
    public class FormatoTests
    {
        [Theory]
        [InlineData(0, "$ 0,00")]
        [InlineData(5, "$ 5,00")]
        [InlineData(1234.5, "$ 1.234,50")]
        [InlineData(28500, "$ 28.500,00")]
        [InlineData(1234567.891, "$ 1.234.567,89")]
        [InlineData(-1500, "$ -1.500,00")]
        public void DineroUsaPuntoParaMilesYComaParaDecimales(double monto, string esperado)
        {
            Assert.Equal(esperado, Formato.Dinero((decimal)monto));
        }

        [Fact]
        public void ElFormatoNoDependeDeLaCulturaDelDispositivo()
        {
            var original = System.Globalization.CultureInfo.CurrentCulture;

            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                Assert.Equal("$ 1.234,50", Formato.Dinero(1234.5m));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void LasFechasLocalesNoSeConvierten_YLasUtcSiSeConvierten()
        {
            var local = new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Local);
            var utc = new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Utc);

            Assert.Equal("01/10/2026 22:30", Formato.FechaHora(local));
            Assert.Equal(utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), Formato.FechaHora(utc));
        }

        [Fact]
        public void UnaFechaSinZonaSeInterpretaComoUtc()
        {
            var sinZona = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Unspecified);
            var utc = DateTime.SpecifyKind(sinZona, DateTimeKind.Utc);

            Assert.Equal(Formato.FechaHora(utc), Formato.FechaHora(sinZona));
        }

        [Fact]
        public void LasFechasParaLaApiVanEnFormatoIso()
        {
            Assert.Equal("2026-03-07", Formato.ParaApi(new DateTime(2026, 3, 7, 23, 59, 0)));
        }

        [Theory]
        [InlineData("Compra", "Compra")]
        [InlineData("AnulacionVenta", "Anulación de venta")]
        [InlineData("AjusteManual", "Ajuste manual")]
        [InlineData("Otro", "Otro")]
        public void LosMotivosTienenEtiquetaLegible(string motivo, string esperado)
        {
            Assert.Equal(esperado, Formato.EtiquetaMotivo(motivo));
        }
    }

    public class ServidorApiTests
    {
        [Theory]
        [InlineData("http://192.168.0.10:5276", "http://192.168.0.10:5276")]
        [InlineData("  http://10.0.2.2:5276/  ", "http://10.0.2.2:5276")]
        [InlineData("192.168.0.10:5276", "http://192.168.0.10:5276")]
        [InlineData("https://tienda.ejemplo.com/", "https://tienda.ejemplo.com")]
        public void NormalizaLaDireccion(string entrada, string esperada)
        {
            Assert.Equal(esperada, ServidorApi.Normalizar(entrada));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ftp://servidor.com")]
        [InlineData("no es una url")]
        public void RechazaDireccionesInvalidas(string entrada)
        {
            Assert.Throws<ArgumentException>(() => ServidorApi.Normalizar(entrada));
        }

        [Fact]
        public async Task SinConfiguracionUsaLaPredeterminada_YLaGuardadaTienePrioridad()
        {
            var almacenamiento = new AlmacenamientoEnMemoria();
            var servidor = new ServidorApi(almacenamiento, new OpcionesTienda { UrlApiPorDefecto = "http://10.0.2.2:5276" });

            Assert.Equal("http://10.0.2.2:5276", await servidor.ObtenerUrlAsync());

            await servidor.GuardarUrlAsync("192.168.1.50:5276");
            Assert.Equal("http://192.168.1.50:5276", await servidor.ObtenerUrlAsync());

            // Una nueva instancia (reinicio de la app) recupera la dirección guardada.
            var reiniciado = new ServidorApi(almacenamiento, new OpcionesTienda { UrlApiPorDefecto = "http://10.0.2.2:5276" });
            Assert.Equal("http://192.168.1.50:5276", await reiniciado.ObtenerUrlAsync());

            await reiniciado.RestablecerAsync();
            Assert.Equal("http://10.0.2.2:5276", await reiniciado.ObtenerUrlAsync());
        }

        [Fact]
        public async Task UnaDireccionInvalidaNoSeGuarda()
        {
            var almacenamiento = new AlmacenamientoEnMemoria();
            var servidor = new ServidorApi(almacenamiento, new OpcionesTienda());

            await Assert.ThrowsAsync<ArgumentException>(() => servidor.GuardarUrlAsync("ftp://x"));

            Assert.Empty(almacenamiento.Datos);
        }
    }
}
