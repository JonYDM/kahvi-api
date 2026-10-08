using Chiron.Application.PuntoVenta;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Infrastructure.Persistencia;

/// <summary>
/// Implementación en memoria de IProductoRepository. Filtro por CafeteriaId (tenant).
/// </summary>
public sealed class ProductoRepositorioEnMemoria : RepositorioEnMemoria<Producto>, IProductoRepository
{
    public async Task<IReadOnlyList<Producto>> ListarPorCafeteriaAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Producto> todos = await ObtenerTodosAsync(cancellationToken);
        return todos.Where(p => p.CafeteriaId == cafeteriaId).ToList();
    }
}
