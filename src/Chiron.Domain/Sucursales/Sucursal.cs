using Chiron.Domain.Common;
using Chiron.Domain.Cafeterias;

namespace Chiron.Domain.Sucursales;

/// <summary>
/// Sucursal de una cafeter韆 = unidad de COBRO del SaaS. Cada sucursal paga su propia
/// renta: tiene plan, precio (ajustable por sucursal) y fecha de renovaci贸n.
/// Toda cafeter韆 tiene al menos una: la Matriz (su estado sigue al de la cafeter韆).
/// </summary>
public sealed class Sucursal : EntidadBase
{
    /// <summary>Precio base mensual por sucursal (MXN).</summary>
    public const decimal PrecioMensualBase = 250m;

    /// <summary>Precio base anual por sucursal (MXN): 10 meses, 2 gratis.</summary>
    public const decimal PrecioAnualBase = 2500m;

    public Guid CafeteriaId { get; private set; }
    public string Nombre { get; private set; }
    public string? Direccion { get; private set; }
    public string? Telefono { get; private set; }

    /// <summary>Sucursal principal (se crea con la cafeter韆; no se desactiva por separado).</summary>
    public bool EsMatriz { get; private set; }

    public bool Activa { get; private set; }
    public DateTime FechaAlta { get; private set; }
    public PlanSuscripcion Plan { get; private set; }

    /// <summary>Renta por periodo del plan (por mes si es mensual, por a帽o si es anual).</summary>
    public decimal Precio { get; private set; }

    /// <summary>Fecha en que vence/renueva la suscripci贸n de esta sucursal.</summary>
    public DateOnly FechaRenovacion { get; private set; }

    private Sucursal(Guid CafeteriaId, string nombre, string? direccion, string? telefono,
        bool esMatriz, PlanSuscripcion plan, decimal precio, DateOnly fechaRenovacion)
    {
        CafeteriaId = CafeteriaId;
        Nombre = nombre;
        Direccion = direccion;
        Telefono = telefono;
        EsMatriz = esMatriz;
        Activa = true;
        FechaAlta = DateTime.UtcNow;
        Plan = plan;
        Precio = precio;
        FechaRenovacion = fechaRenovacion;
    }

    // Constructor privado sin par谩metros para EF Core.
    private Sucursal() => Nombre = string.Empty;

    /// <summary>Precio sugerido seg煤n el plan (lo que se precarga en el alta).</summary>
    public static decimal PrecioSugerido(PlanSuscripcion plan)
        => plan == PlanSuscripcion.Anual ? PrecioAnualBase : PrecioMensualBase;

    /// <summary>
    /// Crea una sucursal. Si no se indica precio, usa el sugerido del plan. La primera
    /// renovaci贸n es un periodo despu茅s de hoy (o la fecha indicada, p. ej. la Matriz
    /// heredada de una cafeter韆 existente).
    /// </summary>
    public static Result<Sucursal> Crear(Guid CafeteriaId, string nombre, string? direccion, string? telefono,
        PlanSuscripcion plan, decimal? precio, bool esMatriz = false, DateOnly? fechaRenovacion = null, DateOnly? hoy = null)
    {
        if (CafeteriaId == Guid.Empty)
            return Result<Sucursal>.Falla("La sucursal debe pertenecer a una cafeter韆.");
        Result<bool> datos = Validar(nombre, telefono, plan, precio ?? PrecioSugerido(plan));
        if (!datos.EsExito)
            return Result<Sucursal>.Falla(datos.Error!);

        // "Hoy" lo decide quien llama (hora de M茅xico); UTC solo como respaldo.
        DateOnly dia = hoy ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Result<Sucursal>.Exito(new Sucursal(
            CafeteriaId, nombre.Trim(), Limpiar(direccion), Limpiar(telefono), esMatriz, plan,
            precio ?? PrecioSugerido(plan), fechaRenovacion ?? SiguientePeriodo(dia, plan)));
    }

    /// <summary>Edita datos, plan y precio. No mueve la fecha de renovaci贸n.</summary>
    public Result<bool> Editar(string nombre, string? direccion, string? telefono, PlanSuscripcion plan, decimal precio)
    {
        Result<bool> datos = Validar(nombre, telefono, plan, precio);
        if (!datos.EsExito)
            return datos;
        Nombre = nombre.Trim();
        Direccion = Limpiar(direccion);
        Telefono = Limpiar(telefono);
        Plan = plan;
        Precio = precio;
        return Result<bool>.Exito(true);
    }

    /// <summary>
    /// Renueva un periodo seg煤n el plan: si ya venci贸 cuenta desde hoy; si sigue vigente se
    /// suma al vencimiento (no se pierden d铆as pagados). Reactiva la sucursal.
    /// Devuelve el periodo cubierto por la renovaci贸n.
    /// </summary>
    public (DateOnly Desde, DateOnly Hasta) Renovar(DateOnly hoy)
    {
        DateOnly desde = FechaRenovacion > hoy ? FechaRenovacion : hoy;
        FechaRenovacion = SiguientePeriodo(desde, Plan);
        Activa = true;
        return (desde, FechaRenovacion);
    }

    /// <summary>Ajuste manual de la fecha de renovaci贸n (pr贸rrogas, pagos irregulares).</summary>
    public void AjustarRenovacion(DateOnly nuevaFecha) => FechaRenovacion = nuevaFecha;

    public void Activar() => Activa = true;
    public void Desactivar() => Activa = false;

    private static Result<bool> Validar(string nombre, string? telefono, PlanSuscripcion plan, decimal precio)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Result<bool>.Falla("El nombre de la sucursal es obligatorio.");
        if (nombre.Trim().Length > 120)
            return Result<bool>.Falla("El nombre de la sucursal es demasiado largo.");
        if (!string.IsNullOrWhiteSpace(telefono) && telefono.Trim().Length > 20)
            return Result<bool>.Falla("El tel茅fono no es v谩lido.");
        if (!Enum.IsDefined(plan))
            return Result<bool>.Falla("El plan de suscripci贸n no es v谩lido.");
        if (precio < 0 || precio > 99_999_999m)
            return Result<bool>.Falla("El precio no es v谩lido.");
        return Result<bool>.Exito(true);
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static DateOnly SiguientePeriodo(DateOnly desde, PlanSuscripcion plan)
        => plan == PlanSuscripcion.Anual ? desde.AddYears(1) : desde.AddMonths(1);
}
