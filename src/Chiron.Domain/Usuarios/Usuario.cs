using Chiron.Domain.Common;

namespace Chiron.Domain.Usuarios;

/// <summary>
/// Usuario que accede al sistema. Autenticación con identificador + PIN (sin correo):
///  - Staff (Administrador/Mesero/Cocina/Caja): identificador = nombre de usuario.
///  - SuperAdmin: nombre de usuario.
///
/// El PIN se almacena SIEMPRE como hash (BCrypt); nunca en claro. Se incluye bloqueo
/// temporal tras varios intentos fallidos (los PIN son cortos, hay que protegerlos).
/// </summary>
public sealed class Usuario : EntidadBase
{
    /// <summary>Máximo de intentos fallidos antes de bloquear.</summary>
    public const int MaxIntentosFallidos = 5;

    /// <summary>Minutos de bloqueo tras exceder los intentos.</summary>
    public const int MinutosBloqueo = 5;

    /// <summary>Cafetería (tenant). Guid.Empty para SuperAdmin.</summary>
    public Guid CafeteriaId { get; private set; }

    /// <summary>
    /// Identificador de acceso: nombre de usuario del staff.
    /// Único a nivel global. Se normaliza a minúsculas/sin espacios.
    /// </summary>
    public string NombreUsuario { get; private set; }

    /// <summary>Nombre para mostrar del usuario.</summary>
    public string Nombre { get; private set; }

    /// <summary>Hash del PIN (nunca el PIN en claro).</summary>
    public string HashPin { get; private set; }

    /// <summary>Rol del usuario.</summary>
    public RolUsuario Rol { get; private set; }

    /// <summary>Indica si el usuario está activo.</summary>
    public bool Activo { get; private set; }

    /// <summary>Contador de intentos fallidos consecutivos.</summary>
    public int IntentosFallidos { get; private set; }

    /// <summary>Momento (UTC) hasta el cual el usuario está bloqueado, si aplica.</summary>
    public DateTime? BloqueadoHasta { get; private set; }

    /// <summary>Apellido paterno (datos personales del staff; null en usuarios antiguos).</summary>
    public string? ApellidoPaterno { get; private set; }

    /// <summary>Apellido materno (opcional).</summary>
    public string? ApellidoMaterno { get; private set; }

    /// <summary>Teléfono de contacto del staff (no es el identificador de login).</summary>
    public string? Telefono { get; private set; }

    /// <summary>CURP (opcional). Se guarda en mayúsculas y con formato validado.</summary>
    public string? Curp { get; private set; }

    private Usuario(Guid cafeteriaId, string nombreUsuario, string nombre, string hashPin, RolUsuario rol)
    {
        CafeteriaId = cafeteriaId;
        NombreUsuario = nombreUsuario;
        Nombre = nombre;
        HashPin = hashPin;
        Rol = rol;
        Activo = true;
    }

    // Constructor privado sin parámetros para EF Core.
    private Usuario()
    {
        NombreUsuario = string.Empty;
        Nombre = string.Empty;
        HashPin = string.Empty;
    }

    /// <summary>
    /// Crea un usuario de staff (o SuperAdmin) con nombre de usuario y PIN (ya hasheado).
    /// </summary>
    public static Result<Usuario> CrearStaff(
        Guid cafeteriaId, string nombreUsuario, string nombre, string hashPin, RolUsuario rol)
    {
        if (rol != RolUsuario.SuperAdmin && cafeteriaId == Guid.Empty)
            return Result<Usuario>.Falla("El usuario debe pertenecer a una cafetería válida.");

        return CrearInterno(cafeteriaId, nombreUsuario, nombre, hashPin, rol);
    }

