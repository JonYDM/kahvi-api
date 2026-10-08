namespace Chiron.Domain.Usuarios;

/// <summary>
/// Rol de un usuario. Define qué puede hacer en el sistema. Los permisos concretos
/// se aplican en la capa de API mediante autorización por rol.
/// </summary>
public enum RolUsuario
{
    /// <summary>Dueño/gerente de la cafetería: acceso total dentro de su tenant.</summary>
    Administrador = 1,

    /// <summary>Mesero: levanta comandas y las envía a cocina.</summary>
    Mesero = 2,

    /// <summary>Cocina: ve las comandas y avanza su preparación.</summary>
    Cocina = 3,

    /// <summary>Caja: cobra las comandas listas y genera la venta.</summary>
    Caja = 4,

    /// <summary>SuperAdmin (dueño de Kahvi): gestiona cafeterías y suscripciones. Por encima de los tenants.</summary>
    SuperAdmin = 99
}
