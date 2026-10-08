using Chiron.Application.Common;
using Chiron.Domain.Common;
using Chiron.Domain.Sucursales;
using Chiron.Domain.Suscripciones;
using Chiron.Domain.Cafeterias;

namespace Chiron.Application.Sucursales;

/// <summary>Sucursal tal como la ve el SuperAdmin (unidad de cobro).</summary>
public sealed record SucursalDto(
    Guid Id,
    Guid CafeteriaId,
    string Nombre,
    string? Direccion,
    string? Telefono,
    bool EsMatriz,
    bool Activa,
    DateTime FechaAlta,
    PlanSuscripcion Plan,
    decimal Precio,
    DateOnly FechaRenovacion)
{
    public static SucursalDto Desde(Sucursal s) => new(
        s.Id, s.CafeteriaId, s.Nombre, s.Direccion, s.Telefono, s.EsMatriz, s.Activa,
        s.FechaAlta, s.Plan, s.Precio, s.FechaRenovacion);
}

/// <summary>
/// Cafeteria con sus sucursales. <c>Direccion</c>, <c>Plan</c> y <c>FechaRenovacion</c> se
/// toman de la MATRIZ (compatibilidad con el contrato anterior, cuando vivían en Cafeteria).
/// </summary>
public sealed record CafeteriaConSucursalesDto(
    Guid Id,
    string Nombre,
    string Telefono,
    string? Direccion,
    bool Activa,
    DateTime FechaAlta,
    PlanSuscripcion Plan,
    DateOnly FechaRenovacion,
    IReadOnlyList<SucursalDto> Sucursales);

public sealed record CrearSucursalComando(
    string Nombre, string? Direccion, string? Telefono, PlanSuscripcion Plan, decimal? Precio);

public sealed record EditarSucursalComando(
    string Nombre, string? Direccion, string? Telefono, PlanSuscripcion Plan, decimal Precio);

/// <summary>Datos opcionales del cobro al renovar (si no vienen: precio de la sucursal y hoy).</summary>
public sealed record RenovarComando(decimal? Monto, DateOnly? FechaPago, string? Nota);

/// <summary>Pago de suscripción para el historial de cobros.</summary>
public sealed record PagoSuscripcionDto(
    Guid Id,
    Guid CafeteriaId,
    string CafeteriaNombre,
    Guid SucursalId,
    string SucursalNombre,
    bool EsMatriz,
    decimal Monto,
    DateOnly FechaPago,
    PlanSuscripcion Plan,
    DateOnly PeriodoDesde,
    DateOnly PeriodoHasta,
    string? Nota,
    bool Anulado);

/// <summary>
/// Reglas de suscripción por sucursal (HU-SU1..SU3):
///  - Toda Cafeteria tiene una Matriz; si falta (datos en memoria o viejos), se crea al vuelo
///    con el plan/fecha que tenía la Cafeteria.
///  - La Matriz sigue el estado de la Cafeteria: se activa/desactiva junto con ella, y
///    renovarla reactiva a la Cafeteria.
///  - Las demás sucursales se activan/desactivan por separado.
/// </summary>
public sealed class GestionSucursales
{
    private readonly IRepository<Cafeteria> _Cafeterias;
    private readonly IRepository<Sucursal> _sucursales;
    private readonly IRepository<PagoSuscripcion> _pagos;

    public GestionSucursales(
        IRepository<Cafeteria> Cafeterias, IRepository<Sucursal> sucursales, IRepository<PagoSuscripcion> pagos)
    {
        _Cafeterias = Cafeterias;
        _sucursales = sucursales;
        _pagos = pagos;
    }

    private static DateOnly Hoy() => HoraMexico.Hoy();

    /// <summary>Todas las Cafeterias con sus sucursales (Matriz primero).</summary>
    public async Task<IReadOnlyList<CafeteriaConSucursalesDto>> ListarAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Cafeteria> vets = await _Cafeterias.ObtenerTodosAsync(ct);
        List<Sucursal> todas = (await _sucursales.ObtenerTodosAsync(ct)).ToList();

