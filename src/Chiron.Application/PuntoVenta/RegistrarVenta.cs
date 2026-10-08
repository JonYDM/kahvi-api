using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>Un renglón solicitado en la venta: qué producto y cuánta cantidad.</summary>
public sealed record ItemVentaComando(Guid ProductoId, int Cantidad);

/// <summary>Datos de entrada para registrar una venta.</summary>
public sealed record RegistrarVentaComando(
    Guid CafeteriaId,
    IReadOnlyList<ItemVentaComando> Items,
    MetodoPago MetodoPago = MetodoPago.Efectivo,
    decimal? MontoRecibido = null);

/// <summary>Resultado de una venta registrada.</summary>
public sealed record VentaResultado(Guid VentaId, decimal Total, decimal? Cambio);

/// <summary>
/// Caso de uso: registrar una venta de mostrador. Cobra productos (con stock) y descuenta
/// el inventario. La venta debe incluir al menos un producto.
/// </summary>
public sealed class RegistrarVenta
{
    private readonly IProductoRepository _productos;
    private readonly IVentaRepository _ventas;

    public RegistrarVenta(IProductoRepository productos, IVentaRepository ventas)
    {
        _productos = productos;
        _ventas = ventas;
    }

    public async Task<Result<VentaResultado>> EjecutarAsync(
        RegistrarVentaComando comando, CancellationToken cancellationToken = default)
    {
        var items = comando.Items ?? new List<ItemVentaComando>();

        if (items.Count == 0)
            return Result<VentaResultado>.Falla("La venta debe incluir al menos un producto.");

        var lineas = new List<LineaVenta>(items.Count);
        var descuentos = new List<(Producto producto, int cantidad)>(items.Count);

        foreach (ItemVentaComando item in items)
        {
            Producto? producto = await _productos.ObtenerPorIdAsync(item.ProductoId, cancellationToken);
            if (producto is null)
                return Result<VentaResultado>.Falla($"El producto {item.ProductoId} no existe.");
            if (producto.CafeteriaId != comando.CafeteriaId)
                return Result<VentaResultado>.Falla("Un producto no pertenece a la cafetería indicada.");
            if (item.Cantidad <= 0)
                return Result<VentaResultado>.Falla($"La cantidad de '{producto.Nombre}' debe ser mayor que cero.");
            if (item.Cantidad > producto.Stock)
                return Result<VentaResultado>.Falla(
                    $"Stock insuficiente de '{producto.Nombre}' (disponible: {producto.Stock}).");

            lineas.Add(new LineaVenta(producto.Id, producto.Nombre, item.Cantidad, producto.Precio));
            descuentos.Add((producto, item.Cantidad));
        }

        // Construir la venta (valida que el monto recibido cubra el total).
        Result<Venta> ventaResult = Venta.Crear(
            comando.CafeteriaId, lineas, comando.MetodoPago, comando.MontoRecibido);
        if (!ventaResult.EsExito)
            return Result<VentaResultado>.Falla(ventaResult.Error!);

        Venta venta = ventaResult.Valor!;

        // Descontar stock de los productos.
        foreach ((Producto producto, int cantidad) in descuentos)
        {
            producto.DescontarStock(cantidad);
            await _productos.ActualizarAsync(producto, cancellationToken);
        }

        await _ventas.AgregarAsync(venta, cancellationToken);

        return Result<VentaResultado>.Exito(new VentaResultado(venta.Id, venta.Total, venta.Cambio));
    }
}
