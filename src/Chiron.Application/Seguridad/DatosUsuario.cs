using Chiron.Domain.Common;
using Chiron.Domain.Usuarios;

namespace Chiron.Application.Seguridad;

/// <summary>
/// Detalle de un usuario para el drawer de gestión. La CURP va ENMASCARADA
/// (dato sensible): solo se muestra si existe, nunca completa.
/// </summary>
public sealed record UsuarioDetalleDto(
    Guid Id,
    string NombreUsuario,
    string Nombre,
    string Nombres,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Telefono,
    string? CurpEnmascarada,
    RolUsuario Rol,
    bool Activo,
    Guid CafeteriaId)
{
    public static UsuarioDetalleDto Desde(Usuario u) => new(
        u.Id, u.NombreUsuario, u.Nombre, u.ObtenerNombres(), u.ApellidoPaterno, u.ApellidoMaterno,
        u.Telefono, u.CurpEnmascarada(), u.Rol, u.Activo, u.CafeteriaId);
}

/// <summary>Caso de uso: obtener el detalle de un usuario (mismas reglas que gestionar).</summary>
public sealed class ObtenerDetalleUsuario
{
    private readonly IUsuarioRepository _usuarios;

    public ObtenerDetalleUsuario(IUsuarioRepository usuarios) => _usuarios = usuarios;

    public async Task<Result<UsuarioDetalleDto>> EjecutarAsync(
        Guid usuarioId, RolUsuario solicitanteRol, Guid solicitanteCafeteriaId,
        CancellationToken cancellationToken = default)
    {
        Usuario? u = await _usuarios.ObtenerPorIdAsync(usuarioId, cancellationToken);
        if (u is null)
            return Result<UsuarioDetalleDto>.Falla("El usuario indicado no existe.");

        Result<bool> autorizado = GestionarUsuario.Autorizar(solicitanteRol, solicitanteCafeteriaId, u);
        return autorizado.EsExito
            ? Result<UsuarioDetalleDto>.Exito(UsuarioDetalleDto.Desde(u))
            : Result<UsuarioDetalleDto>.Falla(autorizado.Error!);
    }
}

/// <summary>
/// Datos a editar. CURP: <c>null</c> = conservar, vacía = quitar, valor = reemplazar.
/// El nombre de usuario NO cambia al editar (es su login; cambiarlo lo dejaría fuera).
/// </summary>
public sealed record EditarDatosUsuarioComando(
    Guid UsuarioId,
    string Nombres,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Telefono,
    string? Curp,
    RolUsuario SolicitanteRol,
    Guid SolicitanteCafeteriaId);

/// <summary>Caso de uso: editar los datos personales de un usuario de staff.</summary>
public sealed class EditarDatosUsuario
{
    private readonly IUsuarioRepository _usuarios;

    public EditarDatosUsuario(IUsuarioRepository usuarios) => _usuarios = usuarios;

    public async Task<Result<UsuarioDetalleDto>> EjecutarAsync(
        EditarDatosUsuarioComando c, CancellationToken cancellationToken = default)
    {
        Usuario? u = await _usuarios.ObtenerPorIdAsync(c.UsuarioId, cancellationToken);
        if (u is null)
            return Result<UsuarioDetalleDto>.Falla("El usuario indicado no existe.");

        Result<bool> autorizado = GestionarUsuario.Autorizar(c.SolicitanteRol, c.SolicitanteCafeteriaId, u);
        if (!autorizado.EsExito)
            return Result<UsuarioDetalleDto>.Falla(autorizado.Error!);

        Result<bool> editado = u.EditarDatosPersonales(
            c.Nombres, c.ApellidoPaterno, c.ApellidoMaterno, c.Telefono, c.Curp);
        if (!editado.EsExito)
            return Result<UsuarioDetalleDto>.Falla(editado.Error!);

        await _usuarios.ActualizarAsync(u, cancellationToken);
        return Result<UsuarioDetalleDto>.Exito(UsuarioDetalleDto.Desde(u));
    }
}
