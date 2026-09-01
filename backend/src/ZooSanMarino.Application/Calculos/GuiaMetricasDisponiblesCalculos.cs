// src/ZooSanMarino.Application/Calculos/GuiaMetricasDisponiblesCalculos.cs
//
// Qué columnas de comparación contra la guía tienen realmente un dato que mostrar.
//
// La guía PROPIA (`guia_genetica_santa_reyes`) es un modelo simple: una curva por semana con
// `prod_porcentaje`, `retiro_ac_h` y `gr_ave_dia_h`. `GuiaGeneticaLookup.ATransitoria` la proyecta
// a la forma de `ProduccionAvicolaRaw` para no tocar a los consumidores, y todo lo que esa tabla no
// tiene queda en `null`. Los reportes técnicos pintan igual las ~17 columnas GUÍA, así que la
// empresa ve una pared de celdas vacías que parecen un error del reporte.
//
// Este cálculo responde "¿alguna fila de esta guía trae este dato?" para que el reporte pinte solo
// lo que puede comparar. Con guía COMPARTIDA devuelve todas disponibles, así el comportamiento
// histórico (pintar siempre, aunque una fila puntual venga incompleta) queda intacto por
// construcción y no por revisión.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Valores crudos de una fila de guía, como strings —igual que <c>ProduccionAvicolaRaw</c>, que los
/// guarda así—. Record propio en vez de la entidad para que el cálculo siga siendo puro y testeable
/// sin EF ni Domain.
/// </summary>
public readonly record struct FilaGuiaMetricas(
    string? ProdPorcentaje,
    string? PesoHuevo,
    string? HTotalAa,
    string? Uniformidad,
    string? PesoH,
    string? PesoM,
    string? MortSemH,
    string? MortSemM,
    string? RetiroAcH,
    string? RetiroAcM,
    string? ConsAcH,
    string? ConsAcM,
    string? GrAveDiaH,
    string? GrAveDiaM);

/// <summary>Qué métricas de guía tienen dato en al menos una fila del conjunto cargado.</summary>
public readonly record struct GuiaMetricasDisponibles(
    bool ProdPorcentaje,
    bool PesoHuevo,
    bool HTotalAa,
    bool Uniformidad,
    bool PesoH,
    bool PesoM,
    bool MortSemH,
    bool MortSemM,
    bool RetiroAcH,
    bool RetiroAcM,
    bool ConsAcH,
    bool ConsAcM,
    bool GrAveDiaH,
    bool GrAveDiaM);

public static class GuiaMetricasDisponiblesCalculos
{
    /// <summary>Todas disponibles: lo que se informa cuando la guía es la compartida.</summary>
    public static readonly GuiaMetricasDisponibles Todas = new(
        ProdPorcentaje: true, PesoHuevo: true, HTotalAa: true, Uniformidad: true,
        PesoH: true, PesoM: true, MortSemH: true, MortSemM: true,
        RetiroAcH: true, RetiroAcM: true, ConsAcH: true, ConsAcM: true,
        GrAveDiaH: true, GrAveDiaM: true);

    /// <summary>Ninguna disponible: guía vacía o inexistente.</summary>
    public static readonly GuiaMetricasDisponibles Ninguna = new(
        ProdPorcentaje: false, PesoHuevo: false, HTotalAa: false, Uniformidad: false,
        PesoH: false, PesoM: false, MortSemH: false, MortSemM: false,
        RetiroAcH: false, RetiroAcM: false, ConsAcH: false, ConsAcM: false,
        GrAveDiaH: false, GrAveDiaM: false);

    /// <summary>
    /// ¿Este valor cuenta como dato? Cualquier texto no vacío, <c>"0"</c> incluido: un cero de guía
    /// es un dato legítimo (mortalidad 0 en la primera semana, por ejemplo). La ausencia se expresa
    /// como <c>null</c> o blanco, que es lo que deja <c>ATransitoria</c> en los campos que la guía
    /// propia no tiene.
    /// </summary>
    public static bool TieneDato(string? valor) => !string.IsNullOrWhiteSpace(valor);

    /// <summary>
    /// Recorre las filas y marca cada métrica que aparezca con dato al menos una vez. Lista nula o
    /// vacía ⇒ <see cref="Ninguna"/>.
    /// </summary>
    public static GuiaMetricasDisponibles Detectar(IEnumerable<FilaGuiaMetricas>? filas)
    {
        if (filas is null) return Ninguna;

        bool prodPorcentaje = false, pesoHuevo = false, hTotalAa = false, uniformidad = false;
        bool pesoH = false, pesoM = false, mortSemH = false, mortSemM = false;
        bool retiroAcH = false, retiroAcM = false, consAcH = false, consAcM = false;
        bool grAveDiaH = false, grAveDiaM = false;

        foreach (var f in filas)
        {
            prodPorcentaje |= TieneDato(f.ProdPorcentaje);
            pesoHuevo      |= TieneDato(f.PesoHuevo);
            hTotalAa       |= TieneDato(f.HTotalAa);
            uniformidad    |= TieneDato(f.Uniformidad);
            pesoH          |= TieneDato(f.PesoH);
            pesoM          |= TieneDato(f.PesoM);
            mortSemH       |= TieneDato(f.MortSemH);
            mortSemM       |= TieneDato(f.MortSemM);
            retiroAcH      |= TieneDato(f.RetiroAcH);
            retiroAcM      |= TieneDato(f.RetiroAcM);
            consAcH        |= TieneDato(f.ConsAcH);
            consAcM        |= TieneDato(f.ConsAcM);
            grAveDiaH      |= TieneDato(f.GrAveDiaH);
            grAveDiaM      |= TieneDato(f.GrAveDiaM);
        }

        return new GuiaMetricasDisponibles(
            prodPorcentaje, pesoHuevo, hTotalAa, uniformidad,
            pesoH, pesoM, mortSemH, mortSemM,
            retiroAcH, retiroAcM, consAcH, consAcM,
            grAveDiaH, grAveDiaM);
    }

    /// <summary>
    /// Punto de entrada de los servicios: con guía compartida se informa <see cref="Todas"/> sin
    /// mirar las filas —el reporte sigue pintando lo de siempre—; solo la guía propia se inspecciona.
    /// </summary>
    public static GuiaMetricasDisponibles Resolver(bool guiaEsPropia, IEnumerable<FilaGuiaMetricas>? filas)
        => guiaEsPropia ? Detectar(filas) : Todas;
}