    private static Result<Usuario> CrearInterno(
        Guid cafeteriaId, string identificador, string nombre, string hashPin, RolUsuario rol)
    {
        if (string.IsNullOrWhiteSpace(identificador))
            return Result<Usuario>.Falla("El identificador de usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(nombre))
            return Result<Usuario>.Falla("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(hashPin))
            return Result<Usuario>.Falla("El PIN es obligatorio.");

        string idNormalizado = NormalizarIdentificador(identificador);
        var usuario = new Usuario(cafeteriaId, idNormalizado, nombre.Trim(), hashPin, rol);
        return Result<Usuario>.Exito(usuario);
    }

    /// <summary>Indica si el usuario está bloqueado en este momento.</summary>
    public bool EstaBloqueado() => BloqueadoHasta is { } hasta && hasta > DateTime.UtcNow;

    /// <summary>
    /// Registra un intento fallido de login. Bloquea temporalmente al alcanzar el máximo.
    /// </summary>
    public void RegistrarIntentoFallido()
    {
        IntentosFallidos++;
        if (IntentosFallidos >= MaxIntentosFallidos)
        {
            BloqueadoHasta = DateTime.UtcNow.AddMinutes(MinutosBloqueo);
            IntentosFallidos = 0;
        }
    }

    /// <summary>Reinicia los contadores tras un login exitoso.</summary>
    public void RegistrarLoginExitoso()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    /// <summary>Desactiva el usuario.</summary>
    public void Desactivar() => Activo = false;

    /// <summary>Reactiva el usuario.</summary>
    public void Activar() => Activo = true;

    /// <summary>Actualiza el hash del PIN (cambio de PIN).</summary>
    public void CambiarHashPin(string nuevoHash)
    {
        if (!string.IsNullOrWhiteSpace(nuevoHash))
            HashPin = nuevoHash;
    }

    /// <summary>Edita el nombre para mostrar del usuario.</summary>
    public Result<bool> EditarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Result<bool>.Falla("El nombre es obligatorio.");
        Nombre = nombre.Trim();
        return Result<bool>.Exito(true);
    }

    /// <summary>Normaliza el identificador: minúsculas y sin espacios alrededor.</summary>
    public static string NormalizarIdentificador(string identificador)
        => identificador.Trim().ToLowerInvariant();

    /// <summary>Formato oficial de CURP (18 caracteres). El sexo admite H, M o X.</summary>
    private static readonly System.Text.RegularExpressions.Regex PatronCurp =
        new(@"^[A-Z]{4}\d{6}[HMX][A-Z]{5}[A-Z0-9]\d$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Asigna los datos personales del staff. Apellido paterno y teléfono (10 dígitos)
    /// obligatorios; materno y CURP opcionales (la CURP se valida si viene).
    /// </summary>
    public Result<bool> AsignarDatosPersonales(
        string apellidoPaterno, string? apellidoMaterno, string telefono, string? curp)
    {
        if (string.IsNullOrWhiteSpace(apellidoPaterno))
            return Result<bool>.Falla("El apellido paterno es obligatorio.");

        string tel = new string((telefono ?? string.Empty).Where(char.IsDigit).ToArray());
        if (tel.Length != 10)
            return Result<bool>.Falla("El teléfono debe tener 10 dígitos.");

        string? curpNormalizada = string.IsNullOrWhiteSpace(curp) ? null : curp.Trim().ToUpperInvariant();
        if (curpNormalizada is not null && !PatronCurp.IsMatch(curpNormalizada))
            return Result<bool>.Falla("La CURP no tiene un formato válido.");

        ApellidoPaterno = apellidoPaterno.Trim();
        ApellidoMaterno = string.IsNullOrWhiteSpace(apellidoMaterno) ? null : apellidoMaterno.Trim();
        Telefono = tel;
        Curp = curpNormalizada;
        return Result<bool>.Exito(true);
    }

    /// <summary>
    /// Edita nombre(s), apellidos, teléfono y CURP, y recompone el nombre para mostrar.
    /// CURP: <c>null</c> = conservar la actual, cadena vacía = quitarla, valor = reemplazarla.
    /// </summary>
    public Result<bool> EditarDatosPersonales(
        string nombres, string apellidoPaterno, string? apellidoMaterno, string telefono, string? curp)
    {
        if (string.IsNullOrWhiteSpace(nombres))
            return Result<bool>.Falla("El nombre es obligatorio.");

        Result<bool> datos = AsignarDatosPersonales(apellidoPaterno, apellidoMaterno, telefono, curp ?? Curp);
        if (!datos.EsExito)
            return datos;

        Nombre = string.Join(' ', new[] { nombres.Trim(), ApellidoPaterno, ApellidoMaterno }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        return Result<bool>.Exito(true);
    }

    /// <summary>
    /// Nombre(s) de pila. <see cref="Nombre"/> guarda el nombre completo (nombres + apellidos),
    /// así que se deriva quitando los apellidos del final. Usuarios sin apellidos: el Nombre tal cual.
    /// </summary>
    public string ObtenerNombres()
    {
        if (string.IsNullOrWhiteSpace(ApellidoPaterno))
            return Nombre;
        string sufijo = string.Join(' ', new[] { ApellidoPaterno, ApellidoMaterno }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        return Nombre.EndsWith(" " + sufijo, StringComparison.OrdinalIgnoreCase)
            ? Nombre[..^(sufijo.Length + 1)].Trim()
            : Nombre;
    }

    /// <summary>CURP enmascarada para mostrar (4 primeros + 2 últimos), o null si no hay.</summary>
    public string? CurpEnmascarada()
        => Curp is { Length: 18 } c ? $"{c[..4]}{new string('•', 12)}{c[^2..]}" : null;
}