        var resultado = new List<CafeteriaConSucursalesDto>(vets.Count);
        foreach (Cafeteria v in vets)
        {
            List<Sucursal> propias = todas.Where(s => s.CafeteriaId == v.Id).ToList();
            if (!propias.Any(s => s.EsMatriz))
                propias.Add(await CrearMatrizHeredadaAsync(v, ct));
            resultado.Add(Mapear(v, propias));
        }
        return resultado;
    }

    /// <summary>Alta de Cafeteria + su Matriz (con el plan y precio indicados).</summary>
    public async Task<Result<CafeteriaConSucursalesDto>> CrearCafeteriaAsync(
        string nombre, string telefono, string? direccion, PlanSuscripcion? plan, decimal? precio,
        CancellationToken ct = default)
    {
        PlanSuscripcion p = plan ?? PlanSuscripcion.Mensual;
        Result<Cafeteria> vet = Cafeteria.Crear(nombre, telefono, direccion, p);
        if (!vet.EsExito)
            return Result<CafeteriaConSucursalesDto>.Falla(vet.Error!);

        Result<Sucursal> matriz = Sucursal.Crear(vet.Valor!.Id, "Matriz", direccion, telefono, p, precio, esMatriz: true, hoy: Hoy());
        if (!matriz.EsExito)
            return Result<CafeteriaConSucursalesDto>.Falla(matriz.Error!);

        await _Cafeterias.AgregarAsync(vet.Valor, ct);
        await _sucursales.AgregarAsync(matriz.Valor!, ct);
        return Result<CafeteriaConSucursalesDto>.Exito(Mapear(vet.Valor, [matriz.Valor!]));
    }

    public async Task<Result<SucursalDto>> CrearSucursalAsync(Guid CafeteriaId, CrearSucursalComando c, CancellationToken ct = default)
    {
        Cafeteria? vet = await _Cafeterias.ObtenerPorIdAsync(CafeteriaId, ct);
        if (vet is null)
            return Result<SucursalDto>.Falla("La Cafeteria no existe.");
        Result<Sucursal> s = Sucursal.Crear(CafeteriaId, c.Nombre, c.Direccion, c.Telefono, c.Plan, c.Precio, hoy: Hoy());
        if (!s.EsExito)
            return Result<SucursalDto>.Falla(s.Error!);
        await _sucursales.AgregarAsync(s.Valor!, ct);
        return Result<SucursalDto>.Exito(SucursalDto.Desde(s.Valor!));
    }

    public Task<Result<SucursalDto>> EditarSucursalAsync(Guid id, EditarSucursalComando c, CancellationToken ct = default)
        => ConSucursalAsync(id, s => s.Editar(c.Nombre, c.Direccion, c.Telefono, c.Plan, c.Precio), ct);

    /// <summary>
    /// Renueva un periodo y REGISTRA EL PAGO (monto = precio de la sucursal si no se indica;
    /// fecha = hoy si no se indica). Si es la Matriz, además reactiva la Cafeteria.
    /// </summary>
    public async Task<Result<SucursalDto>> RenovarAsync(Guid id, RenovarComando? c = null, CancellationToken ct = default)
    {
        Sucursal? s = await _sucursales.ObtenerPorIdAsync(id, ct);
        if (s is null)
            return Result<SucursalDto>.Falla("La sucursal no existe.");

        DateOnly hoy = Hoy();
        DateOnly fechaPago = c?.FechaPago ?? hoy;
        if (fechaPago > hoy)
            return Result<SucursalDto>.Falla("La fecha de pago no puede ser futura.");

        // Se valida el pago ANTES de mover la fecha (si falla, no se renueva).
        DateOnly vencimientoPrevio = s.FechaRenovacion;
        (DateOnly desde, DateOnly hasta) = s.Renovar(hoy);
        Result<PagoSuscripcion> pago = PagoSuscripcion.Registrar(
            s.CafeteriaId, s.Id, c?.Monto ?? s.Precio, fechaPago, s.Plan, desde, hasta, c?.Nota);
        if (!pago.EsExito)
        {
            s.AjustarRenovacion(vencimientoPrevio);
            return Result<SucursalDto>.Falla(pago.Error!);
        }

        await _sucursales.ActualizarAsync(s, ct);
        await _pagos.AgregarAsync(pago.Valor!, ct);
        if (s.EsMatriz)
            await CambiarEstadoCafeteriaInternoAsync(s.CafeteriaId, activar: true, ct);
        return Result<SucursalDto>.Exito(SucursalDto.Desde(s));
    }

    /// <summary>Historial de pagos (más recientes primero), con nombres para mostrar.</summary>
    public async Task<IReadOnlyList<PagoSuscripcionDto>> ListarPagosAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        IReadOnlyList<CafeteriaConSucursalesDto> vets = await ListarAsync(ct);
        var nombreVet = vets.ToDictionary(v => v.Id, v => v.Nombre);
        var nombreSuc = vets.SelectMany(v => v.Sucursales).ToDictionary(s => s.Id, s => (s.Nombre, s.EsMatriz));

        return (await _pagos.ObtenerTodosAsync(ct))
            .Where(p => (desde is null || p.FechaPago >= desde) && (hasta is null || p.FechaPago <= hasta))
            .OrderByDescending(p => p.FechaPago)
            .ThenByDescending(p => p.FechaRegistro)
            .Select(p =>
            {
                (string Nombre, bool EsMatriz) suc = nombreSuc.GetValueOrDefault(p.SucursalId, ("Sucursal", false));
                return new PagoSuscripcionDto(
                    p.Id, p.CafeteriaId, nombreVet.GetValueOrDefault(p.CafeteriaId, "Cafeteria"),
                    p.SucursalId, suc.Nombre, suc.EsMatriz, p.Monto, p.FechaPago, p.Plan,
                    p.PeriodoDesde, p.PeriodoHasta, p.Nota, p.Anulado);
            })
            .ToList();
    }

    /// <summary>Anula un pago mal capturado (deja de contar en ingresos; la fecha no se revierte).</summary>
    public async Task<Result<bool>> AnularPagoAsync(Guid pagoId, CancellationToken ct = default)
    {
        PagoSuscripcion? p = await _pagos.ObtenerPorIdAsync(pagoId, ct);
        if (p is null)
            return Result<bool>.Falla("El pago no existe.");
        Result<bool> r = p.Anular();
        if (r.EsExito)
            await _pagos.ActualizarAsync(p, ct);
        return r;
    }

    /// <summary>Todos los pagos (para métricas de ingresos).</summary>
    public Task<IReadOnlyList<PagoSuscripcion>> PagosAsync(CancellationToken ct = default)
        => _pagos.ObtenerTodosAsync(ct);

    public Task<Result<SucursalDto>> AjustarRenovacionAsync(Guid id, DateOnly fecha, CancellationToken ct = default)
        => ConSucursalAsync(id, s => { s.AjustarRenovacion(fecha); return Result<bool>.Exito(true); }, ct);

    /// <summary>Activa/desactiva una sucursal que NO es la Matriz (la Matriz sigue a la Cafeteria).</summary>
    public Task<Result<SucursalDto>> CambiarEstadoAsync(Guid id, bool activar, CancellationToken ct = default)
        => ConSucursalAsync(id, s =>
        {
            if (s.EsMatriz)
                return Result<bool>.Falla("La Matriz se activa o desactiva junto con la Cafeteria.");
            if (activar) s.Activar(); else s.Desactivar();
            return Result<bool>.Exito(true);
        }, ct);

    // ── Compatibilidad: operaciones "de Cafeteria" que ahora actúan sobre su Matriz ──

    public async Task<Result<SucursalDto>> RenovarMatrizAsync(Guid CafeteriaId, CancellationToken ct = default)
    {
        Sucursal? m = await MatrizAsync(CafeteriaId, ct);
        return m is null ? Result<SucursalDto>.Falla("La Cafeteria no existe.") : await RenovarAsync(m.Id, null, ct);
    }

    public async Task<Result<SucursalDto>> AjustarRenovacionMatrizAsync(Guid CafeteriaId, DateOnly fecha, CancellationToken ct = default)
    {
        Sucursal? m = await MatrizAsync(CafeteriaId, ct);
        return m is null ? Result<SucursalDto>.Falla("La Cafeteria no existe.") : await AjustarRenovacionAsync(m.Id, fecha, ct);
    }

    /// <summary>Edita la Cafeteria. Dirección y plan (si vienen) se aplican a la Matriz.</summary>
    public async Task<Result<bool>> EditarCafeteriaAsync(
        Guid CafeteriaId, string nombre, string telefono, string? direccion, PlanSuscripcion? plan,
        CancellationToken ct = default)
    {
        Cafeteria? vet = await _Cafeterias.ObtenerPorIdAsync(CafeteriaId, ct);
        if (vet is null)
            return Result<bool>.Falla("La Cafeteria no existe.");
        Result<bool> r = vet.Editar(nombre, telefono, direccion, plan ?? vet.Plan);
        if (!r.EsExito)
            return r;
        await _Cafeterias.ActualizarAsync(vet, ct);

        // La dirección siempre se sincroniza con la Matriz; el plan solo si viene.
        Sucursal? m = await MatrizAsync(CafeteriaId, ct);
        if (m is not null)
        {
            Result<bool> rm = m.Editar(m.Nombre, direccion, m.Telefono, plan ?? m.Plan, m.Precio);
            if (!rm.EsExito)
                return rm;
            await _sucursales.ActualizarAsync(m, ct);
        }
        return Result<bool>.Exito(true);
    }

    /// <summary>Activa/desactiva la Cafeteria completa (y su Matriz con ella).</summary>
    public async Task<Result<bool>> CambiarEstadoCafeteriaAsync(Guid CafeteriaId, bool activar, CancellationToken ct = default)
        => await CambiarEstadoCafeteriaInternoAsync(CafeteriaId, activar, ct)
            ? Result<bool>.Exito(true)
            : Result<bool>.Falla("La Cafeteria no existe.");

    // ── Internos ──

    private async Task<bool> CambiarEstadoCafeteriaInternoAsync(Guid CafeteriaId, bool activar, CancellationToken ct)
    {
        Cafeteria? vet = await _Cafeterias.ObtenerPorIdAsync(CafeteriaId, ct);
        if (vet is null)
            return false;
        if (activar) vet.Activar(); else vet.Desactivar();
        await _Cafeterias.ActualizarAsync(vet, ct);

        Sucursal? m = await MatrizAsync(CafeteriaId, ct);
        if (m is not null)
        {
            if (activar) m.Activar(); else m.Desactivar();
            await _sucursales.ActualizarAsync(m, ct);
        }
        return true;
    }

    private async Task<Result<SucursalDto>> ConSucursalAsync(Guid id, Func<Sucursal, Result<bool>> accion, CancellationToken ct)
    {
        Sucursal? s = await _sucursales.ObtenerPorIdAsync(id, ct);
        if (s is null)
            return Result<SucursalDto>.Falla("La sucursal no existe.");
        Result<bool> r = accion(s);
        if (!r.EsExito)
            return Result<SucursalDto>.Falla(r.Error!);
        await _sucursales.ActualizarAsync(s, ct);
        return Result<SucursalDto>.Exito(SucursalDto.Desde(s));
    }

    /// <summary>Matriz de la Cafeteria; la crea (heredada) si aún no existe. Null si no existe la Cafeteria.</summary>
    private async Task<Sucursal?> MatrizAsync(Guid CafeteriaId, CancellationToken ct)
    {
        Sucursal? m = (await _sucursales.ObtenerTodosAsync(ct))
            .FirstOrDefault(s => s.CafeteriaId == CafeteriaId && s.EsMatriz);
        if (m is not null)
            return m;
        Cafeteria? vet = await _Cafeterias.ObtenerPorIdAsync(CafeteriaId, ct);
        return vet is null ? null : await CrearMatrizHeredadaAsync(vet, ct);
    }

    /// <summary>Crea la Matriz de una Cafeteria anterior a las sucursales, con su plan/fecha/estado.</summary>
    private async Task<Sucursal> CrearMatrizHeredadaAsync(Cafeteria v, CancellationToken ct)
    {
        DateOnly? fecha = v.FechaRenovacion == default ? null : v.FechaRenovacion;
        Sucursal m = Sucursal.Crear(v.Id, "Matriz", v.Direccion, v.Telefono, v.Plan, null, esMatriz: true, fechaRenovacion: fecha, hoy: Hoy()).Valor!;
        if (!v.Activa)
            m.Desactivar();
        await _sucursales.AgregarAsync(m, ct);
        return m;
    }

    private static CafeteriaConSucursalesDto Mapear(Cafeteria v, IEnumerable<Sucursal> sucursales)
    {
        List<SucursalDto> lista = sucursales
            .OrderByDescending(s => s.EsMatriz)
            .ThenBy(s => s.Nombre)
            .Select(SucursalDto.Desde)
            .ToList();
        SucursalDto? matriz = lista.FirstOrDefault(s => s.EsMatriz);
        return new CafeteriaConSucursalesDto(
            v.Id, v.Nombre, v.Telefono, matriz?.Direccion ?? v.Direccion, v.Activa, v.FechaAlta,
            matriz?.Plan ?? v.Plan, matriz?.FechaRenovacion ?? v.FechaRenovacion, lista);
    }
}
