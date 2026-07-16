using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.PuentePanama;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Mapeo puro del puente ZooPanamaPollo → engorde: filtro por año, claves determinísticas
/// (idempotencia) y equivalencia de cada modelo origen con los DTO de creación del sistema.
/// </summary>
public class PuentePanamaCalculosTests
{
    private static PanamaLote Lote(int id = 90, string? nombre = "77", string? cod = "L90", DateTime? inicio = null) => new()
    {
        Id = id,
        Nombre = nombre,
        CodLote = cod,
        FechaRegistroInicio = inicio ?? new DateTime(2025, 1, 9),
        NumAvesEncasetadas = 51885,
        AvesHembra = 23555,
        AvesMacho = 28330,
        AvesMixta = 0,
        PesoPromLlegHembra = 41.0,
        PesoPromLlegMacho = 39.25,
        PesoPromLlegMixt = 0.0,
        IdGalpon = 64
    };

    // ── Filtro por año ────────────────────────────────────────────────────────
    [Fact]
    public void LoteEnAnio_NullTraeTodos()
        => Assert.True(PuentePanamaCalculos.LoteEnAnio(Lote(inicio: new DateTime(2019, 5, 1)), null));

    [Fact]
    public void LoteEnAnio_IncluyeMismoAnio()
        => Assert.True(PuentePanamaCalculos.LoteEnAnio(Lote(inicio: new DateTime(2025, 12, 31)), 2025));

    [Fact]
    public void LoteEnAnio_ExcluyeOtroAnio()
        => Assert.False(PuentePanamaCalculos.LoteEnAnio(Lote(inicio: new DateTime(2024, 1, 1)), 2025));

    [Fact]
    public void LoteEnAnio_SinFecha_ExcluidoCuandoHayAnio()
        => Assert.False(PuentePanamaCalculos.LoteEnAnio(new PanamaLote { Id = 1, FechaRegistroInicio = null }, 2025));

    // ── Claves determinísticas (idempotencia) ─────────────────────────────────
    [Fact]
    public void ClavesDeterministicas()
    {
        Assert.Equal("PA-64", PuentePanamaCalculos.ClaveGalpon(64));
        Assert.Equal("PA-90", PuentePanamaCalculos.LoteErp(90));
        Assert.Equal("PA-82", PuentePanamaCalculos.ReproductoraId(82));
    }

    [Theory]
    [InlineData("77", "L90", "77")]
    [InlineData(null, "L90", "L90")]
    [InlineData("  ", "  ", "L90")]
    public void NombreLote_PrioridadNombreLuegoCodLuegoFallback(string? nombre, string? cod, string esperado)
        => Assert.Equal(esperado, PuentePanamaCalculos.NombreLote(Lote(nombre: nombre, cod: cod)));

    // ── Año de tabla genética ─────────────────────────────────────────────────
    [Fact]
    public void AnioTablaGenetica_ConfiguradoManda()
        => Assert.Equal(2022, PuentePanamaCalculos.AnioTablaGenetica(Lote(inicio: new DateTime(2025, 1, 9)), 2022));

    [Fact]
    public void AnioTablaGenetica_SinConfig_UsaAnioDeInicio()
        => Assert.Equal(2025, PuentePanamaCalculos.AnioTablaGenetica(Lote(inicio: new DateTime(2025, 1, 9)), null));

    // ── Helpers numéricos / saneo ─────────────────────────────────────────────
    [Theory]
    [InlineData(50.4, 50)]
    [InlineData(50.5, 51)]   // AwayFromZero
    [InlineData(null, 0)]
    [InlineData(-3.0, 0)]    // negativos del origen → 0
    public void RedondearEntero(double? v, int esperado)
        => Assert.Equal(esperado, PuentePanamaCalculos.RedondearEntero(v));

    [Fact]
    public void Truncar_RecortaAlMaximoYVacioEsNull()
    {
        Assert.Equal("abc", PuentePanamaCalculos.Truncar("  abc  ", 10));
        Assert.Equal("abcde", PuentePanamaCalculos.Truncar("abcdefgh", 5));
        Assert.Null(PuentePanamaCalculos.Truncar("   ", 5));
        Assert.Null(PuentePanamaCalculos.Truncar(null, 5));
    }

