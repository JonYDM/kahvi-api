using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chiron.Application.Seguridad;
using Microsoft.IdentityModel.Tokens;

namespace Chiron.Infrastructure.Seguridad;

/// <summary>
/// Implementación de IGeneradorToken que emite JWT firmados (HMAC-SHA256).
/// El token incluye claims con el Id, cafetería (tenant), nombre de usuario y rol del usuario.
/// </summary>
public sealed class GeneradorTokenJwt : IGeneradorToken
{
    private readonly JwtOpciones _opciones;

    public GeneradorTokenJwt(JwtOpciones opciones) => _opciones = opciones;

    public (string token, DateTime expiraEn) Generar(DatosToken datos)
    {
        DateTime expiraEn = DateTime.UtcNow.AddMinutes(_opciones.MinutosValidez);

        // Claims: información que viaja dentro del token (el backend la lee para autorizar).
        // cafeteriaId identifica el tenant; el frontend nunca debe enviarlo, siempre viene del token.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, datos.UsuarioId.ToString()),
            new("nombreUsuario", datos.NombreUsuario),
            new("cafeteriaId", datos.CafeteriaId.ToString()),
            new(ClaimTypes.Role, datos.Rol.ToString()),
            new("adminOperativo", datos.AdminOperativo ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            expires: expiraEn,
            signingCredentials: credenciales);

        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiraEn);
    }
}
