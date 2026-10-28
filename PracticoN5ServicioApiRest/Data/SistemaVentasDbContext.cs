using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Data
{
    public class SistemaVentasDbContext : DbContext
    {
        public SistemaVentasDbContext(DbContextOptions<SistemaVentasDbContext> options) : base(options) { }

        public DbSet<CategoriaProducto> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<Compra> Compras { get; set; }
        public DbSet<DetalleCompra> DetallesCompra { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetallesVenta { get; set; }
        public DbSet<MedioPago> MediosPago { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<MovimientoStock> MovimientosStock { get; set; }
        public DbSet<RegistroAuditoria> RegistrosAuditoria { get; set; }
        public DbSet<RegistroError> RegistrosError { get; set; }

        // Todas las fechas se guardan y se leen como UTC: así el JSON sale con "Z" y la app las convierte a la hora local del dispositivo.
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<FechaUtcConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<FechaUtcNulableConverter>();

            // Solo para las pruebas automáticas: SQLite no puede ordenar columnas decimal, así que allí se guardan como double.
            // Con SQL Server (el proveedor real) esta condición no se cumple y las columnas siguen siendo decimal(18,2).
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                configurationBuilder.Properties<decimal>().HaveConversion<double>();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //1:N Categoría-Productos
            modelBuilder.Entity<Producto>(e =>
                e.HasOne(p => p.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Proveedor-Compra
            modelBuilder.Entity<Compra>(e =>
                e.HasOne(c => c.Proveedor)
                .WithMany(p => p.Compras)
                .HasForeignKey(c => c.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Cliente-Venta
            modelBuilder.Entity<Venta>(e =>
                e.HasOne(v => v.Cliente)
                .WithMany(c => c.Ventas)
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Producto-DetalleCompra
            modelBuilder.Entity<DetalleCompra>(e =>
                e.HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Producto-DetalleVenta
            modelBuilder.Entity<DetalleVenta>(e =>
                e.HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Compra-DetalleCompra
            modelBuilder.Entity<DetalleCompra>(e =>
                e.HasOne(d => d.Compra)
                .WithMany(c => c.Detalles)
                .HasForeignKey(d => d.CompraId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Venta-DetalleVenta
            modelBuilder.Entity<DetalleVenta>(e =>
                e.HasOne(d => d.Venta)
                .WithMany(v => v.Detalles)
                .HasForeignKey(d => d.VentaId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            //1:N Rol-Usuarios (el nombre del rol es único; los roles base se siembran con la migración)
            modelBuilder.Entity<Rol>(e =>
            {
                e.HasIndex(r => r.Nombre).IsUnique();

                e.HasData(
                    new Rol { RolId = 1, Nombre = NombresRol.Administrador, Descripcion = "Acceso total: usuarios, roles, registros de auditoría y errores, además de todo lo del Operador." },
                    new Rol { RolId = 2, Nombre = NombresRol.Operador, Descripcion = "Personal del negocio: catálogo, stock, compras, ventas y reportes." },
                    new Rol { RolId = 3, Nombre = NombresRol.Cliente, Descripcion = "Comprador: consulta el catálogo, usa el carrito y ve sus propios pedidos." });
            });

            modelBuilder.Entity<Usuario>(e =>
            {
                e.HasOne(u => u.Rol)
                    .WithMany(r => r.Usuarios)
                    .HasForeignKey(u => u.RolId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(u => u.NombreUsuario).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
            });

            //1:1 Usuario-Cliente (opcional): un cliente puede tener a lo sumo una cuenta de acceso
            modelBuilder.Entity<Cliente>(e =>
            {
                e.HasOne(c => c.Usuario)
                    .WithOne()
                    .HasForeignKey<Cliente>(c => c.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(c => c.UsuarioId).IsUnique();
            });

            //N:1 Venta/Compra-Usuario (quién registró el documento)
            modelBuilder.Entity<Venta>(e =>
                e.HasOne(v => v.Usuario)
                .WithMany()
                .HasForeignKey(v => v.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            modelBuilder.Entity<Compra>(e =>
                e.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict)
            );

            modelBuilder.Entity<MedioPago>(e =>
            {
                e.HasIndex(m => m.Nombre).IsUnique();

                e.HasData(
                    new MedioPago { MedioPagoId = 1, Nombre = "Tarjeta de crédito", RequiereTarjeta = true },
                    new MedioPago { MedioPagoId = 2, Nombre = "Tarjeta de débito", RequiereTarjeta = true },
                    new MedioPago { MedioPagoId = 3, Nombre = "Transferencia bancaria", RequiereTarjeta = false });
            });

            //1:1 Venta-Pago (una venta tiene a lo sumo un pago, y un pago pertenece a una sola venta)
            modelBuilder.Entity<Pago>(e =>
            {
                e.HasOne(p => p.Venta)
                    .WithOne(v => v.Pago)
                    .HasForeignKey<Pago>(p => p.VentaId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(p => p.MedioPago)
                    .WithMany(m => m.Pagos)
                    .HasForeignKey(p => p.MedioPagoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            //N:1 MovimientoStock-Producto/Compra/Venta/Usuario
            modelBuilder.Entity<MovimientoStock>(e =>
            {
                e.HasOne(m => m.Producto)
                    .WithMany()
                    .HasForeignKey(m => m.ProductoId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(m => m.Compra)
                    .WithMany()
                    .HasForeignKey(m => m.CompraId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(m => m.Venta)
                    .WithMany()
                    .HasForeignKey(m => m.VentaId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(m => m.Usuario)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(m => m.Fecha);
            });

            // Los registros de auditoría y de errores se consultan siempre por fecha.
            modelBuilder.Entity<RegistroAuditoria>(e => e.HasIndex(r => r.Fecha));
            modelBuilder.Entity<RegistroError>(e => e.HasIndex(r => r.Fecha));
        }
    }

    public class FechaUtcConverter : ValueConverter<DateTime, DateTime>
    {
        public FechaUtcConverter() : base(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        { }
    }

    public class FechaUtcNulableConverter : ValueConverter<DateTime?, DateTime?>
    {
        public FechaUtcNulableConverter() : base(
            v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
        { }
    }
}
