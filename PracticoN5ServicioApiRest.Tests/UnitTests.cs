using Microsoft.Extensions.Configuration;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Tests
{
    public class ZonaHorariaNegocioTests
    {
        private static ZonaHorariaNegocio Crear(string? zona = "America/Argentina/Buenos_Aires")
        {
            var configuracion = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Negocio:ZonaHoraria"] = zona })
                .Build();
            return new ZonaHorariaNegocio(configuracion);
        }

        [Fact]
        public void ElRangoDeDiasLocalesSeConvierteAUtc()
        {
            var (desde, hasta) = Crear().RangoUtc(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

            // Argentina es UTC-3 todo el año: el día local empieza a las 03:00 UTC y "hasta" es exclusivo (día siguiente).
            Assert.Equal(new DateTime(2026, 10, 1, 3, 0, 0), desde);
            Assert.Equal(new DateTime(2026, 11, 1, 3, 0, 0), hasta);
        }

        [Fact]
        public void UnExtremoNuloSignificaSinLimite()
        {
            var (desde, hasta) = Crear().RangoUtc(null, null);

            Assert.Null(desde);
            Assert.Null(hasta);
        }

        [Fact]
        public void LaHoraDelDiaSeIgnoraAlArmarElRango()
        {
            var (desde, hasta) = Crear().RangoUtc(new DateTime(2026, 10, 1, 17, 45, 0), new DateTime(2026, 10, 1, 8, 0, 0));

            Assert.Equal(new DateTime(2026, 10, 1, 3, 0, 0), desde);
            Assert.Equal(new DateTime(2026, 10, 2, 3, 0, 0), hasta);
        }

        [Fact]
        public void LasFechasInvertidasSeRechazan()
        {
            Assert.Throws<ArgumentException>(() => Crear().RangoUtc(new DateTime(2026, 10, 2), new DateTime(2026, 10, 1)));
        }

        [Fact]
        public void UnaVentaDeLaNocheArgentinaCaeEnElDiaLocalCorrecto()
        {
            var zona = Crear();
            var ventaNocturnaUtc = new DateTime(2026, 10, 2, 1, 30, 0, DateTimeKind.Utc); // 22:30 del 1/10 en Argentina

            var (desde, hasta) = zona.RangoUtc(new DateTime(2026, 10, 1), new DateTime(2026, 10, 1));

            Assert.InRange(ventaNocturnaUtc, desde!.Value, hasta!.Value.AddTicks(-1));
            Assert.Equal(new DateTime(2026, 10, 1, 22, 30, 0), zona.ALocal(ventaNocturnaUtc));
        }

        [Fact]
        public void SiLaZonaNoExiste_SeTrabajaEnUtcSinFallar()
        {
            var zona = Crear("Zona/Inexistente");

            Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), zona.ALocal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        }
    }

    public class PasarelaPagoSimuladaTests
    {
        private static readonly MedioPago Tarjeta = new() { MedioPagoId = 1, Nombre = "Tarjeta", RequiereTarjeta = true };
        private static readonly MedioPago Transferencia = new() { MedioPagoId = 3, Nombre = "Transferencia", RequiereTarjeta = false };
        private readonly PasarelaPagoSimulada _pasarela = new();

        [Theory]
        [InlineData("4242424242424242")]
        [InlineData("4242 4242 4242 4242")]
        [InlineData("4242-4242-4242-4242")]
        public void UnaTarjetaValidaSeAprueba_ConUnCodigoDeAutorizacion(string numero)
        {
            string codigo = _pasarela.Autorizar(Tarjeta, 100m, numero);

            Assert.StartsWith("SIM-", codigo);
            Assert.Equal(12, codigo.Length);
        }

        [Fact]
        public void LosCodigosDeAutorizacionSonUnicos()
        {
            Assert.NotEqual(_pasarela.Autorizar(Transferencia, 1m, null), _pasarela.Autorizar(Transferencia, 1m, null));
        }

        [Theory]
        [InlineData("4000000000000000")]
        [InlineData("1234 5678 9012 0000")]
        public void UnaTarjetaQueTerminaEnCuatroCeros_SeRechaza(string numero)
        {
            var ex = Assert.Throws<PagoRechazadoException>(() => _pasarela.Autorizar(Tarjeta, 100m, numero));

            Assert.Contains("rechazado", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("abcdefghijklmnop")]
        [InlineData("12345678901234567890")]
        public void UnNumeroDeTarjetaInvalido_EsUnErrorDeValidacion(string? numero)
        {
            Assert.Throws<ArgumentException>(() => _pasarela.Autorizar(Tarjeta, 100m, numero));
        }

        [Fact]
        public void LaTransferenciaNoPideTarjeta()
        {
            Assert.StartsWith("SIM-", _pasarela.Autorizar(Transferencia, 100m, null));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void UnMontoNoPositivoSeRechaza(double monto)
        {
            Assert.Throws<ArgumentException>(() => _pasarela.Autorizar(Transferencia, (decimal)monto, null));
        }
    }
}
