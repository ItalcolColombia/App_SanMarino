using System.Globalization;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.DTOs.PuentePanama;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Funciones PURAS del puente ZooPanamaPollo → engorde: filtro por año/fecha, claves determinísticas
/// (idempotencia), saneo (truncado a los varchar destino, clamp de negativos vs check constraints) y
/// mapeo de cada modelo origen a los DTO de creación del sistema. Sin EF, sin estado, sin DI → testeable.
/// La aritmética de redondeo se mantiene explícita (AwayFromZero).
/// </summary>
public static class PuentePanamaCalculos
{
    // El origen no tiene concepto de "núcleo"; nuestro modelo exige Granja → Núcleo → Galpón,
    // así que se crea un núcleo único por granja con esta clave/nombre fijos.
    public const string NucleoIdPorDefecto = "1";
    public const string NucleoNombrePorDefecto = "PRINCIPAL";

    /// <summary>Umbral del cruce reproductora: los días 1..7 del lote los genera el trigger (no se importan del lote si hay reproductoras).</summary>
    public const int DiasCruceReproductora = 7;

    /// <summary>Factor oficial del sistema para quintales (mismo QQ_TO_KG del front y de la liquidación Panamá).</summary>
    public const double KgPorQuintal = 45.36;

    /// <summary>Tipo de lista del origen (Listas) que cataloga las líneas genéticas (ROSS 308 AP, COBB 500…).</summary>
    public const int TipoListaLineaGenetica = 1;

    /// <summary>Tipo de lista del origen (Listas) que cataloga los tipos de lesión (SL, ONF, CSV, PC, L.H., ASPER.…).</summary>
    public const int TipoListaLesion = 13;

    /// <summary>Clave determinística de galpón destino a partir del id de galpón origen.</summary>
    public static string ClaveGalpon(int idGalponOrigen) => $"PA-{idGalponOrigen}";

    /// <summary>Clave ERP/externa del lote (idempotencia) a partir del id de lote origen.</summary>
    public static string LoteErp(int idLoteOrigen) => $"PA-{idLoteOrigen}";

    /// <summary>Código determinístico de reproductora destino a partir del id origen.</summary>
    public static string ReproductoraId(int idReproductoraOrigen) => $"PA-{idReproductoraOrigen}";

    /// <summary>
    /// Nomenclatura de las reproductoras de un lote: "&lt;nombreLote&gt;-&lt;n&gt;" (lote "94" → "94-1", "94-2"…),
    /// con n = posición por orden de id origen. Así se distinguen a simple vista del lote "94".
    /// </summary>
    public static string NombreReproductora(string nombreLote, int indice) =>
        Truncar($"{nombreLote.Trim()}-{indice}", 200)!;

