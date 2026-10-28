---
name: blazor-hybrid
description: Desarrollo frontend con Blazor Hybrid (.NET MAUI + BlazorWebView) para Android en este proyecto: componentes Razor, binding, eventos, formularios, servicios, capacidades del dispositivo y diferencias con una Blazor Web App.
---

# Skill: Blazor Hybrid (.NET MAUI)

> Adaptada de la skill `blazor-webapp` (pensada para web) a una app móvil **Blazor Hybrid**. Se aplica junto con la skill `frontend-core`.

## 1. Objetivo y alcance

Desarrollar la interfaz mediante componentes `.razor` que se ejecutan **dentro de un `BlazorWebView`** en una app .NET MAUI para Android, respetando la arquitectura, los servicios y las convenciones existentes.

## 2. Qué cambia respecto de una Blazor Web App

- **No existen los render modes** (Static SSR, Interactive Server/WebAssembly/Auto) ni el prerenderizado: todos los componentes son interactivos y corren en el dispositivo. No usar `@rendermode`, `[SupplyParameterFromForm]`, `FormName` ni navegación mejorada (*enhanced navigation*).
- No hay servidor Blazor ni `HttpContext`: la autenticación es **JWT contra la API**; el token se guarda en el almacenamiento cifrado del dispositivo, no en cookies.
- La inyección de dependencias de `scoped` dura **toda la vida de la app** (se comporta como un singleton): no guardar en servicios scoped estado que deba reiniciarse al cerrar sesión sin limpiarlo explícitamente (ver `SesionService`/`CarritoService`).
- El punto de entrada es `BlazorWebView` (`MainPage.xaml`) + `wwwroot/index.html` del host; el `Router` está en `App.razor` del proyecto compartido.

## 3. Arquitectura del proyecto

```
PracticoN6.Frontend.Shared   → TODA la UI y la lógica (Razor Class Library)
PracticoN6.Frontend.Maui     → host Android: BlazorWebView + capacidades del dispositivo
PracticoN6.Frontend.Web      → host de navegador (desarrollo y pruebas): mismas pantallas
```

- **Nada específico de MAUI en el proyecto compartido** (`Preferences`, `SecureStorage`, `Share`, `Connectivity`, `FileSystem`, …). Las capacidades del dispositivo se usan a través de **interfaces definidas en `Services/Plataforma.cs`** (`IAlmacenamientoSeguro`, `IPreferencias`, `IGestorArchivos`, `IConectividad`, `ICompartir`).
- Para sumar una capacidad nueva: interfaz en el proyecto compartido → implementación en `PracticoN6.Frontend.Maui/Servicios` → implementación en `PracticoN6.Frontend.Web/Servicios` → registro en ambos `Program`/`MauiProgram` → prueba con un doble.
- Código nuevo de pantallas, componentes y servicios: **siempre en el proyecto compartido**.

## 4. Componentes Razor

- Dividir en componentes reutilizables cuando haya una responsabilidad clara; recibir datos con `[Parameter]` y comunicar eventos con `EventCallback`.
- Separar presentación (marcado), estado de interfaz (campos del `@code`) y lógica de negocio (servicios; el negocio vive en la **API**, no en los componentes).
- Usar `@key` en listas dinámicas. Liberar eventos en `Dispose` (`IDisposable`) cuando el componente se suscribe a un servicio.
- Respetar nombres, namespaces y estructura existentes (nombres en español, componentes de página en `Pages/`, de administración en `Pages/Admin/`, reutilizables en `Components/`).
- Evitar duplicar componentes o crear abstracciones innecesarias; antes de escribir marcado repetido, buscar si ya existe (`EstadoVista`, `Alerta`, `EncabezadoPagina`, …).

## 5. Binding, eventos y formularios

