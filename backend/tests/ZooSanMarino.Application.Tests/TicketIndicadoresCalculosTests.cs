// tests/ZooSanMarino.Application.Tests/TicketIndicadoresCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;
using Fila = ZooSanMarino.Application.Calculos.TicketIndicadoresCalculos.FilaCaso;

namespace ZooSanMarino.Application.Tests;

public class TicketIndicadoresCalculosTests
{
    private static readonly DateTime Ahora = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Caso mínimo; cada test ajusta solo lo que le importa.</summary>
    private static Fila Caso(
        long id = 1,
        int paisId = 1,
        string? pais = "Colombia",
        int companyId = 1,
        string? empresa = "Agroavicola Sanmarino",
        string tipo = TicketTipos.Soporte,
        string estado = TicketEstados.Abierto,
        string prioridad = TicketPrioridades.Media,
        Guid? asignado = null,
        string? asignadoNombre = null,
        double horasDesdeCreacion = 48,
        DateTime? apertura = null,
        DateTime? solucion = null,
        DateTime? cierre = null,
        DateTime? limite = null,
        decimal? estimadas = null,
        decimal registradas = 0,
        int tareas = 0,
        int listas = 0)
        => new(id, paisId, pais, companyId, empresa, tipo, estado, prioridad, asignado, asignadoNombre,
               Ahora.AddHours(-horasDesdeCreacion), apertura, solucion, cierre, limite,
               estimadas, registradas, tareas, listas);

    // ── Volumen ──────────────────────────────────────────────────────────────

    [Fact]
    public void SinCasos_TodoEnCero()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(Array.Empty<Fila>(), Ahora);

