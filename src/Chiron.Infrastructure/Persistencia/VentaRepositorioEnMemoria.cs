using Chiron.Application.PuntoVenta;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Infrastructure.Persistencia;

/// <summary>
/// Implementación en memoria de IVentaRepository. Filtro por CafeteriaId y rango de fechas.
/// </summary>
public sealed class VentaRepositorioEnMemoria : RepositorioEnMemoria<Venta>, IVentaRepository
{
    public async Task<IReadOnlyList<Venta>> ListarPorCafeteriaAsync(
        Guid cafeteriaId, DateTime desde, DateTime hasta, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Venta> todas = await ObtenerTodosAsync(cancellationToken);
        return todas
            .Where(v => v.CafeteriaId == cafeteriaId && v.FechaHora >= desde && v.FechaHora <= hasta)
            .OrderByDescending(v => v.FechaHora)
            .ToList();
    }
}
