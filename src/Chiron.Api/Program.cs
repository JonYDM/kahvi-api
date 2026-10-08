using System.Security.Claims;
using System.Text;
using Chiron.Application;
using Chiron.Application.Categorias;
using Chiron.Application.Comandas;
using Chiron.Application.Common;
using Chiron.Application.Metricas;
using Chiron.Application.PuntoVenta;
using Chiron.Application.Seguridad;
using Chiron.Application.Sucursales;
using Chiron.Domain.Cafeterias;
using Chiron.Domain.Categorias;
using Chiron.Domain.Comandas;
using Chiron.Domain.Common;
using Chiron.Domain.PuntoVenta;
using Chiron.Domain.Usuarios;
using Chiron.Infrastructure;
using Chiron.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// ─────────────────────────────────────────────────────────────────────────────
// Kahvi.Api — SaaS multi-tenant para cafeterías con autenticación JWT.
// Persistencia: solo EN MEMORIA (demo/desarrollo, sin PostgreSQL).
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Railway inyecta el puerto por la variable PORT.
string? puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(puerto))
    builder.WebHost.UseUrls($"http://+:{puerto}");

// Opciones de JWT desde configuración/variables de entorno.
// La clave DEBE venir de una variable de entorno en producción (Jwt__Clave).
var jwtOpciones = new JwtOpciones
{
    Clave = builder.Configuration["Jwt:Clave"]
            ?? "clave-de-desarrollo-solo-local-cambiar-en-produccion-1234567890",
    Emisor = builder.Configuration["Jwt:Emisor"] ?? "Kahvi",
    Audiencia = builder.Configuration["Jwt:Audiencia"] ?? "KahviApi"
};

builder.Services.AddApplication();

// Solo persistencia en memoria (sin Postgres).
builder.Services.AddInfrastructure(jwtOpciones);

// ── Autenticación JWT ──
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOpciones.Emisor,
            ValidAudience = jwtOpciones.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpciones.Clave))
        };
    });
builder.Services.AddAuthorization();

