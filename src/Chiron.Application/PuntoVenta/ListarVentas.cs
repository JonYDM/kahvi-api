using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>Línea de una venta para exponer por la API.</summary>
public sealed record LineaVentaDto(
    Guid ProductoId,
    string NombreProducto,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);

/// <summary>Venta para exponer por la API (con sus líneas de producto).</summary>
public sealed record VentaDto(
    Guid Id,
    DateTime FechaHora,
    decimal Total,
    MetodoPago MetodoPago,
    decimal? MontoRecibido,
    decimal? Cambio,
    IReadOnlyList<LineaVentaDto> Lineas)
{
    public static VentaDto Desde(Venta v) => new(
        v.Id,
        v.FechaHora,
        v.Total,
        v.MetodoPago,
        v.MontoRecibido,
        v.Cambio,
        v.Lineas.Select(l => new LineaVentaDto(
            l.ProductoId, l.NombreProducto, l.Cantidad, l.PrecioUnitario, l.Subtotal)).ToList());
}

/// <summary>
/// Caso de uso: historial de ventas de una cafetería en un rango de fechas.
/// Si no se indican fechas, usa un rango amplio por defecto (últimos 90 días).
/// </summary>
public sealed class ListarVentas
{
    private readonly IVentaRepository _ventas;

    public ListarVentas(IVentaRepository ventas) => _ventas = ventas;

    public async Task<IReadOnlyList<VentaDto>> EjecutarAsync(
        Guid cafeteriaId, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken = default)
    {
        DateTime hastaReal = hasta ?? DateTime.UtcNow;
        DateTime desdeReal = desde ?? hastaReal.AddDays(-90);
        IReadOnlyList<Venta> ventas = await _ventas.ListarPorCafeteriaAsync(
            cafeteriaId, desdeReal, hastaReal, cancellationToken);
        return ventas.Select(VentaDto.Desde).ToList();
    }
}

/// <summary>Resumen de ventas de un período: total, conteo y desglose por método de pago.</summary>
public sealed record ResumenVentasDto(
    decimal Total,
    int NumeroVentas,
    decimal Efectivo,
    decimal Tarjeta,
    decimal Transferencia,
    decimal TotalProductos);

/// <summary>
/// Caso de uso: resumen de ventas de una cafetería en un rango de fechas. El cálculo
/// (totales y desglose) se hace en el servidor, no en el cliente.
/// </summary>
public sealed class ResumenVentas
{
    private readonly IVentaRepository _ventas;

    public ResumenVentas(IVentaRepository ventas) => _ventas = ventas;

    public async Task<ResumenVentasDto> EjecutarAsync(
        Guid cafeteriaId, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken = default)
    {
        DateTime hastaReal = hasta ?? DateTime.UtcNow;
        DateTime desdeReal = desde ?? hastaReal.AddDays(-30);
        IReadOnlyList<Venta> ventas = await _ventas.ListarPorCafeteriaAsync(
            cafeteriaId, desdeReal, hastaReal, cancellationToken);

        decimal PorMetodo(MetodoPago m) => ventas.Where(v => v.MetodoPago == m).Sum(v => v.Total);

        return new ResumenVentasDto(
            Total: ventas.Sum(v => v.Total),
            NumeroVentas: ventas.Count,
            Efectivo: PorMetodo(MetodoPago.Efectivo),
            Tarjeta: PorMetodo(MetodoPago.Tarjeta),
            Transferencia: PorMetodo(MetodoPago.Transferencia),
            TotalProductos: ventas.Sum(v => v.TotalProductos));
    }
}
