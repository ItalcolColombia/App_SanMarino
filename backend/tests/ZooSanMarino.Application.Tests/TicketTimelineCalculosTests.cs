// tests/ZooSanMarino.Application.Tests/TicketTimelineCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;
using TL = ZooSanMarino.Application.Calculos.TicketTimelineCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// La línea de tiempo se DERIVA de los datos existentes (sin tabla de eventos), así que el caso
/// crítico es el ticket viejo: creado antes de que existieran tareas y worklogs, tiene que
/// mostrarse completo igual.
/// </summary>
public class TicketTimelineCalculosTests
{
    private static readonly DateTime T0 = new(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc);

    private static TL.CabeceraCaso Cabecera(
        string? creadoPor = "Ana Pérez",
        string? solicitante = null,
        string? asignado = "Bruno Díaz",
        DateTime? apertura = null,
        DateTime? solucion = null,
        string? solucionDesc = null,
        DateTime? cierre = null,
        DateTime? notificacion = null,
        string? correo = null)
        => new(T0, creadoPor, solicitante ?? creadoPor, asignado, apertura, solucion, solucionDesc,
               cierre, notificacion, correo);

    private static IReadOnlyList<TL.EventoTimeline> Construir(
        TL.CabeceraCaso cabecera,
        IEnumerable<TL.NotaTimeline>? notas = null,
        IEnumerable<TL.AdjuntoTimeline>? adjuntos = null,
        IEnumerable<TL.TareaTimeline>? tareas = null,
        IEnumerable<TL.TiempoTimeline>? tiempos = null,
        bool incluirInternas = true)
        => TL.Construir(cabecera,
            notas ?? Array.Empty<TL.NotaTimeline>(),
            adjuntos ?? Array.Empty<TL.AdjuntoTimeline>(),
            tareas ?? Array.Empty<TL.TareaTimeline>(),
            tiempos ?? Array.Empty<TL.TiempoTimeline>(),
            incluirInternas);

    // ── Caso mínimo ──────────────────────────────────────────────────────────

    [Fact]
    public void CasoRecienCreado_TieneUnSoloEvento()
    {
        var eventos = Construir(Cabecera());

        var unico = Assert.Single(eventos);
        Assert.Equal(TL.EvCreado, unico.Tipo);
        Assert.Equal("Caso creado", unico.Titulo);
        Assert.Equal(TicketEstados.Abierto, unico.EstadoResultante);
        Assert.Equal(T0, unico.Momento);
    }

    [Fact]
    public void CasoViejoSinTareasNiWorklogs_SeArmaCompleto()
    {
        // Exactamente la forma de un ticket anterior a esta funcionalidad.
        var eventos = Construir(
            Cabecera(apertura: T0.AddHours(1), solucion: T0.AddHours(5),
                     solucionDesc: "Se corrigió el cálculo.", cierre: T0.AddHours(9),
                     notificacion: T0.AddHours(5), correo: "user@italcol.com"),
            notas: new[]
            {
                new TL.NotaTimeline(1, T0.AddHours(2), "Revisando", TicketEstados.EnAnalisis, false, null, "Bruno Díaz"),
                new TL.NotaTimeline(2, T0.AddHours(3), "¿Alguna novedad?", null, false, null, "Ana Pérez"),
            });

        Assert.Collection(eventos,
            e => Assert.Equal(TL.EvCreado, e.Tipo),
            e => Assert.Equal(TL.EvApertura, e.Tipo),
            e => Assert.Equal(TL.EvEstado, e.Tipo),
            e => Assert.Equal(TL.EvComentario, e.Tipo),
            e => Assert.Equal(TL.EvNotificacion, e.Tipo),
            e => Assert.Equal(TL.EvSolucion, e.Tipo),
            e => Assert.Equal(TL.EvCierre, e.Tipo));
    }