    [Fact]
    public void NoNeg_DescartaNegativosANull()
    {
        Assert.Null(PuentePanamaCalculos.NoNeg((int?)-1));
        Assert.Equal(5, PuentePanamaCalculos.NoNeg((int?)5));
        Assert.Null(PuentePanamaCalculos.NoNeg((double?)-0.5));
        Assert.Equal(2.5, PuentePanamaCalculos.NoNeg((double?)2.5));
    }

    [Fact]
    public void QuintalesAKg_FactorOficialYNullSiCero()
    {
        Assert.Equal(45.36, PuentePanamaCalculos.QuintalesAKg(1.0)!.Value, 3);
        Assert.Equal(6 * 45.36, PuentePanamaCalculos.QuintalesAKg(6.0, 0.0)!.Value, 3);
        Assert.Equal(7 * 45.36, PuentePanamaCalculos.QuintalesAKg(3.0, 4.0)!.Value, 3); // pliega mixta
        Assert.Null(PuentePanamaCalculos.QuintalesAKg(0.0, null));
    }

    [Fact]
    public void TipoAlimento_ConcatenaMarcaYFaseYTrunca100()
    {
        Assert.Equal("ITALCOL - PREINICIO", PuentePanamaCalculos.TipoAlimento("ITALCOL", "PREINICIO"));
        Assert.Equal("ITALCOL", PuentePanamaCalculos.TipoAlimento("ITALCOL", null));
        Assert.Equal("", PuentePanamaCalculos.TipoAlimento(null, "  "));
        Assert.Equal(100, PuentePanamaCalculos.TipoAlimento(new string('A', 80), new string('B', 80)).Length);
    }

    // ── Mapeos ────────────────────────────────────────────────────────────────
    [Fact]
    public void MapGranja_FijaEmpresaEstadoGeoYDepartamentoMunicipio()
    {
        var g = new PanamaGranja { Id = 44, Nombre = "GR DOÑA MARIA B", Latitud = 8.789, Longitud = -79.873, CertificadoGab = true, IdCliente = 63 };
        var dto = PuentePanamaCalculos.MapGranja(g, companyId: 5, departamentoId: 21, municipioId: 101);
        Assert.Equal("GR DOÑA MARIA B", dto.Name);
        Assert.Equal(5, dto.CompanyId);
        Assert.Equal("A", dto.Status);
        Assert.Equal(21, dto.DepartamentoId);   // obligatorio en destino (FarmService + NOT NULL)
        Assert.Equal(101, dto.CiudadId);
        Assert.Null(dto.ClienteId);             // clientes NO se migran
        Assert.True(dto.CertificadoGab);
        Assert.Equal(8.789m, dto.Latitud);
        Assert.Equal(-79.873m, dto.Longitud);
    }

    [Fact]
    public void MapGalpon_ClaveDeterministicaYNucleoPorDefecto()
    {
        var gp = new PanamaGalpon { Id = 64, Nombre = "GALPON 1", Largo = 150.0, Ancho = 17.0, Tipogalpon = "AMBIENTE CONTROLADO", IdGranja = 44 };
        var dto = PuentePanamaCalculos.MapGalpon(gp, granjaId: 7);
        Assert.Equal("PA-64", dto.GalponId);
        Assert.Equal(PuentePanamaCalculos.NucleoIdPorDefecto, dto.NucleoId);
        Assert.Equal(7, dto.GranjaId);
        Assert.Equal("17", dto.Ancho);
        Assert.Equal("150", dto.Largo);
        Assert.Equal("AMBIENTE CONTROLADO", dto.TipoGalpon);
    }

