using Chiron.Application.Comandas;
using Chiron.Domain.Comandas;

namespace Chiron.Infrastructure.Persistencia;

/// <summary>
/// Implementación en memoria de IComandaRepository. Singleton para que las comandas
/// persistan durante toda la vida de la aplicación (sin BD real). Usa lock para
/// proteger el acceso concurrente al contador de folios y a la lista interna.
/// </summary>
public sealed class ComandaRepositorioEnMemoria : IComandaRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Comanda> _comandas = new();

    // Contador de folios por cafetería: empieza en 0; se incrementa ANTES de devolver.
    private readonly Dictionary<Guid, int> _folios = new();

    public Task AgregarAsync(Comanda comanda, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comanda);
        lock (_lock)
            _comandas.TryAdd(comanda.Id, comanda);
        return Task.CompletedTask;
    }

    public Task<Comanda?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _comandas.TryGetValue(id, out Comanda? comanda);
            return Task.FromResult(comanda);
        }
    }

    public Task<IReadOnlyList<Comanda>> ListarActivasAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Activas = Recibida, EnPreparacion o Lista (no Entregada ni Cancelada).
            IReadOnlyList<Comanda> activas = _comandas.Values
                .Where(c => c.CafeteriaId == cafeteriaId
                    && c.Estado is EstadoComanda.Recibida
                        or EstadoComanda.EnPreparacion
                        or EstadoComanda.Lista)
                .OrderBy(c => c.CreadaEn)
                .ToList();
            return Task.FromResult(activas);
        }
    }

    public Task ActualizarAsync(Comanda comanda, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comanda);
        lock (_lock)
            _comandas[comanda.Id] = comanda;
        return Task.CompletedTask;
    }

    public Task<int> SiguienteFolioAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Incrementa el contador y devuelve el siguiente folio (empieza en 1).
            if (!_folios.TryGetValue(cafeteriaId, out int actual))
                actual = 0;
            int siguiente = actual + 1;
            _folios[cafeteriaId] = siguiente;
            return Task.FromResult(siguiente);
        }
    }
}
