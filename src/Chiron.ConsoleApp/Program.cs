using Chiron.Application;
using Chiron.Application.Common;
using Chiron.Application.PuntoVenta;
using Chiron.Domain.Cafeterias;
using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;
using Chiron.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// Chiron.ConsoleApp — Smoke test: punto de venta (cafetería, catálogo + ventas).
// ─────────────────────────────────────────────────────────────────────────────

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure();
    })
    .Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("☕ Kahvi — Smoke test punto de venta");

// Cafetería de prueba.
IRepository<Cafeteria> cafeterias = host.Services.GetRequiredService<IRepository<Cafeteria>>();
Cafeteria cafe = Cafeteria.Crear("Café Demo", "7770000000").Valor!;
await cafeterias.AgregarAsync(cafe);
logger.LogInformation("🏪 Cafetería creada: {Nombre} ({Id})", cafe.Nombre, cafe.Id);

// ── Agregar productos al catálogo (sin stock, con costo opcional) ──
var agregarProducto = host.Services.GetRequiredService<AgregarProducto>();

Result<Guid> americano = await agregarProducto.EjecutarAsync(
    new AgregarProductoComando(cafe.Id, "Americano", CategoriaProducto.Cafe, 35m, Costo: 12m));
Result<Guid> croissant = await agregarProducto.EjecutarAsync(
    new AgregarProductoComando(cafe.Id, "Croissant de jamón", CategoriaProducto.Desayunos, 55m, Costo: 20m));

logger.LogInformation("🛒 Catálogo cargado (americano y croissant)");

// Validación: precio inválido (debe fallar).
Result<Guid> malo = await agregarProducto.EjecutarAsync(
    new AgregarProductoComando(cafe.Id, "Producto gratis", CategoriaProducto.Otro, 0m));
if (!malo.EsExito) logger.LogWarning("⛔ Rechazado (esperado): {Error}", malo.Error);

// Ver catálogo (ya sin campo Stock).
var listarCatalogo = host.Services.GetRequiredService<ListarCatalogo>();
foreach (Producto p in await listarCatalogo.EjecutarAsync(cafe.Id))
    logger.LogInformation("   • {Nombre} | {Cat} | ${Precio} | costo {Costo}", p.Nombre, p.Categoria, p.Precio, p.Costo?.ToString("C") ?? "N/A");

// ── Registrar una venta (1 americano + 1 croissant) ──
var registrarVenta = host.Services.GetRequiredService<RegistrarVenta>();
Result<VentaResultado> venta = await registrarVenta.EjecutarAsync(new RegistrarVentaComando(
    cafe.Id,
    Items: new[]
    {
        new ItemVentaComando(americano.Valor, 1),
        new ItemVentaComando(croissant.Valor, 1)
    },
    MetodoPago: MetodoPago.Efectivo,
    MontoRecibido: 100m));

if (venta.EsExito)
    logger.LogInformation("✅ Venta registrada. Total: ${Total} | Cambio: ${Cambio}", venta.Valor!.Total, venta.Valor!.Cambio);

logger.LogInformation("✅ Smoke test completado.");
await host.StopAsync();
