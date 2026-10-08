using Chiron.Domain.Common;

namespace Chiron.Domain.Comandas;

/// <summary>
/// Comanda = pedido de una mesa en la cafetería. Es el corazón del flujo operativo:
/// el Mesero la levanta (Recibida), Cocina la avanza (EnPreparacion -> Lista) y Caja la
/// cobra (Entregada). Multi-tenant: pertenece a una Cafetería y los flujos la aíslan por ella.
///
/// Diseño rico: constructor privado + fábrica Crear que valida; el avance de estado solo
/// ocurre por métodos del dominio (no se puede saltar ni retroceder arbitrariamente).
/// </summary>
public sealed class Comanda : EntidadBase
{
    private readonly List<LineaComanda> _lineas;

    /// <summary>Cafetería (tenant) dueña de la comanda.</summary>
    public Guid CafeteriaId { get; private set; }

    /// <summary>Folio consecutivo por cafetería (número visible para el personal).</summary>
    public int Folio { get; private set; }

    /// <summary>Mesa o referencia del pedido (ej. "Mesa 4", "Barra", "Para llevar").</summary>
    public string Mesa { get; private set; }

    /// <summary>Mesero que levantó la comanda.</summary>
    public Guid MeseroId { get; private set; }

    /// <summary>Nombre del mesero (para mostrar en cocina/caja sin otra consulta).</summary>
    public string MeseroNombre { get; private set; }

    /// <summary>Líneas de productos solicitados (solo lectura desde fuera).</summary>
    public IReadOnlyList<LineaComanda> Lineas => _lineas;

    /// <summary>Estado actual en el flujo Mesero -> Cocina -> Caja.</summary>
    public EstadoComanda Estado { get; private set; }

    /// <summary>Momento (UTC) en que se creó la comanda.</summary>
    public DateTime CreadaEn { get; private set; }

    /// <summary>Total de la comanda (suma de las líneas).</summary>
    public decimal Total { get; private set; }

    private Comanda(Guid cafeteriaId, int folio, string mesa, Guid meseroId, string meseroNombre, List<LineaComanda> lineas)
    {
        CafeteriaId = cafeteriaId;
        Folio = folio;
        Mesa = mesa;
        MeseroId = meseroId;
        MeseroNombre = meseroNombre;
        _lineas = lineas;
        Estado = EstadoComanda.Recibida;
        CreadaEn = DateTime.UtcNow;
        Total = lineas.Sum(l => l.Subtotal);
    }

    // Constructor privado sin parámetros para EF Core.
    private Comanda()
    {
        Mesa = string.Empty;
        MeseroNombre = string.Empty;
        _lineas = new List<LineaComanda>();
    }

    /// <summary>
    /// Crea una Comanda validando las reglas de negocio: debe pertenecer a una cafetería,
    /// tener mesa, un mesero y al menos una línea.
    /// </summary>
    public static Result<Comanda> Crear(
        Guid cafeteriaId, int folio, string mesa, Guid meseroId, string meseroNombre,
        IEnumerable<LineaComanda> lineas)
    {
        if (cafeteriaId == Guid.Empty)
            return Result<Comanda>.Falla("La comanda debe pertenecer a una cafetería válida.");
        if (meseroId == Guid.Empty)
            return Result<Comanda>.Falla("La comanda debe tener un mesero válido.");
        if (string.IsNullOrWhiteSpace(mesa))
            return Result<Comanda>.Falla("La mesa es obligatoria.");

        var listaLineas = lineas?.ToList() ?? new List<LineaComanda>();
        if (listaLineas.Count == 0)
            return Result<Comanda>.Falla("La comanda debe tener al menos una línea.");

        string nombre = string.IsNullOrWhiteSpace(meseroNombre) ? "Mesero" : meseroNombre.Trim();
        return Result<Comanda>.Exito(new Comanda(cafeteriaId, folio, mesa.Trim(), meseroId, nombre, listaLineas));
    }

    /// <summary>
    /// Avanza la comanda al siguiente estado del flujo:
    /// Recibida -> EnPreparacion -> Lista -> Entregada. No avanza si ya está Entregada o Cancelada.
    /// </summary>
    public Result<bool> Avanzar()
    {
        switch (Estado)
        {
            case EstadoComanda.Recibida:
                Estado = EstadoComanda.EnPreparacion;
                return Result<bool>.Exito(true);
            case EstadoComanda.EnPreparacion:
                Estado = EstadoComanda.Lista;
                return Result<bool>.Exito(true);
            case EstadoComanda.Lista:
                Estado = EstadoComanda.Entregada;
                return Result<bool>.Exito(true);
            case EstadoComanda.Entregada:
                return Result<bool>.Falla("La comanda ya fue entregada.");
            case EstadoComanda.Cancelada:
                return Result<bool>.Falla("La comanda está cancelada.");
            default:
                return Result<bool>.Falla("Estado de comanda no válido.");
        }
    }

    /// <summary>Marca la comanda como Entregada (al cobrarla). Solo si estaba Lista.</summary>
    public Result<bool> MarcarEntregada()
    {
        if (Estado == EstadoComanda.Entregada)
            return Result<bool>.Falla("La comanda ya fue entregada.");
        if (Estado == EstadoComanda.Cancelada)
            return Result<bool>.Falla("La comanda está cancelada.");
        if (Estado != EstadoComanda.Lista)
            return Result<bool>.Falla("Solo se puede cobrar una comanda que esté lista.");
        Estado = EstadoComanda.Entregada;
        return Result<bool>.Exito(true);
    }

    /// <summary>Cancela la comanda. No se puede cancelar si ya fue entregada.</summary>
    public Result<bool> Cancelar()
    {
        if (Estado == EstadoComanda.Entregada)
            return Result<bool>.Falla("No se puede cancelar una comanda ya entregada.");
        if (Estado == EstadoComanda.Cancelada)
            return Result<bool>.Falla("La comanda ya está cancelada.");
        Estado = EstadoComanda.Cancelada;
        return Result<bool>.Exito(true);
    }
}
