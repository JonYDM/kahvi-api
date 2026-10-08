using Chiron.Application.Seguridad;
using Chiron.Domain.Usuarios;

namespace Chiron.Infrastructure.Persistencia;

/// <summary>
/// Implementación en memoria de IUsuarioRepository. Singleton para que el seed del
/// Program.cs persista durante toda la vida de la aplicación (sin BD real).
/// </summary>
public sealed class UsuarioRepositorioEnMemoria : RepositorioEnMemoria<Usuario>, IUsuarioRepository
{
    public async Task<Usuario?> ObtenerPorNombreUsuarioAsync(
        string nombreUsuario, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Usuario> todos = await ObtenerTodosAsync(cancellationToken);
        return todos.FirstOrDefault(u => u.NombreUsuario == nombreUsuario);
    }

    public async Task<IReadOnlyList<Usuario>> ListarPorCafeteriaAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Usuario> todos = await ObtenerTodosAsync(cancellationToken);
        return todos.Where(u => u.CafeteriaId == cafeteriaId).ToList();
    }

    public async Task<IReadOnlyList<Usuario>> ListarPorRolAsync(
        RolUsuario rol, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Usuario> todos = await ObtenerTodosAsync(cancellationToken);
        return todos.Where(u => u.Rol == rol).ToList();
    }
}
