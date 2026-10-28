using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PracticoN5ServicioApiRest.DTOs;
using System.Net.Sockets;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>No se pudo entregar el correo al servidor SMTP (se informa como 502 Bad Gateway).</summary>
    public class EnvioCorreoException : Exception
    {
        public EnvioCorreoException(string mensaje, Exception? interna = null) : base(mensaje, interna) { }
    }

    /// <summary>
    /// Envía correos con adjunto por SMTP (sección "Smtp" de la configuración). Si no hay servidor configurado,
    /// el mensaje se guarda como archivo .eml en App_Data/Correos: sirve para probar sin credenciales reales.
    /// </summary>
    public class EmailService
    {
        private const string RemitentePorDefecto = "no-responder@tienda.local";

        // Si el servidor no responde, la app no debe quedar esperando los 2 minutos que MailKit espera por defecto.
        private const int TiempoDeEsperaMs = 20_000;

        private readonly IConfiguration _configuracion;
        private readonly IWebHostEnvironment _entorno;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuracion, IWebHostEnvironment entorno, ILogger<EmailService> logger)
        {
            _configuracion = configuracion;
            _entorno = entorno;
            _logger = logger;
        }

        public async Task<ResultadoEnvioDTO> EnviarAsync(
            string destinatario,
            string asunto,
            string cuerpo,
            string nombreAdjunto,
            byte[] adjunto,
            CancellationToken cancellationToken = default)
        {
            if (!MailboxAddress.TryParse(destinatario, out var direccionDestino))
            {
                throw new ArgumentException("La dirección de correo del destinatario no es válida.", nameof(destinatario));
            }

            string? host = _configuracion["Smtp:Host"];
            string remitente = ResolverRemitente();
            string nombreRemitente = _configuracion["Smtp:NombreRemitente"] ?? _configuracion["Negocio:Nombre"] ?? "Tienda Móvil";

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(nombreRemitente, remitente));
            mensaje.To.Add(direccionDestino);
            mensaje.Subject = asunto;

            var constructor = new BodyBuilder { TextBody = cuerpo };
            constructor.Attachments.Add(nombreAdjunto, adjunto, ContentType.Parse("application/pdf"));
            mensaje.Body = constructor.ToMessageBody();

            if (string.IsNullOrWhiteSpace(host))
            {
                return await GuardarComoArchivoAsync(mensaje, cancellationToken);
            }

            try
            {
                using var cliente = new SmtpClient();

                cliente.Timeout = TiempoDeEsperaMs;

                int puerto = _configuracion.GetValue<int?>("Smtp:Puerto") ?? 587;
                bool usarSsl = _configuracion.GetValue<bool?>("Smtp:UsarSsl") ?? true;

                await cliente.ConnectAsync(host, puerto, OpcionesSeguridad(puerto, usarSsl), cancellationToken);

                string? usuario = _configuracion["Smtp:Usuario"];
                if (!string.IsNullOrWhiteSpace(usuario))
                {
                    await cliente.AuthenticateAsync(usuario, _configuracion["Smtp:Password"] ?? string.Empty, cancellationToken);
                }

                await cliente.SendAsync(mensaje, cancellationToken);
                await cliente.DisconnectAsync(true, cancellationToken);
            }
            // Una cancelación pedida por quien llamó se propaga tal cual; un tiempo de espera agotado es una falla de envío.
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger.LogError(ex, "No se pudo enviar el correo a {Destinatario} por SMTP ({Host}).", destinatario, host);
                throw new EnvioCorreoException(DescribirFalla(ex), ex);
            }

            return new ResultadoEnvioDTO
            {
                Enviado = true,
                Modo = "SMTP",
                Mensaje = $"Correo enviado a {destinatario}."
            };
        }

        /// <summary>
        /// Cómo se cifra la conexión: el puerto 465 usa TLS desde el primer byte (SMTPS) y el resto (587) negocia STARTTLS.
        /// Con UsarSsl en falso se deja que MailKit decida según lo que ofrezca el servidor (útil con servidores locales de prueba).
        /// </summary>
        public static SecureSocketOptions OpcionesSeguridad(int puerto, bool usarSsl)
        {
            if (puerto == 465)
            {
                return SecureSocketOptions.SslOnConnect;
            }

            return usarSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        }

        /// <summary>
        /// Smtp:Remitente si está; si no, la cuenta con la que se inicia sesión (Gmail y Office 365 solo aceptan enviar
        /// desde la propia cuenta); y si tampoco hay, una dirección genérica.
        /// </summary>
        private string ResolverRemitente()
        {
            string? remitente = _configuracion["Smtp:Remitente"];
            if (!string.IsNullOrWhiteSpace(remitente))
            {
                return remitente.Trim();
            }

            string? usuario = _configuracion["Smtp:Usuario"];
            return !string.IsNullOrWhiteSpace(usuario) && MailboxAddress.TryParse(usuario.Trim(), out _)
                ? usuario.Trim()
                : RemitentePorDefecto;
        }

        /// <summary>
        /// Mensaje para quien usa la app. Distingue las causas que sí puede corregir quien administra el servidor (cuenta,
        /// conexión, destinatario) sin exponer la respuesta cruda del servidor ni su dirección; el detalle técnico queda en el log.
        /// </summary>
        private static string DescribirFalla(Exception ex)
        {
            return ex switch
            {
                AuthenticationException =>
                    "No se pudo enviar el correo: el servidor de correo rechazó el usuario o la contraseña configurados. " +
                    "Con Gmail hay que usar una «contraseña de aplicación», no la contraseña de la cuenta.",
                SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } =>
                    "No se pudo enviar el correo: el servidor no aceptó la dirección del destinatario.",
                SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } =>
                    "No se pudo enviar el correo: el servidor no aceptó la dirección del remitente (Smtp:Remitente debe ser la propia cuenta).",
                SslHandshakeException or SmtpProtocolException =>
                    "No se pudo enviar el correo: falló la conexión segura con el servidor (revise Smtp:Puerto y Smtp:UsarSsl: 587 con UsarSsl, o 465).",
                SocketException or IOException or TimeoutException or OperationCanceledException =>
                    "No se pudo enviar el correo: no se pudo conectar con el servidor de correo (revise Smtp:Host y Smtp:Puerto, y la conexión a internet).",
                _ =>
                    "No se pudo enviar el correo: el servidor de correo no respondió o rechazó el mensaje."
            };
        }

        private async Task<ResultadoEnvioDTO> GuardarComoArchivoAsync(MimeMessage mensaje, CancellationToken cancellationToken)
        {
            string carpeta = Path.Combine(_entorno.ContentRootPath, "App_Data", "Correos");
            Directory.CreateDirectory(carpeta);

            string nombre = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml";
            string ruta = Path.Combine(carpeta, nombre);

            await using (var flujo = File.Create(ruta))
            {
                await mensaje.WriteToAsync(flujo, cancellationToken);
            }

            _logger.LogWarning("Smtp:Host no está configurado: el correo se guardó en {Ruta} en lugar de enviarse.", ruta);

            return new ResultadoEnvioDTO
            {
                Enviado = false,
                Modo = "Archivo",
                Mensaje = $"No hay un servidor SMTP configurado: el correo no se envió, se guardó en App_Data/Correos/{nombre}. " +
                          "Quien administra el sistema debe completar la sección Smtp (ver README, «Envío de correo»)."
            };
        }
    }
}
