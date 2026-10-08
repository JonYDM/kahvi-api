using Chiron.Domain.Comandas;
using Chiron.Domain.Common;

namespace Chiron.Application.Comandas;

/// <summary>
/// Caso de uso: listar las comandas activas de una cafetería (Recibida, EnPreparacion, Lista).
/// Es el endpoint que cocina y caja hacen polling para ver el tablero en tiempo real.
/// </summary>
public sealed class ListarComandasActivas
{
    private readonly IComandaRepository _comandas;

    public ListarComandasActivas(IComandaRepository comandas) => _comandas = comandas;

    public async Task<Result<IReadOnlyList<Comanda>>> EjecutarAsync(
        Guid cafeteriaId, CancellationToken cancellationToken = default)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<IReadOnlyList<Comanda>>.Falla("Cafetería no válida.");

        IReadOnlyList<Comanda> activas = await _comandas.ListarActivasAsync(cafeteriaId, cancellationToken);
        return Result<IReadOnlyList<Comanda>>.Exito(activas);
    }
}
