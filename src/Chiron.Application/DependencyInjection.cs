using Chiron.Application.Categorias;
using Chiron.Application.Comandas;
using Chiron.Application.PuntoVenta;
using Chiron.Application.Seguridad;
using Microsoft.Extensions.DependencyInjection;

namespace Chiron.Application;

/// <summary>
/// Punto único de registro de dependencias de la capa de Aplicación.
/// Cada capa es responsable de registrar sus propios servicios (SOLID: SRP).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra los casos de uso de la capa de Aplicación.
    /// Se registran como Transient: son operaciones sin estado, se crea una por uso.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ── Categorías dinámicas del menú ──
        services.AddTransient<GestionCategorias>();

        // ── Punto de venta (catálogo + ventas) ──
        services.AddTransient<AgregarProducto>();
        services.AddTransient<ListarCatalogo>();
        services.AddTransient<RegistrarVenta>();
        services.AddTransient<EditarProducto>();
        services.AddTransient<DesactivarProducto>();
        services.AddTransient<ActivarProducto>();
        services.AddTransient<ListarVentas>();
        services.AddTransient<ResumenVentas>();

        // ── Comandas (flujo Mesero -> Cocina -> Caja) ──
        services.AddTransient<EnviarComanda>();
        services.AddTransient<ListarComandasActivas>();
        services.AddTransient<AvanzarComanda>();
        services.AddTransient<CancelarComanda>();
        services.AddTransient<CobrarComanda>();

        // ── Seguridad / usuarios ──
        services.AddTransient<Login>();
        services.AddTransient<Identificar>();
        services.AddTransient<CrearUsuarioStaff>();
        services.AddTransient<AltaStaff>();
        services.AddTransient<Chiron.Application.Sucursales.GestionSucursales>();
        services.AddTransient<ObtenerDetalleUsuario>();
        services.AddTransient<EditarDatosUsuario>();
        services.AddTransient<ResetearPin>();
        services.AddTransient<ListarUsuariosDeCafeteria>();
        services.AddTransient<ListarAdministradores>();
        services.AddTransient<CambiarMiPin>();
        services.AddTransient<GestionarUsuario>();

        // ── Métricas ──
        services.AddTransient<Chiron.Application.Metricas.MetricasDashboard>();
        services.AddTransient<Chiron.Application.Metricas.MetricasSuperAdmin>();

        return services;
    }
}