    [Fact]
    public void MapLote_MapeaAvesFechaGeneticaLineaYLoteErp()
    {
        var dto = PuentePanamaCalculos.MapLote(Lote(), granjaId: 7, galponId: "PA-64", raza: "ROSS 308 AP", anio: 2022, linea: "ROSS 308 AP");
        Assert.Equal("77", dto.LoteNombre);
        Assert.Equal(7, dto.GranjaId);
        Assert.Equal("PA-64", dto.GalponId);
        Assert.Equal(PuentePanamaCalculos.NucleoIdPorDefecto, dto.NucleoId);
        Assert.Equal(23555, dto.HembrasL);
        Assert.Equal(28330, dto.MachosL);
        Assert.Equal(51885, dto.AvesEncasetadas);
        Assert.Equal(41.0, dto.PesoInicialH);
        Assert.Equal("ROSS 308 AP", dto.Raza);
        Assert.Equal("ROSS 308 AP", dto.Linea);       // línea del origen visible en el lote
        Assert.Equal(2022, dto.AnoTablaGenetica);
        Assert.Equal("PA-90", dto.LoteErp);
        Assert.Equal(new DateTime(2025, 1, 9), dto.FechaEncaset!.Value.Date);
        Assert.Equal(DateTimeKind.Utc, dto.FechaEncaset!.Value.Kind); // evita corrimiento por TZ del servidor
    }

    [Fact]
    public void MapLote_SinLinea_CreaIgualSinAsignar_YClampNegativos()
    {
        var l = Lote();
        l.AvesMixta = -5;             // dato sucio del origen
        l.PesoPromLlegMixt = -1.0;
        var dto = PuentePanamaCalculos.MapLote(l, 7, null, "COBB 500", 2025, linea: null);
        Assert.Null(dto.Linea);       // regla: sin línea en origen → se crea sin asignar
        Assert.Null(dto.GalponId);
        Assert.Null(dto.Mixtas);      // negativos → null (check constraints destino)
        Assert.Null(dto.PesoMixto);
    }

    [Fact]
    public void FechaPorEdad_EsEncasetMasEdadUtc()
    {
        var f = PuentePanamaCalculos.FechaPorEdad(new DateTime(2025, 1, 9), 10);
        Assert.Equal(new DateTime(2025, 1, 19), f.Date);   // encaset + 10
        Assert.Equal(DateTimeKind.Utc, f.Kind);
    }

    [Fact]
    public void MapSeguimiento_Dia8Mixta_PliegaAHembras_FechaPorEdad()
    {
        // Día 10 (parvada mixta): mortalidad/selección mixta se pliegan a Hembras; fecha = encaset + edad.
        var s = new PanamaInfoProductiva
        {
            Id = 300,
            EdadDias = 10,
            FechaRegistro = new DateTime(2025, 1, 20), // fecha origen "de script": se IGNORA
            MortalidadHembra = 0.0,
            MortalidadMacho = 0.0,
            MortalidadMixta = 19.0,
            SeleccionHembra = 0.0,
            SeleccionMacho = 0.0,
            SeleccionMixta = 16.0,
            PesoHembra = 0.0,
            PesoMacho = 0.0,
            PesoMixta = 522.0,
            Qqhembra = 0.0,
            Qqmacho = 0.0,
            Qqmixta = 58.0,
            MarcaAlimento = "ITALCOL",
            FaseAlimentacion = "PREINICIO"
        };
        var req = PuentePanamaCalculos.MapSeguimiento(s, loteId: 100, fechaEncaset: new DateTime(2025, 1, 9), createdByUserId: "2");
        Assert.Equal(new DateTime(2025, 1, 19), req.FechaRegistro.Date); // encaset(9)+10, NO la fecha origen
        Assert.Equal(DateTimeKind.Utc, req.FechaRegistro.Kind);
        Assert.Equal(19, req.MortalidadHembras);   // mixta → hembras
        Assert.Equal(0, req.MortalidadMachos);
        Assert.Equal(16, req.SelH);                // selección mixta → hembras
        Assert.Equal(522.0, req.PesoPromH);        // peso parvada → hembras
        Assert.Equal(58.0m, req.QqMixtas);
        // Consumo: quintales → kg (factor oficial 45.36); el mixto pliega a Hembras
        Assert.Equal(58 * 45.36, req.ConsumoHembras!.Value, 3);
        Assert.Equal("kg", req.UnidadConsumoHembras);
        Assert.Null(req.ConsumoMachos);            // qqMacho = 0 → sin dato
    }

    [Fact]
    public void MapSeguimiento_Dia1a7_HM_SeMapeaDirecto()
    {
        // Días sexados (si se importaran): H/M directos, sin mixta.
        var s = new PanamaInfoProductiva { EdadDias = 3, MortalidadHembra = 34, MortalidadMacho = 53, MortalidadMixta = 0 };
        var req = PuentePanamaCalculos.MapSeguimiento(s, 100, new DateTime(2025, 1, 9), "2");
        Assert.Equal(34, req.MortalidadHembras);
        Assert.Equal(53, req.MortalidadMachos);
        Assert.Equal(new DateTime(2025, 1, 12), req.FechaRegistro.Date); // encaset+3
    }