    [Fact]
    public void LosEventosSalenEnOrdenCronologico()
    {
        var eventos = Construir(
            Cabecera(apertura: T0.AddHours(4), solucion: T0.AddHours(20), cierre: T0.AddHours(30)),
            notas: new[] { new TL.NotaTimeline(1, T0.AddHours(10), "x", null, false, null, null) },
            adjuntos: new[] { new TL.AdjuntoTimeline(1, T0.AddHours(6), "ARCHIVO", "plan.xlsx", null) },
            tareas: new[] { new TL.TareaTimeline(1, T0.AddHours(8), "TK-1-T1", "Analizar", TicketTareaEstados.Listo, T0.AddHours(15), null) },
            tiempos: new[] { new TL.TiempoTimeline(1, T0.AddHours(12), 2m, null, null) });

        var momentos = eventos.Select(e => e.Momento).ToList();
        Assert.Equal(momentos.OrderBy(m => m), momentos);
    }

    // ── Solicitante delegado ─────────────────────────────────────────────────

    [Fact]
    public void RegistradoPorUnTercero_LoDiceEnElEventoDeCreacion()
    {
        var eventos = Construir(Cabecera(creadoPor: "Moisés Murillo", solicitante: "Ana Pérez"));

        var creacion = eventos.Single();
        Assert.Equal("Caso creado a nombre de Ana Pérez", creacion.Titulo);
        Assert.Equal("Registrado por Moisés Murillo", creacion.Detalle);
    }

    [Fact]
    public void CasoPropio_NoDiceANombreDeNadie()
    {
        var eventos = Construir(Cabecera(creadoPor: "Ana Pérez", solicitante: "Ana Pérez"));
        Assert.Equal("Caso creado", eventos.Single().Titulo);
    }

    // ── Notas ────────────────────────────────────────────────────────────────

    [Fact]
    public void NotaConEstado_EsEventoDeEstadoConEtiquetaLegible()
    {
        var eventos = Construir(Cabecera(),
            notas: new[] { new TL.NotaTimeline(1, T0.AddHours(1), "n", TicketEstados.EnDocumentacion, false, null, null) });

        var estado = eventos.Last();
        Assert.Equal(TL.EvEstado, estado.Tipo);
        Assert.Equal("Estado: En documentación", estado.Titulo);
    }

    [Fact]
    public void NotaDeSistema_GanaSobreElCambioDeEstado()
    {
        var eventos = Construir(Cabecera(),
            notas: new[]
            {
                new TL.NotaTimeline(1, T0.AddHours(1), "Prioridad: MEDIA → ALTA.",
                    TicketEstados.EnAnalisis, false, TicketNotaEventos.Prioridad, "Moisés"),
            });

        var evento = eventos.Last();
        Assert.Equal(TL.EvSistema, evento.Tipo);
        Assert.Equal("Prioridad actualizada", evento.Titulo);
    }

    [Fact]
    public void NotaSimple_EsComentario()
    {
        var eventos = Construir(Cabecera(),
            notas: new[] { new TL.NotaTimeline(1, T0.AddHours(1), "Gracias", null, false, null, "Ana") });

        Assert.Equal(TL.EvComentario, eventos.Last().Tipo);
        Assert.Equal("Comentario", eventos.Last().Titulo);
    }

    [Fact]
    public void NotaInterna_SeOcultaAlSolicitante()
    {
        var notas = new[] { new TL.NotaTimeline(1, T0.AddHours(1), "Ojo con la migración", null, true, null, "Bruno") };

        Assert.Equal(2, Construir(Cabecera(), notas, incluirInternas: true).Count);
        Assert.Single(Construir(Cabecera(), notas, incluirInternas: false));
    }

    [Fact]
    public void NotaInterna_VisibleParaElEquipoSeMarcaComoTal()
    {
        var eventos = Construir(Cabecera(),
            notas: new[] { new TL.NotaTimeline(1, T0.AddHours(1), "interno", null, true, null, null) },
            incluirInternas: true);

        var interna = eventos.Last();
        Assert.True(interna.EsInterna);
        Assert.Equal("Nota interna", interna.Titulo);
    }

