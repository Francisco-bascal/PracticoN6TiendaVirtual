using Microsoft.AspNetCore.Components.Forms;

namespace PracticoN6.Frontend.Shared.Components
{
    /// <summary>Traduce el estado de validación de un campo de Blazor a las clases de validación de Bootstrap.</summary>
    public class ProveedorClasesBootstrap : FieldCssClassProvider
    {
        public override string GetFieldCssClass(EditContext editContext, in FieldIdentifier fieldIdentifier)
        {
            return editContext.GetValidationMessages(fieldIdentifier).Any() ? "is-invalid" : string.Empty;
        }
    }
}
