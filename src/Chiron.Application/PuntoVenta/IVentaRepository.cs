using Chiron.Application.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>
/// Repositorio específico de Venta, acotado por cafetería.
/// </summary>
public interface IVentaRepository : IRepository<Venta>
{
    /// <summary>Lista las ventas de una cafetería en un rango de fechas.</summary>
    Task<IReadOnlyList<Venta>> ListarPorCafeteriaAsync(
        Guid cafeteriaId, DateTime desde, DateTime hasta, CancellationToken cancellationToken = default);
}
