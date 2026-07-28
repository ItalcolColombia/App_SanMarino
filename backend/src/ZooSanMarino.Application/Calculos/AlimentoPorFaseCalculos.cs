using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Hoja «ALIMLev» del Informe RA Pesadas: energía y proteína consumidas,
/// AGRUPADAS POR FASE DE ALIMENTO (INI / LEV / PP / F1 en hembras; INI / LEV / M
/// en machos), real contra guía.
///
/// La fase de cada semana NO se deduce de la edad: la fija la guía genética
/// (`alim_h` / `alim_m`), porque el corte de fase depende de la línea y del año
/// de la guía. Semana sin fase en la guía ⇒ la semana no entra en ninguna fase
/// (no se inventa un cubo «sin fase» que después nadie sabría leer).
///
/// Lógica PURA: sin EF, sin estado.
/// </summary>
public static class AlimentoPorFaseCalculos
{
    /// <summary>Etiqueta de la fila de cierre, igual que el Excel.</summary>
    public const string TotalGeneral = "Total general";

    /// <summary>
    /// Arma las cuatro tablas de la hoja a partir de las semanas ya calculadas.
    /// El orden de las fases es el de APARICIÓN por semana (INI → LEV → PP → F1),
    /// que es el cronológico y el del archivo; no alfabético.
    /// </summary>
    public static ReporteSemanalAlimentoPorFaseDto Construir(
        IReadOnlyList<ReporteSemanalLevanteSemanaDto> semanas)
    {
        return new ReporteSemanalAlimentoPorFaseDto
        {
            EnergiaHembras = Agrupar(semanas, s => s.FaseAlimentoHembras,
                s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia),
            EnergiaMachos = Agrupar(semanas, s => s.FaseAlimentoMachos,
                s => s.KcalSemanaMachos, s => s.KcalSemanaMachosGuia),
            ProteinaHembras = Agrupar(semanas, s => s.FaseAlimentoHembras,
                s => s.ProtSemanaHembras, s => s.ProtSemanaHembrasGuia),
            ProteinaMachos = Agrupar(semanas, s => s.FaseAlimentoMachos,
                s => s.ProtSemanaMachos, s => s.ProtSemanaMachosGuia)
        };
    }

    /// <summary>
    /// Suma real y guía por fase y agrega la fila «Total general».
    /// Una fase sin ninguna semana con dato queda con Real/Guía en null (no 0):
    /// «no se midió» y «se midió cero» no son lo mismo.
    /// </summary>
    public static List<ReporteSemanalAlimentoFaseDto> Agrupar(
        IReadOnlyList<ReporteSemanalLevanteSemanaDto> semanas,
        Func<ReporteSemanalLevanteSemanaDto, string?> fase,
        Func<ReporteSemanalLevanteSemanaDto, double?> real,
        Func<ReporteSemanalLevanteSemanaDto, double?> guia)
    {
        var filas = new List<ReporteSemanalAlimentoFaseDto>();
        var orden = new List<string>();
        var acum = new Dictionary<string, (double? Real, double? Guia, int Semanas)>();

        foreach (var s in semanas.OrderBy(x => x.Semana))
        {
            var f = fase(s)?.Trim();
            if (string.IsNullOrEmpty(f)) continue;

            if (!acum.ContainsKey(f)) { acum[f] = (null, null, 0); orden.Add(f); }
            var (r, g, n) = acum[f];

            var vr = real(s);
            var vg = guia(s);
            acum[f] = (
                vr.HasValue ? (r ?? 0) + vr.Value : r,
                vg.HasValue ? (g ?? 0) + vg.Value : g,
                n + 1);
        }

        foreach (var f in orden)
        {
            var (r, g, n) = acum[f];
            filas.Add(Fila(f, n, r, g));
        }

        if (filas.Count > 0)
        {
            var tr = filas.Where(x => x.Real.HasValue).Select(x => x.Real!.Value).ToList();
            var tg = filas.Where(x => x.Guia.HasValue).Select(x => x.Guia!.Value).ToList();
            filas.Add(Fila(
                TotalGeneral,
                filas.Sum(x => x.Semanas),
                tr.Count > 0 ? tr.Sum() : null,
                tg.Count > 0 ? tg.Sum() : null));
        }

        return filas;
    }

    private static ReporteSemanalAlimentoFaseDto Fila(string fase, int semanas, double? real, double? guia)
        => new()
        {
            Fase = fase,
            Semanas = semanas,
            Real = real,
            Guia = guia,
            Diferencia = real.HasValue && guia.HasValue ? real.Value - guia.Value : null,
            // %DIF contra guía 0 no existe: null, nunca división por cero ni 100 %.
            DiferenciaPct = real.HasValue && guia is not null && guia.Value != 0
                ? (real.Value - guia.Value) / guia.Value * 100.0
                : null
        };
}
