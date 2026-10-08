using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>Datos para editar un producto del catálogo.</summary>
public sealed record EditarProductoComando(
    Guid ProductoId,
    Guid CafeteriaId,
    string Nombre,
    CategoriaProducto Categoria,
    decimal Precio);

/// <summary>
/// Caso de uso: editar nombre, categoría y precio de un producto.
/// Valida existencia y pertenencia a la cafeter�a (multi-tenant).
/// </summary>
public sealed class EditarProducto
{
    private readonly IProductoRepository _productos;

    public EditarProducto(IProductoRepository productos) => _productos = productos;

    public async Task<Result<bool>> EjecutarAsync(
        EditarProductoComando comando, CancellationToken cancellationToken = default)
    {
        Producto? producto = await _productos.ObtenerPorIdAsync(comando.ProductoId, cancellationToken);
        if (producto is null)
            return Result<bool>.Falla("El producto no existe.");
        if (producto.CafeteriaId != comando.CafeteriaId)
            return Result<bool>.Falla("El producto no pertenece a tu cafeter�a.");

        Result<bool> datos = producto.ActualizarDatos(comando.Nombre, comando.Categoria);
        if (!datos.EsExito) return datos;

        Result<bool> precio = producto.CambiarPrecio(comando.Precio);
        if (!precio.EsExito) return precio;

        await _productos.ActualizarAsync(producto, cancellationToken);
        return Result<bool>.Exito(true);
    }
}

/// <summary>Datos para reabastecer stock de un producto.</summary>
public sealed record ReabastecerStockComando(
    Guid ProductoId,
    Guid CafeteriaId,
    int Cantidad);

/// <summary>Caso de uso: aumentar el stock de un producto (reabastecer).</summary>
public sealed class ReabastecerStock
{
    private readonly IProductoRepository _productos;

    public ReabastecerStock(IProductoRepository productos) => _productos = productos;

    public async Task<Result<bool>> EjecutarAsync(
        ReabastecerStockComando comando, CancellationToken cancellationToken = default)
    {
        if (comando.Cantidad <= 0)
            return Result<bool>.Falla("La cantidad a reabastecer debe ser mayor que cero.");

        Producto? producto = await _productos.ObtenerPorIdAsync(comando.ProductoId, cancellationToken);
        if (producto is null)
            return Result<bool>.Falla("El producto no existe.");
        if (producto.CafeteriaId != comando.CafeteriaId)
            return Result<bool>.Falla("El producto no pertenece a tu cafeter�a.");

        producto.ReabastecerStock(comando.Cantidad);
        await _productos.ActualizarAsync(producto, cancellationToken);
        return Result<bool>.Exito(true);
    }
}

/// <summary>
/// Caso de uso: desactivar (dar de baja) un producto del catálogo. Se hace baja lógica
/// para preservar el histórico de ventas que lo referencian.
/// </summary>
public sealed class DesactivarProducto
{
    private readonly IProductoRepository _productos;

    public DesactivarProducto(IProductoRepository productos) => _productos = productos;

    public async Task<Result<bool>> EjecutarAsync(
        Guid productoId, Guid CafeteriaId, CancellationToken cancellationToken = default)
    {
        Producto? producto = await _productos.ObtenerPorIdAsync(productoId, cancellationToken);
        if (producto is null)
            return Result<bool>.Falla("El producto no existe.");
        if (producto.CafeteriaId != CafeteriaId)
            return Result<bool>.Falla("El producto no pertenece a tu cafeter�a.");

        producto.Desactivar();
        await _productos.ActualizarAsync(producto, cancellationToken);
        return Result<bool>.Exito(true);
    }
}
