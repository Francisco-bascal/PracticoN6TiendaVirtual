// Recorrido automático de la interfaz en un navegador con tamaño de teléfono (Playwright).
// Requiere la API corriendo en http://localhost:5276 con los datos de ejemplo (ella también sirve la tienda web)
// (ver e2e/README.md). Las capturas quedan en e2e/capturas (o en CAPTURAS).
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const WEB = process.env.WEB_URL || 'http://localhost:5276';
const SHOTS = process.env.CAPTURAS || path.join(__dirname, 'capturas');
const ESC = __dirname;
const SUFIJO = Date.now().toString(36);   // nombres únicos: el recorrido se puede repetir sobre la misma base
fs.mkdirSync(SHOTS, { recursive: true });

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 1, isMobile: true, hasTouch: true, acceptDownloads: true, permissions: ['clipboard-read', 'clipboard-write'] });
  const page = await context.newPage();
  const problemas = [];
  page.on('console', m => { if (['error', 'warning'].includes(m.type())) problemas.push(`[${m.type()}] ${m.text()}`); });
  page.on('pageerror', e => problemas.push('[pageerror] ' + e.message));
  page.on('requestfailed', r => problemas.push('[requestfailed] ' + r.url() + ' ' + r.failure()?.errorText));

  const paso = async (nombre) => { await page.screenshot({ path: `${SHOTS}/${nombre}.png` }); console.log('OK  ' + nombre); };

  await page.goto(WEB);
  await page.waitForSelector('text=Ingrese para ver el catálogo', { timeout: 60000 });
  await paso('01-login');

  // Pantalla "Servidor": guardar y probar la conexión con la API.
  await page.click('a:has-text("Servidor:")');
  await page.waitForSelector('h1:has-text("Servidor")');
  await page.click('button:has-text("Guardar y probar")');
  await page.waitForSelector('.alert-success:has-text("Conexión correcta")');
  await paso('01b-servidor');
  await page.fill('#url', 'http://localhost:9');
  await page.click('button:has-text("Guardar y probar")');
  await page.waitForSelector('.alert-danger:has-text("No se pudo conectar")');
  await paso('01c-servidor-error');
  await page.click('button:has-text("Restablecer")');
  await page.waitForSelector('.alert-success:has-text("predeterminada")');
  await page.goto(WEB);
  await page.waitForSelector('text=Ingrese para ver el catálogo');

  // Credenciales incorrectas
  await page.fill('#usuario', 'cliente');
  await page.fill('#password', 'mala');
  await page.click('button:has-text("Ingresar")');
  await page.waitForSelector('text=incorrectos');
  await paso('02-login-error');

  // Login correcto
  await page.fill('#password', 'Demo1234');
  await page.click('button:has-text("Ingresar")');
  await page.waitForSelector('.grilla-productos');
  await page.waitForTimeout(500);
  await paso('03-catalogo');

  // Búsqueda
  await page.fill('input[type=search]', 'auricul');
  await page.press('input[type=search]', 'Enter');
  await page.waitForSelector('.tarjeta-titulo:has-text("Auriculares")');
  const resultados = await page.locator('.tarjeta-producto').count();
  console.log('Resultados de "auricul":', resultados);
  await paso('04-busqueda');

  // Limpiar y filtrar por disponibles / orden
  await page.fill('input[type=search]', '');
  await page.press('input[type=search]', 'Enter');
  await page.waitForTimeout(600);
  await page.click('button[aria-label="Ordenar y filtrar"]');
  await page.waitForSelector('#orden');
  await paso('05a-hoja-filtros');
  await page.selectOption('#orden', 'precio_asc');
  await page.click('.hoja-inferior button:has-text("Aplicar")');
  await page.waitForSelector('.hoja-inferior', { state: 'detached' });
  await page.waitForTimeout(600);
  const primerPrecio = await page.locator('.tarjeta-producto .precio').first().innerText();
  console.log('Primer precio (asc):', primerPrecio);
  await paso('05-orden-precio');

  // Producto sin stock: botón deshabilitado
  const sinStock = page.locator('.tarjeta-producto', { hasText: 'Teclado inalámbrico' });
  console.log('Teclado deshabilitado:', await sinStock.locator('button').isDisabled());

  // "Solo productos con stock" oculta el que no tiene; "Limpiar" vuelve a mostrarlo.
  await page.click('button[aria-label="Ordenar y filtrar"]');
  await page.check('#solo-stock');
  await page.click('.hoja-inferior button:has-text("Aplicar")');
  await page.waitForSelector('.hoja-inferior', { state: 'detached' });
  await page.waitForTimeout(600);
  console.log('Sin stock oculto con el filtro (esperado 0):', await page.locator('.tarjeta-producto', { hasText: 'Teclado inalámbrico' }).count());
  await page.click('button[aria-label="Ordenar y filtrar"]');
  await page.click('.hoja-inferior button:has-text("Limpiar")');
  await page.waitForSelector('.hoja-inferior', { state: 'detached' });
  await page.waitForSelector('.tarjeta-producto:has-text("Teclado inalámbrico")');

  // Sin red: aparece la franja de aviso y se va al volver la conexión.
  await context.setOffline(true);
  await page.waitForSelector('text=Sin conexión de red');
  await paso('05b-sin-red');
  await context.setOffline(false);
  await page.waitForSelector('text=Sin conexión de red', { state: 'detached' });
  console.log('Aviso de red: aparece y desaparece OK');

  // Detalle de producto
  await page.fill('input[type=search]', 'Auriculares');
  await page.press('input[type=search]', 'Enter');
  await page.waitForSelector('.tarjeta-titulo:has-text("Auriculares")');
  await page.click('.tarjeta-producto a:has-text("Auriculares")');
  await page.waitForSelector('text=Agregar al carrito');
  await paso('06-detalle');

  // Compartir: el navegador de pruebas no tiene hoja de compartir, así que copia al portapapeles.
  await page.click('button[aria-label="Compartir producto"]');
  await page.waitForSelector('.toast:has-text("portapapeles")');
  const copiado = await page.evaluate(() => navigator.clipboard.readText());
  console.log('Texto compartido:', JSON.stringify(copiado));
  if (!copiado.includes('Auriculares')) throw new Error('El texto compartido no menciona el producto');
  await page.click('button[aria-label="Más"]');
  await page.click('button:has-text("Agregar al carrito")');
  await page.waitForSelector('.toast:has-text("carrito")');
  await paso('07-agregado');

  // Carrito
  await page.click('.tab:has-text("Carrito")');
  await page.waitForSelector('h1:has-text("Mi carrito")');
  await page.waitForSelector('text=Auriculares Bluetooth');
  await paso('08-carrito');
  console.log('Total carrito:', await page.locator('.sticky-bottom').innerText());

  // Checkout con tarjeta rechazada
  await page.click('button:has-text("Continuar con el pago")');
  await page.waitForSelector('text=Medio de pago');
  await page.click('label.opcion-pago:has-text("Tarjeta de crédito")');
  await page.fill('#tarjeta', '4000 0000 0000 0000');
  await page.click('button:has-text("Pagar")');
  await page.waitForSelector('text=rechazado');
  await paso('09-pago-rechazado');

  // Checkout aprobado
  await page.fill('#tarjeta', '4242 4242 4242 4242');
  await page.click('button:has-text("Pagar")');
  await page.waitForSelector('text=Compra realizada');
  await page.waitForTimeout(300);
  await paso('10-pedido-confirmado');

  // Comprobante PDF
  const [descarga] = await Promise.all([page.waitForEvent('download'), page.click('button:has-text("Comprobante PDF")')]);
  await descarga.saveAs(path.join(SHOTS, 'comprobante.pdf'));
  console.log('Comprobante descargado:', descarga.suggestedFilename());

  // Enviar por correo (sin SMTP => modo archivo)
  await page.click('button:has-text("Enviar por correo")');
  await page.waitForSelector('text=SMTP');
  await paso('11-correo');

  // Mis pedidos
  await page.click('.tab:has-text("Pedidos")');
  await page.waitForSelector('a.list-group-item');
  await paso('12-mis-pedidos');

  // Cuenta
  await page.click('.tab:has-text("Cuenta")');
  await page.waitForSelector('text=Mis datos');
  await paso('13-cuenta');

  // Carrito vacío tras la compra y el panel de personal no aparece
  console.log('Tab Panel visible para cliente:', await page.locator('.tab:has-text("Panel")').count());
  await page.click('.tab:has-text("Carrito")');
  await page.waitForSelector('text=Su carrito está vacío');

  // Acceso a pantalla de personal por URL: debe negarse
  await page.goto(WEB + '/admin/productos');
  await page.waitForSelector('text=Acceso restringido', { timeout: 30000 });
  await paso('14-acceso-restringido');

  // Cerrar sesión
  await page.click('.tab:has-text("Cuenta")');
  await page.click('button:has-text("Cerrar sesión")');
  await page.waitForSelector('text=Ingrese para ver el catálogo');
  await paso('15-logout');

  // Modo oscuro: sigue el del teléfono (prefers-color-scheme).
  const oscuro = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 1, isMobile: true, hasTouch: true, colorScheme: 'dark' });
  const paginaOscura = await oscuro.newPage();
  await paginaOscura.goto(WEB);
  await paginaOscura.waitForSelector('#usuario', { timeout: 60000 });
  await paginaOscura.screenshot({ path: `${SHOTS}/16-login-oscuro.png` });
  await paginaOscura.fill('#usuario', 'cliente');
  await paginaOscura.fill('#password', 'Demo1234');
  await paginaOscura.click('button:has-text("Ingresar")');
  await paginaOscura.waitForSelector('.grilla-productos');
  await paginaOscura.waitForTimeout(500);
  await paginaOscura.screenshot({ path: `${SHOTS}/17-catalogo-oscuro.png` });
  const tema = await paginaOscura.evaluate(() => document.documentElement.getAttribute('data-bs-theme'));
  console.log('Tema con el teléfono en modo oscuro:', tema);
  if (tema !== 'dark') throw new Error('No se aplicó el modo oscuro');
  await oscuro.close();

  console.log('\nProblemas de consola/red:', problemas.length);
  problemas.slice(0, 15).forEach(p => console.log('  ' + p));
  await browser.close();
})().catch(e => { console.error('FALLÓ:', e.message); process.exit(1); });
