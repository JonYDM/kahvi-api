using Chiron.Domain.Common;

namespace Chiron.Domain.Categorias;

/// <summary>
/// Categoría dinámica del catálogo de una cafetería.
/// Permite al Administrador organizar los productos del menú (ej. Café, Desayunos, Postres).
/// Multi-tenant: pertenece a una cafetería específica.
/// Diseño rico con fábrica estática Crear que valida las reglas de negocio.
/// </summary>
public sealed class Categoria : EntidadBase
{
    /// <summary>Cafetería (tenant) dueña de la categoría.</summary>
    public Guid CafeteriaId { get; private set; }

    /// <summary>Nombre de la categoría (ej. "Café", "Desayunos").</summary>
    public string Nombre { get; private set; }

    /// <summary>Orden de aparición en el menú. Menor número = aparece primero.</summary>
    public int Orden { get; private set; }

    /// <summary>
    /// Constructor principal. Solo se invoca desde la fábrica Crear.
    /// </summary>
    private Categoria(Guid cafeteriaId, string nombre, int orden)
    {
        CafeteriaId = cafeteriaId;
        Nombre = nombre;
        Orden = orden;
    }

    /// <summary>
    /// Constructor sin parámetros requerido por EF Core para reconstruir entidades desde la BD.
    /// No debe usarse en la lógica de negocio.
    /// </summary>
    private Categoria()
    {
        Nombre = string.Empty;
    }

    /// <summary>
    /// Fábrica estática que crea una Categoría validando las reglas de negocio.
    /// </summary>
    /// <param name="cafeteriaId">Identificador de la cafetería dueña.</param>
    /// <param name="nombre">Nombre de la categoría (no vacío).</param>
    /// <param name="orden">Posición en el menú (0 o mayor).</param>
    /// <returns>Result con la Categoría creada, o un error de validación.</returns>
    public static Result<Categoria> Crear(Guid cafeteriaId, string nombre, int orden)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<Categoria>.Falla("La categoría debe pertenecer a una cafetería válida.");

        if (string.IsNullOrWhiteSpace(nombre))
            return Result<Categoria>.Falla("El nombre de la categoría es obligatorio.");

        if (orden < 0)
            return Result<Categoria>.Falla("El orden debe ser 0 o mayor.");

        return Result<Categoria>.Exito(new Categoria(cafeteriaId, nombre.Trim(), orden));
    }

    /// <summary>
    /// Actualiza el nombre y el orden de la categoría.
    /// </summary>
    /// <param name="nombre">Nuevo nombre (no vacío).</param>
    /// <param name="orden">Nuevo orden (0 o mayor).</param>
    /// <returns>Result con true si se actualizó correctamente, o un error de validación.</returns>
    public Result<bool> Editar(string nombre, int orden)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Result<bool>.Falla("El nombre de la categoría es obligatorio.");

        if (orden < 0)
            return Result<bool>.Falla("El orden debe ser 0 o mayor.");

        Nombre = nombre.Trim();
        Orden = orden;
        return Result<bool>.Exito(true);
    }
}
