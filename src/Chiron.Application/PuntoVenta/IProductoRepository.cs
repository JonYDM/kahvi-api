using Chiron.Application.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>
/// Repositorio específico de Producto (catálogo), acotado por cafetería.
/// </summary>
public interface IProductoRepository : IRepository<Producto>
{
    /// <summary>Lista el catálogo de productos de una cafetería.</summary>
    Task<IReadOnlyList<Producto>> ListarPorCafeteriaAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default);
}