    /// <summary>
    /// Código de la reproductora destino: incubadora del origen + el nombre origen de la reproductora
    /// ("SAN MARINO · 27"), para no perder el nombre con el que vivía en el sistema viejo
    /// (el NombreLote destino pasa a ser "&lt;lote&gt;-&lt;n&gt;"). Truncado al varchar(100) destino.
    /// </summary>
    public static string? CodigoReproductora(string? incubadora, string? nombreOrigen)
    {
        var partes = new[] { incubadora, nombreOrigen }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim());
        return Truncar(string.Join(" · ", partes), 100);
    }

    /// <summary>Nombre de lote destino: nombre, si no codLote, si no "L{id}".</summary>
    public static string NombreLote(PanamaLote l) =>
        !string.IsNullOrWhiteSpace(l.Nombre) ? l.Nombre!.Trim()
        : !string.IsNullOrWhiteSpace(l.CodLote) ? l.CodLote!.Trim()
        : $"L{l.Id}";

    /// <summary>True si el lote entra en el año pedido (null = todos los años).</summary>
    public static bool LoteEnAnio(PanamaLote l, int? anio) =>
        anio is null || (l.FechaRegistroInicio.HasValue && l.FechaRegistroInicio.Value.Year == anio.Value);

    /// <summary>Filtro combinado del lote: por año y por "hasta fecha" (fecha de inicio ≤ fechaHasta).</summary>
    public static bool LotePasaFiltros(PanamaLote l, int? anio, DateTime? fechaHasta)
    {
        if (!LoteEnAnio(l, anio)) return false;
        if (fechaHasta.HasValue && (!l.FechaRegistroInicio.HasValue || l.FechaRegistroInicio.Value.Date > fechaHasta.Value.Date))
            return false;
        return true;
    }

    /// <summary>
    /// Fecha destino de un registro a partir de la edad: fechaEncaset + edad (Utc). Se usa en vez del
    /// fechaRegistro del origen (poco confiable, "actualizado por script") para alinear con el trigger de
    /// cruce, que materializa los días 1-7 del lote en fecha_encaset + día.
    /// </summary>
    public static DateTime FechaPorEdad(DateTime fechaEncaset, int edad) =>
        DateTime.SpecifyKind(fechaEncaset.Date.AddDays(edad), DateTimeKind.Utc);

    /// <summary>Año de tabla genética a asignar: el configurado, o el año de inicio del lote.</summary>
    public static int? AnioTablaGenetica(PanamaLote l, int? anioConfigurado) =>
        anioConfigurado ?? l.FechaRegistroInicio?.Year;

    // ── Saneo (alineado a los varchar y check constraints del destino) ────────
    /// <summary>Trim + truncado defensivo al MaxLength de la columna destino. Null si queda vacío.</summary>
    public static string? Truncar(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length <= max ? t : t[..max];
    }

    /// <summary>Entero opcional ≥ 0: los negativos del origen (datos sucios) se descartan a null (ck_lae_nonneg_counts).</summary>
    public static int? NoNeg(int? v) => v is < 0 ? null : v;

    /// <summary>Double opcional ≥ 0: los negativos del origen se descartan a null (ck_lae_nonneg_pesos).</summary>
    public static double? NoNeg(double? v) => v is < 0 ? null : v;

    /// <summary>Entero ≥ 0 desde double origen (mortalidad/selección): redondeo AwayFromZero, negativos → 0.</summary>
    public static int RedondearEntero(double? v) =>
        v.HasValue ? Math.Max(0, (int)Math.Round(v.Value, MidpointRounding.AwayFromZero)) : 0;

    public static decimal? ToDecimal(double? v) => v.HasValue ? (decimal)v.Value : (decimal?)null;

    /// <summary>Quintales → kg con el factor oficial. Null cuando el total es 0 (sin dato de consumo).</summary>
    public static double? QuintalesAKg(params double?[] quintales)
    {
        var total = quintales.Sum(q => q ?? 0);
        return total > 0 ? total * KgPorQuintal : (double?)null;
    }

    /// <summary>Descripción de alimento a partir de marca + fase, truncada al varchar(100) destino.</summary>
    public static string TipoAlimento(string? marca, string? fase)
    {
        var partes = new[] { marca, fase }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim());
        return Truncar(string.Join(" - ", partes), 100) ?? string.Empty;
    }

    // ── Mapeos origen → CreateXDto ─────────────────────────────────────────────
    /// <summary>
    /// Granja destino. Departamento/municipio son OBLIGATORIOS en el destino (FarmService los exige y las
    /// columnas son NOT NULL): se resuelven fuera (texto del cliente origen o defaults) y llegan resueltos.
    /// Regional queda null (no inferible del origen; la columna se vuelve nullable por migración).
    /// Clientes NO se migran: ClienteId=null siempre.
    /// </summary>
    public static CreateFarmDto MapGranja(PanamaGranja g, int companyId, int departamentoId, int municipioId) => new(
        Name: Truncar(g.Nombre, 200) ?? $"Granja {g.Id}",
        CompanyId: companyId,
        Status: "A",
        RegionalId: null,
        RegionalOptionId: null,
        DepartamentoId: departamentoId,
        CiudadId: municipioId,
        ClienteId: null,
        Zona: null,
        CertificadoGab: g.CertificadoGab,
        Latitud: g.Latitud.HasValue ? (decimal?)g.Latitud.Value : null,
        Longitud: g.Longitud.HasValue ? (decimal?)g.Longitud.Value : null,
        ManejaAlimentoPorGalpon: null);

    public static CreateNucleoDto MapNucleo(int granjaId) =>
        new(granjaId, NucleoIdPorDefecto, NucleoNombrePorDefecto);

    public static CreateGalponDto MapGalpon(PanamaGalpon gp, int granjaId) => new(
        GalponId: ClaveGalpon(gp.Id),
        GalponNombre: Truncar(gp.Nombre, 200) ?? $"Galpón {gp.Id}",
        NucleoId: NucleoIdPorDefecto,
        GranjaId: granjaId,
        Ancho: gp.Ancho?.ToString(CultureInfo.InvariantCulture),
        Largo: gp.Largo?.ToString(CultureInfo.InvariantCulture),
        TipoGalpon: Truncar(gp.Tipogalpon, 50));

    /// <summary>
    /// Lote de engorde. <paramref name="linea"/> = nombre de la línea genética del origen (Listas tipo 1),
    /// null si el lote no la trae (regla: el lote se crea igual, sin línea asignada). La raza/año alimentan
    /// la validación de guía genética del servicio. FechaEncaset se fija Kind=Utc (el ToUniversalTime del
    /// servicio queda no-op y no depende de la TZ del servidor). Negativos del origen → null (checks BD).
    /// </summary>
    public static CreateLoteAveEngordeDto MapLote(PanamaLote l, int granjaId, string? galponId, string raza, int anio, string? linea) => new()
    {
        LoteNombre = Truncar(NombreLote(l), 200)!,
        GranjaId = granjaId,
        NucleoId = NucleoIdPorDefecto,
        GalponId = galponId,
        FechaEncaset = l.FechaRegistroInicio.HasValue
            ? DateTime.SpecifyKind(l.FechaRegistroInicio.Value.Date, DateTimeKind.Utc)
            : (DateTime?)null,
        HembrasL = NoNeg(l.AvesHembra),
        MachosL = NoNeg(l.AvesMacho),
        Mixtas = NoNeg(l.AvesMixta),
        AvesEncasetadas = NoNeg(l.NumAvesEncasetadas),
        PesoInicialH = NoNeg(l.PesoPromLlegHembra),
        PesoInicialM = NoNeg(l.PesoPromLlegMacho),
        PesoMixto = NoNeg(l.PesoPromLlegMixt),
        Raza = raza,
        AnoTablaGenetica = anio,
        Linea = Truncar(linea, 80),
        LoteErp = LoteErp(l.Id)
    };

    /// <summary>
    /// Seguimiento diario de engorde (día 8+; los días 1-7 los genera el trigger de cruce cuando hay
    /// reproductoras). Fecha por edad (fechaEncaset + edad, Utc). Panamá va MIXTA del día 8: la
    /// mortalidad/selección mixta se PLIEGA a Hembras (decisión de negocio) para no perder el total; el
    /// peso de la parvada (pesoMixta) va a Hembras. Consumo: quintales → kg (factor oficial 45.36), el
    /// mixto pliega a Hembras; los Qq* se conservan además tal cual (columnas qq_* de Panamá).
    /// </summary>
    public static CreateSeguimientoLoteLevanteRequest MapSeguimiento(PanamaInfoProductiva s, int loteId, DateTime fechaEncaset, string? createdByUserId) => new()
    {
        LoteId = loteId,
        FechaRegistro = FechaPorEdad(fechaEncaset, s.EdadDias ?? 0),
        MortalidadHembras = RedondearEntero(s.MortalidadHembra) + RedondearEntero(s.MortalidadMixta),
        MortalidadMachos = RedondearEntero(s.MortalidadMacho),
        SelH = RedondearEntero(s.SeleccionHembra) + RedondearEntero(s.SeleccionMixta),
        SelM = RedondearEntero(s.SeleccionMacho),
        TipoAlimento = TipoAlimento(s.MarcaAlimento, s.FaseAlimentacion),
        ConsumoHembras = QuintalesAKg(s.Qqhembra, s.Qqmixta),
        UnidadConsumoHembras = "kg",
        ConsumoMachos = QuintalesAKg(s.Qqmacho),
        UnidadConsumoMachos = "kg",
        PesoPromH = (s.PesoMixta ?? 0) > 0 ? NoNeg(s.PesoMixta) : NoNeg(s.PesoHembra),
        PesoPromM = NoNeg(s.PesoMacho),
        Observaciones = s.Observacion,
        Ciclo = "Normal",
        QqHembras = ToDecimal(NoNeg(s.Qqhembra)),
        QqMachos = ToDecimal(NoNeg(s.Qqmacho)),
        QqMixtas = ToDecimal(NoNeg(s.Qqmixta)),
        CreatedByUserId = createdByUserId
    };

    /// <summary>
    /// Reproductora destino. <paramref name="nombreLote"/> + <paramref name="indice"/> arman la nomenclatura
    /// "&lt;lote&gt;-&lt;n&gt;" (n por orden de id origen); el nombre origen queda preservado dentro de
    /// CodigoReproductora junto a la incubadora. ReproductoraId="PA-&lt;id&gt;" sigue siendo la clave idempotente.
    /// </summary>
    public static CreateLoteReproductoraAveEngordeDto MapReproductora(PanamaLoteReproductora r, int loteAveEngordeId, DateTime fechaEncaset, string nombreLote, int indice) => new(
        LoteAveEngordeId: loteAveEngordeId,
        ReproductoraId: ReproductoraId(r.Id),
        NombreLote: NombreReproductora(nombreLote, indice),
        // Alineado al encaset del lote para que el trigger de cruce calcule el día correcto.
        FechaEncasetamiento: DateTime.SpecifyKind(fechaEncaset.Date, DateTimeKind.Utc),
        M: NoNeg(r.AvesMacho),
        H: NoNeg(r.AvesHembra),
        Mixtas: NoNeg(r.AvesMixta),
        MortCajaH: null,
        MortCajaM: null,
        UnifH: null,
        UnifM: null,
        PesoInicialM: ToDecimal(NoNeg(r.PesoLlegadaMacho)),
        PesoInicialH: ToDecimal(NoNeg(r.PesoLlegadaHembra)),
        PesoMixto: ToDecimal(NoNeg(r.PesoLlegadaMixto)),
        CodigoReproductora: CodigoReproductora(r.Incubadora, r.NombreReproductora));

    /// <summary>
    /// Seguimiento diario de la reproductora (días 1-7). Fecha por edad (fechaEncaset + edadDia).
    /// La mortalidad/selección/peso MIXTA se pliega a Hembras (misma regla que el día 8+ del lote; aquí
    /// los campos origen ya son int, sin redondeo). Consumo: quintales → kg (mixto a Hembras) + Qq crudos.
    /// </summary>
    public static CreateSeguimientoDiarioLoteReproductoraRequest MapSeguimientoRepro(PanamaInfoProductivaRepro s, int loteReproductoraId, DateTime fechaEncaset, string? createdByUserId) => new()
    {
        LoteId = loteReproductoraId,
        FechaRegistro = FechaPorEdad(fechaEncaset, s.EdadDia ?? 0),
        MortalidadHembras = Math.Max(0, s.MortalidadHembra ?? 0) + Math.Max(0, s.MortalidadMixta ?? 0),
        MortalidadMachos = Math.Max(0, s.MortalidadMacho ?? 0),
        SelH = Math.Max(0, s.SeleccionHembra ?? 0) + Math.Max(0, s.SeleccionMixto ?? 0),
        SelM = Math.Max(0, s.SeleccionMacho ?? 0),
        ConsumoHembras = QuintalesAKg(s.QqHembra, s.QqMixto),
        UnidadConsumoHembras = "kg",
        ConsumoMachos = QuintalesAKg(s.QqMacho),
        UnidadConsumoMachos = "kg",
        PesoPromH = (s.PesoAveMixto ?? 0) > 0 ? NoNeg(s.PesoAveMixto) : NoNeg(s.PesoAveHembra),
        PesoPromM = NoNeg(s.PesoAveMacho),
        Observaciones = s.Observacion,
        Ciclo = "Normal",
        QqHembras = ToDecimal(NoNeg(s.QqHembra)),
        QqMachos = ToDecimal(NoNeg(s.QqMacho)),
        QqMixtas = ToDecimal(NoNeg(s.QqMixto)),
        CreatedByUserId = createdByUserId
    };

    /// <summary>
    /// Mapea la guía genética de Panamá (gramoDiaQq por edad) al detalle Ecuador diario:
    /// CantidadAlimentoDiarioG = gramoDiaQq; AlimentoAcumuladoG = suma acumulada; resto 0. Ordenado por edad.
    /// </summary>
    public static List<GuiaGeneticaEcuadorDetalleInputDto> MapGuiaGeneticaDetalle(IEnumerable<PanamaGuiaGenetica> filas)
    {
        decimal acum = 0m;
        var items = new List<GuiaGeneticaEcuadorDetalleInputDto>();
        foreach (var f in filas.OrderBy(x => x.Edad))
        {
            var diaria = (decimal)f.GramoDiaQq;
            acum += diaria;
            items.Add(new GuiaGeneticaEcuadorDetalleInputDto(
                Dia: f.Edad,
                PesoCorporalG: 0m,
                GananciaDiariaG: 0m,
                PromedioGananciaDiariaG: 0m,
                CantidadAlimentoDiarioG: diaria,
                AlimentoAcumuladoG: acum,
                CA: 0m,
                MortalidadSeleccionDiaria: 0m));
        }
        return items;
    }

    // ── Guía de PRUEBA (FAKE) ──────────────────────────────────────────────────
    /// <summary>
    /// Curva placeholder para la guía de PRUEBA cuando la del origen no está disponible: consumo diario
    /// lineal 14 → 264 g/día en 49 días (misma forma y extremos que la guía real del origen). Es un
    /// RELLENO para destrabar la creación de lotes: los indicadores contra guía NO son válidos con ella.
    /// </summary>
    public static List<PanamaGuiaGenetica> GuiaFakePlaceholder(int dias = 49)
    {
        var filas = new List<PanamaGuiaGenetica>(dias);
        for (var d = 1; d <= dias; d++)
            filas.Add(new PanamaGuiaGenetica
            {
                Id = d,
                Edad = d,
                GramoDiaQq = Math.Round(14.0 + (264.0 - 14.0) * (d - 1) / (dias - 1), 1, MidpointRounding.AwayFromZero)
            });
        return filas;
    }

    // ── Lesiones de reproductora ───────────────────────────────────────────────
    /// <summary>Marcador idempotente de una lesión migrada (va al inicio de Observaciones; la tabla destino no tiene clave externa).</summary>
    public static string MarcadorLesion(int idLesionOrigen) => $"[PA-LES-{idLesionOrigen}]";

    /// <summary>Tipo de lesión destino: texto resuelto del origen → catálogo Listas tipo 13 → fallback por id.</summary>
    public static string ResolverTipoLesion(PanamaLesion l, IReadOnlyDictionary<int, string> tiposPorId)
    {
        if (!string.IsNullOrWhiteSpace(l.LesionTipoText)) return l.LesionTipoText!.Trim();
        if (l.TipoLesionLista.HasValue && tiposPorId.TryGetValue(l.TipoLesionLista.Value, out var t)) return t;
        return l.TipoLesionLista.HasValue ? $"Tipo {l.TipoLesionLista}" : "Sin tipo";
    }

    /// <summary>
    /// Lesión destino (tab Lesiones de reproductora engorde, módulo REPRODUCTORA). El contrato del front:
    /// LoteReproductoraId = PK destino de lote_reproductora_ave_engorde como string, LoteId = lote engorde,
    /// FarmId/GalponId = ubicación del lote. La idempotencia va por el marcador "[PA-LES-id]" en
    /// Observaciones (la tabla no tiene clave de origen). Fecha: la del origen (Utc) o encaset + edad.
    /// La auditoría (CompanyId/CreatedByUserId/CreatedAt) la completa el servicio.
    /// </summary>
    public static Lesion MapLesion(PanamaLesion l, int farmId, string? galponId, int loteId, int loteReproductoraDestinoId, DateTime fechaEncaset, string tipoLesion)
    {
        var fecha = l.FechaRegistro.HasValue
            ? DateTime.SpecifyKind(l.FechaRegistro.Value, DateTimeKind.Utc)
            : FechaPorEdad(fechaEncaset, l.EdadDia ?? 0);
        var obs = string.IsNullOrWhiteSpace(l.Observacion)
            ? MarcadorLesion(l.Id)
            : $"{MarcadorLesion(l.Id)} {l.Observacion!.Trim()}";
        return new Lesion
        {
            FarmId = farmId,
            GalponId = Truncar(galponId, 50),
            LoteId = loteId,
            LoteReproductoraId = loteReproductoraDestinoId.ToString(),
            EdadDias = NoNeg(l.EdadDia),
            AvesHembra = NoNeg(l.AveHembra),
            AvesMacho = NoNeg(l.AveMacho),
            AvesMixtas = NoNeg(l.AveMixto),
            TipoLesion = Truncar(tipoLesion, 120) ?? "Sin tipo",
            Observaciones = obs,
            FechaRegistro = fecha,
            ModuloOrigen = "REPRODUCTORA",
            Status = "A"
        };
    }

    // ── Historial de corridas ──────────────────────────────────────────────────
    /// <summary>Tope de lotes que se persisten en el detalle del historial (evita inflar el jsonb).</summary>
    public const int MaxLotesDetalleHistorial = 500;

    /// <summary>
    /// Lotes pendientes de una corrida derivados de los contadores de auditoría (no tienen columna propia):
    /// cada lote del año termina en exactamente uno de Nuevo/Omitido/Pendiente/Error.
    /// </summary>
    public static int LotesPendientesDerivados(int totales, int nuevos, int omitidos, int conError) =>
        Math.Max(0, totales - nuevos - omitidos - conError);

    /// <summary>
    /// Copia del resultado lista para persistir como detalle del historial: conserva TODOS los contadores
    /// y mensajes, pero poda la lista de lotes (solo los con novedad — se excluyen los "YaExiste", que en
    /// re-corridas serían cientos de filas sin información — y con tope <paramref name="maxLotes"/>).
    /// Si se podó algo, lo deja dicho en un mensaje. Función pura: no muta el original.
    /// </summary>
    public static ResultadoSincronizacionDto PodarDetalleParaHistorial(ResultadoSincronizacionDto r, int maxLotes = MaxLotesDetalleHistorial)
    {
        var conNovedad = r.Lotes
            .Where(l => !string.Equals(l.Estado, "YaExiste", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sinNovedad = r.Lotes.Count - conNovedad.Count;
        var recortados = Math.Max(0, conNovedad.Count - maxLotes);

        var podado = new ResultadoSincronizacionDto
        {
            DryRun = r.DryRun,
            Anio = r.Anio,
            CompanyId = r.CompanyId,
            GuiaGeneticaImportada = r.GuiaGeneticaImportada,
            GuiaGeneticaFilas = r.GuiaGeneticaFilas,
            GuiaGeneticaRazaAnio = r.GuiaGeneticaRazaAnio,
            GuiaGeneticaFakeCreada = r.GuiaGeneticaFakeCreada,
            GranjasVistas = r.GranjasVistas,
            GranjasNuevas = r.GranjasNuevas,
            GalponesVistos = r.GalponesVistos,
            GalponesNuevos = r.GalponesNuevos,
            LotesEnAnio = r.LotesEnAnio,
            LotesNuevos = r.LotesNuevos,
            LotesOmitidos = r.LotesOmitidos,
            LotesConError = r.LotesConError,
            LotesPendientes = r.LotesPendientes,
            SeguimientosNuevos = r.SeguimientosNuevos,
            SeguimientosOmitidos = r.SeguimientosOmitidos,
            ReproductorasNuevas = r.ReproductorasNuevas,
            ReproductorasOmitidas = r.ReproductorasOmitidas,
            SeguimientosReproNuevos = r.SeguimientosReproNuevos,
            SeguimientosReproOmitidos = r.SeguimientosReproOmitidos,
            LesionesNuevas = r.LesionesNuevas,
            LesionesOmitidas = r.LesionesOmitidas,
            DuracionMs = r.DuracionMs,
            Estado = r.Estado,
            AuditoriaId = r.AuditoriaId,
            Lotes = conNovedad.Take(maxLotes).ToList(),
            Mensajes = new List<string>(r.Mensajes)
        };

        if (sinNovedad > 0 || recortados > 0)
        {
            var partes = new List<string>();
            if (sinNovedad > 0) partes.Add($"se omiten {sinNovedad} lote(s) 'YaExiste' sin novedad");
            if (recortados > 0) partes.Add($"se recortaron {recortados} fila(s) al tope de {maxLotes}");
            podado.Mensajes.Add($"Detalle del historial: se listan solo los lotes con novedad ({string.Join(" y ", partes)}); los contadores de arriba son los de la corrida completa.");
        }
        return podado;
    }
}