    [Fact]
    public void MapReproductora_NomenclaturaLoteNYCodigoConNombreOrigen()
    {
        var rep = new PanamaLoteReproductora { Id = 82, Incubadora = "SAN MARINO", NombreReproductora = "27", AvesHembra = 10500, AvesMacho = 10830, PesoLlegadaHembra = 37.9, PesoLlegadaMacho = 38.0, IdLote = 90 };
        var dto = PuentePanamaCalculos.MapReproductora(rep, loteAveEngordeId: 100, fechaEncaset: new DateTime(2025, 1, 9), nombreLote: "94", indice: 2);
        Assert.Equal(100, dto.LoteAveEngordeId);
        Assert.Equal("PA-82", dto.ReproductoraId);          // clave idempotente intacta
        Assert.Equal("94-2", dto.NombreLote);               // "<lote>-<n>": se distingue del lote "94"
        Assert.Equal("SAN MARINO · 27", dto.CodigoReproductora); // incubadora + nombre origen preservados
        Assert.Equal(10500, dto.H);
        Assert.Equal(10830, dto.M);
        Assert.Equal(37.9m, dto.PesoInicialH);
        Assert.Equal(38.0m, dto.PesoInicialM);
        Assert.Equal(new DateTime(2025, 1, 9), dto.FechaEncasetamiento!.Value.Date); // alineado al encaset del lote
    }

    [Theory]
    [InlineData("94", 1, "94-1")]
    [InlineData("94", 2, "94-2")]
    [InlineData("  77 ", 10, "77-10")]
    public void NombreReproductora_NomenclaturaLoteGuionN(string lote, int n, string esperado)
        => Assert.Equal(esperado, PuentePanamaCalculos.NombreReproductora(lote, n));

    [Theory]
    [InlineData("SAN MARINO", "27", "SAN MARINO · 27")]
    [InlineData("SAN MARINO ", null, "SAN MARINO")]     // sin nombre origen → solo incubadora
    [InlineData(null, "CHONG", "CHONG")]                // sin incubadora → solo nombre origen
    [InlineData("  ", "  ", null)]                      // nada → null
    public void CodigoReproductora_CombinaIncubadoraYNombreOrigen(string? incubadora, string? nombre, string? esperado)
        => Assert.Equal(esperado, PuentePanamaCalculos.CodigoReproductora(incubadora, nombre));

    // ── Filtro combinado + guía genética ─────────────────────────────────────
    [Fact]
    public void LotePasaFiltros_FechaHasta_ExcluyePosteriores()
    {
        var l = Lote(inicio: new DateTime(2025, 6, 1));
        Assert.True(PuentePanamaCalculos.LotePasaFiltros(l, null, new DateTime(2025, 12, 31)));
        Assert.False(PuentePanamaCalculos.LotePasaFiltros(l, null, new DateTime(2025, 1, 1)));
    }

    [Fact]
    public void LotePasaFiltros_CombinaAnioYFecha()
    {
        var l = Lote(inicio: new DateTime(2025, 3, 10));
        Assert.True(PuentePanamaCalculos.LotePasaFiltros(l, 2025, new DateTime(2025, 6, 1)));
        Assert.False(PuentePanamaCalculos.LotePasaFiltros(l, 2024, new DateTime(2025, 6, 1))); // año no coincide
    }

    [Fact]
    public void MapGuiaGeneticaDetalle_MapeaConsumoDiarioYAcumulado()
    {
        var filas = new List<PanamaGuiaGenetica>
        {
            new() { Id = 3, Edad = 3, GramoDiaQq = 20 },
            new() { Id = 1, Edad = 1, GramoDiaQq = 14 },
            new() { Id = 2, Edad = 2, GramoDiaQq = 16 },
        };
        var items = PuentePanamaCalculos.MapGuiaGeneticaDetalle(filas);
        Assert.Equal(3, items.Count);
        Assert.Equal(1, items[0].Dia);                       // ordenado por edad
        Assert.Equal(14m, items[0].CantidadAlimentoDiarioG);
        Assert.Equal(14m, items[0].AlimentoAcumuladoG);
        Assert.Equal(30m, items[1].AlimentoAcumuladoG);       // 14+16
        Assert.Equal(50m, items[2].AlimentoAcumuladoG);       // 14+16+20
        Assert.Equal(0m, items[0].PesoCorporalG);
    }

