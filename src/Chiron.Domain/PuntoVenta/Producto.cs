using Chiron.Domain.Common;

namespace Chiron.Domain.PuntoVenta;

/// <summary>
/// Producto del catálogo de la cafeter�a (alimento, medicina, accesorio, etc.).
/// Multi-tenant (pertenece a una cafeter�a). Diseño rico con fábrica Crear.
/// </summary>
public sealed class Producto : EntidadBase
{
    /// <summary>cafeter�a (tenant) dueña del producto.</summary>
    public Guid CafeteriaId { get; private set; }

    /// <summary>Nombre del producto.</summary>
    public string Nombre { get; private set; }

    /// <summary>Categoría del producto.</summary>
    public CategoriaProducto Categoria { get; private set; }

    /// <summary>Precio de venta unitario. Se almacena como decimal (correcto para dinero).</summary>
    public decimal Precio { get; private set; }

    /// <summary>Existencias disponibles en inventario.</summary>
    public int Stock { get; private set; }

    /// <summary>Indica si el producto está activo en el catálogo (baja lógica).</summary>
    public bool Activo { get; private set; }

    private Producto(Guid CafeteriaId, string nombre, CategoriaProducto categoria, decimal precio, int stock)
    {
        CafeteriaId = CafeteriaId;
        Nombre = nombre;
        Categoria = categoria;
        Precio = precio;
        Stock = stock;
        Activo = true;
    }

    /// <summary>
    /// Crea un Producto validando las reglas de negocio.
    /// </summary>
    public static Result<Producto> Crear(
        Guid CafeteriaId, string nombre, CategoriaProducto categoria, decimal precio, int stock)
    {
        if (CafeteriaId == Guid.Empty)
            return Result<Producto>.Falla("El producto debe pertenecer a una cafeter�a válida.");

        if (string.IsNullOrWhiteSpace(nombre))
            return Result<Producto>.Falla("El nombre del producto es obligatorio.");

        // El precio debe ser positivo. (> 0, no >= 0: un producto no se vende en 0.)
        if (precio <= 0)
            return Result<Producto>.Falla("El precio debe ser mayor que cero.");

        // El stock no puede ser negativo. (< 0 inválido; 0 es válido: producto agotado.)
        if (stock < 0)
            return Result<Producto>.Falla("El stock no puede ser negativo.");

        return Result<Producto>.Exito(new Producto(CafeteriaId, nombre.Trim(), categoria, precio, stock));
    }

    /// <summary>
    /// Descuenta unidades del stock (al vender). Valida que haya existencias suficientes.
    /// </summary>
    public Result<bool> DescontarStock(int cantidad)
    {
        if (cantidad <= 0)
            return Result<bool>.Falla("La cantidad a descontar debe ser mayor que cero.");
        if (cantidad > Stock)
            return Result<bool>.Falla($"Stock insuficiente de '{Nombre}' (disponible: {Stock}).");

        Stock -= cantidad;
        return Result<bool>.Exito(true);
    }

    /// <summary>Aumenta el stock (al reabastecer).</summary>
    public void ReabastecerStock(int cantidad)
    {
        if (cantidad > 0)
            Stock += cantidad;
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
    public Result<bool> ActualizarDatos(string nombre, CategoriaProducto categoria)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Result<bool>.Falla("El nombre del producto es obligatorio.");
        Nombre = nombre.Trim();
        Categoria = categoria;
        return Result<bool>.Exito(true);
    }

    /// <summary>Da de baja lógica el producto (no se elimina para preservar histórico).</summary>
    public void Desactivar() => Activo = false;

    /// <summary>Reactiva un producto dado de baja.</summary>
    public void Activar() => Activo = true;
}
