namespace Chiron.Domain.Comandas;

/// <summary>
/// Estado de una comanda en el flujo Mesero -> Cocina -> Caja. Lista cerrada para que
/// el polling y los filtros de pantalla sean consistentes entre estaciones.
/// </summary>
public enum EstadoComanda
{
    /// <summary>El mesero la envió; cocina aún no la toma.</summary>
    Recibida = 1,

    /// <summary>Cocina la está preparando.</summary>
    EnPreparacion = 2,

    /// <summary>Lista para entregar/cobrar.</summary>
    Lista = 3,

    /// <summary>Entregada y cobrada (sale del tablero activo).</summary>
    Entregada = 4,

    /// <summary>Cancelada (sale del tablero activo).</summary>
    Cancelada = 5
}