    [Fact]
    public void MapSeguimientoRepro_MapeaBajasPesosYQuintales()
    {
        var s = new PanamaInfoProductivaRepro
        {
            Id = 159,
            EdadDia = 1,
            FechaRegistro = new DateTime(2025, 1, 10),
            MortalidadHembra = 11,
            MortalidadMacho = 12,
            SeleccionHembra = 0,
            SeleccionMacho = 0,
            PesoAveHembra = 50.0,
            PesoAveMacho = 50.0,
            QqHembra = 3.0,
            QqMacho = 3.0,
            QqMixto = 0.0
        };
        var req = PuentePanamaCalculos.MapSeguimientoRepro(s, loteReproductoraId: 200, fechaEncaset: new DateTime(2025, 1, 9), createdByUserId: "2");
        Assert.Equal(200, req.LoteId);
        Assert.Equal(DateTimeKind.Utc, req.FechaRegistro.Kind);
        Assert.Equal(new DateTime(2025, 1, 10), req.FechaRegistro.Date); // encaset(9)+edad(1)
        Assert.Equal(11, req.MortalidadHembras);
        Assert.Equal(12, req.MortalidadMachos);
        Assert.Equal(50.0, req.PesoPromH);
        Assert.Equal(3.0m, req.QqHembras);
        Assert.Equal(3 * 45.36, req.ConsumoHembras!.Value, 3); // quintales → kg
        Assert.Equal(3 * 45.36, req.ConsumoMachos!.Value, 3);
    }

    [Fact]
    public void MapSeguimientoRepro_MixtaSePliegaAHembras()
    {
        // Misma regla que el día 8+ del lote: mixta → Hembras (aquí los campos ya son int, sin redondeo).
        var s = new PanamaInfoProductivaRepro
        {
            Id = 160,
            EdadDia = 2,
            MortalidadHembra = 5,
            MortalidadMixta = 7,
            SeleccionHembra = 1,
            SeleccionMixto = 2,
            PesoAveHembra = 50.0,
            PesoAveMixto = 61.0,
            QqHembra = 2.0,
            QqMixto = 1.0
        };
        var req = PuentePanamaCalculos.MapSeguimientoRepro(s, 200, new DateTime(2025, 1, 9), "2");
        Assert.Equal(12, req.MortalidadHembras);           // 5 + 7 mixta
        Assert.Equal(3, req.SelH);                         // 1 + 2 mixta
        Assert.Equal(61.0, req.PesoPromH);                 // peso mixto (parvada) manda
        Assert.Equal(3 * 45.36, req.ConsumoHembras!.Value, 3); // (2 + 1 mixto) qq → kg
    }

    // ── Guía de PRUEBA (FAKE) ─────────────────────────────────────────────────
    [Fact]
    public void GuiaFakePlaceholder_49DiasLineal14a264Creciente()
    {
        var filas = PuentePanamaCalculos.GuiaFakePlaceholder();
        Assert.Equal(49, filas.Count);
        Assert.Equal(1, filas[0].Edad);
        Assert.Equal(14.0, filas[0].GramoDiaQq);            // mismo arranque que la guía real del origen
        Assert.Equal(49, filas[^1].Edad);
        Assert.Equal(264.0, filas[^1].GramoDiaQq);          // mismo final que la guía real del origen
        for (var i = 1; i < filas.Count; i++)
            Assert.True(filas[i].GramoDiaQq > filas[i - 1].GramoDiaQq, $"la curva debe ser creciente (día {filas[i].Edad})");
    }

    [Fact]
    public void GuiaFakePlaceholder_EsCompatibleConMapGuiaGeneticaDetalle()
    {
        var items = PuentePanamaCalculos.MapGuiaGeneticaDetalle(PuentePanamaCalculos.GuiaFakePlaceholder());
        Assert.Equal(49, items.Count);
        Assert.Equal(14m, items[0].CantidadAlimentoDiarioG);
        Assert.Equal(items.Sum(i => i.CantidadAlimentoDiarioG), items[^1].AlimentoAcumuladoG);
    }