    // ── Adjuntos, tareas y tiempos ───────────────────────────────────────────

    [Fact]
    public void LinkYArchivo_SeDistinguen()
    {
        var eventos = Construir(Cabecera(), adjuntos: new[]
        {
            new TL.AdjuntoTimeline(1, T0.AddHours(1), "ARCHIVO", "informe.pdf", "Ana"),
            new TL.AdjuntoTimeline(2, T0.AddHours(2), "LINK", "https://ejemplo", "Ana"),
        });

        Assert.Equal("Documento adjuntado", eventos[1].Titulo);
        Assert.Equal("Link agregado", eventos[2].Titulo);
    }

    [Fact]
    public void TareaTerminada_GeneraDosEventos()
    {
        var eventos = Construir(Cabecera(), tareas: new[]
        {
            new TL.TareaTimeline(9, T0.AddHours(1), "TK-1-T1", "Documentar", TicketTareaEstados.Listo, T0.AddHours(4), "Bruno"),
        });

        var deTarea = eventos.Where(e => e.Tipo == TL.EvTarea).ToList();
        Assert.Equal(2, deTarea.Count);
        Assert.Equal("Tarea creada", deTarea[0].Titulo);
        Assert.Equal("Tarea completada", deTarea[1].Titulo);
        Assert.All(deTarea, e => Assert.Equal("TK-1-T1 · Documentar", e.Detalle));
        Assert.All(deTarea, e => Assert.Equal(9, e.ReferenciaId));
    }

    [Fact]
    public void TareaEnCurso_SoloGeneraElEventoDeCreacion()
    {
        var eventos = Construir(Cabecera(), tareas: new[]
        {
            new TL.TareaTimeline(9, T0.AddHours(1), null, "Analizar", TicketTareaEstados.EnCurso, null, null),
        });

        var deTarea = Assert.Single(eventos, e => e.Tipo == TL.EvTarea);
        Assert.Equal("Tarea creada", deTarea.Titulo);
        Assert.Equal("Analizar", deTarea.Detalle);   // sin código, usa el título
    }

    [Theory]
    [InlineData(2, "2 h de trabajo registradas")]
    [InlineData(1.5, "1,5 h de trabajo registradas")]
    [InlineData(0.25, "0,25 h de trabajo registradas")]
    public void RegistroDeTiempo_FormateaLasHoras(decimal horas, string esperado)
    {
        var eventos = Construir(Cabecera(), tiempos: new[]
        {
            new TL.TiempoTimeline(1, T0.AddHours(1), horas, "Ajustes", "Bruno"),
        });

        // El separador decimal depende de la cultura del runner: se compara sin él.
        var titulo = eventos.Last().Titulo.Replace('.', ',');
        Assert.Equal(esperado, titulo);
    }

    // ── Etiquetas de estado ──────────────────────────────────────────────────

    [Theory]
    [InlineData(TicketEstados.Abierto, "Abierto")]
    [InlineData(TicketEstados.EnAnalisis, "En análisis")]
    [InlineData(TicketEstados.EnDocumentacion, "En documentación")]
    [InlineData(TicketEstados.EnImplementacion, "En implementación")]
    [InlineData(TicketEstados.EnRevision, "En revisión")]
    [InlineData(TicketEstados.Solucionado, "Solucionado")]
    [InlineData(TicketEstados.Cerrado, "Cerrado")]
    public void EtiquetaDeEstado(string estado, string esperado)
        => Assert.Equal(esperado, TL.EtiquetaEstado(estado));

    [Fact]
    public void EstadoDesconocido_SeDevuelveTalCual()
        => Assert.Equal("INVENTADO", TL.EtiquetaEstado("INVENTADO"));
}
