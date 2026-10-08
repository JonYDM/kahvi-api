using Chiron.Domain.Categorias;

namespace Chiron.Application.Categorias;

/// <summary>
/// Contrato de persistencia para las categorías del catálogo.
/// Permite al Administrador gestionar las categorías dinámicas del menú.
/// La implementación real vive en la capa de Infraestructura.
/// </summary>
public interface ICategoriaRepository
{
    /// <summary>Agrega una nueva categoría.</summary>
    Task AgregarAsync(Categoria categoria);

    /// <summary>Obtiene una categoría por su identificador, o null si no existe.</summary>
    Task<Categoria?> ObtenerPorIdAsync(Guid id);

    /// <summary>Lista todas las categorías de una cafetería, ordenadas por Orden ascendente.</summary>
    Task<IReadOnlyList<Categoria>> ListarPorCafeteriaAsync(Guid cafeteriaId);

    /// <summary>Actualiza una categoría existente.</summary>
    Task ActualizarAsync(Categoria categoria);

    /// <summary>Elimina una categoría por su identificador.</summary>
    Task EliminarAsync(Guid id);
}
