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
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 1, isMobile: true, hasTouch: true, acceptDownloads: true });
  const page = await context.newPage();
  const problemas = [];
  page.on('console', m => { if (['error', 'warning'].includes(m.type())) problemas.push(`[${m.type()}] ${m.text()}`); });
  page.on('pageerror', e => problemas.push('[pageerror] ' + e.message));
  page.on('requestfailed', r => problemas.push('[requestfailed] ' + r.url() + ' ' + r.failure()?.errorText));
  const paso = async (n) => { await page.screenshot({ path: `${SHOTS}/${n}.png` }); console.log('OK  ' + n); };
  const login = async (u, p) => {
    await page.goto(WEB); await page.waitForSelector('#usuario', { timeout: 60000 });
    await page.fill('#usuario', u); await page.fill('#password', p); await page.click('button:has-text("Ingresar")');
    await page.waitForSelector('.tabbar');
  };

  await login('admin', 'Admin1234');
  await page.click('.tab:has-text("Panel")');
  await page.waitForSelector('text=Panel de gestión');
  console.log('Sección administración visible para admin:', await page.locator('text=Usuarios y roles').count());
  await paso('20-panel-admin');

  // ---- Producto nuevo ----
  await page.click('a:has-text("Productos")');
  await page.waitForSelector('a[href="admin/productos/nuevo"]');
  await paso('21-productos');
  await page.click('a[href="admin/productos/nuevo"]');
  await page.fill('#nombre', `Producto E2E ${SUFIJO}`);
  await page.fill('#descripcion', 'Creado por la prueba automática');
  await page.fill('#precio', '1234.5');
  await page.fill('#stock', '5');
  await page.click('button:has-text("Crear producto")');
  await page.waitForSelector('.validation-message:has-text("Elija una categoría")');
  await paso('22-validacion-producto');
  await page.selectOption('#categoria', { label: 'Electrónica' });
  await page.click('button:has-text("Crear producto")');
  await page.waitForSelector('h1:has-text("Editar producto")');
  await page.waitForSelector('text=Stock actual');
  await page.setInputFiles('input[type=file]', `${ESC}/producto.png`);
  await page.waitForSelector('.miniatura img.producto-img', { timeout: 20000 });
  console.log('Imagen cargada visible: OK');
  await paso('23-producto-editar');

  // Ajuste de stock
  await page.fill('#ajuste', '-2');
  await page.fill('#motivo', 'Rotura E2E');
  await page.click('button:has-text("Aplicar ajuste")');
  await page.waitForSelector('.badge:has-text("Últimas 3")');
  await paso('24-ajuste-stock');
  await page.fill('#ajuste', '-50');
  await page.click('button:has-text("Aplicar ajuste")');
  await page.waitForSelector('text=Stock insuficiente');
  await paso('25-ajuste-invalido');

  // ---- Compra (entrada) ----
  await page.goto(WEB + '/admin/compras/nueva');
  await page.waitForSelector('#proveedor', { timeout: 30000 });
  await page.selectOption('#proveedor', { index: 1 });
  await page.fill('input[type=search]', `Producto E2E ${SUFIJO}`);
  await page.press('input[type=search]', 'Enter');
  await page.click(`button.list-group-item:has-text("Producto E2E ${SUFIJO}")`);
  await page.fill('input[min="1"]', '10');
  await page.fill('input[min="0.01"]', '500');
  await paso('26-compra-nueva');
  await page.click('button:has-text("Registrar entrada")');
  await page.waitForSelector('h1:has-text("Entradas de mercadería")');
  await page.waitForSelector(`text=Producto E2E ${SUFIJO}`);
  await paso('27-compras');

  // ---- Movimientos ----
  await page.goto(WEB + '/admin/movimientos');
  await page.waitForSelector(`li.list-group-item:has-text("Producto E2E ${SUFIJO}")`, { timeout: 30000 });
  console.log('Movimientos de Producto E2E:', await page.locator(`li.list-group-item:has-text("Producto E2E ${SUFIJO}")`).count());
  await paso('28-movimientos');

  // ---- Ventas: anular la del cliente ----
  await page.goto(WEB + '/admin/ventas');
  await page.waitForSelector('li.list-group-item', { timeout: 30000 });
  await paso('29-ventas');
  if (await page.locator('button:has-text("Anular")').count() > 0) {
    await page.locator('button:has-text("Anular")').first().click();
    await page.waitForSelector('.modal.show');
    await page.click('.modal.show .modal-footer button:has-text("Anular")');
    await page.waitForSelector('.badge.text-bg-danger:has-text("Anulada")');
    await paso('30-venta-anulada');
  }

  // ---- Reportes ----
  await page.goto(WEB + '/admin/reportes');
  await page.waitForSelector('#tipo', { timeout: 30000 });
  for (const tipo of ['stock', 'entradas', 'salidas', 'movimientos']) {
    await page.selectOption('#tipo', tipo);
    const [d] = await Promise.all([page.waitForEvent('download'), page.click('button:has-text("Ver / guardar PDF")')]);
    await d.saveAs(path.join(SHOTS, `reporte-${tipo}.pdf`));
    console.log('Reporte descargado:', d.suggestedFilename());
  }
  await page.fill('#correo', 'gerencia@ejemplo.com');
  await page.click('button:has-text("Enviar reporte")');
  await page.waitForSelector('.alert:has-text("SMTP")');
  await paso('31-reportes');

  // ---- Usuarios y roles ----
  await page.goto(WEB + '/admin/usuarios');
  await page.waitForSelector('h2:has-text("Usuarios")', { timeout: 30000 });
  await page.click('button:has-text("Nuevo")');
  await page.fill('#n-usuario', `op${SUFIJO}`);
  await page.fill('#n-email', `op${SUFIJO}@tienda.local`);
  await page.fill('#n-password', 'secreta1');
  await page.selectOption('#n-rol', 'Operador');
  await page.click('button:has-text("Crear usuario")');
  await page.waitForSelector(`.card:has-text("op${SUFIJO}@tienda.local")`);
  await paso('32-usuarios');
  const tarjeta = page.locator('.card', { hasText: `op${SUFIJO}@tienda.local` });
  await tarjeta.locator('input[type=checkbox]').uncheck();
  await tarjeta.locator('button:has-text("Guardar")').click();
  await page.waitForSelector(`.card:has-text("op${SUFIJO}@tienda.local") .badge.text-bg-danger:has-text("Inactivo")`);
  console.log('Usuario desactivado: OK');
  const propia = page.locator('.card', { hasText: 'admin@tienda.local' });
  await propia.locator('input[type=checkbox]').uncheck();
  await propia.locator('button:has-text("Guardar")').click();
  await page.waitForSelector('.toast.text-bg-danger');
  console.log('Auto-desactivación rechazada:', (await page.locator('.toast.text-bg-danger').first().innerText()));
  await paso('33-usuarios-error');

  // ---- Auditoría y errores ----
  await page.goto(WEB + '/admin/auditoria');
  await page.waitForSelector('li.list-group-item', { timeout: 30000 });
  await page.selectOption('#entidad', 'Productos');
  await page.click('button:has-text("Filtrar")');
  await page.waitForSelector('li.list-group-item:has-text("admin · Crear")');
  await paso('34-auditoria');
  await page.goto(WEB + '/admin/errores');
  await page.waitForSelector('h1:has-text("Registro de errores")');
  await page.waitForTimeout(800);
  await paso('35-errores');

  // ---- Operador: sin sección de administración ----
  await page.click('.tab:has-text("Cuenta")');
  await page.click('button:has-text("Cerrar sesión")');
  await page.waitForSelector('#usuario');
  await login('operador', 'Demo1234');
  await page.click('.tab:has-text("Panel")');
  await page.waitForSelector('text=Panel de gestión');
  console.log('Operador ve "Usuarios y roles":', await page.locator('text=Usuarios y roles').count());
  await paso('36-panel-operador');
  await page.goto(WEB + '/admin/usuarios');
  await page.waitForSelector('text=Acceso restringido', { timeout: 30000 });
  await page.goto(WEB + '/admin/auditoria');
  await page.waitForSelector('text=Acceso restringido', { timeout: 30000 });
  console.log('Operador bloqueado en usuarios y auditoría: OK');
  await paso('37-operador-restringido');

  console.log('\nProblemas de consola/red:', problemas.length);
  problemas.slice(0, 15).forEach(p => console.log('  ' + p));
  await browser.close();
})().catch(e => { console.error('FALLÓ:', e.message.split('\n').slice(0, 6).join('\n')); process.exit(1); });
