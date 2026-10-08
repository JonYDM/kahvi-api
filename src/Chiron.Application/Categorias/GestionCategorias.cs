using Chiron.Domain.Categorias;
using Chiron.Domain.Common;

namespace Chiron.Application.Categorias;

/// <summary>
/// Caso de uso unificado para la gestión de categorías del menú.
/// El Administrador puede crear, listar, editar y eliminar categorías dinámicas.
/// Todas las operaciones aplican aislamiento multi-tenant (cafeteriaId del token JWT).
/// </summary>
public sealed class GestionCategorias
{
    private readonly ICategoriaRepository _categorias;

    public GestionCategorias(ICategoriaRepository categorias) => _categorias = categorias;

    /// <summary>
    /// Crea una nueva categoría para la cafetería indicada.
    /// </summary>
    /// <param name="cafeteriaId">Tenant dueño (del claim JWT).</param>
    /// <param name="nombre">Nombre de la nueva categoría.</param>
    /// <param name="orden">Posición en el menú.</param>
    /// <returns>Id de la categoría creada.</returns>
    public async Task<Result<Guid>> CrearAsync(Guid cafeteriaId, string nombre, int orden)
    {
        Result<Categoria> resultado = Categoria.Crear(cafeteriaId, nombre, orden);
        if (!resultado.EsExito)
            return Result<Guid>.Falla(resultado.Error!);

        Categoria categoria = resultado.Valor!;
        await _categorias.AgregarAsync(categoria);
        return Result<Guid>.Exito(categoria.Id);
    }

    /// <summary>
    /// Lista todas las categorías de una cafetería ordenadas por Orden.
    /// </summary>
    /// <param name="cafeteriaId">Tenant dueño (del claim JWT).</param>
    /// <returns>Lista de categorías ordenadas.</returns>
    public async Task<Result<IReadOnlyList<Categoria>>> ListarAsync(Guid cafeteriaId)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<IReadOnlyList<Categoria>>.Falla("Token sin cafetería válida.");

        IReadOnlyList<Categoria> lista = await _categorias.ListarPorCafeteriaAsync(cafeteriaId);
        return Result<IReadOnlyList<Categoria>>.Exito(lista);
    }

    /// <summary>
    /// Edita el nombre y orden de una categoría existente.
    /// Verifica que la categoría pertenezca a la cafetería del token (multi-tenant).
    /// </summary>
    /// <param name="cafeteriaId">Tenant del solicitante.</param>
    /// <param name="id">Id de la categoría a editar.</param>
    /// <param name="nombre">Nuevo nombre.</param>
    /// <param name="orden">Nuevo orden.</param>
    /// <returns>True si se actualizó correctamente.</returns>
    public async Task<Result<bool>> EditarAsync(Guid cafeteriaId, Guid id, string nombre, int orden)
    {
        Categoria? categoria = await _categorias.ObtenerPorIdAsync(id);
        if (categoria is null)
            return Result<bool>.Falla("La categoría no existe.");

        // Aislamiento multi-tenant: la categoría debe pertenecer a la misma cafetería.
        if (categoria.CafeteriaId != cafeteriaId)
            return Result<bool>.Falla("La categoría no pertenece a tu cafetería.");

        Result<bool> resultado = categoria.Editar(nombre, orden);
        if (!resultado.EsExito)
            return resultado;

        await _categorias.ActualizarAsync(categoria);
        return Result<bool>.Exito(true);
    }

    /// <summary>
    /// Elimina una categoría. Verifica que pertenezca a la cafetería del token.
    /// </summary>
    /// <param name="cafeteriaId">Tenant del solicitante.</param>
    /// <param name="id">Id de la categoría a eliminar.</param>
    /// <returns>True si se eliminó correctamente.</returns>
    public async Task<Result<bool>> EliminarAsync(Guid cafeteriaId, Guid id)
    {
        Categoria? categoria = await _categorias.ObtenerPorIdAsync(id);
        if (categoria is null)
            return Result<bool>.Falla("La categoría no existe.");

        // Aislamiento multi-tenant: la categoría debe pertenecer a la misma cafetería.
        if (categoria.CafeteriaId != cafeteriaId)
            return Result<bool>.Falla("La categoría no pertenece a tu cafetería.");

        await _categorias.EliminarAsync(id);
        return Result<bool>.Exito(true);
    }
}
