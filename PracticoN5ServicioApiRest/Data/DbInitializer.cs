using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Data
{
    /// <summary>
    /// Prepara la base al arrancar la API, según la configuración:
    /// Database:AplicarMigraciones (crea/actualiza el esquema con las migraciones de EF Core), Seed:Admin* (primer administrador, necesario porque
    /// el registro público solo crea clientes) y Seed:DatosDemo (catálogo y usuarios de ejemplo para probar la app).
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InicializarAsync(IServiceProvider servicios, CancellationToken cancellationToken = default)
        {
            using var scope = servicios.CreateScope();

            var contexto = scope.ServiceProvider.GetRequiredService<SistemaVentasDbContext>();
            var configuracion = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            if (configuracion.GetValue<bool>("Database:AplicarMigraciones"))
            {
                logger.LogInformation("Aplicando migraciones pendientes...");
                await contexto.Database.MigrateAsync(cancellationToken);
            }
            else if (configuracion.GetValue<bool>("Database:CrearEsquema"))
            {
                // Solo para pruebas automáticas con una base en memoria (SQLite): crea las tablas sin pasar por las migraciones.
                await contexto.Database.EnsureCreatedAsync(cancellationToken);
            }

            await SembrarAdministradorAsync(contexto, configuracion, logger, cancellationToken);

            if (configuracion.GetValue<bool>("Seed:DatosDemo"))
            {
                // Los datos de ejemplo traen usuarios con contraseñas conocidas: nunca se cargan en producción,
                // aunque la configuración (por ejemplo, copiada de la plantilla) lo pida.
                if (scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsProduction())
                {
                    logger.LogWarning("Seed:DatosDemo se ignora en el entorno Production.");
                }
                else
                {
                    await SembrarDatosDemoAsync(contexto, configuracion, logger, cancellationToken);
                }
            }
        }

        private static async Task SembrarAdministradorAsync(
            SistemaVentasDbContext contexto,
            IConfiguration configuracion,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            bool hayAdministrador = await contexto.Usuarios
                .AnyAsync(u => u.Rol.Nombre == NombresRol.Administrador, cancellationToken);

            if (hayAdministrador)
            {
                return;
            }

            string? password = configuracion["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "No hay ningún administrador y Seed:AdminPassword no está configurado: no se creó el usuario inicial.");
                return;
            }

            string nombre = configuracion["Seed:AdminUsuario"] ?? "admin";
            string email = configuracion["Seed:AdminEmail"] ?? "admin@tienda.local";

            await CrearUsuarioAsync(contexto, nombre, email, password, NombresRol.Administrador, cancellationToken);
            await contexto.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Se creó el administrador inicial '{Usuario}'.", nombre);
        }

        private static async Task SembrarDatosDemoAsync(
            SistemaVentasDbContext contexto,
            IConfiguration configuracion,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            string passwordDemo = configuracion["Seed:PasswordDemo"] ?? "Demo1234";

            if (!await contexto.Usuarios.AnyAsync(u => u.NombreUsuario == "operador", cancellationToken))
            {
                await CrearUsuarioAsync(contexto, "operador", "operador@tienda.local", passwordDemo, NombresRol.Operador, cancellationToken);
            }

            if (!await contexto.Usuarios.AnyAsync(u => u.NombreUsuario == "cliente", cancellationToken))
            {
                var cliente = await CrearUsuarioAsync(contexto, "cliente", "cliente@tienda.local", passwordDemo, NombresRol.Cliente, cancellationToken);

                contexto.Clientes.Add(new Cliente
                {
                    Nombre = "Cliente",
                    Apellido = "Demo",
                    Email = cliente.Email,
                    Direccion = "Calle Falsa 123",
                    Usuario = cliente
                });
            }

            if (!await contexto.Proveedores.AnyAsync(cancellationToken))
            {
                contexto.Proveedores.Add(new Proveedor
                {
                    Nombre = "Distribuidora Central S.A.",
                    Telefono = "3814000000",
                    Email = "ventas@distribuidoracentral.test",
                    Direccion = "Av. Siempreviva 742"
                });
            }

            if (!await contexto.Categorias.AnyAsync(cancellationToken) && !await contexto.Productos.AnyAsync(cancellationToken))
            {
                var catalogo = new (string Categoria, (string Nombre, string Descripcion, decimal Precio, int Stock)[] Productos)[]
                {
                    ("Electrónica", new[]
                    {
                        ("Auriculares Bluetooth", "Auriculares inalámbricos con estuche de carga y 20 horas de batería.", 28500m, 25),
                        ("Cargador USB-C 30W", "Cargador rápido compatible con la mayoría de los teléfonos y tablets.", 12900m, 40),
                        ("Parlante portátil", "Parlante resistente al agua con 12 horas de autonomía.", 34900m, 3),
                        ("Teclado inalámbrico", "Teclado compacto en español, conexión USB o Bluetooth.", 19800m, 0)
                    }),
                    ("Hogar", new[]
                    {
                        ("Pava eléctrica 1,7 L", "Corte automático y base giratoria de 360°.", 24500m, 15),
                        ("Juego de sábanas 2 plazas", "Algodón peinado, incluye dos fundas.", 31200m, 12),
                        ("Lámpara de escritorio LED", "Tres intensidades de luz y brazo flexible.", 15400m, 4)
                    }),
                    ("Deportes", new[]
                    {
                        ("Pelota de fútbol N°5", "Pelota cosida a máquina, ideal para césped natural y sintético.", 18900m, 30),
                        ("Botella térmica 1 L", "Acero inoxidable, mantiene el frío 24 horas.", 16700m, 22),
                        ("Soga de saltar", "Mango antideslizante y cable regulable.", 5200m, 50)
                    }),
                    ("Libros", new[]
                    {
                        ("Cuaderno de tapa dura A5", "200 hojas rayadas, papel de 90 g.", 6800m, 60),
                        ("Set de 5 lapiceras de gel", "Tinta de secado rápido, trazo de 0,5 mm.", 4300m, 2)
                    })
                };

                foreach (var (nombreCategoria, productos) in catalogo)
                {
                    var categoria = new CategoriaProducto { Nombre = nombreCategoria };
                    contexto.Categorias.Add(categoria);

                    foreach (var p in productos)
                    {
                        var producto = new Producto
                        {
                            Nombre = p.Nombre,
                            Descripcion = p.Descripcion,
                            Precio = p.Precio,
                            Stock = p.Stock,
                            Categoria = categoria
                        };

                        contexto.Productos.Add(producto);

                        // El stock con el que nace cada producto queda en el libro de movimientos.
                        if (p.Stock > 0)
                        {
                            contexto.MovimientosStock.Add(new MovimientoStock
                            {
                                Fecha = DateTime.UtcNow,
                                Cantidad = p.Stock,
                                Motivo = MotivosMovimiento.StockInicial,
                                Observacion = "Carga inicial de datos de ejemplo",
                                Producto = producto
                            });
                        }
                    }
                }
            }

            await contexto.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Datos de ejemplo cargados (usuarios 'operador' y 'cliente', catálogo y proveedor).");
        }

        private static async Task<Usuario> CrearUsuarioAsync(
            SistemaVentasDbContext contexto,
            string nombreUsuario,
            string email,
            string password,
            string nombreRol,
            CancellationToken cancellationToken)
        {
            var rol = await contexto.Roles.FirstAsync(r => r.Nombre == nombreRol, cancellationToken);

            var usuario = new Usuario
            {
                NombreUsuario = nombreUsuario,
                Email = email,
                RolId = rol.RolId,
                PasswordHash = new PasswordHasher<Usuario>().HashPassword(new Usuario(), password)
            };

            contexto.Usuarios.Add(usuario);

            return usuario;
        }
    }
}
