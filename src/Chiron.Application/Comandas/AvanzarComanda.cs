using Chiron.Domain.Comandas;
using Chiron.Domain.Common;

namespace Chiron.Application.Comandas;

/// <summary>
/// Caso de uso: avanza la comanda al siguiente estado del flujo
/// (Recibida -> EnPreparacion -> Lista -> Entregada).
/// Lo usa cocina para tomar una comanda y marcarla lista.
/// </summary>
public sealed class AvanzarComanda
{
    private readonly IComandaRepository _comandas;

    public AvanzarComanda(IComandaRepository comandas) => _comandas = comandas;

    public async Task<Result<EstadoComanda>> EjecutarAsync(
        Guid cafeteriaId, Guid comandaId, CancellationToken cancellationToken = default)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<EstadoComanda>.Falla("Cafetería no válida.");

        Comanda? comanda = await _comandas.ObtenerPorIdAsync(comandaId, cancellationToken);
        if (comanda is null)
            return Result<EstadoComanda>.Falla("La comanda no existe.");

        // Aislamiento multi-tenant: no devolver datos de otra cafetería.
        if (comanda.CafeteriaId != cafeteriaId)
            return Result<EstadoComanda>.Falla("La comanda no pertenece a esta cafetería.");

        Result<bool> avanzado = comanda.Avanzar();
        if (!avanzado.EsExito)
            return Result<EstadoComanda>.Falla(avanzado.Error!);

        await _comandas.ActualizarAsync(comanda, cancellationToken);
        return Result<EstadoComanda>.Exito(comanda.Estado);
    }
}
