// tests/ZooSanMarino.Application.Tests/TicketTareaCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;
using P = ZooSanMarino.Application.Calculos.TicketTareaCalculos.Posicion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El reordenamiento del tablero es lo que sostiene el drag &amp; drop: si el <c>orden</c> queda
/// con huecos o repetido, la próxima carga muestra las tarjetas barajadas. Estos tests fijan
/// el invariante de que TODA columna tocada queda con orden 0..n-1 exacto.
/// </summary>
public class TicketTareaCalculosTests
{
    private const string A = TicketTareaEstados.Backlog;
    private const string B = TicketTareaEstados.EnCurso;

    /// <summary>Aplica los cambios devueltos sobre el estado inicial y devuelve el tablero resultante.</summary>
    private static List<P> Aplicar(IEnumerable<P> inicial, IReadOnlyList<P> cambios)
    {
        var mapa = inicial.ToDictionary(p => p.Id);
        foreach (var c in cambios) mapa[c.Id] = c;
        return mapa.Values.ToList();
    }

    private static void AssertColumnaCompacta(IEnumerable<P> tablero, string estado)
    {
        var ordenes = tablero.Where(p => p.Estado == estado).OrderBy(p => p.Orden).Select(p => p.Orden).ToList();
        Assert.Equal(Enumerable.Range(0, ordenes.Count), ordenes);
    }

    // ── Mover dentro de la misma columna ─────────────────────────────────────

    [Fact]
    public void ReordenarDentroDeLaMismaColumna_DejaOrdenSinHuecos()
    {
        var inicial = new[] { new P(1, A, 0), new P(2, A, 1), new P(3, A, 2) };

        var cambios = TicketTareaCalculos.Reordenar(inicial, idMovido: 3, A, indiceDestino: 0);
        var final = Aplicar(inicial, cambios);

        Assert.Equal(0, final.First(p => p.Id == 3).Orden);
        Assert.Equal(1, final.First(p => p.Id == 1).Orden);
        Assert.Equal(2, final.First(p => p.Id == 2).Orden);
        AssertColumnaCompacta(final, A);
    }

