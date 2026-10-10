using Chiron.Domain.Comandas;
using Chiron.Domain.Common;

namespace Chiron.Application.Comandas;

/// <summary>
/// Caso de uso: listar las comandas activas de una cafeteria (Recibida, EnPreparacion, Lista).
/// - Cocina/Caja/Admin: ven todas las comandas activas de la cafeteria.
/// - Mesero: solo ve sus propias comandas (filtra por meseroId).
/// Es el endpoint que cocina y caja hacen polling para ver el tablero en tiempo real.
/// </summary>
public sealed class ListarComandasActivas
{
    private readonly IComandaRepository _comandas;

    public ListarComandasActivas(IComandaRepository comandas) => _comandas = comandas;

    public async Task<Result<IReadOnlyList<Comanda>>> EjecutarAsync(
        Guid cafeteriaId,
        Guid? meseroId = null,
        CancellationToken cancellationToken = default)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<IReadOnlyList<Comanda>>.Falla("Cafeteria no valida.");

        IReadOnlyList<Comanda> activas = await _comandas.ListarActivasAsync(cafeteriaId, cancellationToken);

        // Si se especifica meseroId (rol Mesero), filtrar solo sus comandas.
        if (meseroId.HasValue && meseroId.Value != Guid.Empty)
        {
            activas = activas.Where(c => c.MeseroId == meseroId.Value).ToList();
        }

        return Result<IReadOnlyList<Comanda>>.Exito(activas);
    }
}