- `@bind` para enlazar controles. En pantalla táctil, cuando un botón depende del valor que se está escribiendo, usar `@bind:event="oninput"`: con el evento `change` el botón sigue deshabilitado hasta que el campo pierde el foco y hace falta tocar dos veces.
- `EditForm` + Data Annotations + `ValidacionBootstrap` + `ValidationMessage`; los modelos de formulario están en `Models/Modelos.cs` y repiten las reglas de la API.
- Gestionar explícitamente los estados de carga, éxito, error y ausencia de datos (`EstadoVista`); deshabilitar el envío mientras se procesa.
- **Parámetros de tipo `string`: pasar variables con `@`** (`Error="@_error"`). Sin la arroba Blazor entrega el texto literal `_error` y compila igual; en los parámetros `bool`, `int` o `EventCallback` no hace falta.
- **No usar `alert`, `confirm` ni `prompt` de JavaScript**: la vista web de Android no los muestra. Usar `DialogoConfirmacion` y `NotificacionesService`.

## 6. Comunicación con el backend

- Todo acceso a la API pasa por `TiendaApi` (operaciones por recurso) sobre `ApiClient` (token, errores, sesión vencida). Los componentes no construyen `HttpRequestMessage` ni URLs.
- Capturar `ApiException` y mostrar `ex.Message` (ya viene en español y listo para el usuario). Nunca asumir que una operación salió bien.
- Los **precios, el stock y las reglas de negocio los decide el servidor**; el cliente solo envía lo que el usuario eligió.
- La dirección de la API es configurable (`ServidorApi`): en el emulador de Android es `http://10.0.2.2:5276`; en un teléfono real, la IP de la PC.
- No cambiar contratos, servicios ni arquitectura sin necesidad; si cambia la API, actualizar `Models/Modelos.cs` y `TiendaApi` y sus pruebas.

## 7. El dispositivo y el WebView de Android

- **Áreas seguras:** `viewport-fit=cover` y `env(safe-area-inset-*)` en la barra superior e inferior.
- **Botón "atrás" de Android:** lo maneja `MainPage.OnBackButtonPressed` → `tiendaVolver()`; las pantallas internas muestran además un botón "volver" en la barra.
- **Descargas:** `<a download>` no funciona en el WebView. Los archivos (PDF) se entregan con `IGestorArchivos` (hoja de compartir de Android).
- **Almacenamiento:** `IAlmacenamientoSeguro` (token) y `IPreferencias` (URL del servidor, carrito). No usar `localStorage` desde el código compartido.
- **Recursos de la interfaz** (Bootstrap, íconos, fuentes) **empaquetados** en `Shared/wwwroot/lib`: nunca un CDN. Se referencian como `_content/PracticoN6.Frontend.Shared/...`.
- **HTTP en desarrollo:** el manifiesto permite tráfico `http` (`usesCleartextTraffic`) y la vista web permite contenido mixto para las imágenes del catálogo; en producción, servir la API por HTTPS y quitar ambos ajustes.
- **Permisos:** declararlos en `Platforms/Android/AndroidManifest.xml` (hoy solo `INTERNET` y `ACCESS_NETWORK_STATE`).
- Pensar en **conectividad intermitente**: informar la falta de red (`IConectividad`), mostrar errores recuperables con "Reintentar" y no perder lo que el usuario escribió.

## 8. JavaScript

- Solo cuando Blazor no alcance: historial del WebView (`tiendaVolver`), tema inicial claro/oscuro, y lo propio del host web (`localStorage`, descarga). Todo en `Shared/wwwroot/js/tienda.js` o inline mínimo en `index.html`.
- Sin librerías JavaScript de terceros (tampoco `bootstrap.bundle.js`). No manipular el DOM a mano si un componente lo resuelve.

## 9. Reglas de implementación y verificación

- Reutilizar layouts, componentes y servicios; presentación en componentes, negocio en la API.
- Verificar: `dotnet build` y `dotnet test` del proyecto compartido y sus pruebas; el recorrido `e2e/` contra la API; el aspecto en pantalla de teléfono (claro y oscuro).
- Compilar no alcanza: errores como el del `@` olvidado o un estilo de Bootstrap que no se aplica (en 5.3 las reglas de `.offcanvas` tienen especificidad `.offcanvas.offcanvas-bottom`, así que una clase propia sola no las pisa) solo se ven al mirar la pantalla. Correr el e2e y revisar las capturas.
- El proyecto `PracticoN6.Frontend.Maui` solo compila con la carga de trabajo MAUI (Visual Studio). Si cambia, comprobar su código en un equipo que la tenga y probar en el emulador de Android.
- Mantener la documentación (`README.md`) al día cuando cambien la estructura o la forma de ejecutar.
