using Microsoft.AspNetCore.Components.Forms;
using PracticoN6.Frontend.Shared.Components;
using Xunit;

namespace PracticoN6.Frontend.Shared.Tests
{
    public class ProveedorClasesBootstrapTests
    {
        private class Modelo
        {
            public string? Nombre { get; set; }
        }

        [Fact]
        public void CampoConErrorDeValidacion_RecibeIsInvalid()
        {
            var contexto = new EditContext(new Modelo());
            var campo = contexto.Field(nameof(Modelo.Nombre));
            var mensajes = new ValidationMessageStore(contexto);
            mensajes.Add(campo, "El nombre es obligatorio.");

            string clase = new ProveedorClasesBootstrap().GetFieldCssClass(contexto, campo);

            Assert.Equal("is-invalid", clase);
        }

        [Fact]
        public void CampoValido_NoRecibeClasesDeBootstrap()
        {
            var contexto = new EditContext(new Modelo { Nombre = "Mouse" });
            var campo = contexto.Field(nameof(Modelo.Nombre));
            contexto.NotifyFieldChanged(campo);

            string clase = new ProveedorClasesBootstrap().GetFieldCssClass(contexto, campo);

            Assert.Equal(string.Empty, clase);
        }

        [Fact]
        public void AlCorregirElCampo_DejaDeSerInvalido()
        {
            var contexto = new EditContext(new Modelo());
            var campo = contexto.Field(nameof(Modelo.Nombre));
            var mensajes = new ValidationMessageStore(contexto);
            var proveedor = new ProveedorClasesBootstrap();

            mensajes.Add(campo, "El nombre es obligatorio.");
            Assert.Equal("is-invalid", proveedor.GetFieldCssClass(contexto, campo));

            mensajes.Clear(campo);
            Assert.Equal(string.Empty, proveedor.GetFieldCssClass(contexto, campo));
        }
    }
}
