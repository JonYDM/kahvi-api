using Chiron.Application.Common;
using Chiron.Domain.Usuarios;

namespace Chiron.Application.Seguridad;

/// <summary>
/// Repositorio específico de Usuario, con búsqueda por nombre de usuario (identificador
/// de login del staff) y por cafetería (aislamiento multi-tenant).
/// </summary>
public interface IUsuarioRepository : IRepository<Usuario>
{
    /// <summary>Obtiene un usuario por su identificador de acceso (ya normalizado), o null.</summary>
    Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken cancellationToken = default);

    /// <summary>Lista los usuarios de una cafetería (tenant).</summary>
    Task<IReadOnlyList<Usuario>> ListarPorCafeteriaAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default);

    /// <summary>Lista todos los usuarios con un rol dado (ej: Administradores, para el SuperAdmin).</summary>
    Task<IReadOnlyList<Usuario>> ListarPorRolAsync(
        RolUsuario rol, CancellationToken cancellationToken = default);
}