        Assert.Equal(0, r.Total);
        Assert.Equal(0m, r.PorcentajeResueltos);
        Assert.Null(r.Efectividad);
        Assert.Null(r.PromedioResolucion);
        Assert.Null(r.PromedioPrimeraRespuesta);
    }

    [Fact]
    public void CuentaCadaEstadoEnSuCasillero()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, estado: TicketEstados.Abierto),
            Caso(2, estado: TicketEstados.EnAnalisis),
            Caso(3, estado: TicketEstados.EnDocumentacion),
            Caso(4, estado: TicketEstados.EnImplementacion),
            Caso(5, estado: TicketEstados.EnRevision),
            Caso(6, estado: TicketEstados.Transferido),
            Caso(7, estado: TicketEstados.Solucionado, solucion: Ahora.AddHours(-1)),
            Caso(8, estado: TicketEstados.Cerrado, solucion: Ahora.AddHours(-3), cierre: Ahora.AddHours(-1)),
            Caso(9, estado: TicketEstados.Suspendido),
        }, Ahora);

        Assert.Equal(9, r.Total);
        Assert.Equal(1, r.Abiertos);
        Assert.Equal(5, r.EnCurso);          // las 4 fases de trabajo + transferido
        Assert.Equal(1, r.Solucionados);
        Assert.Equal(1, r.Cerrados);
        Assert.Equal(1, r.Suspendidos);
        // Abierto y suspendido NO son "en curso": el caso no se está trabajando.
        Assert.Equal(22.2m, r.PorcentajeResueltos);   // 2 de 9
    }

    [Fact]
    public void CuentaLosCasosSinResponsable()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, asignado: Guid.NewGuid()),
            Caso(2),
            Caso(3),
        }, Ahora);

        Assert.Equal(2, r.SinAsignar);
    }

    // ── Efectividad y SLA ────────────────────────────────────────────────────

    [Fact]
    public void SinCompromisos_LaEfectividadEsNull()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[] { Caso(1), Caso(2) }, Ahora);

        Assert.Null(r.Efectividad);
        Assert.Equal(0, r.ConCompromiso);
    }

    [Fact]
    public void Efectividad_SoloMideLosCasosQueTenianCompromiso()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            // Cumplido: solucionado antes del límite
            Caso(1, estado: TicketEstados.Cerrado, limite: Ahora.AddHours(-10), solucion: Ahora.AddHours(-20)),
            // Incumplido: solucionado después
            Caso(2, estado: TicketEstados.Cerrado, limite: Ahora.AddHours(-30), solucion: Ahora.AddHours(-10)),
            // Vencido: sigue abierto y ya pasó la fecha
            Caso(3, limite: Ahora.AddHours(-5)),
            // Sin compromiso: no debe entrar en el denominador
            Caso(4),
        }, Ahora);

        Assert.Equal(3, r.ConCompromiso);
        Assert.Equal(1, r.CompromisoCumplido);
        Assert.Equal(1, r.Vencidos);
        Assert.Equal(33.3m, r.Efectividad);   // 1 de 3
    }

    [Fact]
    public void PorVencer_CuentaLosQueEstanDentroDelUmbral()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, limite: Ahora.AddHours(6)),     // por vencer
            Caso(2, limite: Ahora.AddDays(10)),     // en tiempo
        }, Ahora);

        Assert.Equal(1, r.PorVencer);
        Assert.Equal(0, r.Vencidos);
    }

    // ── Tiempos promedio ─────────────────────────────────────────────────────

    [Fact]
    public void PromedioPrimeraRespuesta_IgnoraLosCasosQueNadieTomo()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, horasDesdeCreacion: 10, apertura: Ahora.AddHours(-8)),   // 2 h
            Caso(2, horasDesdeCreacion: 10, apertura: Ahora.AddHours(-6)),   // 4 h
            Caso(3, horasDesdeCreacion: 10),                                  // sin tomar
        }, Ahora);

        // (2 + 4) / 2 = 3 — el tercero no arrastra el promedio a la baja.
        Assert.Equal(3, r.PromedioPrimeraRespuesta);
    }

    [Fact]
    public void PromedioResolucion_SoloSobreLosSolucionados()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, horasDesdeCreacion: 30, solucion: Ahora.AddHours(-10)),  // 20 h
            Caso(2, horasDesdeCreacion: 50, solucion: Ahora.AddHours(-10)),  // 40 h
            Caso(3, horasDesdeCreacion: 500),                                 // abierto: no cuenta
        }, Ahora);

        Assert.Equal(30, r.PromedioResolucion);
    }

    [Fact]
    public void PromedioConfirmacionCierre_MideDeSolucionACierre()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, solucion: Ahora.AddHours(-10), cierre: Ahora.AddHours(-8)),   // 2 h
            Caso(2, solucion: Ahora.AddHours(-10), cierre: Ahora.AddHours(-6)),   // 4 h
            Caso(3, solucion: Ahora.AddHours(-10)),                                // sin cerrar
        }, Ahora);

        Assert.Equal(3, r.PromedioConfirmacionCierre);
    }

    // ── Tareas y horas ───────────────────────────────────────────────────────

    [Fact]
    public void SumaTareasYCalculaLasPendientes()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[]
        {
            Caso(1, tareas: 4, listas: 1, estimadas: 8m, registradas: 3m),
            Caso(2, tareas: 6, listas: 5, estimadas: 4m, registradas: 5.5m),
        }, Ahora);

        Assert.Equal(10, r.TareasTotal);
        Assert.Equal(6, r.TareasListas);
        Assert.Equal(4, r.TareasPendientes);
        Assert.Equal(12m, r.HorasEstimadas);
        Assert.Equal(8.5m, r.HorasRegistradas);
        Assert.Equal(60m, r.AvanceTareas);
    }

    [Fact]
    public void SinTareas_NoHayPendientesNegativas()
    {
        var r = TicketIndicadoresCalculos.CalcularResumen(new[] { Caso(1) }, Ahora);

        Assert.Equal(0, r.TareasPendientes);
        Assert.Equal(0m, r.AvanceTareas);
    }

    // ── Desglose por país ────────────────────────────────────────────────────

    [Fact]
    public void PorPais_SeparaYOrdenaPorVolumen()
    {
        var filas = new[]
        {
            Caso(1, paisId: 1, pais: "Colombia"),
            Caso(2, paisId: 1, pais: "Colombia", estado: TicketEstados.Cerrado, solucion: Ahora.AddHours(-2)),
            Caso(3, paisId: 2, pais: "Ecuador"),
        };

        var r = TicketIndicadoresCalculos.PorPais(filas, Ahora);

        Assert.Equal(2, r.Count);
        Assert.Equal("Colombia", r[0].Nombre);
        Assert.Equal(2, r[0].Total);
        Assert.Equal(1, r[0].Resueltos);
        Assert.Equal("Ecuador", r[1].Nombre);
    }

    [Fact]
    public void PorPais_SinNombreUsaElId()
    {
        var r = TicketIndicadoresCalculos.PorPais(new[] { Caso(1, paisId: 9, pais: null) }, Ahora);
        Assert.Equal("País 9", r[0].Nombre);
    }

    // ── Desglose por empresa ─────────────────────────────────────────────────

    [Fact]
    public void PorEmpresa_SeparaYOrdenaPorVolumen()
    {
        var filas = new[]
        {
            Caso(1, companyId: 1, empresa: "Sanmarino"),
            Caso(2, companyId: 1, empresa: "Sanmarino", estado: TicketEstados.Cerrado, solucion: Ahora.AddHours(-2)),
            Caso(3, companyId: 3, empresa: "ItalcolEcuador"),
        };

        var r = TicketIndicadoresCalculos.PorEmpresa(filas, Ahora);

        Assert.Equal(2, r.Count);
        Assert.Equal("Sanmarino", r[0].Nombre);
        Assert.Equal(2, r[0].Total);
        Assert.Equal(1, r[0].Resueltos);
        Assert.Equal("ItalcolEcuador", r[1].Nombre);
    }

    [Fact]
    public void PorEmpresa_SinNombreUsaElId()
    {
        var r = TicketIndicadoresCalculos.PorEmpresa(new[] { Caso(1, companyId: 7, empresa: null) }, Ahora);
        Assert.Equal("Empresa 7", r[0].Nombre);
    }

    [Fact]
    public void PaisYEmpresaSonCortesIndependientesDelMismoConjunto()
    {
        // Una empresa puede operar en varios países: los dos desgloses tienen que sumar el total.
        var filas = new[]
        {
            Caso(1, paisId: 1, pais: "Colombia", companyId: 1, empresa: "Sanmarino"),
            Caso(2, paisId: 2, pais: "Ecuador",  companyId: 1, empresa: "Sanmarino"),
            Caso(3, paisId: 2, pais: "Ecuador",  companyId: 3, empresa: "ItalcolEcuador"),
        };

        var porPais = TicketIndicadoresCalculos.PorPais(filas, Ahora);
        var porEmpresa = TicketIndicadoresCalculos.PorEmpresa(filas, Ahora);

        Assert.Equal(3, porPais.Sum(p => p.Total));
        Assert.Equal(3, porEmpresa.Sum(e => e.Total));
        Assert.Equal(2, porPais.First(p => p.Nombre == "Ecuador").Total);
        Assert.Equal(2, porEmpresa.First(e => e.Nombre == "Sanmarino").Total);
    }

    // ── Desglose por estado / categoría ──────────────────────────────────────

    [Fact]
    public void PorEstado_ListaTodosLosEstadosIncluidosLosQueEstanEnCero()
    {
        var r = TicketIndicadoresCalculos.PorEstado(new[] { Caso(1, estado: TicketEstados.Abierto) }, Ahora);

        // 7 columnas del tablero + transferido + suspendido
        Assert.Equal(9, r.Count);
        Assert.Equal(1, r.First(c => c.Clave == TicketEstados.Abierto).Total);
        Assert.Equal(0, r.First(c => c.Clave == TicketEstados.EnRevision).Total);
    }

    [Fact]
    public void PorCategoria_AgrupaPorTipoYCuentaResueltos()
    {
        var r = TicketIndicadoresCalculos.PorCategoria(new[]
        {
            Caso(1, tipo: TicketTipos.Soporte),
            Caso(2, tipo: TicketTipos.Soporte, estado: TicketEstados.Cerrado, solucion: Ahora.AddHours(-1)),
            Caso(3, tipo: TicketTipos.Desarrollo),
        }, f => f.Tipo, Ahora);

        var soporte = r.First(c => c.Clave == TicketTipos.Soporte);
        Assert.Equal(2, soporte.Total);
        Assert.Equal(1, soporte.Resueltos);
        Assert.Equal(TicketTipos.Soporte, r[0].Clave);   // ordena por volumen
    }

    // ── Desglose por responsable ─────────────────────────────────────────────

    [Fact]
    public void PorResponsable_AgrupaYNombraALosSinAsignar()
    {
        var bruno = Guid.NewGuid();
        var r = TicketIndicadoresCalculos.PorResponsable(new[]
        {
            Caso(1, asignado: bruno, asignadoNombre: "Bruno", registradas: 3m, tareas: 2, listas: 2),
            Caso(2, asignado: bruno, asignadoNombre: "Bruno", registradas: 1m,
                 estado: TicketEstados.Cerrado, solucion: Ahora.AddHours(-1)),
            Caso(3),
        }, Ahora);

        var deBruno = r.First(x => x.Guid == bruno);
        Assert.Equal(2, deBruno.Asignados);
        Assert.Equal(1, deBruno.Resueltos);
        Assert.Equal(4m, deBruno.HorasRegistradas);
        Assert.Equal(2, deBruno.TareasListas);
        Assert.Contains(r, x => x.Nombre == "Sin asignar" && x.Asignados == 1);
    }

    // ── Clasificación ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TicketEstados.Solucionado, true)]
    [InlineData(TicketEstados.Cerrado, true)]
    [InlineData(TicketEstados.EnAnalisis, false)]
    [InlineData(TicketEstados.Abierto, false)]
    public void EsResuelto(string estado, bool esperado)
        => Assert.Equal(esperado, TicketIndicadoresCalculos.EsResuelto(estado));

    [Theory]
    [InlineData(TicketEstados.EnAnalisis, true)]
    [InlineData(TicketEstados.EnDocumentacion, true)]
    [InlineData(TicketEstados.EnImplementacion, true)]
    [InlineData(TicketEstados.EnRevision, true)]
    [InlineData(TicketEstados.Transferido, true)]
    [InlineData(TicketEstados.Abierto, false)]
    [InlineData(TicketEstados.Suspendido, false)]
    public void EsEnCurso(string estado, bool esperado)
        => Assert.Equal(esperado, TicketIndicadoresCalculos.EsEnCurso(estado));
}
