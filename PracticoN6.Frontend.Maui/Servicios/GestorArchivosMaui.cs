using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Maui.Servicios;

/// <summary>
/// Entrega un archivo al usuario con la hoja de compartir de Android: desde ahí puede abrir el PDF con un visor,
/// guardarlo en el teléfono o en la nube, o enviarlo por correo o mensajería.
/// </summary>
public class GestorArchivosMaui : IGestorArchivos
{
    public async Task GuardarYCompartirAsync(string nombreArchivo, byte[] contenido, string tipoMime)
    {
        string carpeta = Path.Combine(FileSystem.CacheDirectory, "compartir");
        Directory.CreateDirectory(carpeta);

        // El nombre llega de la API: se descarta cualquier ruta para que el archivo quede siempre dentro de la carpeta.
        string ruta = Path.Combine(carpeta, Path.GetFileName(nombreArchivo));
        await File.WriteAllBytesAsync(ruta, contenido);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = nombreArchivo,
            File = new ShareFile(ruta, tipoMime)
        });
    }
}
