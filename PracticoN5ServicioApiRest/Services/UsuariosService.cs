using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PracticoN5ServicioApiRest.Services
{
    public class UsuariosService
    {
        private const int LongitudMinimaPassword = 6;

        private readonly SistemaVentasDbContext _contexto;
        private readonly IConfiguration _configuracion;
        private readonly PasswordHasher<Usuario> _passwordHasher;

        public UsuariosService(SistemaVentasDbContext contexto, IConfiguration configuracion)
        {
            _contexto = contexto;
            _configuracion = configuracion;
            _passwordHasher = new PasswordHasher<Usuario>();
        }

        /// <summary>Valida las credenciales y, si son correctas, genera el token JWT firmado con la expiración de la configuración.</summary>
        /// <param name="nombreUsuario">Nombre de usuario.</param>
        /// <param name="password">Contraseña en texto plano.</param>
        /// <returns>El token JWT con los datos básicos de la sesión, o null si las credenciales son inválidas o el usuario está desactivado.</returns>
        public async Task<LoginResponseDTO?> LoginAsync(string nombreUsuario, string password, CancellationToken cancellationToken = default)
        {
            // Busca el usuario por nombre, insensible a mayúsculas/minúsculas.
            var usuario = await _contexto.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(
                    u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower(),
                    cancellationToken);

            if (usuario == null)
            {
                return null;
            }

            // Compara la contraseña ingresada con el hash guardado en la base de datos.
            var resultadoContraseña = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password);

            if (resultadoContraseña == PasswordVerificationResult.Failed)
            {
                return null;
            }

            // Un usuario desactivado no puede iniciar sesión (se responde igual que una credencial inválida).
            if (!usuario.Activo)
            {
                return null;
            }

            // Claims del usuario que viajan dentro del token.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                new Claim(ClaimTypes.Role, usuario.Rol.Nombre)
            };

            // Clave simétrica de firma, la misma que usa Program.cs para validar.
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuracion["Jwt:Key"]!));

            // Algoritmo de firma HMAC-SHA256.
            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var expiracion = DateTime.UtcNow.AddMinutes(_configuracion.GetValue<int?>("Jwt:ExpirationMinutes") ?? 60);

            // Emisor, claims, expiración (de la configuración) y firma.
            var token = new JwtSecurityToken(
                issuer: _configuracion["Jwt:Issuer"],
                claims: claims,
                expires: expiracion,
                signingCredentials: credentials);

            return new LoginResponseDTO
            {
                // Serializa el token a su forma de string (header.payload.signature).
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                NombreUsuario = usuario.NombreUsuario,
                Rol = usuario.Rol.Nombre,
                ExpiraUtc = expiracion
            };
        }

        public async Task<ICollection<Usuario>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
        {
            return await _contexto.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .OrderBy(u => u.NombreUsuario)
                .ToListAsync(cancellationToken);
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _contexto.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.UsuarioId == id, cancellationToken);
        }

        public async Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default)
        {
            return await _contexto.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower(), cancellationToken);
        }

        public async Task<ICollection<Rol>> ObtenerRolesAsync(CancellationToken cancellationToken = default)
        {
            return await _contexto.Roles
                .AsNoTracking()
                .OrderBy(r => r.RolId)
                .ToListAsync(cancellationToken);
        }

        /// <summary>Datos del usuario autenticado junto con su perfil de cliente (si tiene).</summary>
        public async Task<PerfilUsuarioDTO> ObtenerPerfilAsync(int usuarioId, CancellationToken cancellationToken = default)
        {
            var usuario = await ObtenerPorIdAsync(usuarioId, cancellationToken);

            if (usuario == null)
            {
                throw new KeyNotFoundException($"No se encontró el usuario con ID {usuarioId}.");
            }

            var cliente = await _contexto.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

            return new PerfilUsuarioDTO
            {
                UsuarioId = usuario.UsuarioId,
                NombreUsuario = usuario.NombreUsuario,
                Email = usuario.Email,
                Rol = usuario.Rol.Nombre,
                ClienteId = cliente?.ClienteId,
                Nombre = cliente?.Nombre,
                Apellido = cliente?.Apellido,
                Telefono = cliente?.Telefono,
                Direccion = cliente?.Direccion
            };
        }

        public async Task<PerfilUsuarioDTO> ActualizarPerfilAsync(
            int usuarioId,
            ActualizarPerfilDTO dto,
            CancellationToken cancellationToken = default)
        {
            var usuario = await _contexto.Usuarios
                .FirstOrDefaultAsync(u => u.UsuarioId == usuarioId, cancellationToken);

            if (usuario == null)
            {
                throw new KeyNotFoundException($"No se encontró el usuario con ID {usuarioId}.");
            }

            var cliente = await _contexto.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

            if (cliente == null)
            {
                cliente = new Cliente { UsuarioId = usuarioId, Email = usuario.Email };
                _contexto.Clientes.Add(cliente);
            }

            cliente.Nombre = dto.Nombre;
            cliente.Apellido = dto.Apellido;
            cliente.Telefono = dto.Telefono;
            cliente.Direccion = dto.Direccion;

            await _contexto.SaveChangesAsync(cancellationToken);

            return await ObtenerPerfilAsync(usuarioId, cancellationToken);
        }

        /// <summary>Registro público: crea una cuenta con rol Cliente y su perfil de cliente.</summary>
        public async Task<Usuario> RegistrarClienteAsync(RegistrarUsuarioDTO dto, CancellationToken cancellationToken = default)
        {
            ValidarPassword(dto.Password);
            await ValidarUnicidadAsync(dto.NombreUsuario, dto.Email, null, cancellationToken);

            // El registro público asigna siempre el rol Cliente: los roles del personal los otorga un administrador.
            var usuario = await CrearUsuarioAsync(dto.NombreUsuario, dto.Email, dto.Password, NombresRol.Cliente, cancellationToken);

            await VincularClienteAsync(usuario, dto.Nombre, dto.Apellido, dto.Telefono, dto.Direccion, cancellationToken);
            await _contexto.SaveChangesAsync(cancellationToken);

            return usuario;
        }

        public async Task<Usuario> CrearAsync(CrearUsuarioDTO dto, CancellationToken cancellationToken = default)
        {
            ValidarPassword(dto.Password);
            await ValidarUnicidadAsync(dto.NombreUsuario, dto.Email, null, cancellationToken);

            var usuario = await CrearUsuarioAsync(dto.NombreUsuario, dto.Email, dto.Password, dto.Rol, cancellationToken);

            if (usuario.Rol.Nombre == NombresRol.Cliente)
            {
                await VincularClienteAsync(usuario, null, null, null, null, cancellationToken);
            }

            await _contexto.SaveChangesAsync(cancellationToken);

            return usuario;
        }

        public async Task<Usuario> ActualizarAsync(
            int id,
            ActualizarUsuarioDTO dto,
            int idUsuarioActual,
            CancellationToken cancellationToken = default)
        {
            var rol = await ObtenerRolAsync(dto.Rol, cancellationToken);

            var usuarioExistente = await _contexto.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.UsuarioId == id, cancellationToken);

            if (usuarioExistente == null)
            {
                throw new KeyNotFoundException($"No se encontró el usuario con ID {id}.");
            }

            await ValidarUnicidadAsync(dto.NombreUsuario, dto.Email, id, cancellationToken);

            bool quedaActivo = dto.Activo ?? usuarioExistente.Activo;

            if (id == idUsuarioActual && !quedaActivo)
            {
                throw new InvalidOperationException("No se puede desactivar el propio usuario.");
            }

            // No se puede degradar ni desactivar al único administrador activo del sistema.
            bool dejaDeSerAdministrador = rol.Nombre != NombresRol.Administrador || !quedaActivo;

            if (usuarioExistente.Rol.Nombre == NombresRol.Administrador
                && dejaDeSerAdministrador
                && await EsElUnicoAdministradorAsync(id, cancellationToken))
            {
                throw new InvalidOperationException("No se puede degradar ni desactivar al único administrador del sistema.");
            }

            usuarioExistente.NombreUsuario = dto.NombreUsuario;
            usuarioExistente.Email = dto.Email;
            usuarioExistente.RolId = rol.RolId;
            usuarioExistente.Activo = quedaActivo;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                ValidarPassword(dto.Password);
                usuarioExistente.PasswordHash = _passwordHasher.HashPassword(usuarioExistente, dto.Password);
            }

            await _contexto.SaveChangesAsync(cancellationToken);

            usuarioExistente.Rol = rol;

            return usuarioExistente;
        }

        public async Task<bool> EliminarAsync(int id, int idUsuarioActual, CancellationToken cancellationToken = default)
        {
            if (id == idUsuarioActual)
            {
                throw new InvalidOperationException("No se puede eliminar el propio usuario.");
            }

            var usuario = await _contexto.Usuarios
                .FirstOrDefaultAsync(u => u.UsuarioId == id, cancellationToken);

            if (usuario == null)
            {
                throw new KeyNotFoundException($"No se encontró el usuario con ID {id}.");
            }

            // No se puede eliminar al único administrador del sistema.
            if (await EsElUnicoAdministradorAsync(id, cancellationToken))
            {
                throw new InvalidOperationException("No se puede eliminar al único administrador del sistema.");
            }

            // Si el usuario ya operó en el sistema se conserva su historial: se lo desactiva en lugar de eliminarlo.
            bool tieneHistorial =
                await _contexto.Ventas.AnyAsync(v => v.UsuarioId == id, cancellationToken)
                || await _contexto.Compras.AnyAsync(c => c.UsuarioId == id, cancellationToken)
                || await _contexto.MovimientosStock.AnyAsync(m => m.UsuarioId == id, cancellationToken);

            if (tieneHistorial)
            {
                throw new InvalidOperationException(
                    "No se puede eliminar el usuario porque registró ventas, compras o movimientos de stock. Desactívelo para impedir su acceso.");
            }

            var cliente = await _contexto.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == id, cancellationToken);

            if (cliente != null)
            {
                bool clienteTieneVentas = await _contexto.Ventas
                    .AnyAsync(v => v.ClienteId == cliente.ClienteId, cancellationToken);

                if (clienteTieneVentas)
                {
                    throw new InvalidOperationException(
                        "No se puede eliminar el usuario porque su perfil de cliente posee ventas registradas. Desactívelo para impedir su acceso.");
                }

                _contexto.Clientes.Remove(cliente);
            }

            _contexto.Usuarios.Remove(usuario);
            await _contexto.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task<Usuario> CrearUsuarioAsync(
            string nombreUsuario,
            string email,
            string password,
            string nombreRol,
            CancellationToken cancellationToken)
        {
            var rol = await ObtenerRolAsync(nombreRol, cancellationToken);

            var usuario = new Usuario
            {
                NombreUsuario = nombreUsuario,
                Email = email,
                RolId = rol.RolId,
                Rol = rol,
                PasswordHash = _passwordHasher.HashPassword(new Usuario(), password)
            };

            await _contexto.Usuarios.AddAsync(usuario, cancellationToken);

            return usuario;
        }

        // Crea el perfil de cliente de la cuenta. NO se reutiliza un cliente existente con el mismo correo: sin verificar que
        // el correo pertenece a quien se registra, enlazarlo le daría acceso al historial de otra persona.
        private Task VincularClienteAsync(
            Usuario usuario,
            string? nombre,
            string? apellido,
            string? telefono,
            string? direccion,
            CancellationToken cancellationToken)
        {
            _contexto.Clientes.Add(new Cliente
            {
                Nombre = string.IsNullOrWhiteSpace(nombre) ? usuario.NombreUsuario : nombre,
                Apellido = apellido,
                Telefono = telefono,
                Direccion = direccion,
                Email = usuario.Email,
                Usuario = usuario
            });

            return Task.CompletedTask;
        }

        private async Task<Rol> ObtenerRolAsync(string nombreRol, CancellationToken cancellationToken)
        {
            var rol = await _contexto.Roles
                .FirstOrDefaultAsync(r => r.Nombre == nombreRol, cancellationToken);

            if (rol == null)
            {
                var validos = await _contexto.Roles.OrderBy(r => r.RolId).Select(r => r.Nombre).ToListAsync(cancellationToken);
                throw new ArgumentException($"El rol debe ser uno de: {string.Join(", ", validos)}.", nameof(nombreRol));
            }

            return rol;
        }

        private static void ValidarPassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < LongitudMinimaPassword)
            {
                throw new ArgumentException($"La contraseña debe tener al menos {LongitudMinimaPassword} caracteres.", nameof(password));
            }
        }

        private async Task ValidarUnicidadAsync(
            string? nombreUsuario,
            string? email,
            int? idExcepto,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(nombreUsuario))
            {
                throw new ArgumentException("El nombre de usuario es obligatorio.", nameof(nombreUsuario));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("El correo electrónico es obligatorio.", nameof(email));
            }

            IQueryable<Usuario> consulta = _contexto.Usuarios;

            if (idExcepto.HasValue)
            {
                consulta = consulta.Where(u => u.UsuarioId != idExcepto.Value);
            }

            if (await consulta.AnyAsync(u => u.NombreUsuario.ToLower() == nombreUsuario.ToLower(), cancellationToken))
            {
                throw new InvalidOperationException($"El nombre de usuario '{nombreUsuario}' ya está en uso.");
            }

            if (await consulta.AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken))
            {
                throw new InvalidOperationException($"El correo electrónico '{email}' ya está registrado.");
            }
        }

        // Verdadero si el usuario es administrador activo y no hay otro administrador activo en el sistema.
        private async Task<bool> EsElUnicoAdministradorAsync(int idUsuario, CancellationToken cancellationToken)
        {
            bool esAdministradorActivo = await _contexto.Usuarios
                .AnyAsync(u => u.UsuarioId == idUsuario && u.Activo && u.Rol.Nombre == NombresRol.Administrador, cancellationToken);

            if (!esAdministradorActivo)
            {
                return false;
            }

            return await _contexto.Usuarios
                .CountAsync(u => u.Activo && u.Rol.Nombre == NombresRol.Administrador, cancellationToken) == 1;
        }
    }
}