    // ── Lesiones de reproductora ──────────────────────────────────────────────
    private static PanamaLesion Lesion(int id = 49) => new()
    {
        Id = id,
        FechaRegistro = new DateTime(2025, 1, 15, 17, 36, 39),
        EdadDia = 5,
        Observacion = "Evaluación",
        IdLoteReproductora = 82,
        TipoLesionLista = 103,
        LesionTipoText = "ONF",
        AveHembra = 5,
        AveMacho = 4,
        AveMixto = 0
    };

    [Fact]
    public void MarcadorLesion_EsDeterministico()
        => Assert.Equal("[PA-LES-49]", PuentePanamaCalculos.MarcadorLesion(49));

    [Fact]
    public void ResolverTipoLesion_TextoDelOrigenMandaLuegoCatalogoLuegoFallback()
    {
        var catalogo = new Dictionary<int, string> { [103] = "ONF", [104] = "CSV" };
        Assert.Equal("ONF", PuentePanamaCalculos.ResolverTipoLesion(Lesion(), catalogo));

        var sinTexto = Lesion();
        sinTexto.LesionTipoText = null;
        Assert.Equal("ONF", PuentePanamaCalculos.ResolverTipoLesion(sinTexto, catalogo));

        sinTexto.TipoLesionLista = 999;
        Assert.Equal("Tipo 999", PuentePanamaCalculos.ResolverTipoLesion(sinTexto, catalogo));

        sinTexto.TipoLesionLista = null;
        Assert.Equal("Sin tipo", PuentePanamaCalculos.ResolverTipoLesion(sinTexto, catalogo));
    }

    [Fact]
    public void MapLesion_ContratoDelTabReproductoraYMarcadorIdempotente()
    {
        var e = PuentePanamaCalculos.MapLesion(Lesion(), farmId: 88, galponId: "PA-64", loteId: 100,
            loteReproductoraDestinoId: 200, fechaEncaset: new DateTime(2025, 1, 9), tipoLesion: "ONF");
        Assert.Equal(88, e.FarmId);
        Assert.Equal("PA-64", e.GalponId);
        Assert.Equal(100, e.LoteId);
        Assert.Equal("200", e.LoteReproductoraId);          // PK destino como string (contrato del front)
        Assert.Equal(5, e.EdadDias);
        Assert.Equal(5, e.AvesHembra);
        Assert.Equal(4, e.AvesMacho);
        Assert.Equal(0, e.AvesMixtas);
        Assert.Equal("ONF", e.TipoLesion);
        Assert.Equal("REPRODUCTORA", e.ModuloOrigen);
        Assert.Equal("A", e.Status);
        Assert.StartsWith("[PA-LES-49] ", e.Observaciones); // marcador idempotente + observación origen
        Assert.Contains("Evaluación", e.Observaciones);
        Assert.Equal(new DateTime(2025, 1, 15), e.FechaRegistro.Date); // fecha del origen (existe) en Utc
        Assert.Equal(DateTimeKind.Utc, e.FechaRegistro.Kind);
    }

    [Fact]
    public void MapLesion_SinFechaOrigen_UsaEncasetMasEdad_YSinObservacionQuedaSoloElMarcador()
    {
        var les = Lesion();
        les.FechaRegistro = null;
        les.Observacion = null;
        var e = PuentePanamaCalculos.MapLesion(les, 88, null, 100, 200, new DateTime(2025, 1, 9), "ONF");
        Assert.Equal(new DateTime(2025, 1, 14), e.FechaRegistro.Date); // encaset(9) + edad(5)
        Assert.Equal("[PA-LES-49]", e.Observaciones);
        Assert.Null(e.GalponId);
    }

