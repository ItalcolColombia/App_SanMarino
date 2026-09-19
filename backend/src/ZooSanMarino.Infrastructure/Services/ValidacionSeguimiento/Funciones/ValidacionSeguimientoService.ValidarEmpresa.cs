// Validar TODOS los pendientes de una empresa: lo que se hace justo antes de APAGAR su doble validación.
// Partial de ValidacionSeguimientoService (namespace plano).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ValidacionSeguimientoService
{
    /// <summary>
    /// Rondas de re-lectura de pendientes: entre que se lee el conjunto y se termina de validar, un
    /// operario todavía puede guardar un día nuevo (el flag sigue encendido hasta el final). Cada ronda
    /// recoge lo que apareció; si tras la última siguen apareciendo, se cuentan como «no intentados» y el
    /// apagado se rechaza en vez de dejar un pendiente atrás.
    /// </summary>
    private const int MaxRondasValidacionEmpresa = 3;

    /// <summary>
    /// Pasadas por lote: cada pasada valida hasta <see cref="ValidacionEnBloqueCalculos.MaxRegistrosPorBloque"/>
    /// registros y se repite mientras sobren. Es una red de seguridad contra un bucle sin progreso.
    /// </summary>
    private const int MaxPasadasPorLote = 50;

    /// <inheritdoc />
    public async Task<ResultadoValidacionEmpresaDto> ValidarPendientesDeLaEmpresaAsync(
        int companyId, CancellationToken ct = default)
    {
        var inicial = await LeerLotesConPendientesDeLaEmpresaAsync(companyId, ct);
        var pendientes = inicial.Sum(g => g.Pendientes);

        if (inicial.Count == 0)
            return new ResultadoValidacionEmpresaDto(0, 0, 0, 0, 0, 0, 0m, 0, []);

        // Con pendientes, la validación descuenta inventario y aves de la empresa: solo se puede hacer
        // desde su propia sesión. Sin pendientes no se pregunta (arriba), apagar no exige nada.
        if (!ApagadoDobleValidacionCalculos.PuedeValidarDesdeEmpresaActiva(companyId, _current.CompanyId))
            throw new InvalidOperationException(
                ApagadoDobleValidacionCalculos.MensajeEmpresaActivaDistinta(pendientes));

        var validados = 0;
        var yaValidados = 0;
        var fallidos = 0;
        var noIntentados = 0;
        var kg = 0m;
        var aves = 0;
        var fallos = new List<FalloValidacionEmpresaDto>();

        // Lotes que cortaron: se reportan una vez y no se reintentan en las rondas siguientes.
        var cortados = new HashSet<(string Modulo, int LoteId)>();

        var grupos = inicial;
        for (var ronda = 0; ronda < MaxRondasValidacionEmpresa && grupos.Count > 0; ronda++)
        {
            foreach (var g in grupos)
            {
                ct.ThrowIfCancellationRequested();

                ResultadoValidacionEnBloqueDto? ultimo = null;
                var terminado = false;

                for (var pasada = 0; pasada < MaxPasadasPorLote && !terminado; pasada++)
                {
                    var r = await ValidarPendientesDelLoteSinPermisoAsync(g.Modulo, g.LoteId, ct);
                    ultimo = r;

                    validados += r.Validados;
                    yaValidados += r.YaValidados;
                    kg += r.KgAplicados;
                    aves += r.AvesDescontadas;

                    if (r.Fallidos > 0)
                    {
                        // El corte de un lote no frena a los demás: se listan todos de una vez.
                        fallidos += r.Fallidos;
                        noIntentados += r.NoIntentados;
                        cortados.Add((g.Modulo, g.LoteId));
                        fallos.Add(new FalloValidacionEmpresaDto(
                            g.Modulo, g.LoteId, r.SeguimientoCorte ?? 0,
                            r.FechaCorte ?? default, r.MotivoCorte ?? "no se pudo validar"));
                        terminado = true;
                    }
                    else if (r.NoIntentados == 0)
                    {
                        terminado = true;                      // agotó los pendientes del lote
                    }
                    else if (r.Validados + r.YaValidados == 0)
                    {
                        // Sobran pendientes pero esta pasada no avanzó: no se insiste.
                        noIntentados += r.NoIntentados;
                        cortados.Add((g.Modulo, g.LoteId));
                        terminado = true;
                    }
                    // else: quedaron fuera del tope de la pasada → otra pasada.
                }

                if (!terminado && ultimo is not null)
                {
                    // Se agotaron las pasadas con pendientes todavía: se cuentan y el lote queda cortado.
                    noIntentados += ultimo.NoIntentados;
                    cortados.Add((g.Modulo, g.LoteId));
                }
            }

            // Lo que apareció mientras tanto (el flag sigue encendido): se recoge en otra ronda.
            grupos = (await LeerLotesConPendientesDeLaEmpresaAsync(companyId, ct))
                .Where(x => !cortados.Contains((x.Modulo, x.LoteId)))
                .ToList();
        }

        // Tras la última ronda todavía hay lotes con pendientes que no cortaron (llegan más rápido de lo
        // que se validan): no se apaga con pendientes atrás.
        foreach (var g in grupos)
            noIntentados += g.Pendientes;

        _logger?.LogInformation(
            "Validación previa al apagado de la doble validación, empresa {CompanyId}: {Pendientes} pendientes en " +
            "{Lotes} lotes → {Validados} validados, {YaValidados} ya validados, {Fallidos} cortes, {NoIntentados} sin " +
            "intentar; {Kg} kg y {Aves} aves aplicados.",
            companyId, pendientes, inicial.Count, validados, yaValidados, fallidos, noIntentados, kg, aves);

        return new ResultadoValidacionEmpresaDto(
            LotesConPendientes: inicial.Count,
            Pendientes: pendientes,
            Validados: validados,
            YaValidados: yaValidados,
            Fallidos: fallidos,
            NoIntentados: noIntentados,
            KgAplicados: kg,
            AvesDescontadas: aves,
            Fallos: fallos);
    }

    /// <summary>Un lote con pendientes, con la clave que entiende <see cref="LeerPendientesDelLoteAsync"/>.</summary>
    private sealed record LoteConPendientes(string Modulo, int LoteId, int Pendientes);

    /// <summary>
    /// Lotes de la empresa con algún registro sin validar, en los cuatro módulos y con la MISMA clave de
    /// lote que usa <see cref="LeerPendientesDelLoteAsync"/> (para que cada lote que se devuelve acá se
    /// pueda validar con el bloque por lote). La empresa sale del dato —columna propia en producción y
    /// levante, el lote en engorde y reproductora—, nunca de la sesión.
    ///
    /// <para>
    /// Los lotes borrados de engorde no cuentan: <c>LeerPendientesDelLoteAsync</c> los ignora, y sus
    /// reservas las libera <see cref="LiberarDelLoteEngordeAsync"/> al borrarlos.
    /// </para>
    /// </summary>
    private async Task<List<LoteConPendientes>> LeerLotesConPendientesDeLaEmpresaAsync(int companyId, CancellationToken ct)
    {
        var resultado = new List<LoteConPendientes>();
        if (companyId <= 0) return resultado;

        // Levante: el id del lote puede ser el de postura-levante o el `lote_id` legado (texto); la
        // empresa sale de la fila y, si viene vacía, del lote (mismo criterio que LeerPendientesDelLoteAsync).
        var filasLevante = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(s => !s.Validado && s.TipoSeguimiento == "levante")
            .Select(s => new { s.LotePosturaLevanteId, s.LoteId, s.CompanyId })
            .ToListAsync(ct);

        var empresaPorLoteLevante = new Dictionary<int, int>();
        var levante = new Dictionary<int, int>();
        foreach (var f in filasLevante)
        {
            var loteRef = f.LotePosturaLevanteId ?? (int.TryParse(f.LoteId, out var li) ? li : 0);
            if (loteRef <= 0) continue;

            var empresaFila = f.CompanyId is > 0 ? f.CompanyId.Value : 0;
            if (empresaFila == 0)
            {
                if (!empresaPorLoteLevante.TryGetValue(loteRef, out empresaFila))
                {
                    empresaFila = await LeerCompanyDelLoteLevanteAsync(loteRef, ct);
                    empresaPorLoteLevante[loteRef] = empresaFila;
                }
            }

            if (empresaFila != companyId) continue;
            levante[loteRef] = levante.GetValueOrDefault(loteRef) + 1;
        }
        resultado.AddRange(levante.Select(kv => new LoteConPendientes(ModuloSeguimiento.Levante, kv.Key, kv.Value)));

        // Producción: la clave es el lote de postura-producción o, si la fila no lo trae, el lote base.
        var produccion = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => !s.Validado && s.DeletedAt == null && s.CompanyId == companyId)
            .GroupBy(s => s.LotePosturaProduccionId ?? s.LoteId)
            .Select(g => new { LoteId = g.Key, N = g.Count() })
            .ToListAsync(ct);
        resultado.AddRange(produccion.Select(g => new LoteConPendientes(ModuloSeguimiento.Produccion, g.LoteId, g.N)));

        // Engorde (los dos módulos comparten tabla): la empresa la trae el lote.
        var engorde = await (
                from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                where !s.Validado && l.CompanyId == companyId && l.DeletedAt == null
                group s by s.LoteAveEngordeId into g
                select new { LoteId = g.Key, N = g.Count() })
            .ToListAsync(ct);
        resultado.AddRange(engorde.Select(g => new LoteConPendientes(ModuloSeguimiento.Engorde, g.LoteId, g.N)));

        // Reproductora: «pendiente» es `confirmado = false`; la empresa cuelga del lote de engorde.
        var reproductora = await (
                from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                join lr in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals lr.Id
                join l in _ctx.LoteAveEngorde.AsNoTracking() on lr.LoteAveEngordeId equals l.LoteAveEngordeId
                where !s.Confirmado && l.CompanyId == companyId
                group s by s.LoteReproductoraAveEngordeId into g
                select new { LoteId = g.Key, N = g.Count() })
            .ToListAsync(ct);
        resultado.AddRange(reproductora.Select(g => new LoteConPendientes(ModuloSeguimiento.Reproductora, g.LoteId, g.N)));

        return resultado;
    }
}
