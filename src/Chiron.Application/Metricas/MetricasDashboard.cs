using Chiron.Application.Common;
using Chiron.Application.PuntoVenta;

namespace Chiron.Application.Metricas;

/// <summary>Métricas del dashboard, calculadas en el servidor.</summary>
public sealed record MetricasDashboardDto(
    decimal VentasHoy,
    decimal VentasMes,
    int NumeroVentasMes);

/// <summary>
/// Alcance de las métricas de dinero que puede ver quien consulta el dashboard.
/// - Ninguno: no ve ventas (Cocina/Mesero).
/// - SoloHoy: ve solo la venta del día, su caja (Caja).
/// - Completo: ve ventas del día y del mes, lo que genera el negocio (Admin).
/// </summary>
public enum AlcanceMetricas
{
    Ninguno = 0,
    SoloHoy = 1,
    Completo = 2,
}

/// <summary>
/// Caso de uso: reunir las métricas del dashboard de una cafetería. Todo el cálculo
/// (sumas, conteos, filtros por fecha) se hace en el servidor. Las métricas de dinero se
/// ENTREGAN según el alcance del rol (Mesero/Cocina no las reciben; Caja solo la del día;
/// el Admin todo).
/// </summary>
public sealed class MetricasDashboard
{
    private readonly IVentaRepository _ventas;

    public MetricasDashboard(IVentaRepository ventas) => _ventas = ventas;

    public async Task<MetricasDashboardDto> EjecutarAsync(
        Guid cafeteriaId, AlcanceMetricas alcance, CancellationToken cancellationToken = default)
    {
        DateTime ahora = DateTime.UtcNow;
        // Cortes de día y mes en hora de México (en UTC, las ventas después de las 6 pm
        // contaban para el día siguiente).
        DateTime inicioDia = HoraMexico.InicioDeHoyUtc();
        DateTime inicioMes = HoraMexico.InicioDeMesUtc();

        decimal ventasHoy = 0m, ventasMes = 0m;
        int numeroVentasMes = 0;
        if (alcance != AlcanceMetricas.Ninguno)
        {
            var ventasMesLista = await _ventas.ListarPorCafeteriaAsync(cafeteriaId, inicioMes, ahora, cancellationToken);
            ventasHoy = ventasMesLista.Where(v => v.FechaHora >= inicioDia).Sum(v => v.Total);
            // El mes/acumulado solo lo ve el Admin (alcance Completo).
            if (alcance == AlcanceMetricas.Completo)
            {
                ventasMes = ventasMesLista.Sum(v => v.Total);
                numeroVentasMes = ventasMesLista.Count;
            }
        }

        return new MetricasDashboardDto(
            VentasHoy: ventasHoy,
            VentasMes: ventasMes,
            NumeroVentasMes: numeroVentasMes);
    }
}
