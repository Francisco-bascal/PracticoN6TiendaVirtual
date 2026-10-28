namespace PracticoN5ServicioApiRest.Services
{
    public class ImagenesService
    {
        private const string CarpetaUploads = "Uploads";
        private const long TamañoMaximoBytes = 5 * 1024 * 1024;
        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };

        private readonly IWebHostEnvironment _entorno;

        public ImagenesService(IWebHostEnvironment entorno)
        {
            _entorno = entorno;
        }

        public async Task<string> GuardarAsync(IFormFile archivo, CancellationToken cancellationToken = default)
        {
            if (archivo == null || archivo.Length == 0)
            {
                throw new ArgumentException("Debe enviar un archivo de imagen.", nameof(archivo));
            }

            string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

            if (!ExtensionesPermitidas.Contains(extension))
            {
                throw new ArgumentException($"Formato no permitido. Extensiones válidas: {string.Join(", ", ExtensionesPermitidas)}.", nameof(archivo));
            }

            if (archivo.Length > TamañoMaximoBytes)
            {
                throw new ArgumentException("La imagen supera el tamaño máximo de 5 MB.", nameof(archivo));
            }

            // Nombre único para evitar colisiones; no se confía en el nombre del archivo del cliente.
            string rutaRelativa = Path.Combine(CarpetaUploads, $"{Guid.NewGuid():N}{extension}");

            string carpetaFisica = Path.Combine(_entorno.WebRootPath!, CarpetaUploads);
            Directory.CreateDirectory(carpetaFisica);

            string rutaFisica = Path.Combine(carpetaFisica, Path.GetFileName(rutaRelativa));

            await using (var flujo = File.Create(rutaFisica))
            {
                await archivo.CopyToAsync(flujo, cancellationToken);
            }

            return rutaRelativa.Replace('\\', '/');
        }

        public string ObtenerRutaFisica(string rutaRelativa)
        {
            // Path.GetFileName evita path traversal: siempre resuelve dentro de wwwroot/Uploads.
            return Path.Combine(_entorno.WebRootPath!, CarpetaUploads, Path.GetFileName(rutaRelativa));
        }

        public string ObtenerContentType(string rutaRelativa)
        {
            string extension = Path.GetExtension(rutaRelativa).ToLowerInvariant();

            return extension switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
        }

        public void Eliminar(string? rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                return;
            }

            string rutaFisica = ObtenerRutaFisica(rutaRelativa);

            if (File.Exists(rutaFisica))
            {
                File.Delete(rutaFisica);
            }
        }
    }
}