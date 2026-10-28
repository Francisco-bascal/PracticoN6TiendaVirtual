---
name: frontend-core
description: Convenciones generales de desarrollo frontend de este proyecto (Tienda Móvil): HTML semántico, Bootstrap 5, CSS, accesibilidad, formularios y estados de la interfaz, pensadas para pantallas de teléfono. Usar al crear o modificar interfaces, formularios, estilos, navegación y comportamientos visuales.
---

# Skill: Frontend Core

> Adaptada de la skill `frontend-core` genérica para el frontend móvil de este proyecto (Blazor Hybrid / .NET MAUI, Android). Se aplica junto con la skill `blazor-hybrid`.

## 1. Objetivo

Desarrollar interfaces consistentes, responsivas, accesibles y mantenibles, pensadas **primero para teléfono** (mobile-first), respetando la arquitectura, las dependencias y las convenciones existentes del proyecto.

## 2. Tecnologías y estilos

- **Bootstrap 5.3** es la biblioteca CSS base y **Bootstrap Icons** el set de íconos. Ambos están **empaquetados** en `PracticoN6.Frontend.Shared/wwwroot/lib/` y se cargan desde los `index.html` de cada host. **Nunca desde un CDN**: la app debe funcionar sin depender de un tercero y el WebView de Android no debe pedir recursos a internet para dibujarse.
- No se usa el JavaScript de Bootstrap (`bootstrap.bundle.js`): los componentes interactivos (modal, toast, offcanvas, collapse) se arman con las **clases CSS de Bootstrap** y el estado del componente Blazor.
- Priorizar las **utilidades y componentes de Bootstrap** (`card`, `btn`, `badge`, `alert`, `modal`, `toast`, `list-group`, `form-control`, `d-flex`, `gap-*`, `text-body-secondary`, …) antes de escribir CSS propio.
- `wwwroot/css/app.css` contiene **solo** lo que Bootstrap no resuelve: tema de la marca, estructura de la app (barra superior y barra de pestañas), áreas seguras (*safe areas*), ajustes táctiles. No duplicar estilos que ya existen en Bootstrap.
- Usar **colores semánticos de Bootstrap** (`text-bg-*`, `bg-body`, `text-body-secondary`, variables `--bs-*`) y nunca colores fijos en el marcado: así funciona el **modo oscuro** (`data-bs-theme`, que sigue la preferencia del dispositivo).
- HTML semántico: `header`, `main`, `nav`, `section`, `button` para acciones y `a` para navegación.

## 3. Componentes e interfaz

- Reutilizar layouts y componentes compartidos (`EstadoVista`, `Alerta`, `Cargando`, `Paginador`, `DialogoConfirmacion`, `EncabezadoPagina`, …) antes de crear otros.
- Jerarquía visual clara: un `h1` por pantalla (`EncabezadoPagina`), navegación inferior con las secciones principales, botón "volver" en las pantallas internas.
- **Diseño táctil:** objetivos de toque de al menos 44 × 44 px, separación suficiente entre acciones, nada que dependa de `:hover`, acciones principales al alcance del pulgar.
- Íconos decorativos con `aria-hidden="true"`; botones que solo tienen ícono con `aria-label`.
- Diferenciar claramente los cuatro estados de toda vista de datos: **cargando, error (con "Reintentar"), vacío y con datos** (componente `EstadoVista`).
- Accesibilidad básica: un `label` por campo, `lang="es"`, foco visible, `aria-current` en la pestaña activa, regiones `aria-live` para avisos, contraste suficiente, vínculo "saltar al contenido", respeto de `prefers-reduced-motion`.

## 4. Formularios e interacciones

- Validar en el cliente (Data Annotations) **y siempre en el servidor**: el cliente no es una barrera de seguridad.
- Mostrar el error **junto al campo** con un mensaje comprensible (`ValidationMessage`) y los estados `is-invalid` de Bootstrap (`ValidacionBootstrap`).
- Evitar envíos duplicados: deshabilitar el botón y cambiar su texto mientras se procesa.
- Usar tipos de campo e `inputmode`/`autocomplete` adecuados (`type="email"`, `inputmode="numeric"`, …) para que el teclado del teléfono sea el correcto.
- Comunicar el resultado de cada operación: aviso breve (toast) o `Alerta`; nunca dejar al usuario sin respuesta.

## 5. Reglas de implementación

- Revisar la estructura y las dependencias existentes antes de modificar archivos.
- Respetar nombres (en español), rutas, convenciones y arquitectura del proyecto.
- No inventar contratos de API, propiedades ni comportamientos del backend: los modelos del frontend reflejan los DTO reales de la API.
- No exponer secretos ni confiar exclusivamente en las validaciones del cliente.
- Preferir cambios localizados y soluciones simples antes que reescrituras o abstracciones innecesarias.
- Verificar siempre: compilación, pruebas unitarias, recorrido de la interfaz (`e2e/`) y aspecto en pantalla de teléfono, en modo claro y oscuro.
