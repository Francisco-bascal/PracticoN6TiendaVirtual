// Funciones de apoyo que usa la interfaz (Blazor las invoca con IJSRuntime).

// Vuelve a la pantalla anterior si hay historial; devuelve false si no hay a dónde volver.
window.tiendaVolver = function () {
    if (window.history.length > 1) {
        window.history.back();
        return true;
    }
    return false;
};

// Descarga un archivo en el navegador (host web de pruebas). En la app móvil se usa la hoja de compartir de Android.
window.tiendaDescargar = function (nombre, tipoMime, base64) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: tipoMime }));
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombre;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
};

// Almacenamiento local del navegador (host web de pruebas).
window.tiendaAlmacen = {
    leer: clave => window.localStorage.getItem(clave),
    guardar: (clave, valor) => window.localStorage.setItem(clave, valor),
    eliminar: clave => window.localStorage.removeItem(clave)
};

// Estado de la red del navegador (host web). Devuelve el estado actual y avisa a .NET de cada cambio.
window.tiendaConectividad = {
    suscribir: function (referencia) {
        const avisar = () => referencia.invokeMethodAsync('Notificar', navigator.onLine);
        window.addEventListener('online', avisar);
        window.addEventListener('offline', avisar);
        return navigator.onLine;
    }
};

// Comparte texto con la hoja de compartir del navegador o, si no existe, lo copia al portapapeles.
// Devuelve true si se compartió y false si solo se copió.
window.tiendaCompartir = async function (titulo, texto) {
    if (navigator.share) {
        try {
            await navigator.share({ title: titulo, text: texto });
        } catch (e) {
            if (e.name !== 'AbortError') throw e;   // cerrar la hoja sin elegir nada no es un error
        }
        return true;
    }

    await navigator.clipboard.writeText(titulo + '\n' + texto);
    return false;
};
