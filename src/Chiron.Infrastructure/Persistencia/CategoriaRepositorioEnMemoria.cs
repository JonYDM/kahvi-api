using System.Collections.Concurrent;
using Chiron.Application.Categorias;
using Chiron.Domain.Categorias;

namespace Chiron.Infrastructure.Persistencia;

/// <summary>
/// Implementación en memoria del repositorio de categorías.
/// Singleton: la lista interna persiste durante toda la vida de la aplicación (demo/desarrollo).
/// Usa ConcurrentDictionary para acceso seguro en escenarios concurrentes.
/// </summary>
public sealed class CategoriaRepositorioEnMemoria : ICategoriaRepository
{
    private readonly ConcurrentDictionary<Guid, Categoria> _almacen = new();

    /// <summary>Agrega una nueva categoría al almacén.</summary>
    public Task AgregarAsync(Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        _almacen.TryAdd(categoria.Id, categoria);
        return Task.CompletedTask;
    }

    /// <summary>Obtiene una categoría por su identificador, o null si no existe.</summary>
    public Task<Categoria?> ObtenerPorIdAsync(Guid id)
    {
        _almacen.TryGetValue(id, out Categoria? categoria);
        return Task.FromResult(categoria);
    }

    /// <summary>
    /// Lista las categorías de una cafetería específica, ordenadas por Orden ascendente.
    /// </summary>
    public Task<IReadOnlyList<Categoria>> ListarPorCafeteriaAsync(Guid cafeteriaId)
    {
        IReadOnlyList<Categoria> lista = _almacen.Values
            .Where(c => c.CafeteriaId == cafeteriaId)
            .OrderBy(c => c.Orden)
            .ToList();

        return Task.FromResult(lista);
    }

    /// <summary>Actualiza una categoría existente.</summary>
    public Task ActualizarAsync(Categoria categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        _almacen[categoria.Id] = categoria;
        return Task.CompletedTask;
    }

    /// <summary>Elimina una categoría por su identificador.</summary>
    public Task EliminarAsync(Guid id)
    {
        _almacen.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
