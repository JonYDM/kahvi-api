using Chiron.Domain.Comandas;
using Chiron.Domain.Common;

namespace Chiron.Application.Comandas;

/// <summary>
/// Caso de uso: cancelar una comanda. El mesero o el administrador la pueden cancelar
/// siempre que no haya sido entregada ya.
/// </summary>
public sealed class CancelarComanda
{
    private readonly IComandaRepository _comandas;

    public CancelarComanda(IComandaRepository comandas) => _comandas = comandas;

    public async Task<Result<bool>> EjecutarAsync(
        Guid cafeteriaId, Guid comandaId, CancellationToken cancellationToken = default)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<bool>.Falla("Cafetería no válida.");

        Comanda? comanda = await _comandas.ObtenerPorIdAsync(comandaId, cancellationToken);
        if (comanda is null)
            return Result<bool>.Falla("La comanda no existe.");

        // Aislamiento multi-tenant: no operar sobre comandas de otra cafetería.
        if (comanda.CafeteriaId != cafeteriaId)
            return Result<bool>.Falla("La comanda no pertenece a esta cafetería.");

        Result<bool> cancelada = comanda.Cancelar();
        if (!cancelada.EsExito)
            return cancelada;

        await _comandas.ActualizarAsync(comanda, cancellationToken);
        return Result<bool>.Exito(true);
    }
}
