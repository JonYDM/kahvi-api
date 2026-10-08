using Chiron.Domain.Common;
using Chiron.Domain.Usuarios;

namespace Chiron.Application.Seguridad;

/// <summary>Datos para que un usuario cambie su propio PIN (autoservicio).</summary>
public sealed record CambiarMiPinComando(Guid UsuarioId, string PinActual, string NuevoPin);

/// <summary>
/// Caso de uso: un usuario autenticado cambia su propio PIN. Verifica el PIN actual
/// antes de cambiarlo (seguridad) y valida el formato del nuevo.
/// </summary>
public sealed class CambiarMiPin
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasheadorContrasena _hasheador;

    public CambiarMiPin(IUsuarioRepository usuarios, IHasheadorContrasena hasheador)
    {
        _usuarios = usuarios;
        _hasheador = hasheador;
    }

    public async Task<Result<bool>> EjecutarAsync(
        CambiarMiPinComando comando, CancellationToken cancellationToken = default)
    {
        Result<bool> pinValido = ValidadorPin.Validar(comando.NuevoPin);
        if (!pinValido.EsExito)
            return pinValido;

        Usuario? usuario = await _usuarios.ObtenerPorIdAsync(comando.UsuarioId, cancellationToken);
        if (usuario is null)
            return Result<bool>.Falla("Usuario no encontrado.");

        if (!_hasheador.Verificar(comando.PinActual, usuario.HashPin))
            return Result<bool>.Falla("El PIN actual es incorrecto.");

        usuario.CambiarHashPin(_hasheador.Hashear(comando.NuevoPin));
        await _usuarios.ActualizarAsync(usuario, cancellationToken);
        return Result<bool>.Exito(true);
    }
}

/// <summary>Acción de gestión sobre un usuario (por Admin/SuperAdmin).</summary>
public enum AccionUsuario
{
    Activar = 1,
    Desactivar = 2,
}

/// <summary>Datos para editar nombre y/o estado de un usuario.</summary>
public sealed record GestionarUsuarioComando(
    Guid UsuarioId,
    string? NuevoNombre,
    AccionUsuario? Accion,
    RolUsuario SolicitanteRol,
    Guid SolicitanteCafeteriaId);

/// <summary>
/// Caso de uso: editar el nombre y/o activar-desactivar un usuario, con las mismas reglas
/// de autorización que el reset de PIN:
///  - Administrador: gestiona su staff (Mesero/Cocina/Caja) de SU cafetería.
///  - SuperAdmin: gestiona Administradores.
/// </summary>
public sealed class GestionarUsuario
{
    private readonly IUsuarioRepository _usuarios;

    public GestionarUsuario(IUsuarioRepository usuarios) => _usuarios = usuarios;

    public async Task<Result<bool>> EjecutarAsync(
        GestionarUsuarioComando comando, CancellationToken cancellationToken = default)
    {
        Usuario? objetivo = await _usuarios.ObtenerPorIdAsync(comando.UsuarioId, cancellationToken);
        if (objetivo is null)
            return Result<bool>.Falla("El usuario indicado no existe.");

        Result<bool> autorizado = Autorizar(comando.SolicitanteRol, comando.SolicitanteCafeteriaId, objetivo);
        if (!autorizado.EsExito)
            return autorizado;

        if (comando.NuevoNombre is not null)
        {
            Result<bool> nombre = objetivo.EditarNombre(comando.NuevoNombre);
            if (!nombre.EsExito) return nombre;
        }

        if (comando.Accion is { } accion)
        {
            if (accion == AccionUsuario.Desactivar)
                objetivo.Desactivar();
            else
                objetivo.Activar();
        }

        await _usuarios.ActualizarAsync(objetivo, cancellationToken);
        return Result<bool>.Exito(true);
    }

    /// <summary>
    /// Regla única de quién puede gestionar a quién (la usan gestionar, detalle y edición de datos).
    /// </summary>
    internal static Result<bool> Autorizar(RolUsuario solicitanteRol, Guid solicitanteCafeteriaId, Usuario objetivo)
    {
        switch (solicitanteRol)
        {
            case RolUsuario.SuperAdmin:
                if (objetivo.Rol != RolUsuario.Administrador)
                    return Result<bool>.Falla("El SuperAdmin solo gestiona Administradores.");
                return Result<bool>.Exito(true);

            case RolUsuario.Administrador:
                if (objetivo.CafeteriaId != solicitanteCafeteriaId)
                    return Result<bool>.Falla("No puedes gestionar usuarios de otra cafetería.");
                if (objetivo.Rol is not (RolUsuario.Mesero or RolUsuario.Cocina or RolUsuario.Caja))
                    return Result<bool>.Falla("No tienes permiso para gestionar ese usuario.");
                return Result<bool>.Exito(true);

            default:
                return Result<bool>.Falla("No tienes permiso para gestionar usuarios.");
        }
    }
}
