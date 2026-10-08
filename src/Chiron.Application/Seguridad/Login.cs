using Chiron.Application.Common;
using Chiron.Domain.Common;
using Chiron.Domain.Usuarios;
using Chiron.Domain.Cafeterias;

namespace Chiron.Application.Seguridad;

/// <summary>Datos de entrada del login: identificador (usuario) + PIN.</summary>
public sealed record LoginComando(string Identificador, string Pin);

/// <summary>Resultado del login: token, expiración y datos básicos del usuario.</summary>
public sealed record LoginResultado(
    string Token, DateTime ExpiraEn, string Nombre, RolUsuario Rol, bool AdminOperativo);

/// <summary>
/// Caso de uso: autenticar con identificador + PIN y emitir un JWT.
/// Incluye bloqueo temporal por intentos fallidos (los PIN son cortos) y validación de
/// suscripción (cafetería activa). Mensajes genéricos para no dar pistas a atacantes.
/// </summary>
public sealed class Login
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasheadorContrasena _hasheador;
    private readonly IGeneradorToken _generadorToken;
    private readonly IRepository<Cafeteria> _cafeterias;

    public Login(
        IUsuarioRepository usuarios,
        IHasheadorContrasena hasheador,
        IGeneradorToken generadorToken,
        IRepository<Cafeteria> cafeterias)
    {
        _usuarios = usuarios;
        _hasheador = hasheador;
        _generadorToken = generadorToken;
        _cafeterias = cafeterias;
    }

    public async Task<Result<LoginResultado>> EjecutarAsync(
        LoginComando comando, CancellationToken cancellationToken = default)
    {
        const string errorGenerico = "Usuario o PIN incorrectos.";

        if (string.IsNullOrWhiteSpace(comando.Identificador) || string.IsNullOrWhiteSpace(comando.Pin))
            return Result<LoginResultado>.Falla(errorGenerico);

        string id = Usuario.NormalizarIdentificador(comando.Identificador);
        Usuario? usuario = await _usuarios.ObtenerPorNombreUsuarioAsync(id, cancellationToken);

        if (usuario is null)
            return Result<LoginResultado>.Falla(errorGenerico);

        if (!usuario.Activo)
            return Result<LoginResultado>.Falla("El usuario está desactivado.");

        // Bloqueo por intentos fallidos.
        if (usuario.EstaBloqueado())
            return Result<LoginResultado>.Falla("Demasiados intentos. Intente de nuevo en unos minutos.");

        // Verificar el PIN.
        if (!_hasheador.Verificar(comando.Pin, usuario.HashPin))
        {
            usuario.RegistrarIntentoFallido();
            await _usuarios.ActualizarAsync(usuario, cancellationToken);
            return Result<LoginResultado>.Falla(errorGenerico);
        }

        // Control de suscripción (el SuperAdmin no depende de una cafetería).
        bool adminOperativo = true;
        if (usuario.Rol != RolUsuario.SuperAdmin)
        {
            Cafeteria? caf = await _cafeterias.ObtenerPorIdAsync(usuario.CafeteriaId, cancellationToken);
            if (caf is null || !caf.Activa)
                return Result<LoginResultado>.Falla("La cafetería está inactiva. Contacte al proveedor.");
            adminOperativo = caf.AdminOperativo;
        }

        // Login exitoso: reiniciar contadores y emitir token.
        usuario.RegistrarLoginExitoso();
        await _usuarios.ActualizarAsync(usuario, cancellationToken);

        var datos = new DatosToken(
            usuario.Id, usuario.CafeteriaId, usuario.NombreUsuario, usuario.Rol, adminOperativo);
        (string token, DateTime expiraEn) = _generadorToken.Generar(datos);

        return Result<LoginResultado>.Exito(
            new LoginResultado(token, expiraEn, usuario.Nombre, usuario.Rol, adminOperativo));
    }
}