// CORS: orígenes desde variable de entorno Cors__Origenes (coma-separados).
const string PoliticaCors = "FrontendPermitido";
string[] origenesCors = (builder.Configuration["Cors:Origenes"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy(PoliticaCors, politica =>
    {
        if (origenesCors.Length > 0)
            politica.WithOrigins(origenesCors).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Seed en memoria (cafetería demo + usuarios + categorías para poder hacer login) ──
{
    using var scope = app.Services.CreateScope();
    var hasheador = scope.ServiceProvider.GetRequiredService<IHasheadorContrasena>();
    var repoUsuarios = scope.ServiceProvider.GetRequiredService<IUsuarioRepository>();
    var repoCafeterias = scope.ServiceProvider.GetRequiredService<IRepository<Cafeteria>>();
    var repoCategorias = scope.ServiceProvider.GetRequiredService<ICategoriaRepository>();

    // Cafetería demo.
    Cafeteria cafeDemo = Cafeteria.Crear("Café Kahvi Demo", "7770000000").Valor!;
    await repoCafeterias.AgregarAsync(cafeDemo);

    // SuperAdmin (dueño de Kahvi). PIN 6 dígitos.
    Usuario superAdmin = Usuario.CrearStaff(
        Guid.Empty, "superadmin", "Super Admin",
        hasheador.Hashear("123456"), RolUsuario.SuperAdmin).Valor!;
    await repoUsuarios.AgregarAsync(superAdmin);

    // Administrador de la cafetería demo.
    Usuario admin = Usuario.CrearStaff(
        cafeDemo.Id, "admindemo", "Admin Demo",
        hasheador.Hashear("654321"), RolUsuario.Administrador).Valor!;
    await repoUsuarios.AgregarAsync(admin);

    // Mesero de la cafetería demo.
    Usuario mesero = Usuario.CrearStaff(
        cafeDemo.Id, "meserodemo", "Mesero Demo",
        hasheador.Hashear("111111"), RolUsuario.Mesero).Valor!;
    await repoUsuarios.AgregarAsync(mesero);

    // Cocina de la cafetería demo.
    Usuario cocina = Usuario.CrearStaff(
        cafeDemo.Id, "cocinademo", "Cocina Demo",
        hasheador.Hashear("222222"), RolUsuario.Cocina).Valor!;
    await repoUsuarios.AgregarAsync(cocina);

    // Caja de la cafetería demo.
    Usuario caja = Usuario.CrearStaff(
        cafeDemo.Id, "cajademo", "Caja Demo",
        hasheador.Hashear("333333"), RolUsuario.Caja).Valor!;
    await repoUsuarios.AgregarAsync(caja);

    // Categorías demo del menú de la cafetería.
    var categoriasSeed = new[]
    {
        ("Cafe", 1),
        ("Desayunos", 2),
        ("Postres", 3),
        ("Bebidas", 4)
    };
    foreach (var (nombre, orden) in categoriasSeed)
    {
        Result<Categoria> catResult = Categoria.Crear(cafeDemo.Id, nombre, orden);
        if (catResult.EsExito)
            await repoCategorias.AgregarAsync(catResult.Valor!);
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "Kahvi.Api v1"));

// El orden importa: CORS -> autenticación -> autorización.
app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));

// ── Helpers locales ──

static IResult ToHttp<T>(Result<T> r) =>
    r.EsExito ? Results.Ok(r.Valor) : Results.BadRequest(new { error = r.Error });

// cafeteriaId SIEMPRE del claim JWT (aislamiento multi-tenant).
static Guid? CafeDelToken(ClaimsPrincipal user)
    => Guid.TryParse(user.FindFirst("cafeteriaId")?.Value, out Guid v) && v != Guid.Empty ? v : null;

static IResult SinCafeteria() => Results.BadRequest(new { error = "Token sin cafetería válida." });

// Nombres de roles como constantes para RequireRole.
const string SuperAdmin = nameof(RolUsuario.SuperAdmin);
const string Administrador = nameof(RolUsuario.Administrador);
const string Mesero = nameof(RolUsuario.Mesero);
const string Cocina = nameof(RolUsuario.Cocina);
const string Caja = nameof(RolUsuario.Caja);

// ═══════════════════ AUTENTICACIÓN (público) ═══════════════════

app.MapPost("/api/auth/login", async (LoginComando cmd, Login uc) =>
    ToHttp(await uc.EjecutarAsync(cmd)))
.WithName("Login").WithTags("Auth").AllowAnonymous();

// Paso 1 del login: valida el identificador y devuelve el primer nombre para saludar.
app.MapPost("/api/auth/identificar", async (IdentificarComando cmd, Identificar uc) =>
    ToHttp(await uc.EjecutarAsync(cmd)))
.WithName("Identificar").WithTags("Auth").AllowAnonymous();

// ═══════════════════ SUPERADMIN ═══════════════════

// Alta de cafetería (crea también su sucursal Matriz).
app.MapPost("/api/admin/cafeterias", async (CrearCafeteriaDto dto, GestionSucursales uc) =>
    ToHttp(await uc.CrearCafeteriaAsync(dto.Nombre, dto.Telefono, dto.Direccion, dto.Plan, dto.Precio)))
.WithName("CrearCafeteria").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Editar datos generales de una cafetería.
app.MapPut("/api/admin/cafeterias/{id:guid}", async (Guid id, EditarCafeteriaDto dto, GestionSucursales uc) =>
    ToHttp(await uc.EditarCafeteriaAsync(id, dto.Nombre, dto.Telefono, dto.Direccion, dto.Plan)))
.WithName("EditarCafeteria").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Renovar suscripción de la Matriz de una cafetería.
app.MapPost("/api/admin/cafeterias/{id:guid}/renovar", async (Guid id, GestionSucursales uc) =>
    ToHttp(await uc.RenovarMatrizAsync(id)))
.WithName("RenovarCafeteria").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Activar/desactivar cafetería completa.
app.MapPost("/api/admin/cafeterias/{id:guid}/activar", async (Guid id, GestionSucursales uc) =>
    ToHttp(await uc.CambiarEstadoCafeteriaAsync(id, activar: true)))
.WithName("ActivarCafeteria").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

app.MapPost("/api/admin/cafeterias/{id:guid}/desactivar", async (Guid id, GestionSucursales uc) =>
    ToHttp(await uc.CambiarEstadoCafeteriaAsync(id, activar: false)))
.WithName("DesactivarCafeteria").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Listar todas las cafeterías con sus sucursales.
app.MapGet("/api/admin/cafeterias", async (GestionSucursales uc) =>
    Results.Ok(await uc.ListarAsync()))
.WithName("ListarCafeterias").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// ── Sucursales (unidad de cobro) ──
app.MapPost("/api/admin/cafeterias/{id:guid}/sucursales", async (Guid id, CrearSucursalComando dto, GestionSucursales uc) =>
    ToHttp(await uc.CrearSucursalAsync(id, dto)))
.WithName("CrearSucursal").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

app.MapPut("/api/admin/sucursales/{id:guid}", async (Guid id, EditarSucursalComando dto, GestionSucursales uc) =>
    ToHttp(await uc.EditarSucursalAsync(id, dto)))
.WithName("EditarSucursal").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

app.MapPost("/api/admin/sucursales/{id:guid}/renovar", async (Guid id, RenovarComando? dto, GestionSucursales uc) =>
    ToHttp(await uc.RenovarAsync(id, dto)))
.WithName("RenovarSucursal").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

app.MapPost("/api/admin/sucursales/{id:guid}/activar", async (Guid id, GestionSucursales uc) =>
    ToHttp(await uc.CambiarEstadoAsync(id, activar: true)))
.WithName("ActivarSucursal").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

app.MapPost("/api/admin/sucursales/{id:guid}/desactivar", async (Guid id, GestionSucursales uc) =>
    ToHttp(await uc.CambiarEstadoAsync(id, activar: false)))
.WithName("DesactivarSucursal").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Historial de pagos de suscripción.
app.MapGet("/api/admin/pagos", async (DateOnly? desde, DateOnly? hasta, GestionSucursales uc) =>
    Results.Ok(await uc.ListarPagosAsync(desde, hasta)))
.WithName("ListarPagosSuscripcion").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Anular un pago mal capturado.
app.MapPost("/api/admin/pagos/{id:guid}/anular", async (Guid id, GestionSucursales uc) =>
    ToHttp(await uc.AnularPagoAsync(id)))
.WithName("AnularPagoSuscripcion").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Métricas globales de la plataforma.
app.MapGet("/api/admin/metricas", async (MetricasSuperAdmin uc) =>
    Results.Ok(await uc.EjecutarAsync()))
.WithName("MetricasSuperAdmin").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// SuperAdmin crea el Administrador de una cafetería.
app.MapPost("/api/admin/usuarios-admin", async (CrearAdministradorDto dto, AltaStaff uc) =>
    ToHttp(await uc.EjecutarAsync(new AltaStaffComando(
        dto.CafeteriaId, dto.Nombre, dto.ApellidoPaterno, dto.ApellidoMaterno,
        dto.Telefono, dto.Curp, dto.Pin, RolUsuario.Administrador))))
.WithName("CrearAdminCafeteria").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// Listar todos los Administradores (SuperAdmin).
app.MapGet("/api/admin/administradores", async (FiltroEstado? estado, ListarAdministradores uc) =>
    Results.Ok(await uc.EjecutarAsync(estado ?? FiltroEstado.Activos)))
.WithName("ListarAdministradores").WithTags("SuperAdmin").RequireAuthorization(p => p.RequireRole(SuperAdmin));

// ═══════════════════ USUARIOS / STAFF ═══════════════════

// El Administrador crea staff (Mesero/Cocina/Caja) de SU cafetería.
// El CafeteriaId se toma del token (aislamiento multi-tenant).
app.MapPost("/api/usuarios/staff", async (CrearStaffDto dto, ClaimsPrincipal user, AltaStaff uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    // Un admin solo puede crear Mesero, Cocina o Caja (no otros admins ni superadmin).
    if (dto.Rol is not (RolUsuario.Mesero or RolUsuario.Cocina or RolUsuario.Caja))
        return Results.BadRequest(new { error = "Rol no permitido. Use Mesero, Cocina o Caja." });

    var comando = new AltaStaffComando(cafeteriaId, dto.Nombre, dto.ApellidoPaterno, dto.ApellidoMaterno,
        dto.Telefono, dto.Curp, dto.Pin, dto.Rol);
    return ToHttp(await uc.EjecutarAsync(comando));
})
.WithName("CrearStaff").WithTags("Usuarios").RequireAuthorization(p => p.RequireRole(Administrador));

// Listar los usuarios (staff) de la cafetería del token.
app.MapGet("/api/usuarios/staff", async (ClaimsPrincipal user, FiltroEstado? estado, ListarUsuariosDeCafeteria uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    return Results.Ok(await uc.EjecutarAsync(cafeteriaId, estado ?? FiltroEstado.Activos));
})
.WithName("ListarUsuariosStaff").WithTags("Usuarios").RequireAuthorization(p => p.RequireRole(Administrador));

// Resetear el PIN de un usuario. Administrador -> su staff; SuperAdmin -> Administradores.
app.MapPost("/api/usuarios/{id:guid}/resetear-pin", async (Guid id, ResetearPinDto dto, ClaimsPrincipal user, ResetearPin uc) =>
{
    string? rolClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
    if (!Enum.TryParse<RolUsuario>(rolClaim, out RolUsuario solicitanteRol))
        return Results.BadRequest(new { error = "Token sin rol válido." });
    Guid.TryParse(user.FindFirst("cafeteriaId")?.Value, out Guid solicitanteCafe);
    var comando = new ResetearPinComando(id, dto.NuevoPin, solicitanteRol, solicitanteCafe);
    return ToHttp(await uc.EjecutarAsync(comando));
})
.WithName("ResetearPin").WithTags("Usuarios").RequireAuthorization(p => p.RequireRole(Administrador, SuperAdmin));

// Cambiar el propio PIN (autoservicio). El id sale del token (sub).
app.MapPost("/api/mi-pin", async (CambiarMiPinDto dto, ClaimsPrincipal user, CambiarMiPin uc) =>
{
    string? sub = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(sub, out Guid usuarioId))
        return Results.BadRequest(new { error = "Token inválido." });
    var comando = new CambiarMiPinComando(usuarioId, dto.PinActual, dto.NuevoPin);
    return ToHttp(await uc.EjecutarAsync(comando));
})
.WithName("CambiarMiPin").WithTags("Usuarios").RequireAuthorization();

// Gestionar (activar/desactivar/renombrar) un usuario.
app.MapPost("/api/usuarios/{id:guid}/gestionar", async (Guid id, GestionarUsuarioDto dto, ClaimsPrincipal user, GestionarUsuario uc) =>
{
    string? rolClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
    if (!Enum.TryParse<RolUsuario>(rolClaim, out RolUsuario solicitanteRol))
        return Results.BadRequest(new { error = "Token sin rol válido." });
    Guid.TryParse(user.FindFirst("cafeteriaId")?.Value, out Guid solicitanteCafe);
    var comando = new GestionarUsuarioComando(id, dto.NuevoNombre, dto.Accion, solicitanteRol, solicitanteCafe);
    return ToHttp(await uc.EjecutarAsync(comando));
})
.WithName("GestionarUsuario").WithTags("Usuarios").RequireAuthorization(p => p.RequireRole(Administrador, SuperAdmin));

// Detalle de un usuario (CURP enmascarada). Mismas reglas que gestionar.
app.MapGet("/api/usuarios/{id:guid}", async (Guid id, ClaimsPrincipal user, ObtenerDetalleUsuario uc) =>
{
    string? rolClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
    if (!Enum.TryParse<RolUsuario>(rolClaim, out RolUsuario solicitanteRol))
        return Results.BadRequest(new { error = "Token sin rol válido." });
    Guid.TryParse(user.FindFirst("cafeteriaId")?.Value, out Guid solicitanteCafe);
    return ToHttp(await uc.EjecutarAsync(id, solicitanteRol, solicitanteCafe));
})
.WithName("DetalleUsuario").WithTags("Usuarios").RequireAuthorization(p => p.RequireRole(Administrador, SuperAdmin));

// Editar datos personales de un usuario (nombres, apellidos, teléfono, CURP).
app.MapPut("/api/usuarios/{id:guid}/datos", async (Guid id, EditarDatosUsuarioDto dto, ClaimsPrincipal user, EditarDatosUsuario uc) =>
{
    string? rolClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
    if (!Enum.TryParse<RolUsuario>(rolClaim, out RolUsuario solicitanteRol))
        return Results.BadRequest(new { error = "Token sin rol válido." });
    Guid.TryParse(user.FindFirst("cafeteriaId")?.Value, out Guid solicitanteCafe);
    var comando = new EditarDatosUsuarioComando(id, dto.Nombres, dto.ApellidoPaterno, dto.ApellidoMaterno,
        dto.Telefono, dto.Curp, solicitanteRol, solicitanteCafe);
    return ToHttp(await uc.EjecutarAsync(comando));
})
.WithName("EditarDatosUsuario").WithTags("Usuarios").RequireAuthorization(p => p.RequireRole(Administrador, SuperAdmin));

// ═══════════════════ CATEGORÍAS (menú dinámico de la cafetería) ═══════════════════

// Listar categorías: accesible a todos los roles autenticados (mesero necesita ver el menú).
app.MapGet("/api/categorias", async (ClaimsPrincipal user, GestionCategorias uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    Result<IReadOnlyList<Categoria>> r = await uc.ListarAsync(cafeteriaId);
    if (!r.EsExito) return Results.BadRequest(new { error = r.Error });
    return Results.Ok(r.Valor!.Select(c => new
    {
        id = c.Id,
        nombre = c.Nombre,
        orden = c.Orden
    }));
})
.WithName("ListarCategorias").WithTags("Categorias").RequireAuthorization();

// Crear categoría (solo Administrador).
app.MapPost("/api/categorias", async (NuevaCategoriaDto dto, ClaimsPrincipal user, GestionCategorias uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    Result<Guid> r = await uc.CrearAsync(cafeteriaId, dto.Nombre, dto.Orden);
    return r.EsExito
        ? Results.Created($"/api/categorias/{r.Valor}", new { id = r.Valor })
        : Results.BadRequest(new { error = r.Error });
})
.WithName("CrearCategoria").WithTags("Categorias").RequireAuthorization(p => p.RequireRole(Administrador));

// Editar categoría (solo Administrador).
app.MapPut("/api/categorias/{id:guid}", async (Guid id, NuevaCategoriaDto dto, ClaimsPrincipal user, GestionCategorias uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    return ToHttp(await uc.EditarAsync(cafeteriaId, id, dto.Nombre, dto.Orden));
})
.WithName("EditarCategoria").WithTags("Categorias").RequireAuthorization(p => p.RequireRole(Administrador));

// Eliminar categoría (solo Administrador).
app.MapDelete("/api/categorias/{id:guid}", async (Guid id, ClaimsPrincipal user, GestionCategorias uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    Result<bool> r = await uc.EliminarAsync(cafeteriaId, id);
    return r.EsExito ? Results.NoContent() : Results.BadRequest(new { error = r.Error });
})
.WithName("EliminarCategoria").WithTags("Categorias").RequireAuthorization(p => p.RequireRole(Administrador));

// ═══════════════════ PRODUCTOS (catálogo de la cafetería) ═══════════════════

// Agregar producto al catálogo. CafeteriaId del token.
app.MapPost("/api/productos", async (AgregarProductoDto dto, ClaimsPrincipal user, AgregarProducto uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    var cmd = new AgregarProductoComando(cafeteriaId, dto.Nombre, dto.Categoria, dto.Precio, dto.Costo);
    return ToHttp(await uc.EjecutarAsync(cmd));
})
.WithName("AgregarProducto").WithTags("PuntoVenta").RequireAuthorization(p => p.RequireRole(Administrador));

// Listar el catálogo de la cafetería del token.
// El costo solo se incluye si el solicitante es Administrador.
app.MapGet("/api/productos", async (ClaimsPrincipal user, FiltroEstado? estado, ListarCatalogo uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    string? rolClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
    bool esAdmin = rolClaim == Administrador;

    IReadOnlyList<Producto> productos = await uc.EjecutarAsync(cafeteriaId, estado ?? FiltroEstado.Activos);

    // El costo solo lo ve el Administrador; para otros roles se omite (null).
    var dtos = productos.Select(p => new
    {
        id = p.Id,
        nombre = p.Nombre,
        categoria = p.Categoria.ToString(),
        precio = p.Precio,
        costo = esAdmin ? p.Costo : (decimal?)null,
        activo = p.Activo
    });

    return Results.Ok(dtos);
})
.WithName("ListarCatalogo").WithTags("PuntoVenta")
.RequireAuthorization(p => p.RequireRole(Administrador, Mesero, Cocina, Caja));

// Editar un producto. CafeteriaId del token para aislamiento.
app.MapPut("/api/productos/{id:guid}", async (Guid id, EditarProductoDto dto, ClaimsPrincipal user, EditarProducto uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    var comando = new EditarProductoComando(id, cafeteriaId, dto.Nombre, dto.Categoria, dto.Precio, dto.Costo);
    return ToHttp(await uc.EjecutarAsync(comando));
})
.WithName("EditarProducto").WithTags("PuntoVenta").RequireAuthorization(p => p.RequireRole(Administrador));

// Desactivar (baja lógica) un producto del catálogo.
app.MapPost("/api/productos/{id:guid}/desactivar", async (Guid id, ClaimsPrincipal user, DesactivarProducto uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    return ToHttp(await uc.EjecutarAsync(id, cafeteriaId));
})
.WithName("DesactivarProducto").WithTags("PuntoVenta").RequireAuthorization(p => p.RequireRole(Administrador));

// ═══════════════════ VENTAS ═══════════════════

// Registrar una venta de mostrador (caja directa, sin comanda previa).
app.MapPost("/api/ventas", async (RegistrarVentaComando cmd, ClaimsPrincipal user, RegistrarVenta uc) =>
    CafeDelToken(user) is Guid cafe
        ? ToHttp(await uc.EjecutarAsync(cmd with { CafeteriaId = cafe }))
        : SinCafeteria())
.WithName("RegistrarVenta").WithTags("PuntoVenta")
.RequireAuthorization(p => p.RequireRole(Administrador, Caja));

// Historial de ventas en rango de fechas.
app.MapGet("/api/ventas", async (ClaimsPrincipal user, DateTime? desde, DateTime? hasta, ListarVentas uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    return Results.Ok(await uc.EjecutarAsync(cafeteriaId, desde, hasta));
})
.WithName("ListarVentas").WithTags("PuntoVenta").RequireAuthorization(p => p.RequireRole(Administrador));

// Resumen de ventas (total, conteo, desglose por método de pago).
app.MapGet("/api/ventas/resumen", async (ClaimsPrincipal user, DateTime? desde, DateTime? hasta, ResumenVentas uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    return Results.Ok(await uc.EjecutarAsync(cafeteriaId, desde, hasta));
})
.WithName("ResumenVentas").WithTags("PuntoVenta").RequireAuthorization(p => p.RequireRole(Administrador, Caja));

// ═══════════════════ SUCURSALES (vista del Administrador de la cafetería) ═══════════════════

// Listar las sucursales de la cafetería del token (Admin las ve para contexto).
app.MapGet("/api/sucursales", async (ClaimsPrincipal user, GestionSucursales uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();
    // Devuelve las sucursales de esta cafetería (filtrado en el mapeado).
    var todas = await uc.ListarAsync();
    var mia = todas.FirstOrDefault(c => c.Id == cafeteriaId);
    return Results.Ok(mia?.Sucursales ?? new List<SucursalDto>());
})
.WithName("ListarMisSucursales").WithTags("Sucursales").RequireAuthorization(p => p.RequireRole(Administrador));

// ═══════════════════ MÉTRICAS (dashboard) ═══════════════════

app.MapGet("/api/metricas/dashboard", async (ClaimsPrincipal user, MetricasDashboard uc) =>
{
    string? rol = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    // Alcance de las métricas de dinero según el rol:
    // Admin: todo (ventas del día y del mes). Caja: solo la del día. Mesero/Cocina: ninguna.
    AlcanceMetricas alcance = rol switch
    {
        var r when r == Administrador => AlcanceMetricas.Completo,
        var r when r == Caja => AlcanceMetricas.SoloHoy,
        _ => AlcanceMetricas.Ninguno,
    };
    return Results.Ok(await uc.EjecutarAsync(cafeteriaId, alcance));
})
.WithName("MetricasDashboard").WithTags("Metricas")
.RequireAuthorization(p => p.RequireRole(Administrador, Mesero, Cocina, Caja));

// ═══════════════════ COMANDAS (flujo Mesero -> Cocina -> Caja) ═══════════════════

// Mesero o Admin envían una nueva comanda.
app.MapPost("/api/comandas", async (NuevaComandaDto dto, ClaimsPrincipal user, EnviarComanda uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    string? sub = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(sub, out Guid meseroId))
        return Results.BadRequest(new { error = "Token sin usuario válido." });

    var items = (dto.Items ?? Enumerable.Empty<ItemComandaDto>())
        .Select(i => (i.ProductoId, i.Nombre, i.Cantidad, i.Precio, i.Nota));

    Result<Guid> r = await uc.EjecutarAsync(
        cafeteriaId, dto.Mesa ?? string.Empty, meseroId, dto.MeseroNombre ?? "",
        items, dto.EsParaLlevar, dto.NombreCliente);
    return r.EsExito ? Results.Created($"/api/comandas/{r.Valor}", new { id = r.Valor }) : ToHttp(r);
})
.WithName("EnviarComanda").WithTags("Comandas").RequireAuthorization(p => p.RequireRole(Mesero, Administrador));