    [Fact]
    public void MoverAlFinalDeLaMismaColumna()
    {
        var inicial = new[] { new P(1, A, 0), new P(2, A, 1), new P(3, A, 2) };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, 1, A, 2));

        Assert.Equal(2, final.First(p => p.Id == 1).Orden);
        AssertColumnaCompacta(final, A);
    }

    [Fact]
    public void SoltarEnLaMismaPosicion_NoRompeNada()
    {
        var inicial = new[] { new P(1, A, 0), new P(2, A, 1) };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, 1, A, 0));

        Assert.Equal(0, final.First(p => p.Id == 1).Orden);
        Assert.Equal(1, final.First(p => p.Id == 2).Orden);
    }

    // ── Mover entre columnas ─────────────────────────────────────────────────

    [Fact]
    public void MoverEntreColumnas_CompactaElOrigenYAcomodaElDestino()
    {
        var inicial = new[]
        {
            new P(1, A, 0), new P(2, A, 1), new P(3, A, 2),
            new P(4, B, 0), new P(5, B, 1),
        };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, idMovido: 2, B, indiceDestino: 1));

        var movida = final.First(p => p.Id == 2);
        Assert.Equal(B, movida.Estado);
        Assert.Equal(1, movida.Orden);

        Assert.Equal(0, final.First(p => p.Id == 4).Orden);
        Assert.Equal(2, final.First(p => p.Id == 5).Orden);   // se corrió para dejar el hueco

        // El origen quedó 1 → 0, 3 → 1 (sin el hueco que dejó la tarjeta 2).
        Assert.Equal(0, final.First(p => p.Id == 1).Orden);
        Assert.Equal(1, final.First(p => p.Id == 3).Orden);

        AssertColumnaCompacta(final, A);
        AssertColumnaCompacta(final, B);
    }

    [Fact]
    public void MoverAUnaColumnaVacia()
    {
        var inicial = new[] { new P(1, A, 0), new P(2, A, 1) };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, 1, B, 0));

        Assert.Equal(B, final.First(p => p.Id == 1).Estado);
        Assert.Equal(0, final.First(p => p.Id == 1).Orden);
        Assert.Equal(0, final.First(p => p.Id == 2).Orden);
        AssertColumnaCompacta(final, A);
    }

    [Fact]
    public void IndiceFueraDeRango_SeRecortaAlFinal()
    {
        var inicial = new[] { new P(1, A, 0), new P(2, B, 0), new P(3, B, 1) };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, 1, B, indiceDestino: 99));

        Assert.Equal(2, final.First(p => p.Id == 1).Orden);
        AssertColumnaCompacta(final, B);
    }

    [Fact]
    public void IndiceNegativo_SeRecortaAlPrincipio()
    {
        var inicial = new[] { new P(1, A, 0), new P(2, B, 0) };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, 1, B, indiceDestino: -5));

        Assert.Equal(0, final.First(p => p.Id == 1).Orden);
        Assert.Equal(1, final.First(p => p.Id == 2).Orden);
    }

    [Fact]
    public void OrdenInicialConHuecos_QuedaCompactadoAlMover()
    {
        // Datos ya corruptos (0, 5, 9): mover una tarjeta debe sanear la columna.
        var inicial = new[] { new P(1, A, 0), new P(2, A, 5), new P(3, A, 9) };

        var final = Aplicar(inicial, TicketTareaCalculos.Reordenar(inicial, 3, A, 0));

        AssertColumnaCompacta(final, A);
    }

    [Fact]
    public void TarjetaInexistente_NoDevuelveCambios()
        => Assert.Empty(TicketTareaCalculos.Reordenar(new[] { new P(1, A, 0) }, idMovido: 99, A, 0));

    [Fact]
    public void EstadoDestinoEnMinusculas_SeNormalizaAMayusculas()
    {
        var inicial = new[] { new P(1, A, 0) };

        var cambios = TicketTareaCalculos.Reordenar(inicial, 1, "en_curso", 0);

        Assert.Equal(B, cambios.Single().Estado);
    }

    // ── Código correlativo ───────────────────────────────────────────────────

    [Fact]
    public void CodigoDeTarea_CuelgaDelCodigoDelCaso()
        => Assert.Equal("TK-2026-000123-T3", TicketTareaCalculos.GenerarCodigoTarea("TK-2026-000123", 123, 3));

    [Fact]
    public void CodigoDeTarea_SinCodigoDeCasoCaeAlId()
        => Assert.Equal("TK-77-T1", TicketTareaCalculos.GenerarCodigoTarea(null, 77, 1));

    [Fact]
    public void CodigoDeTarea_ConsecutivoInvalidoArrancaEnUno()
        => Assert.Equal("TK-2026-000001-T1", TicketTareaCalculos.GenerarCodigoTarea("TK-2026-000001", 1, 0));

    [Fact]
    public void SiguienteConsecutivo_ArrancaEnUnoSinCodigos()
        => Assert.Equal(1, TicketTareaCalculos.SiguienteConsecutivo(Array.Empty<string?>()));

    [Fact]
    public void SiguienteConsecutivo_TomaElMaximoYNoReutiliza()
    {
        var codigos = new string?[] { "TK-2026-000123-T1", "TK-2026-000123-T7", "TK-2026-000123-T3", null, "raro" };
        Assert.Equal(8, TicketTareaCalculos.SiguienteConsecutivo(codigos));
    }

    // ── Normalización ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("historia", TicketTareaTipos.Historia)]
    [InlineData("BUG", TicketTareaTipos.Bug)]
    [InlineData(null, TicketTareaTipos.Tarea)]
    [InlineData("inventado", TicketTareaTipos.Tarea)]
    public void NormalizarTipo(string? entrada, string esperado)
        => Assert.Equal(esperado, TicketTareaCalculos.NormalizarTipo(entrada));

    [Theory]
    [InlineData("en_curso", TicketTareaEstados.EnCurso)]
    [InlineData("", TicketTareaEstados.Backlog)]
    [InlineData("inventado", TicketTareaEstados.Backlog)]
    public void NormalizarEstado(string? entrada, string esperado)
        => Assert.Equal(esperado, TicketTareaCalculos.NormalizarEstado(entrada));

    [Theory]
    [InlineData("critica", TicketPrioridades.Critica)]
    [InlineData(null, TicketPrioridades.Media)]
    public void NormalizarPrioridad(string? entrada, string esperado)
        => Assert.Equal(esperado, TicketTareaCalculos.NormalizarPrioridad(entrada));

    // ── Fechas reales ────────────────────────────────────────────────────────

    private static readonly DateTime Ahora = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EnBacklog_NoSeSellaNingunaFecha()
    {
        var (inicio, fin) = TicketTareaCalculos.SellarFechasReales(TicketTareaEstados.Backlog, null, null, Ahora);
        Assert.Null(inicio);
        Assert.Null(fin);
    }

    [Fact]
    public void AlSalirDeBacklog_SeSellaElInicio()
    {
        var (inicio, fin) = TicketTareaCalculos.SellarFechasReales(TicketTareaEstados.EnCurso, null, null, Ahora);
        Assert.Equal(Ahora, inicio);
        Assert.Null(fin);
    }

    [Fact]
    public void ElInicioSeSellaUnaSolaVez()
    {
        var original = Ahora.AddDays(-3);
        var (inicio, _) = TicketTareaCalculos.SellarFechasReales(
            TicketTareaEstados.EnRevision, original, null, Ahora);
        Assert.Equal(original, inicio);
    }

    [Fact]
    public void AlPasarAListo_SeSellaElFin()
    {
        var (_, fin) = TicketTareaCalculos.SellarFechasReales(
            TicketTareaEstados.Listo, Ahora.AddDays(-1), null, Ahora);
        Assert.Equal(Ahora, fin);
    }

    [Fact]
    public void SacarDeListo_BorraElFinPorqueVolvioAEstarEnJuego()
    {
        var (_, fin) = TicketTareaCalculos.SellarFechasReales(
            TicketTareaEstados.EnCurso, Ahora.AddDays(-2), Ahora.AddDays(-1), Ahora);
        Assert.Null(fin);
    }
}
