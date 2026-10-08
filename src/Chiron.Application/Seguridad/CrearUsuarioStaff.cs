using Chiron.Domain.Common;
using Chiron.Domain.Usuarios;

namespace Chiron.Application.Seguridad;

/// <summary>Datos para crear un usuario de staff (Administrador, Mesero, Cocina, Caja).</summary>
public sealed record CrearUsuarioStaffComando(
    Guid CafeteriaId,
    string NombreUsuario,
    string Nombre,
    string Pin,
    RolUsuario Rol);

/// <summary>
/// Caso de uso: crear un usuario de staff. Valida el PIN (6 dígitos), que el identificador
/// no exista ya, y hashea el PIN antes de guardar.
/// </summary>
public sealed class CrearUsuarioStaff
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasheadorContrasena _hasheador;

    public CrearUsuarioStaff(IUsuarioRepository usuarios, IHasheadorContrasena hasheador)
    {
        _usuarios = usuarios;
        _hasheador = hasheador;
    }

    public async Task<Result<Guid>> EjecutarAsync(
        CrearUsuarioStaffComando comando, CancellationToken cancellationToken = default)
    {
        Result<bool> pinValido = ValidadorPin.Validar(comando.Pin);
        if (!pinValido.EsExito)
            return Result<Guid>.Falla(pinValido.Error!);

        string idNormalizado = Usuario.NormalizarIdentificador(comando.NombreUsuario);
        Usuario? existente = await _usuarios.ObtenerPorNombreUsuarioAsync(idNormalizado, cancellationToken);
        if (existente is not null)
            return Result<Guid>.Falla("Ya existe un usuario con ese identificador.");

        string hash = _hasheador.Hashear(comando.Pin);
        Result<Usuario> usuarioResult = Usuario.CrearStaff(
            comando.CafeteriaId, comando.NombreUsuario, comando.Nombre, hash, comando.Rol);
        if (!usuarioResult.EsExito)
            return Result<Guid>.Falla(usuarioResult.Error!);

        Usuario usuario = usuarioResult.Valor!;
        await _usuarios.AgregarAsync(usuario, cancellationToken);
        return Result<Guid>.Exito(usuario.Id);
    }
}
