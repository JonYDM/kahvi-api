using Chiron.Domain.Common;
using Chiron.Domain.Cafeterias;

namespace Chiron.Domain.Suscripciones;

/// <summary>
/// Pago de suscripción de una sucursal (HU-SU3/SU5): se registra al renovar. De estos pagos
/// salen las ganancias del SaaS. Un pago mal capturado se ANULA (no se borra) para no
/// perder el rastro; los anulados no cuentan en los ingresos.
/// </summary>
public sealed class PagoSuscripcion : EntidadBase
{
    public Guid CafeteriaId { get; private set; }
    public Guid SucursalId { get; private set; }

    /// <summary>Monto cobrado (MXN). Puede diferir del precio de la sucursal (descuento, prórroga).</summary>
    public decimal Monto { get; private set; }

    /// <summary>Día en que se recibió el pago (cuenta para el mes de ingresos).</summary>
    public DateOnly FechaPago { get; private set; }

    /// <summary>Plan con el que se renovó.</summary>
    public PlanSuscripcion Plan { get; private set; }

    /// <summary>Periodo que cubre este pago: [PeriodoDesde, PeriodoHasta).</summary>
    public DateOnly PeriodoDesde { get; private set; }
    public DateOnly PeriodoHasta { get; private set; }

    public string? Nota { get; private set; }
    public bool Anulado { get; private set; }

    /// <summary>Momento (UTC) en que se capturó el pago.</summary>
    public DateTime FechaRegistro { get; private set; }

    private PagoSuscripcion(Guid CafeteriaId, Guid sucursalId, decimal monto, DateOnly fechaPago,
        PlanSuscripcion plan, DateOnly periodoDesde, DateOnly periodoHasta, string? nota)
    {
        CafeteriaId = CafeteriaId;
        SucursalId = sucursalId;
        Monto = monto;
        FechaPago = fechaPago;
        Plan = plan;
        PeriodoDesde = periodoDesde;
        PeriodoHasta = periodoHasta;
        Nota = nota;
        FechaRegistro = DateTime.UtcNow;
    }

    // Constructor privado sin parámetros para EF Core.
    private PagoSuscripcion() { }

    public static Result<PagoSuscripcion> Registrar(Guid CafeteriaId, Guid sucursalId, decimal monto,
        DateOnly fechaPago, PlanSuscripcion plan, DateOnly periodoDesde, DateOnly periodoHasta, string? nota)
    {
        if (CafeteriaId == Guid.Empty || sucursalId == Guid.Empty)
            return Result<PagoSuscripcion>.Falla("El pago debe pertenecer a una sucursal.");
        if (monto < 0 || monto > 99_999_999m)
            return Result<PagoSuscripcion>.Falla("El monto no es válido.");
        if (periodoHasta <= periodoDesde)
            return Result<PagoSuscripcion>.Falla("El periodo del pago no es válido.");
        string? n = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        if (n is { Length: > 250 })
            return Result<PagoSuscripcion>.Falla("La nota es demasiado larga.");

        return Result<PagoSuscripcion>.Exito(new PagoSuscripcion(
            CafeteriaId, sucursalId, monto, fechaPago, plan, periodoDesde, periodoHasta, n));
    }

    /// <summary>Anula el pago (no cuenta en ingresos). No revierte la fecha de renovación.</summary>
    public Result<bool> Anular()
    {
        if (Anulado)
            return Result<bool>.Falla("El pago ya estaba anulado.");
        Anulado = true;
        return Result<bool>.Exito(true);
    }
}
