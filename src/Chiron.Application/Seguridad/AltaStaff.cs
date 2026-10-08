using System.Globalization;
using System.Text;
using Chiron.Application.Common;
using Chiron.Domain.Common;
using Chiron.Domain.Usuarios;
using Chiron.Domain.Cafeterias;

namespace Chiron.Application.Seguridad;

/// <summary>
/// Datos para dar de alta a un usuario de staff con sus datos personales:
/// el SuperAdmin crea Administradores y el Administrador crea Mesero/Cocina/Caja.
/// </summary>
public sealed record AltaStaffComando(
    Guid CafeteriaId,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Telefono,
    string? Curp,
    string Pin,
    RolUsuario Rol);

/// <summary>Resultado del alta: el usuario generado se muestra para entregarlo.</summary>
public sealed record UsuarioCreadoDto(Guid Id, string NombreUsuario, string NombreCompleto);

/// <summary>
/// Caso de uso: alta de staff. El nombre de usuario NO se captura: se genera como
/// <c>nombre.apellidopaterno</c> (ver <see cref="GeneradorNombreUsuario"/>).
/// Quién puede crear qué rol lo decide el endpoint (según el token).
/// </summary>
public sealed class AltaStaff
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IRepository<Cafeteria> _cafeterias;
    private readonly IHasheadorContrasena _hasheador;

    public AltaStaff(
        IUsuarioRepository usuarios, IRepository<Cafeteria> cafeterias, IHasheadorContrasena hasheador)
    {
        _usuarios = usuarios;
        _cafeterias = cafeterias;
        _hasheador = hasheador;
    }

    public async Task<Result<UsuarioCreadoDto>> EjecutarAsync(
        AltaStaffComando comando, CancellationToken cancellationToken = default)
    {
        if (comando.Rol is RolUsuario.SuperAdmin)
            return Result<UsuarioCreadoDto>.Falla("Rol no permitido para el alta de staff.");
        if (string.IsNullOrWhiteSpace(comando.Nombre))
            return Result<UsuarioCreadoDto>.Falla("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(comando.ApellidoPaterno))
            return Result<UsuarioCreadoDto>.Falla("El apellido paterno es obligatorio.");

        Result<bool> pinValido = ValidadorPin.Validar(comando.Pin);
        if (!pinValido.EsExito)
            return Result<UsuarioCreadoDto>.Falla(pinValido.Error!);

        Cafeteria? caf = await _cafeterias.ObtenerPorIdAsync(comando.CafeteriaId, cancellationToken);
        if (caf is null)
            return Result<UsuarioCreadoDto>.Falla("La cafetería no existe.");

        string? nombreUsuario = await GenerarDisponibleAsync(comando, cancellationToken);
        if (nombreUsuario is null)
            return Result<UsuarioCreadoDto>.Falla("No se pudo generar un nombre de usuario con esos datos.");

        string nombreCompleto = string.Join(' ', new[] { comando.Nombre, comando.ApellidoPaterno, comando.ApellidoMaterno }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim()));

        Result<Usuario> creado = Usuario.CrearStaff(
            comando.CafeteriaId, nombreUsuario, nombreCompleto, _hasheador.Hashear(comando.Pin), comando.Rol);
        if (!creado.EsExito)
            return Result<UsuarioCreadoDto>.Falla(creado.Error!);

        Usuario usuario = creado.Valor!;
        Result<bool> datos = usuario.AsignarDatosPersonales(
            comando.ApellidoPaterno, comando.ApellidoMaterno, comando.Telefono, comando.Curp);
        if (!datos.EsExito)
            return Result<UsuarioCreadoDto>.Falla(datos.Error!);

        await _usuarios.AgregarAsync(usuario, cancellationToken);
        return Result<UsuarioCreadoDto>.Exito(
            new UsuarioCreadoDto(usuario.Id, usuario.NombreUsuario, usuario.Nombre));
    }

    /// <summary>Recorre los candidatos en orden y devuelve el primero que no exista.</summary>
    private async Task<string?> GenerarDisponibleAsync(AltaStaffComando c, CancellationToken ct)
    {
        foreach (string candidato in GeneradorNombreUsuario.Candidatos(c.Nombre, c.ApellidoPaterno, c.ApellidoMaterno))
        {
            if (await _usuarios.ObtenerPorNombreUsuarioAsync(candidato, ct) is null)
                return candidato;
        }
        return null;
    }
}

/// <summary>
/// Genera nombres de usuario del staff: <c>nombre.apellidopaterno</c> con el PRIMER nombre,
/// minúsculas, sin acentos (ñ→n) y solo letras. Si choca, agrega la inicial del materno
/// y después un número consecutivo (2, 3, …).
/// </summary>
public static class GeneradorNombreUsuario
{
    /// <summary>Máximo de sufijos numéricos a intentar.</summary>
    private const int MaxSufijo = 99;

    public static IEnumerable<string> Candidatos(string nombre, string apellidoPaterno, string? apellidoMaterno)
    {
        string primerNombre = Limpiar(PrimeraPalabra(nombre));
        string paterno = Limpiar(apellidoPaterno);
        if (primerNombre.Length == 0 || paterno.Length == 0)
            yield break;

        string baseUsuario = $"{primerNombre}.{paterno}";
        yield return baseUsuario;

        string materno = Limpiar(apellidoMaterno ?? string.Empty);
        if (materno.Length > 0)
            yield return baseUsuario + materno[0];

        for (int i = 2; i <= MaxSufijo; i++)
            yield return baseUsuario + i.ToString(CultureInfo.InvariantCulture);
    }

    private static string PrimeraPalabra(string texto)
        => texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    /// <summary>Quita acentos/diéresis/tilde de la ñ y deja solo letras a-z (apellidos compuestos se juntan).</summary>
    public static string Limpiar(string texto)
    {
        string descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (char ch in descompuesto)
        {
            if (ch is >= 'a' and <= 'z')
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
