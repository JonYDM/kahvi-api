using Chiron.Application.PuntoVenta;
using Chiron.Domain.Comandas;
using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.Comandas;

/// <summary>
/// Caso de uso: cobrar una comanda. La caja convierte las líneas de la comanda en una
/// Venta real y avanza la comanda a Entregada. Solo se puede cobrar una comanda Lista.
///
/// Flujo: obtener comanda -> verificar tenant y estado -> crear Venta -> guardar Venta
/// -> avanzar comanda a Entregada -> actualizar comanda. Si cualquier paso falla,
/// la comanda no se avanza (consistencia).
/// </summary>
public sealed class CobrarComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IVentaRepository _ventas;

    public CobrarComanda(IComandaRepository comandas, IVentaRepository ventas)
    {
        _comandas = comandas;
        _ventas = ventas;
    }

    public async Task<Result<Guid>> EjecutarAsync(
        Guid cafeteriaId,
        Guid comandaId,
        MetodoPago metodoPago,
        decimal? montoRecibido,
        CancellationToken cancellationToken = default)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<Guid>.Falla("Cafetería no válida.");

        Comanda? comanda = await _comandas.ObtenerPorIdAsync(comandaId, cancellationToken);
        if (comanda is null)
            return Result<Guid>.Falla("La comanda no existe.");

        // Aislamiento multi-tenant.
        if (comanda.CafeteriaId != cafeteriaId)
            return Result<Guid>.Falla("La comanda no pertenece a esta cafetería.");

        // Solo se puede cobrar una comanda Lista.
        if (comanda.Estado != EstadoComanda.Lista)
            return Result<Guid>.Falla("Solo se puede cobrar una comanda que esté Lista.");

        // Convertir las líneas de la comanda en líneas de venta (precio tomado de la comanda,
        // no del catálogo actual, para que el histórico sea fiel).
        var lineasVenta = comanda.Lineas
            .Select(l => new LineaVenta(l.ProductoId, l.NombreProducto, l.Cantidad, l.PrecioUnitario))
            .ToList();

        Result<Venta> ventaResult = Venta.Crear(cafeteriaId, lineasVenta, metodoPago, montoRecibido);
        if (!ventaResult.EsExito)
            return Result<Guid>.Falla(ventaResult.Error!);

        Venta venta = ventaResult.Valor!;
        await _ventas.AgregarAsync(venta, cancellationToken);

        // Avanzar la comanda a Entregada (de Lista -> Entregada).
        Result<bool> avanzado = comanda.Avanzar();
        if (!avanzado.EsExito)
        {
            // Esto no debería ocurrir (ya verificamos el estado arriba), pero por seguridad.
            return Result<Guid>.Falla($"No se pudo marcar la comanda como Entregada: {avanzado.Error}");
        }

        await _comandas.ActualizarAsync(comanda, cancellationToken);
        return Result<Guid>.Exito(venta.Id);
    }
}
