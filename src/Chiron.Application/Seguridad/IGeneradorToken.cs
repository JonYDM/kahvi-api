using Chiron.Domain.Usuarios;

namespace Chiron.Application.Seguridad;

/// <summary>
/// Datos que se incluirán en el token para identificar al usuario y autorizar.
/// </summary>
/// <param name="UsuarioId">Id del usuario.</param>
/// <param name="CafeteriaId">Tenant al que pertenece (aislamiento multi-tenant).</param>
/// <param name="NombreUsuario">Identificador de acceso.</param>
/// <param name="Rol">Rol para autorización.</param>
/// <param name="AdminOperativo">Si el Admin de la cafetería puede operar (no solo supervisar).</param>
public sealed record DatosToken(
    Guid UsuarioId, Guid CafeteriaId, string NombreUsuario, RolUsuario Rol,
    bool AdminOperativo = true);

/// <summary>
/// Abstracción para generar tokens JWT firmados. La implementación (con la clave secreta
/// y la librería de JWT) vive en Infrastructure.
/// </summary>
public interface IGeneradorToken
{
    /// <summary>Genera un JWT firmado con los datos del usuario. Devuelve el token y su expiración (UTC).</summary>
    (string token, DateTime expiraEn) Generar(DatosToken datos);
}
