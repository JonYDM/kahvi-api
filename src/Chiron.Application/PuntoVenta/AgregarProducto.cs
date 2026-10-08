using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;

namespace Chiron.Application.PuntoVenta;

/// <summary>Datos de entrada para agregar un producto al cat√°logo.</summary>
public sealed record AgregarProductoComando(
    Guid CafeteriaId,
    string Nombre,
    CategoriaProducto Categoria,
    decimal Precio,
    int Stock);

/// <summary>
/// Caso de uso: agregar un producto al cat√°logo de la cafeterÌa (H6.1).
/// </summary>
public sealed class AgregarProducto
{
    private readonly IProductoRepository _productos;

    public AgregarProducto(IProductoRepository productos) => _productos = productos;

    public async Task<Result<Guid>> EjecutarAsync(
        AgregarProductoComando comando, CancellationToken cancellationToken = default)
    {
        Result<Producto> resultado = Producto.Crear(
            comando.CafeteriaId, comando.Nombre, comando.Categoria, comando.Precio, comando.Stock);
        if (!resultado.EsExito)
            return Result<Guid>.Falla(resultado.Error!);

        Producto producto = resultado.Valor!;
        await _productos.AgregarAsync(producto, cancellationToken);
        return Result<Guid>.Exito(producto.Id);
    }
}

/// <summary>
/// Caso de uso: listar el cat√°logo de productos de una cafeterÌa.
/// </summary>
public sealed class ListarCatalogo
{
    private readonly IProductoRepository _productos;

    public ListarCatalogo(IProductoRepository productos) => _productos = productos;

    public async Task<IReadOnlyList<Producto>> EjecutarAsync(
        Guid CafeteriaId,
        Chiron.Application.Common.FiltroEstado estado = Chiron.Application.Common.FiltroEstado.Activos,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Producto> todos = await _productos.ListarPorCafeteriaAsync(CafeteriaId, cancellationToken);
        return Chiron.Application.Common.FiltroEstadoExtensiones
            .AplicarFiltro(todos, estado, p => p.Activo)
            .OrderBy(p => p.Nombre)
            .ToList();
    }
}
