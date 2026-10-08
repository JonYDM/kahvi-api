using Chiron.Domain.Common;

namespace Chiron.Domain.PuntoVenta;

/// <summary>
/// Producto del catálogo de la cafetería (alimento, bebida, accesorio, etc.).
/// Multi-tenant (pertenece a una cafetería). Diseño rico con fábrica Crear.
/// El Costo es opcional y solo visible para el Administrador.
/// </summary>
public sealed class Producto : EntidadBase
{
    /// <summary>Cafetería (tenant) dueña del producto.</summary>
    public Guid CafeteriaId { get; private set; }

    /// <summary>Nombre del producto.</summary>
    public string Nombre { get; private set; }

    /// <summary>Categoría del producto (referencia a la entidad dinámica Categoria).</summary>
    public Guid CategoriaId { get; private set; }

    /// <summary>Precio de venta unitario. Se almacena como decimal (correcto para dinero).</summary>
    public decimal Precio { get; private set; }

    /// <summary>
    /// Costo de adquisición o producción. Opcional (null = no capturado).
    /// Solo visible para el Administrador; el mesero y otros roles no lo ven.
    /// </summary>
    public decimal? Costo { get; private set; }

    /// <summary>Indica si el producto está activo en el catálogo (baja lógica).</summary>
    public bool Activo { get; private set; }

    private Producto(Guid cafeteriaId, string nombre, Guid categoriaId, decimal precio, decimal? costo)
    {
        CafeteriaId = cafeteriaId;
        Nombre = nombre;
        CategoriaId = categoriaId;
        Precio = precio;
        Costo = costo;
        Activo = true;
    }

    /// <summary>
    /// Constructor privado sin parámetros requerido por EF Core para reconstruir entidades.
    /// No debe usarse en la lógica de negocio.
    /// </summary>
    private Producto()
    {
        Nombre = string.Empty;
    }

    /// <summary>
    /// Crea un Producto validando las reglas de negocio.
    /// </summary>
    public static Result<Producto> Crear(
        Guid cafeteriaId, string nombre, Guid categoriaId, decimal precio, decimal? costo = null)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<Producto>.Falla("El producto debe pertenecer a una cafetería válida.");

        if (string.IsNullOrWhiteSpace(nombre))
            return Result<Producto>.Falla("El nombre del producto es obligatorio.");

        if (categoriaId == Guid.Empty)
            return Result<Producto>.Falla("La categoria es obligatoria.");

        // El precio debe ser positivo. (> 0: un producto no se vende en 0.)
        if (precio <= 0)
            return Result<Producto>.Falla("El precio debe ser mayor que cero.");

        // El costo, si se proporciona, no puede ser negativo.
        if (costo.HasValue && costo.Value < 0)
            return Result<Producto>.Falla("El costo no puede ser negativo.");

        return Result<Producto>.Exito(new Producto(cafeteriaId, nombre.Trim(), categoriaId, precio, costo));
    }

    /// <summary>Actualiza el precio del producto.</summary>
    public Result<bool> CambiarPrecio(decimal nuevoPrecio)
    {
        if (nuevoPrecio <= 0)
            return Result<bool>.Falla("El precio debe ser mayor que cero.");
        Precio = nuevoPrecio;
        return Result<bool>.Exito(true);
    }

    /// <summary>Actualiza nombre y categoría del producto (edición de catálogo).</summary>
    public Result<bool> ActualizarDatos(string nombre, Guid categoriaId)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Result<bool>.Falla("El nombre del producto es obligatorio.");
        Nombre = nombre.Trim();
        CategoriaId = categoriaId;
        return Result<bool>.Exito(true);
    }

    /// <summary>
    /// Actualiza el costo del producto (solo Admin). Acepta null para borrar el costo.
    /// </summary>
    public void ActualizarCosto(decimal? costo) => Costo = costo;

    /// <summary>Da de baja lógica el producto (no se elimina para preservar histórico).</summary>
    public void Desactivar() => Activo = false;

    /// <summary>Reactiva un producto dado de baja.</summary>
    public void Activar() => Activo = true;
}