    // ── Historial de corridas ─────────────────────────────────────────────────
    [Theory]
    [InlineData(287, 0, 0, 0, 287)]     // la corrida real del 2026-07-16: todos pendientes
    [InlineData(287, 280, 5, 2, 0)]     // sin pendientes
    [InlineData(10, 4, 3, 1, 2)]
    [InlineData(5, 5, 1, 0, 0)]         // datos inconsistentes → clamp a 0, nunca negativo
    public void LotesPendientesDerivados_TotalesMenosNuevosOmitidosError(int tot, int nue, int omi, int err, int esperado)
        => Assert.Equal(esperado, PuentePanamaCalculos.LotesPendientesDerivados(tot, nue, omi, err));

    private static ResultadoSincronizacionDto Resultado(params string[] estadosLotes)
    {
        var r = new ResultadoSincronizacionDto
        {
            DryRun = false,
            Anio = 2025,
            CompanyId = 5,
            GuiaGeneticaImportada = true,
            GuiaGeneticaFilas = 49,
            GuiaGeneticaRazaAnio = "ROSS 308 AP 2025",
            GuiaGeneticaFakeCreada = true,
            GranjasVistas = 9,
            GranjasNuevas = 1,
            GalponesVistos = 38,
            GalponesNuevos = 2,
            LotesEnAnio = estadosLotes.Length,
            LotesNuevos = estadosLotes.Count(e => e == "Nuevo"),
            LotesOmitidos = estadosLotes.Count(e => e == "YaExiste"),
            LotesConError = estadosLotes.Count(e => e == "Error"),
            LotesPendientes = estadosLotes.Count(e => e == "Pendiente"),
            SeguimientosNuevos = 100,
            SeguimientosOmitidos = 3,
            ReproductorasNuevas = 12,
            ReproductorasOmitidas = 1,
            SeguimientosReproNuevos = 84,
            SeguimientosReproOmitidos = 0,
            LesionesNuevas = 7,
            LesionesOmitidas = 2,
            DuracionMs = 1234,
            Estado = "ConAdvertencias",
            AuditoriaId = 42,
            Mensajes = new List<string> { "mensaje 1" }
        };
        r.Lotes = estadosLotes.Select((e, i) => new LotePreviewDto { IdOrigen = i + 1, Lote = $"L{i + 1}", Estado = e }).ToList();
        return r;
    }

    [Fact]
    public void PodarDetalleParaHistorial_ConservaContadoresYFiltraYaExiste()
    {
        var r = Resultado("Nuevo", "YaExiste", "Pendiente", "YaExiste", "Error");
        var p = PuentePanamaCalculos.PodarDetalleParaHistorial(r);

        // Contadores intactos (los de la corrida completa)
        Assert.Equal(r.LotesEnAnio, p.LotesEnAnio);
        Assert.Equal(r.LotesOmitidos, p.LotesOmitidos);
        Assert.Equal(r.SeguimientosNuevos, p.SeguimientosNuevos);
        Assert.Equal(r.LesionesNuevas, p.LesionesNuevas);
        Assert.Equal(r.GuiaGeneticaFakeCreada, p.GuiaGeneticaFakeCreada);
        Assert.Equal(r.AuditoriaId, p.AuditoriaId);
        Assert.Equal(r.Estado, p.Estado);

        // Solo lotes con novedad + mensaje aclaratorio; el original NO se muta
        Assert.Equal(3, p.Lotes.Count);
        Assert.DoesNotContain(p.Lotes, l => l.Estado == "YaExiste");
        Assert.Contains(p.Mensajes, m => m.Contains("se omiten 2 lote(s) 'YaExiste'"));
        Assert.Equal(5, r.Lotes.Count);
        Assert.Single(r.Mensajes);
    }

    [Fact]
    public void PodarDetalleParaHistorial_AplicaTopeYLoInforma()
    {
        var r = Resultado(Enumerable.Repeat("Nuevo", 30).ToArray());
        var p = PuentePanamaCalculos.PodarDetalleParaHistorial(r, maxLotes: 10);
        Assert.Equal(10, p.Lotes.Count);
        Assert.Contains(p.Mensajes, m => m.Contains("se recortaron 20 fila(s)"));
    }

    [Fact]
    public void PodarDetalleParaHistorial_SinPoda_NoAgregaMensaje()
    {
        var r = Resultado("Nuevo", "Pendiente");
        var p = PuentePanamaCalculos.PodarDetalleParaHistorial(r);
        Assert.Equal(2, p.Lotes.Count);
        Assert.Single(p.Mensajes); // solo el mensaje original
    }
}
