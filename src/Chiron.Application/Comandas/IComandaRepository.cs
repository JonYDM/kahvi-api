using Chiron.Domain.Comandas;

namespace Chiron.Application.Comandas;

/// <summary>
/// Repositorio de Comanda: operaciones para el flujo Mesero -> Cocina -> Caja.
/// Solo expone las operaciones que los casos de uso necesitan; sin fugas de EF ni SQL.
/// </summary>
public interface IComandaRepository
{
    /// <summary>Agrega una comanda nueva (recién creada por el mesero).</summary>
    Task AgregarAsync(Comanda comanda, CancellationToken cancellationToken = default);

    /// <summary>Obtiene una comanda por su id, o null si no existe.</summary>
    Task<Comanda?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las comandas activas de una cafetería: Recibida, EnPreparacion y Lista.
    /// Entregadas y Canceladas no aparecen aquí (tablero limpio).
    /// </summary>
    Task<IReadOnlyList<Comanda>> ListarActivasAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default);

    /// <summary>Persiste los cambios de estado o propiedades de una comanda existente.</summary>
    Task ActualizarAsync(Comanda comanda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el siguiente número de folio para la cafetería indicada (auto-incremental
    /// por tenant; el contador empieza en 1 y nunca retrocede).
    /// </summary>
    Task<int> SiguienteFolioAsync(Guid cafeteriaId, CancellationToken cancellationToken = default);
}