// Cocina, Caja o Admin ven el tablero de comandas activas (polling).
app.MapGet("/api/comandas/activas", async (ClaimsPrincipal user, ListarComandasActivas uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    Result<IReadOnlyList<Comanda>> r = await uc.EjecutarAsync(cafeteriaId);
    if (!r.EsExito)
        return Results.BadRequest(new { error = r.Error });

    // Proyectar a DTO (no exponer la entidad directamente).
    var dtos = r.Valor!.Select(c => new
    {
        id = c.Id,
        folio = c.Folio,
        mesa = c.Mesa,
        meseroNombre = c.MeseroNombre,
        estado = c.Estado.ToString(),
        creadaEn = c.CreadaEn,
        total = c.Total,
        esParaLlevar = c.EsParaLlevar,
        nombreCliente = c.NombreCliente,
        items = c.Lineas.Select(l => new
        {
            productoId = l.ProductoId,
            nombre = l.NombreProducto,
            cantidad = l.Cantidad,
            precioUnitario = l.PrecioUnitario,
            nota = l.Nota,
            subtotal = l.Subtotal
        })
    });
    return Results.Ok(dtos);
})
.WithName("ListarComandasActivas").WithTags("Comandas")
.RequireAuthorization(p => p.RequireRole(Cocina, Caja, Administrador));

