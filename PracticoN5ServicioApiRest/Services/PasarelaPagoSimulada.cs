using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>El pago fue rechazado por la pasarela (se informa como 402 Payment Required).</summary>
    public class PagoRechazadoException : Exception
    {
        public PagoRechazadoException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// Simula la autorización de un pago. No se conecta a ningún proveedor ni conserva datos de tarjeta.
    /// Regla de prueba: una tarjeta cuyo número termina en 0000 es rechazada; cualquier otra se aprueba.
    /// </summary>
    public class PasarelaPagoSimulada
    {
        /// <returns>El código de autorización del pago aprobado.</returns>
        /// <exception cref="ArgumentException">Faltan o son inválidos los datos de tarjeta.</exception>
        /// <exception cref="PagoRechazadoException">La pasarela rechazó el pago.</exception>
        public string Autorizar(MedioPago medioPago, decimal monto, string? numeroTarjeta)
        {
            if (monto <= 0)
            {
                throw new ArgumentException("El monto a pagar debe ser mayor a cero.", nameof(monto));
            }

            if (medioPago.RequiereTarjeta)
            {
                string digitos = new string((numeroTarjeta ?? string.Empty).Where(char.IsDigit).ToArray());

                if (digitos.Length < 13 || digitos.Length > 19)
                {
                    throw new ArgumentException("Ingrese un número de tarjeta válido (entre 13 y 19 dígitos).", nameof(numeroTarjeta));
                }

                if (digitos.EndsWith("0000"))
                {
                    throw new PagoRechazadoException("El pago fue rechazado (simulación): fondos insuficientes.");
                }
            }

            return "SIM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        }
    }
}
