using Chiron.Application.Common;
using Chiron.Application.Seguridad;
using Chiron.Application.Sucursales;
using Chiron.Domain.Usuarios;
using Chiron.Domain.Cafeterias;

namespace Chiron.Application.Metricas;

/// <summary>
/// Sucursal con renovación próxima o vencida (para "a quién cobrar").
/// <c>Id</c>/<c>Nombre</c> son de la Cafeteria (compatibilidad); la sucursal va aparte.
/// </summary>
public sealed record RenovacionProximaDto(
    Guid Id,
    string Nombre,
    PlanSuscripcion Plan,
    DateOnly FechaRenovacion,
    int DiasRestantes,
    bool Activa,
    Guid SucursalId,
    string SucursalNombre,
    bool EsMatriz,
    decimal Precio);

/// <summary>
/// Panorama general de la plataforma para el SuperAdmin (calculado en servidor).
/// Los conteos de suscripción (por vencer, vencidas, planes) son por SUCURSAL: es lo que se cobra.
/// </summary>
public sealed record MetricasSuperAdminDto(
    int TotalCafeterias,
    int CafeteriasActivas,
    int CafeteriasInactivas,
    int PorVencer,
    int Vencidas,
    int PlanMensual,
    int PlanAnual,
    int AltasMes,
    int AdministradoresActivos,
    int CafeteriasSinAdmin,
    IReadOnlyList<RenovacionProximaDto> ProximasRenovaciones,
    int TotalSucursales,
    int SucursalesActivas,
    decimal GanadoMes,
    decimal GanadoMesAnterior,
    decimal GanadoHistorico,
    decimal IngresoMensualEsperado,
    decimal MontoPorCobrar,
    int PagosMes);

/// <summary>
/// Caso de uso: métricas globales del SaaS para el SuperAdmin — suscripciones por sucursal
/// (activas, por vencer, vencidas, planes), altas del mes, administradores y Cafeterias
/// activas sin administrador.
/// </summary>
public sealed class MetricasSuperAdmin
{
    /// <summary>Días antes del vencimiento en que una suscripción cuenta como "por vencer".</summary>
    public const int DiasAviso = 7;

    private readonly GestionSucursales _sucursales;
    private readonly IUsuarioRepository _usuarios;

    public MetricasSuperAdmin(GestionSucursales sucursales, IUsuarioRepository usuarios)
    {
        _sucursales = sucursales;
        _usuarios = usuarios;
    }

    public async Task<MetricasSuperAdminDto> EjecutarAsync(CancellationToken cancellationToken = default)
    {
        DateOnly hoy = HoraMexico.Hoy();

        IReadOnlyList<CafeteriaConSucursalesDto> vets = await _sucursales.ListarAsync(cancellationToken);
        IReadOnlyList<Usuario> admins = await _usuarios.ListarPorRolAsync(RolUsuario.Administrador, cancellationToken);

        // Solo cuentan para cobro las sucursales de Cafeterias activas.
        var cobrables = vets
            .Where(v => v.Activa)
            .SelectMany(v => v.Sucursales.Select(s => (Vet: v, Suc: s)))
            .ToList();
        int Dias(SucursalDto s) => s.FechaRenovacion.DayNumber - hoy.DayNumber;

        var vetsConAdmin = admins.Where(a => a.Activo).Select(a => a.CafeteriaId).ToHashSet();

        var proximas = cobrables
            .Where(x => Dias(x.Suc) <= DiasAviso)
            .OrderBy(x => Dias(x.Suc))
            .Take(5)
            .Select(x => new RenovacionProximaDto(
                x.Vet.Id, x.Vet.Nombre, x.Suc.Plan, x.Suc.FechaRenovacion, Dias(x.Suc), x.Suc.Activa,
                x.Suc.Id, x.Suc.Nombre, x.Suc.EsMatriz, x.Suc.Precio))
            .ToList();

        // ── Ingresos (HU-SU4): solo pagos no anulados, por mes de FechaPago ──
        var pagos = (await _sucursales.PagosAsync(cancellationToken)).Where(p => !p.Anulado).ToList();
        DateOnly inicioMes = new(hoy.Year, hoy.Month, 1);
        DateOnly inicioMesAnterior = inicioMes.AddMonths(-1);
        var pagosMes = pagos.Where(p => p.FechaPago >= inicioMes).ToList();

        // Ingreso mensual esperado: renta de las sucursales activas (anuales = precio / 12).
        decimal esperado = cobrables
            .Where(x => x.Suc.Activa)
            .Sum(x => x.Suc.Plan == PlanSuscripcion.Anual ? x.Suc.Precio / 12m : x.Suc.Precio);
        // Por cobrar: renta de las sucursales vencidas o que vencen en la ventana de aviso.
        decimal porCobrar = cobrables.Where(x => Dias(x.Suc) <= DiasAviso).Sum(x => x.Suc.Precio);

        return new MetricasSuperAdminDto(
            TotalCafeterias: vets.Count,
            CafeteriasActivas: vets.Count(v => v.Activa),
            CafeteriasInactivas: vets.Count(v => !v.Activa),
            PorVencer: cobrables.Count(x => Dias(x.Suc) is >= 0 and <= DiasAviso),
            Vencidas: cobrables.Count(x => Dias(x.Suc) < 0),
            PlanMensual: cobrables.Count(x => x.Suc.Plan == PlanSuscripcion.Mensual),
            PlanAnual: cobrables.Count(x => x.Suc.Plan == PlanSuscripcion.Anual),
            AltasMes: vets.Count(v => HoraMexico.EsDelMesActual(v.FechaAlta)),
            AdministradoresActivos: admins.Count(a => a.Activo),
            CafeteriasSinAdmin: vets.Count(v => v.Activa && !vetsConAdmin.Contains(v.Id)),
            ProximasRenovaciones: proximas,
            TotalSucursales: vets.Sum(v => v.Sucursales.Count),
            SucursalesActivas: cobrables.Count(x => x.Suc.Activa),
            GanadoMes: pagosMes.Sum(p => p.Monto),
            GanadoMesAnterior: pagos.Where(p => p.FechaPago >= inicioMesAnterior && p.FechaPago < inicioMes).Sum(p => p.Monto),
            GanadoHistorico: pagos.Sum(p => p.Monto),
            IngresoMensualEsperado: Math.Round(esperado, 2),
            MontoPorCobrar: porCobrar,
            PagosMes: pagosMes.Count);
    }
}