// Cocina o Admin avanzan el estado de una comanda.
app.MapPost("/api/comandas/{id:guid}/avanzar", async (Guid id, ClaimsPrincipal user, AvanzarComanda uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    Result<EstadoComanda> r = await uc.EjecutarAsync(cafeteriaId, id);
    return r.EsExito
        ? Results.Ok(new { nuevoEstado = r.Valor.ToString() })
        : Results.BadRequest(new { error = r.Error });
})
.WithName("AvanzarComanda").WithTags("Comandas").RequireAuthorization(p => p.RequireRole(Cocina, Administrador));

// Mesero o Admin cancelan una comanda.
app.MapPost("/api/comandas/{id:guid}/cancelar", async (Guid id, ClaimsPrincipal user, CancelarComanda uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    Result<bool> r = await uc.EjecutarAsync(cafeteriaId, id);
    return r.EsExito ? Results.NoContent() : Results.BadRequest(new { error = r.Error });
})
.WithName("CancelarComanda").WithTags("Comandas").RequireAuthorization(p => p.RequireRole(Mesero, Administrador));

// Caja o Admin cobran una comanda Lista y generan la venta.
app.MapPost("/api/comandas/{id:guid}/cobrar", async (Guid id, CobrarComandaDto dto, ClaimsPrincipal user, CobrarComanda uc) =>
{
    if (CafeDelToken(user) is not Guid cafeteriaId)
        return SinCafeteria();

    Result<Guid> r = await uc.EjecutarAsync(cafeteriaId, id, dto.MetodoPago, dto.MontoRecibido);
    return r.EsExito
        ? Results.Ok(new { ventaId = r.Valor })
        : Results.BadRequest(new { error = r.Error });
})
.WithName("CobrarComanda").WithTags("Comandas").RequireAuthorization(p => p.RequireRole(Caja, Administrador));

