using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>Datos para editar un producto del catálogo (incluye costo opcional).</summary>
public sealed record EditarProductoComando(
    Guid ProductoId,
    Guid CafeteriaId,
    string Nombre,
    Guid CategoriaId,
    decimal Precio,
    decimal? Costo = null);

/// <summary>
/// Caso de uso: editar nombre, categoría, precio y costo de un producto.
/// Valida existencia y pertenencia a la cafetería (multi-tenant).
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
            return Result<bool>.Falla("El producto no pertenece a tu cafetería.");

        Result<bool> datos = producto.ActualizarDatos(comando.Nombre, comando.CategoriaId);
        if (!datos.EsExito) return datos;

        Result<bool> precio = producto.CambiarPrecio(comando.Precio);
        if (!precio.EsExito) return precio;

        // Actualizar el costo (puede ser null para borrarlo).
        producto.ActualizarCosto(comando.Costo);

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
        Guid productoId, Guid cafeteriaId, CancellationToken cancellationToken = default)
    {
        Producto? producto = await _productos.ObtenerPorIdAsync(productoId, cancellationToken);
        if (producto is null)
            return Result<bool>.Falla("El producto no existe.");
        if (producto.CafeteriaId != cafeteriaId)
            return Result<bool>.Falla("El producto no pertenece a tu cafetería.");

        producto.Desactivar();
        await _productos.ActualizarAsync(producto, cancellationToken);
        return Result<bool>.Exito(true);
    }
}
