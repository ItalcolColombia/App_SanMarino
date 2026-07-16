// Orquestación del puente: recorre el origen (solo GET), aplica filtros (año, cliente/granja, fecha hasta),
// importa la guía genética y hace upsert idempotente reutilizando los servicios de negocio. Orden de FKs:
// Guía genética → Granja → Núcleo → Galpón → Lote → Reproductora + su seguimiento (días 1-7, dispara el
// trigger de cruce) → Seguimiento del lote (día 8+). En dry-run no inserta: cuenta, corre el preflight
// (geografía, empresa, país) y arma el detalle por lote para la vista del front.
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.PuentePanama;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class PuentePanamaService
{
    // ── Métodos de apoyo al front (conexión + filtros) ────────────────────────
    public async Task<ConexionResultDto> ProbarConexionAsync(PanamaConexion? origen, CancellationToken ct = default)
    {
        _api.UsarConexion(origen);
        return await _api.ProbarConexionAsync(ct);
    }

    public async Task<IReadOnlyList<PanamaCliente>> GetClientesOrigenAsync(PanamaConexion? origen, CancellationToken ct = default)
    {
        _api.UsarConexion(origen);
        return await _api.GetClientesAsync(ct);
    }

    public async Task<IReadOnlyList<PanamaGranja>> GetGranjasOrigenAsync(PanamaConexion? origen, int? clienteIdOrigen, CancellationToken ct = default)
    {
        _api.UsarConexion(origen);
        if (clienteIdOrigen.HasValue)
            return await _api.GetGranjasByClienteAsync(clienteIdOrigen.Value, ct);

        var todas = new List<PanamaGranja>();
        foreach (var cli in await _api.GetClientesAsync(ct))
            todas.AddRange(await _api.GetGranjasByClienteAsync(cli.Id, ct));
        return todas;
    }

    // ── Estado de una corrida ─────────────────────────────────────────────────
    private sealed class RunCtx
    {
        public SincronizarPanamaRequest Req = null!;
        public ResultadoSincronizacionDto R = null!;
        public string RazaOverride = "";
        public string? UserId;
        public bool ImportarGuia;
        public bool CrearGuiaFake;
        public HashSet<string> RazaAnioGuia = new();
        public List<PanamaGuiaGenetica> GuiaFilas = new();
        public bool GuiaFilasCargadas;
        public HashSet<string> GuiasAseguradas = new();
        public List<string> GuiasDisplay = new();                          // etiqueta legible por guía asegurada ("RAZA AÑO (PRUEBA)")
        public Dictionary<string, int> PendientesPorCausa = new();         // causa → cantidad de lotes (resumen de pendientes)
        public Dictionary<int, string> LineaPorId = new();                 // Listas tipo 1 del origen
        public Dictionary<int, string> LesionTipoPorId = new();            // Listas tipo 13 del origen (tipos de lesión)
        public Dictionary<string, int> FarmIdPorNombre = new();
        public HashSet<string> FarmsEliminadasNombre = new();              // soft-deleted homónimas (clave normalizada)
        public Dictionary<string, int> GalponGranja = new(StringComparer.OrdinalIgnoreCase); // galponId → granjaId destino
        public Dictionary<string, int> LoteIdPorErp = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LotesErpEliminados = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<int> GranjasConNucleo = new();
        public HashSet<int> FarmsAsignadasAlUsuario = new();
        public int DepartamentoIdDefecto;
        public int MunicipioIdDefecto;
        public Dictionary<string, int> DepPorNombre = new();               // nombre normalizado → id
        public Dictionary<(int dep, string muni), int> MuniPorDepNombre = new();
        public Dictionary<int, int> MuniPrimeroPorDep = new();             // primer municipio por departamento
    }

    public async Task<ResultadoSincronizacionDto> SincronizarAsync(SincronizarPanamaRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _api.UsarConexion(request.Origen);

        var opt = PuentePanamaOptions.FromConfig(_config);
        var companyId = await GetEffectiveCompanyIdAsync();

        var r = new ResultadoSincronizacionDto { DryRun = request.DryRun, Anio = request.Anio, CompanyId = companyId };
        var c = new RunCtx
        {
            Req = request,
            R = r,
            RazaOverride = (!string.IsNullOrWhiteSpace(request.GeneticaRaza) ? request.GeneticaRaza! : opt.GeneticaRaza).Trim(),
            UserId = _current.UserId.ToString(),
            ImportarGuia = request.ImportarGuiaGenetica,
            CrearGuiaFake = request.CrearGuiaFakeSiFalta,
            RazaAnioGuia = await CargarRazaAnioGuiaAsync(companyId, ct)
        };

        // ── PREFLIGHT (corre también en dry-run: si falla acá, la corrida real fallaría entera) ──
        if (!await PreflightAsync(c, opt, companyId, ct))
        {
            r.Estado = "Fallido";
            r.DuracionMs = sw.ElapsedMilliseconds;
            await RegistrarAuditoriaAsync(companyId, r, ct);
            return r;
        }

        // Catálogos del origen: líneas genéticas (Listas tipo 1) → raza por lote; tipos de lesión (tipo 13).
        try
        {
            var listas = await _api.GetListasAsync(ct);
            c.LineaPorId = listas
                .Where(x => x.IdTipoLista == PuentePanamaCalculos.TipoListaLineaGenetica && !string.IsNullOrWhiteSpace(x.Nombre))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Nombre!.Trim());
            c.LesionTipoPorId = listas
                .Where(x => x.IdTipoLista == PuentePanamaCalculos.TipoListaLesion && !string.IsNullOrWhiteSpace(x.Nombre))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Nombre!.Trim());
        }
        catch (Exception ex)
        {
            r.Mensajes.Add($"No se pudo leer el catálogo de líneas genéticas del origen: {ex.Message}. Se usará solo la raza global si está configurada.");
        }

        // ── Estado destino precargado (idempotencia) ──────────────────────────
        foreach (var f in await _ctx.Farms.AsNoTracking()
                     .Where(f => f.CompanyId == companyId)
                     .Select(f => new { f.Id, f.Name, f.DeletedAt }).ToListAsync(ct))
        {
            var clave = MigracionCalculos.NormalizarClave(f.Name);
            if (f.DeletedAt == null) c.FarmIdPorNombre[clave] = f.Id;
            else c.FarmsEliminadasNombre.Add(clave);
        }

        c.GalponGranja = (await _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == companyId)
            .Select(g => new { g.GalponId, g.GranjaId }).ToListAsync(ct))
            .GroupBy(g => g.GalponId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().GranjaId, StringComparer.OrdinalIgnoreCase);

        foreach (var l in await _ctx.LoteAveEngorde.AsNoTracking()
                     .Where(l => l.CompanyId == companyId && l.LoteErp != null)
                     .Select(l => new { l.LoteErp, l.LoteAveEngordeId, l.DeletedAt }).ToListAsync(ct))
        {
            if (l.DeletedAt == null) c.LoteIdPorErp[l.LoteErp!] = l.LoteAveEngordeId ?? 0;
            else c.LotesErpEliminados.Add(l.LoteErp!);
        }

        c.GranjasConNucleo = (await _ctx.Nucleos.AsNoTracking()
            .Where(n => n.NucleoId == PuentePanamaCalculos.NucleoIdPorDefecto)
            .Select(n => n.GranjaId).ToListAsync(ct)).ToHashSet();

        if (_current.UserGuid.HasValue)
            c.FarmsAsignadasAlUsuario = (await _ctx.UserFarms.AsNoTracking()
                .Where(uf => uf.UserId == _current.UserGuid.Value)
                .Select(uf => uf.FarmId).ToListAsync(ct)).ToHashSet();

        // ── Recorrido origen (solo lectura) ───────────────────────────────────
        foreach (var cli in await _api.GetClientesAsync(ct))
        {
            if (request.ClienteIdOrigen.HasValue && cli.Id != request.ClienteIdOrigen.Value) continue;
            var (depId, muniId) = ResolverGeoCliente(c, cli);

            foreach (var g in await _api.GetGranjasByClienteAsync(cli.Id, ct))
            {
                if (request.GranjaIdOrigen.HasValue && g.Id != request.GranjaIdOrigen.Value) continue;

                r.GranjasVistas++;
                var claveNombre = MigracionCalculos.NormalizarClave(g.Nombre ?? $"Granja {g.Id}");

                int granjaId;
                if (c.FarmIdPorNombre.TryGetValue(claveNombre, out var existId))
                {
                    granjaId = existId;
                    // Granja reutilizada: asegurar user_farms del ejecutor (las nuevas quedan asignadas
                    // por FarmService.CreateAsync; sin esto, la creación del lote falla por permiso).
                    await EnsureUserFarmAsync(c, granjaId, ct);
                }
                else if (c.FarmsEliminadasNombre.Contains(claveNombre))
                {
                    r.Mensajes.Add($"Granja '{g.Nombre}' (id {g.Id}): existe una granja ELIMINADA con ese nombre en la empresa. Restaurala o renombrala; se omite su contenido.");
                    continue;
                }
                else
                {
                    r.GranjasNuevas++;
                    granjaId = 0;
                    if (!request.DryRun)
                    {
                        try
                        {
                            var creada = await _farmService.CreateAsync(PuentePanamaCalculos.MapGranja(g, companyId, depId, muniId));
                            granjaId = creada.Id;
                            c.FarmIdPorNombre[claveNombre] = granjaId;
                            c.FarmsAsignadasAlUsuario.Add(granjaId); // CreateAsync auto-asigna al creador
                        }
                        catch (Exception ex)
                        {
                            r.Mensajes.Add($"Granja '{g.Nombre}' (id {g.Id}): no se pudo crear ({ex.Message}). Se omite su contenido.");
                            continue;
                        }
                    }
                }

                if (!request.DryRun && granjaId > 0 && !c.GranjasConNucleo.Contains(granjaId))
                {
                    try
                    {
                        await _nucleoService.CreateAsync(PuentePanamaCalculos.MapNucleo(granjaId));
                        c.GranjasConNucleo.Add(granjaId);
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Ya existe", StringComparison.OrdinalIgnoreCase))
                    {
                        c.GranjasConNucleo.Add(granjaId); // duplicado/carrera: el núcleo ya está
                    }
                    catch (Exception ex)
                    {
                        r.Mensajes.Add($"Granja '{g.Nombre}' (id {g.Id}): no se pudo crear el núcleo por defecto ({ex.Message}).");
                    }
                }

                foreach (var gp in await _api.GetGalponesByGranjaAsync(g.Id, ct))
                {
                    r.GalponesVistos++;
                    var galponEsperado = PuentePanamaCalculos.ClaveGalpon(gp.Id);
                    string? galponIdDestino = galponEsperado;

                    if (c.GalponGranja.TryGetValue(galponEsperado, out var granjaDelGalpon))
                    {
                        if (granjaId > 0 && granjaDelGalpon != granjaId)
                        {
                            // Granja renombrada en el origen: el galpón quedó bajo otra granja destino.
                            r.Mensajes.Add($"Galpón '{gp.Nombre}' ({galponEsperado}): existe bajo otra granja destino (id {granjaDelGalpon}); los lotes de este galpón se crean SIN galpón asignado. Revisá el renombre de la granja.");
                            galponIdDestino = null;
                        }
                    }
                    else
                    {
                        r.GalponesNuevos++;
                        if (!request.DryRun && granjaId > 0)
                        {
                            try
                            {
                                // El servicio puede autogenerar OTRO id si 'PA-x' colisiona globalmente:
                                // usar SIEMPRE el id devuelto como el id real del galpón destino.
                                var creado = await _galponService.CreateAsync(PuentePanamaCalculos.MapGalpon(gp, granjaId));
                                galponIdDestino = creado.GalponId;
                                if (!string.Equals(galponIdDestino, galponEsperado, StringComparison.OrdinalIgnoreCase))
                                    r.Mensajes.Add($"Galpón '{gp.Nombre}': la clave '{galponEsperado}' estaba ocupada; se creó como '{galponIdDestino}'.");
                                c.GalponGranja[galponIdDestino] = granjaId;
                            }
                            catch (Exception ex)
                            {
                                r.Mensajes.Add($"Galpón '{gp.Nombre}' (id {gp.Id}): no se pudo crear ({ex.Message}). Los lotes se crean sin galpón.");
                                galponIdDestino = null;
                            }
                        }
                    }

                    foreach (var lote in await _api.GetLotesByGalponAsync(gp.Id, ct))
                    {
                        if (!PuentePanamaCalculos.LotePasaFiltros(lote, request.Anio, request.FechaHasta)) continue;
                        r.LotesEnAnio++;
                        await ProcesarLoteAsync(c, lote, granjaId, galponIdDestino, g.Nombre, gp.Nombre, ct);
                    }
                }
            }
        }

        // Resumen de PENDIENTES al frente de los mensajes (queda también en la auditoría/errores_json):
        // sin esto la corrida reportaba "Ok" con 0 procesados y no quedaba rastro del porqué.
        if (r.LotesPendientes > 0)
        {
            var causas = string.Join("; ", c.PendientesPorCausa
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key} ({kv.Value})"));
            r.Mensajes.Insert(0,
                $"{r.LotesPendientes} lote(s) quedaron PENDIENTES y NO se {(r.DryRun ? "crearían" : "crearon")}: {causas}. " +
                "Activá 'Importar guía genética' (o la guía de prueba) o cargá la guía real y volvé a ejecutar: la corrida es idempotente.");
        }

        r.DuracionMs = sw.ElapsedMilliseconds;
        r.Estado = r.LotesConError > 0 || r.LotesPendientes > 0 || r.Mensajes.Count > 0 ? "ConAdvertencias" : "Ok";
        await RegistrarAuditoriaAsync(companyId, r, ct);
        return r;
    }

    /// <summary>Registra un lote pendiente agrupando por causa (para el resumen final y la auditoría).</summary>
    private static void AgregarPendiente(RunCtx c, string causa)
    {
        c.R.LotesPendientes++;
        c.PendientesPorCausa[causa] = c.PendientesPorCausa.GetValueOrDefault(causa) + 1;
    }

    // ── Preflight: empresa destino, geografía y país de la empresa (corre en dry-run y real) ──
    private async Task<bool> PreflightAsync(RunCtx c, PuentePanamaOptions opt, int companyId, CancellationToken ct)
    {
        var r = c.R;

        // 1) Empresa destino esperada.
        if (!string.IsNullOrWhiteSpace(opt.EmpresaDestino))
        {
            var esperado = await _companyResolver.GetCompanyIdByNameAsync(opt.EmpresaDestino);
            if (esperado.HasValue && esperado.Value != companyId)
            {
                r.Mensajes.Add($"La empresa activa (id {companyId}) no es la destino esperada '{opt.EmpresaDestino}' (id {esperado}). Cambiá la empresa activa antes de sincronizar.");
                return false;
            }
        }

        // 2) Geografía: precargar departamentos + municipios y resolver defaults.
        var deps = await _ctx.Set<Departamento>().AsNoTracking()
            .Select(d => new { d.DepartamentoId, d.DepartamentoNombre, d.PaisId }).ToListAsync(ct);
        foreach (var d in deps)
            c.DepPorNombre[MigracionCalculos.NormalizarClave(d.DepartamentoNombre)] = d.DepartamentoId;

        var munis = await _ctx.Set<Municipio>().AsNoTracking()
            .Select(m => new { m.MunicipioId, m.MunicipioNombre, m.DepartamentoId }).ToListAsync(ct);
        foreach (var m in munis)
        {
            c.MuniPorDepNombre[(m.DepartamentoId, MigracionCalculos.NormalizarClave(m.MunicipioNombre))] = m.MunicipioId;
            if (!c.MuniPrimeroPorDep.ContainsKey(m.DepartamentoId)) c.MuniPrimeroPorDep[m.DepartamentoId] = m.MunicipioId;
        }

        // Default: config → primer departamento del país Panamá.
        int? depDefecto = opt.DepartamentoId;
        if (depDefecto is null)
        {
            var paisPanama = await _ctx.Paises.AsNoTracking()
                .Where(p => p.PaisNombre.ToLower().Contains("panam"))
                .Select(p => (int?)p.PaisId).FirstOrDefaultAsync(ct);
            depDefecto = deps.Where(d => paisPanama.HasValue && d.PaisId == paisPanama.Value)
                             .OrderBy(d => d.DepartamentoId).Select(d => (int?)d.DepartamentoId).FirstOrDefault();
        }
        if (depDefecto is null || !deps.Any(d => d.DepartamentoId == depDefecto.Value))
        {
            r.Mensajes.Add("No se pudo resolver un departamento por defecto para las granjas (no hay departamentos de Panamá cargados y PuentePanama:DepartamentoId no está configurado). Las granjas del destino exigen departamento/municipio.");
            return false;
        }
        int? muniDefecto = opt.MunicipioId;
        if (muniDefecto is null || !munis.Any(m => m.MunicipioId == muniDefecto.Value && m.DepartamentoId == depDefecto.Value))
            muniDefecto = c.MuniPrimeroPorDep.TryGetValue(depDefecto.Value, out var m0) ? m0 : (int?)null;
        if (muniDefecto is null)
        {
            r.Mensajes.Add($"El departamento por defecto (id {depDefecto}) no tiene municipios cargados; configurá PuentePanama:MunicipioId.");
            return false;
        }
        c.DepartamentoIdDefecto = depDefecto.Value;
        c.MunicipioIdDefecto = muniDefecto.Value;

        // 3) La empresa debe tener el país del departamento asignado (mismo gate de FarmService.CreateAsync).
        var paisDep = deps.First(d => d.DepartamentoId == depDefecto.Value).PaisId;
        var tienePais = await _ctx.CompanyPaises.AsNoTracking()
            .AnyAsync(cp => cp.CompanyId == companyId && cp.PaisId == paisDep, ct);
        if (!tienePais)
            r.Mensajes.Add($"ADVERTENCIA: la empresa {companyId} no tiene el país (id {paisDep}) asignado en empresa-país; la creación de granjas fallará salvo que tu usuario tenga permiso de crear granjas en ese país.");

        return true;
    }

    /// <summary>Departamento/municipio de las granjas de un cliente: por nombre desde el cliente origen, con fallback a los defaults.</summary>
    private (int depId, int muniId) ResolverGeoCliente(RunCtx c, PanamaCliente cli)
    {
        var depId = c.DepartamentoIdDefecto;
        var muniId = c.MunicipioIdDefecto;
        if (!string.IsNullOrWhiteSpace(cli.DepartamentoText) &&
            c.DepPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(cli.DepartamentoText), out var d))
        {
            depId = d;
            muniId = c.MuniPrimeroPorDep.TryGetValue(d, out var m0) ? m0 : c.MunicipioIdDefecto;
        }
        if (!string.IsNullOrWhiteSpace(cli.MunicipioText) &&
            c.MuniPorDepNombre.TryGetValue((depId, MigracionCalculos.NormalizarClave(cli.MunicipioText)), out var m))
            muniId = m;
        return (depId, muniId);
    }

    /// <summary>Asegura user_farms (ejecutor, granja) para granjas reutilizadas; sin esto el lote falla por permiso.</summary>
    private async Task EnsureUserFarmAsync(RunCtx c, int granjaId, CancellationToken ct)
    {
        if (c.Req.DryRun || !_current.UserGuid.HasValue || c.FarmsAsignadasAlUsuario.Contains(granjaId)) return;
        _ctx.UserFarms.Add(new UserFarm
        {
            UserId = _current.UserGuid.Value,
            FarmId = granjaId,
            IsAdmin = false,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _current.UserGuid.Value
        });
        await _ctx.SaveChangesAsync(ct);
        c.FarmsAsignadasAlUsuario.Add(granjaId);
    }

    // ── Guía genética: asegura (o previsualiza) la guía (raza, año) ────────────
    // Orden: (1) ya existe en la empresa → ok; (2) importarla del origen (si está habilitado y responde);
    // (3) red de seguridad: guía de PRUEBA (FAKE) claramente marcada (datos del origen si estaban
    // disponibles; si no, curva placeholder) para que los lotes no queden pendientes.
    private async Task<bool> EnsureGuiaAsync(RunCtx c, string raza, int anio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raza)) return false;
        var key = Clave(raza, anio.ToString());
        if (c.RazaAnioGuia.Contains(key)) return true;
        if (!c.ImportarGuia && !c.CrearGuiaFake) return false;

        // Filas del origen: se cargan una sola vez y sirven tanto para la importación real
        // como de mejores-datos-disponibles para la guía de prueba.
        if (!c.GuiaFilasCargadas)
        {
            c.GuiaFilasCargadas = true;
            try { c.GuiaFilas = (await _api.GetGuiaGeneticaAsync(ct)).ToList(); }
            catch (Exception ex) { c.R.Mensajes.Add($"No se pudo leer la guía genética del origen: {ex.Message}"); }
        }

        var esReal = c.ImportarGuia && c.GuiaFilas.Count > 0;
        if (!esReal && !c.CrearGuiaFake) return false;

        var filas = c.GuiaFilas.Count > 0 ? c.GuiaFilas : PuentePanamaCalculos.GuiaFakePlaceholder();

        if (c.Req.DryRun)
        {
            if (c.GuiasAseguradas.Add(key))
            {
                c.R.GuiaGeneticaFilas += filas.Count;
                RegistrarGuiaAsegurada(c, raza, anio, esReal, conDatosOrigen: c.GuiaFilas.Count > 0);
            }
            return true;
        }

        try
        {
            var items = PuentePanamaCalculos.MapGuiaGeneticaDetalle(filas);
            await _guiaGeneticaService.UpsertManualAsync(
                new GuiaGeneticaEcuadorManualRequestDto(raza, anio, "mixto", "active", items), ct);
            c.RazaAnioGuia.Add(key);
            c.GuiasAseguradas.Add(key);
            c.R.GuiaGeneticaFilas += items.Count;
            RegistrarGuiaAsegurada(c, raza, anio, esReal, conDatosOrigen: c.GuiaFilas.Count > 0);
            return true;
        }
        catch (Exception ex)
        {
            // No abortar la corrida: el lote queda Pendiente y el motivo visible.
            c.R.Mensajes.Add($"No se pudo crear la guía genética para raza '{raza}' año {anio}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Etiqueta la guía asegurada y, si es de PRUEBA, deja el rastro fuerte (flag + mensaje).</summary>
    private static void RegistrarGuiaAsegurada(RunCtx c, string raza, int anio, bool esReal, bool conDatosOrigen)
    {
        c.GuiasDisplay.Add(esReal ? $"{raza} {anio}" : $"{raza} {anio} (PRUEBA)");
        c.R.GuiaGeneticaRazaAnio = string.Join(" · ", c.GuiasDisplay);
        if (esReal)
        {
            if (!c.Req.DryRun) c.R.GuiaGeneticaImportada = true; // en dry-run solo se previsualiza
            return;
        }
        c.R.GuiaGeneticaFakeCreada = true;
        var accion = c.Req.DryRun ? "se crearía" : "fue creada";
        var datos = conDatosOrigen
            ? "con los datos de consumo del origen"
            : "con una curva PLACEHOLDER (14→264 g/día, 49 días)";
        c.R.Mensajes.Add(
            $"GUÍA DE PRUEBA (FAKE) {accion} para raza '{raza}' año {anio} {datos}. " +
            "Cargá la guía genética real de esa raza/año: los indicadores contra guía NO son válidos hasta entonces.");
    }

    // ── Lote engorde + su reproductora (primero) + su seguimiento día 8+ ──────
    private async Task ProcesarLoteAsync(RunCtx c, PanamaLote lote, int granjaId, string? galponId, string? granjaNombre, string? galponNombre, CancellationToken ct)
    {
        var r = c.R;
        var prev = new LotePreviewDto
        {
            IdOrigen = lote.Id,
            Lote = PuentePanamaCalculos.NombreLote(lote),
            Granja = granjaNombre,
            Galpon = galponNombre,
            FechaInicio = lote.FechaRegistroInicio,
            AvesEncasetadas = lote.NumAvesEncasetadas
        };
        r.Lotes.Add(prev);

        var loteErp = PuentePanamaCalculos.LoteErp(lote.Id);
        var anio = PuentePanamaCalculos.AnioTablaGenetica(lote, c.Req.GeneticaAnio);
        if (anio is null || !lote.FechaRegistroInicio.HasValue)
        {
            r.LotesConError++;
            prev.Estado = "Error";
            prev.Mensaje = "Sin fecha de inicio: no se puede determinar el año de tabla genética ni las fechas.";
            r.Mensajes.Add($"Lote {prev.Lote} (id {lote.Id}): {prev.Mensaje}");
            return;
        }
        var fechaEncaset = lote.FechaRegistroInicio.Value;

        // Lote eliminado (soft-delete) en destino: no se re-crea ni se toca su contenido.
        if (c.LotesErpEliminados.Contains(loteErp) && !c.LoteIdPorErp.ContainsKey(loteErp))
        {
            r.LotesOmitidos++;
            prev.Estado = "YaExiste";
            prev.Mensaje = "El lote fue eliminado en el destino; no se re-crea (restauralo si corresponde).";
            return;
        }

        // Raza por lote: override global > línea genética del origen. La línea del origen (si viene)
        // queda visible en el lote (campo Linea). Sin línea NI override → Pendiente (falta guía/raza).
        string? lineaOrigen = (lote.IdLineaGeneticaLista ?? 0) > 0
            ? c.LineaPorId.GetValueOrDefault(lote.IdLineaGeneticaLista!.Value)
            : null;
        var razaLote = !string.IsNullOrWhiteSpace(c.RazaOverride) ? c.RazaOverride : lineaOrigen;
        prev.Raza = razaLote;

        int loteId;
        if (c.LoteIdPorErp.TryGetValue(loteErp, out var existente))
        {
            loteId = existente;
            r.LotesOmitidos++;
            prev.Estado = "YaExiste";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(razaLote))
            {
                AgregarPendiente(c, "sin línea genética en el origen ni raza global configurada");
                prev.Estado = "Pendiente";
                prev.Mensaje = "El lote no trae línea genética en el origen y no hay raza global configurada; cargá una raza para poder crearlo.";
                return;
            }
            if (!await EnsureGuiaAsync(c, razaLote, anio.Value, ct))
            {
                AgregarPendiente(c, $"falta guía genética para raza '{razaLote}' año {anio}");
                prev.Estado = "Pendiente";
                prev.Mensaje = $"Falta guía genética para raza '{razaLote}' y año {anio} en la empresa destino.";
                return;
            }

            if (c.Req.DryRun)
            {
                r.LotesNuevos++;
                prev.Estado = "Nuevo";
                loteId = 0; // aún no existe → su seguimiento cuenta como nuevo
            }
            else if (granjaId <= 0)
            {
                r.LotesConError++;
                prev.Estado = "Error";
                prev.Mensaje = "Su granja no pudo crearse.";
                r.Mensajes.Add($"Lote {prev.Lote} (id {lote.Id}): {prev.Mensaje}");
                return;
            }
            else
            {
                try
                {
                    var creado = await _loteAveEngordeService.CreateAsync(
                        PuentePanamaCalculos.MapLote(lote, granjaId, galponId, razaLote, anio.Value, lineaOrigen));
                    loteId = creado.LoteAveEngordeId;
                    c.LoteIdPorErp[loteErp] = loteId;
                    r.LotesNuevos++;
                    prev.Estado = "Nuevo";
                }
                catch (Exception ex)
                {
                    r.LotesConError++;
                    prev.Estado = "Error";
                    prev.Mensaje = ex.Message;
                    r.Mensajes.Add($"Lote {prev.Lote} (id {lote.Id}): error al crear ({ex.Message}).");
                    return;
                }
            }
        }

        // Reproductora + su seguimiento (días 1-7) PRIMERO: al insertar el seguimiento reproductora, el
        // trigger de BD genera los días 1-7 del lote (origen_cruce). El trigger solo materializa el día d
        // cuando TODAS las reproductoras lo tienen → se reporta la cobertura incompleta.
        var (repros, segRepro, lesiones, tieneRepros, coberturaOk, diasFaltantes) =
            await SincronizarReproductoraAsync(c, lote.Id, loteId, fechaEncaset, prev.Lote, granjaId, galponId, ct);
        prev.Reproductoras = repros;
        prev.SeguimientosReproductora = segRepro;
        prev.Lesiones = lesiones;
        if (tieneRepros && !coberturaOk)
            r.Mensajes.Add($"Lote {prev.Lote} (id {lote.Id}): las reproductoras no cubren los 7 días del cruce (faltan días {diasFaltantes}); esos días del lote quedarán sin seguimiento hasta completarlos.");

        // Seguimiento del lote: día 8+ cuando hay reproductoras (los 1-7 los genera el cruce);
        // si el lote NO tiene reproductoras, se importan también los días 1-7 desde InfoProductiva.
        prev.Seguimientos = await SincronizarSeguimientoAsync(c, lote.Id, loteId, fechaEncaset, tieneRepros, ct);
    }

    // ── Seguimiento diario de engorde. Fecha por edad. ─────────────────────────
    private async Task<int> SincronizarSeguimientoAsync(RunCtx c, int loteOrigenId, int loteDestinoId, DateTime fechaEncaset, bool tieneReproductoras, CancellationToken ct)
    {
        var seg = await _api.GetInfoProductivaByLoteAsync(loteOrigenId, ct);
        if (seg.Count == 0) return 0;
        var r = c.R;
        var hasta = c.Req.FechaHasta?.Date;

        var existentes = loteDestinoId > 0
            ? (await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                .Where(s => s.LoteAveEngordeId == loteDestinoId)
                .Select(s => s.Fecha).ToListAsync(ct)).Select(f => f.Date).ToHashSet()
            : new HashSet<DateTime>();

        var vistas = new HashSet<DateTime>();
        int nuevos = 0;
        foreach (var s in seg.OrderBy(x => x.EdadDias ?? 0))
        {
            var edad = s.EdadDias ?? 0;
            if (edad <= 0) continue;
            // Con reproductoras, los días 1-7 los genera el trigger de cruce; sin ellas se importan del lote.
            if (tieneReproductoras && edad <= PuentePanamaCalculos.DiasCruceReproductora) continue;
            var fecha = PuentePanamaCalculos.FechaPorEdad(fechaEncaset, edad).Date;
            if (hasta.HasValue && fecha > hasta.Value) continue;
            if (!vistas.Add(fecha)) continue;
            if (existentes.Contains(fecha)) { r.SeguimientosOmitidos++; continue; }
            if (c.Req.DryRun) { r.SeguimientosNuevos++; nuevos++; continue; }
            try
            {
                await _seguimientoEngordeService.CreateAsync(PuentePanamaCalculos.MapSeguimiento(s, loteDestinoId, fechaEncaset, c.UserId).ToDto());
                r.SeguimientosNuevos++; nuevos++;
            }
            catch (Exception ex)
            {
                r.Mensajes.Add($"Seguimiento lote origen {loteOrigenId} edad {edad}: {ex.Message}");
            }
        }
        return nuevos;
    }

    // ── Reproductora + su seguimiento diario (días 1-7) + sus lesiones ─────────
    // Nomenclatura destino: "<nombreLote>-<n>" (n por orden de id origen) para distinguirlas del lote;
    // el nombre origen queda preservado en CodigoReproductora (junto a la incubadora).
    private async Task<(int repros, int segRepro, int lesiones, bool tieneRepros, bool coberturaOk, string diasFaltantes)>
        SincronizarReproductoraAsync(RunCtx c, int loteOrigenId, int loteDestinoId, DateTime fechaEncaset, string nombreLote, int granjaId, string? galponId, CancellationToken ct)
    {
        var repros = await _api.GetLoteReproductoraByLoteAsync(loteOrigenId, ct);
        var r = c.R;

        var existentes = loteDestinoId > 0
            ? (await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                .Where(x => x.LoteAveEngordeId == loteDestinoId)
                .Select(x => new { x.Id, x.ReproductoraId }).ToListAsync(ct))
                .ToDictionary(x => x.ReproductoraId, x => x.Id, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var tieneRepros = repros.Count > 0 || existentes.Count > 0;
        if (repros.Count == 0) return (0, 0, 0, tieneRepros, true, "");

        // Cobertura del cruce: el trigger genera el día d SOLO si TODAS las repros tienen ese día.
        HashSet<int>? edadesComunes = null;

        int nuevasRepro = 0, nuevosSeg = 0, nuevasLesiones = 0;
        var ordenadas = repros.OrderBy(x => x.Id).ToList(); // n = posición por orden de id origen
        for (var i = 0; i < ordenadas.Count; i++)
        {
            var rep = ordenadas[i];
            var reproKey = PuentePanamaCalculos.ReproductoraId(rep.Id);
            int reproDestinoId;
            if (existentes.TryGetValue(reproKey, out var exid))
            {
                reproDestinoId = exid;
                r.ReproductorasOmitidas++;
            }
            else
            {
                r.ReproductorasNuevas++;
                nuevasRepro++;
                reproDestinoId = 0;
                if (!c.Req.DryRun && loteDestinoId > 0)
                {
                    try
                    {
                        var creada = await _loteReproService.CreateAsync(
                            PuentePanamaCalculos.MapReproductora(rep, loteDestinoId, fechaEncaset, nombreLote, i + 1));
                        reproDestinoId = creada.Id;
                        existentes[reproKey] = reproDestinoId;
                    }
                    catch (Exception ex)
                    {
                        r.Mensajes.Add($"Reproductora origen {rep.Id} (lote {loteOrigenId}): {ex.Message}");
                        continue;
                    }
                }
            }

            var (nuevos, edades) = await SincronizarSeguimientoReproAsync(c, rep.Id, reproDestinoId, fechaEncaset, ct);
            nuevosSeg += nuevos;
            edadesComunes = edadesComunes is null ? edades : edadesComunes.Intersect(edades).ToHashSet();

            nuevasLesiones += await SincronizarLesionesReproAsync(c, rep.Id, reproDestinoId, granjaId, galponId, loteDestinoId, fechaEncaset, ct);
        }

        var esperadas = Enumerable.Range(1, PuentePanamaCalculos.DiasCruceReproductora)
            .Where(d => !c.Req.FechaHasta.HasValue || PuentePanamaCalculos.FechaPorEdad(fechaEncaset, d).Date <= c.Req.FechaHasta.Value.Date)
            .ToList();
        var faltantes = esperadas.Where(d => edadesComunes is null || !edadesComunes.Contains(d)).ToList();
        return (nuevasRepro, nuevosSeg, nuevasLesiones, tieneRepros, faltantes.Count == 0, string.Join(",", faltantes));
    }

    // ── Lesiones de la reproductora (tab Lesiones, módulo REPRODUCTORA) ────────
    // Contrato del front del tab: LoteReproductoraId = PK destino como string, LoteId = lote engorde,
    // FarmId/GalponId = ubicación. Idempotencia por marcador "[PA-LES-id]" en Observaciones (la tabla
    // no tiene clave de origen; se escribe directo al contexto porque el servicio de lesiones fija
    // FechaRegistro = ahora y acá se preserva la fecha del origen).
    private async Task<int> SincronizarLesionesReproAsync(RunCtx c, int reproOrigenId, int reproDestinoId, int granjaId, string? galponId, int loteDestinoId, DateTime fechaEncaset, CancellationToken ct)
    {
        IReadOnlyList<PanamaLesion> lesiones;
        try { lesiones = await _api.GetLesionesByReproAsync(reproOrigenId, ct); }
        catch (Exception ex)
        {
            c.R.Mensajes.Add($"Lesiones de la reproductora origen {reproOrigenId}: no se pudieron leer ({ex.Message}).");
            return 0;
        }
        if (lesiones.Count == 0) return 0;
        var r = c.R;

        // Observaciones existentes de esa reproductora destino → set de marcadores ya migrados.
        var obsExistentes = reproDestinoId > 0
            ? await _ctx.Lesiones.AsNoTracking()
                .Where(x => x.CompanyId == r.CompanyId
                            && x.ModuloOrigen == "REPRODUCTORA"
                            && x.LoteReproductoraId == reproDestinoId.ToString()
                            && x.Observaciones != null)
                .Select(x => x.Observaciones!).ToListAsync(ct)
            : new List<string>();

        int nuevas = 0;
        foreach (var les in lesiones)
        {
            var marcador = PuentePanamaCalculos.MarcadorLesion(les.Id);
            if (obsExistentes.Any(o => o.Contains(marcador, StringComparison.Ordinal)))
            {
                r.LesionesOmitidas++;
                continue;
            }
            if (c.Req.DryRun || reproDestinoId <= 0 || granjaId <= 0)
            {
                r.LesionesNuevas++;
                nuevas++;
                continue;
            }
            var entity = PuentePanamaCalculos.MapLesion(
                les, granjaId, galponId, loteDestinoId, reproDestinoId, fechaEncaset,
                PuentePanamaCalculos.ResolverTipoLesion(les, c.LesionTipoPorId));
            entity.CompanyId = r.CompanyId;
            entity.CreatedByUserId = _current.UserId;
            entity.CreatedAt = DateTime.UtcNow;
            _ctx.Lesiones.Add(entity);
            try
            {
                await _ctx.SaveChangesAsync(ct);
                r.LesionesNuevas++;
                nuevas++;
            }
            catch (Exception ex)
            {
                _ctx.Entry(entity).State = EntityState.Detached;
                r.Mensajes.Add($"Lesión origen {les.Id} (reproductora {reproOrigenId}): {ex.Message}");
            }
        }
        return nuevas;
    }

    // ── Seguimiento diario de la reproductora: SOLO edades 1..7 (el destino corta por cantidad). ──
    private async Task<(int nuevos, HashSet<int> edades)> SincronizarSeguimientoReproAsync(RunCtx c, int reproOrigenId, int reproDestinoId, DateTime fechaEncaset, CancellationToken ct)
    {
        var seg = await _api.GetInfoProductivaReproByLoteReproAsync(reproOrigenId, ct);
        var edades = new HashSet<int>();
        if (seg.Count == 0) return (0, edades);
        var r = c.R;
        var hasta = c.Req.FechaHasta?.Date;

        var existentes = reproDestinoId > 0
            ? (await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                .Where(s => s.LoteReproductoraAveEngordeId == reproDestinoId)
                .Select(s => s.Fecha).ToListAsync(ct)).Select(f => f.Date).ToHashSet()
            : new HashSet<DateTime>();

        var vistas = new HashSet<DateTime>();
        int nuevos = 0;
        foreach (var s in seg.OrderBy(x => x.EdadDia ?? 0))
        {
            var edad = s.EdadDia ?? 0;
            if (edad <= 0 || edad > PuentePanamaCalculos.DiasCruceReproductora) continue; // el destino rechaza el 8º registro
            var fecha = PuentePanamaCalculos.FechaPorEdad(fechaEncaset, edad).Date;
            if (hasta.HasValue && fecha > hasta.Value) continue;
            if (!vistas.Add(fecha)) continue;
            edades.Add(edad); // cuenta para la cobertura del cruce (importado u omitido-existente)
            if (existentes.Contains(fecha)) { r.SeguimientosReproOmitidos++; continue; }
            if (c.Req.DryRun) { r.SeguimientosReproNuevos++; nuevos++; continue; }
            try
            {
                await _seguimientoReproService.CreateAsync(PuentePanamaCalculos.MapSeguimientoRepro(s, reproDestinoId, fechaEncaset, c.UserId).ToDto());
                r.SeguimientosReproNuevos++; nuevos++;
            }
            catch (Exception ex)
            {
                edades.Remove(edad);
                r.Mensajes.Add($"Seguimiento reproductora origen {reproOrigenId} edad {edad}: {ex.Message}");
            }
        }
        return (nuevos, edades);
    }
}