app.Run();

// ═══════════════════ DTOs de request (solo se usan en este archivo) ═══════════════════

// SuperAdmin: crear cafetería.
record CrearCafeteriaDto(string Nombre, string Telefono, string? Direccion, PlanSuscripcion? Plan, decimal? Precio);

// SuperAdmin: editar cafetería.
record EditarCafeteriaDto(string Nombre, string Telefono, string? Direccion, PlanSuscripcion? Plan);

// SuperAdmin: crear Administrador de una cafetería.
record CrearAdministradorDto(
    Guid CafeteriaId, string Nombre, string ApellidoPaterno, string? ApellidoMaterno,
    string Telefono, string? Curp, string Pin);

// Admin: crear staff (Mesero/Cocina/Caja).
record CrearStaffDto(
    string Nombre, string ApellidoPaterno, string? ApellidoMaterno,
    string Telefono, string? Curp, string Pin, RolUsuario Rol);

// Resetear PIN (Admin o SuperAdmin).
record ResetearPinDto(string NuevoPin);

// Cambiar el propio PIN.
record CambiarMiPinDto(string PinActual, string NuevoPin);

// Gestionar usuario (activar/desactivar/renombrar).
record GestionarUsuarioDto(string? NuevoNombre, AccionUsuario? Accion);

// Editar datos personales de un usuario.
record EditarDatosUsuarioDto(
    string Nombres, string ApellidoPaterno, string? ApellidoMaterno, string Telefono, string? Curp);

// Categorías dinámicas del menú.
record NuevaCategoriaDto(string Nombre, int Orden);

// Agregar producto (sin stock, con costo opcional).
record AgregarProductoDto(string Nombre, CategoriaProducto Categoria, decimal Precio, decimal? Costo = null);

// Editar producto (sin stock, con costo opcional).
record EditarProductoDto(string Nombre, CategoriaProducto Categoria, decimal Precio, decimal? Costo = null);

// Ajustar fecha de renovación de sucursal.
record AjustarRenovacionDto(DateOnly Fecha);

// Comandas (con soporte para llevar y nombre de cliente).
record ItemComandaDto(Guid ProductoId, string Nombre, int Cantidad, decimal Precio, string? Nota);
record NuevaComandaDto(
    string? Mesa,
    string? MeseroNombre,
    IEnumerable<ItemComandaDto>? Items,
    bool EsParaLlevar = false,
    string? NombreCliente = null);
record CobrarComandaDto(MetodoPago MetodoPago, decimal? MontoRecibido);
