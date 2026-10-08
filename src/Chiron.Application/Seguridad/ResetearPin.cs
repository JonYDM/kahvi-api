using Chiron.Application.Common;
using Chiron.Domain.Common;
using Chiron.Domain.Usuarios;

namespace Chiron.Application.Seguridad;

/// <summary>
/// Datos para resetear el PIN de un usuario.
/// </summary>
/// <param name="UsuarioId">Usuario al que se le reinicia el PIN.</param>
/// <param name="NuevoPin">Nuevo PIN (6 dígitos).</param>
/// <param name="SolicitanteRol">Rol de quien solicita el reseteo (del token).</param>
/// <param name="SolicitanteCafeteriaId">Cafetería de quien solicita (del token). Guid.Empty para SuperAdmin.</param>
public sealed record ResetearPinComando(
    Guid UsuarioId,
    string NuevoPin,
    RolUsuario SolicitanteRol,
    Guid SolicitanteCafeteriaId);

/// <summary>
/// Caso de uso: resetear (asignar un nuevo) PIN de un usuario, para recuperar el acceso
/// cuando alguien lo olvida. Aplica reglas de autorización por rol y aislamiento multi-tenant:
///  - Administrador: puede resetear el PIN del staff (Mesero/Cocina/Caja) de SU cafetería.
///    No puede tocar otros Administradores ni SuperAdmin.
///  - SuperAdmin: puede resetear el PIN de los Administradores (de cualquier cafetería).
///
/// El nuevo PIN se valida (6 dígitos) y se guarda hasheado. También se desbloquea al usuario.
/// </summary>
public sealed class ResetearPin
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasheadorContrasena _hasheador;

    public ResetearPin(IUsuarioRepository usuarios, IHasheadorContrasena hasheador)
    {
        _usuarios = usuarios;
        _hasheador = hasheador;
    }

    public async Task<Result<bool>> EjecutarAsync(
        ResetearPinComando comando, CancellationToken cancellationToken = default)
    {
        Result<bool> pinValido = ValidadorPin.Validar(comando.NuevoPin);
        if (!pinValido.EsExito)
            return Result<bool>.Falla(pinValido.Error!);

        Usuario? objetivo = await _usuarios.ObtenerPorIdAsync(comando.UsuarioId, cancellationToken);
        if (objetivo is null)
            return Result<bool>.Falla("El usuario indicado no existe.");

        Result<bool> autorizado = Autorizar(comando, objetivo);
        if (!autorizado.EsExito)
            return autorizado;

        // Asignar el nuevo PIN (hasheado) y desbloquear por si estaba bloqueado.
        objetivo.CambiarHashPin(_hasheador.Hashear(comando.NuevoPin));
        objetivo.RegistrarLoginExitoso(); // reinicia intentos y quita bloqueo
        await _usuarios.ActualizarAsync(objetivo, cancellationToken);

        return Result<bool>.Exito(true);
    }

    /// <summary>Reglas de quién puede resetear a quién.</summary>
    private static Result<bool> Autorizar(ResetearPinComando comando, Usuario objetivo)
    {
        switch (comando.SolicitanteRol)
        {
            case RolUsuario.SuperAdmin:
                // El SuperAdmin gestiona a los Administradores de las cafeterías.
                if (objetivo.Rol != RolUsuario.Administrador)
                    return Result<bool>.Falla("El SuperAdmin solo puede resetear el PIN de Administradores.");
                return Result<bool>.Exito(true);

            case RolUsuario.Administrador:
                // El Administrador solo actúa dentro de SU cafetería.
                if (objetivo.CafeteriaId != comando.SolicitanteCafeteriaId)
                    return Result<bool>.Falla("No puedes resetear usuarios de otra cafetería.");
                // Y solo sobre staff operativo (no otros admins ni superadmin).
                if (objetivo.Rol is not (RolUsuario.Mesero or RolUsuario.Cocina or RolUsuario.Caja))
                    return Result<bool>.Falla("No tienes permiso para resetear el PIN de ese usuario.");
                return Result<bool>.Exito(true);

            default:
                return Result<bool>.Falla("No tienes permiso para resetear PINs.");
        }
    }
}
