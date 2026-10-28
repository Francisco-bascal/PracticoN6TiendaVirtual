# Recorridos automáticos de la interfaz (Playwright)

Abren la tienda en un navegador con tamaño de teléfono y la recorren como lo haría una persona, contra la API real.

- `cliente.js`: pantalla Servidor, login (incorrecto y correcto), catálogo, búsqueda, hoja de orden y filtros (menor precio, solo con stock), aviso de falta de red, detalle, compartir el producto, carrito, pago rechazado y aprobado, comprobante PDF, correo, pedidos, cuenta, acceso restringido, cierre de sesión y modo oscuro.
- `personal.js`: panel, alta de producto con imagen, ajustes de stock, compra (entrada de mercadería), movimientos, anulación de una venta, los cuatro reportes PDF y su envío por correo, usuarios y roles, auditoría, errores y restricciones del Operador.

## Cómo correrlos

1. Iniciar la API (con los datos de ejemplo, que vienen activados en desarrollo):
   `dotnet run --project PracticoN5ServicioApiRest` → la tienda queda en http://localhost:5276
2. En esta carpeta:
   ```bash
   npm install
   npx playwright install chromium
   npm run cliente
   npm run personal
   ```

Las capturas quedan en `e2e/capturas`. Variables opcionales: `WEB_URL` (por defecto `http://localhost:5276`) y `CAPTURAS`.

Los recorridos crean datos con nombres únicos, así que se pueden repetir sobre la misma base. Los "errores de consola" que informan al final son las respuestas 400/401/402/409 que el recorrido provoca a propósito.
