using Chiron.Domain.Comandas;
using Chiron.Domain.Common;

namespace Chiron.Application.Comandas;

/// <summary>
/// Caso de uso: el mesero envía una comanda a cocina. Valida los datos, asigna el
/// siguiente folio y persiste la comanda en estado Recibida.
/// </summary>
public sealed class EnviarComanda
{
    private readonly IComandaRepository _comandas;

    public EnviarComanda(IComandaRepository comandas) => _comandas = comandas;

    /// <summary>
    /// Envía una comanda con los ítems indicados.
    /// </summary>
    /// <param name="cafeteriaId">Tenant dueño de la comanda (del claim JWT).</param>
    /// <param name="mesa">Identificador de la mesa o referencia del pedido.</param>
    /// <param name="meseroId">Id del usuario mesero (del claim JWT).</param>
    /// <param name="meseroNombre">Nombre del mesero para mostrar en cocina/caja.</param>
    /// <param name="items">Líneas del pedido: qué producto, cuánto, a qué precio y nota opcional.</param>
    public async Task<Result<Guid>> EjecutarAsync(
        Guid cafeteriaId,
        string mesa,
        Guid meseroId,
        string meseroNombre,
        IEnumerable<(Guid productoId, string nombre, int cantidad, decimal precio, string? nota)> items,
        CancellationToken cancellationToken = default)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<Guid>.Falla("La comanda debe pertenecer a una cafetería válida.");
        if (meseroId == Guid.Empty)
            return Result<Guid>.Falla("El mesero es obligatorio.");
        if (string.IsNullOrWhiteSpace(mesa))
            return Result<Guid>.Falla("La mesa es obligatoria.");

        var listaItems = items?.ToList()
            ?? new List<(Guid, string, int, decimal, string?)>();

        if (listaItems.Count == 0)
            return Result<Guid>.Falla("La comanda debe tener al menos una línea.");

        // Construir las líneas del dominio a partir de la tupla recibida.
        var lineas = listaItems.Select(i =>
            new LineaComanda(i.productoId, i.nombre, i.cantidad, i.precio, i.nota)).ToList();

        // Obtener el folio consecutivo antes de crear la entidad (así el folio es definitivo).
        int folio = await _comandas.SiguienteFolioAsync(cafeteriaId, cancellationToken);

        Result<Comanda> resultado = Comanda.Crear(cafeteriaId, folio, mesa, meseroId, meseroNombre, lineas);
        if (!resultado.EsExito)
            return Result<Guid>.Falla(resultado.Error!);

        Comanda comanda = resultado.Valor!;
        await _comandas.AgregarAsync(comanda, cancellationToken);

        return Result<Guid>.Exito(comanda.Id);
    }
}
