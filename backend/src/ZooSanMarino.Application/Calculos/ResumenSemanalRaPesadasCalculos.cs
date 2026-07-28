using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA de la hoja «RESUMEN SEMANAL» del Informe RA Pesadas.
/// Sin EF, sin estado: la agregación pesada vive en la BD
/// (fn_resumen_semanal_ra_pesadas_levante / _produccion) y acá quedan
/// la semana calendario, la participación y los totales ponderados.
/// </summary>
public static class ResumenSemanalRaPesadasCalculos
{
    public const string EtapaLevante = "levante";
    public const string EtapaProduccion = "produccion";

    /// <summary>
    /// Semana del año con la convención <b>WEEKNUM de Excel</b> (return_type 1):
    /// las semanas arrancan en DOMINGO y la semana 1 es la que contiene el 1-ene.
    /// NO es la semana ISO — verificado contra el archivo fuente del reporte:
    /// 1825/1825 filas coinciden con WEEKNUM y solo 1736 con ISO.
    /// Es la misma fórmula que aplican las funciones SQL del Resumen.
    /// </summary>
    public static int SemanaExcel(DateOnly fecha)
    {
        var enero1 = new DateOnly(fecha.Year, 1, 1);
        var dowEnero1 = (int)enero1.DayOfWeek;          // 0 = domingo
        return (fecha.DayOfYear - 1 + dowEnero1) / 7 + 1;
    }

    /// <inheritdoc cref="SemanaExcel(DateOnly)"/>
    public static int SemanaExcel(DateTime fecha) => SemanaExcel(DateOnly.FromDateTime(fecha));

    /// <summary>
    /// Domingo..sábado que abarca la semana <paramref name="semana"/> de
    /// <paramref name="anio"/>. La semana 1 puede arrancar en diciembre del año
    /// anterior (igual que Excel); no se recorta a propósito.
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fin) RangoSemanaExcel(int anio, int semana)
    {
        var enero1 = new DateOnly(anio, 1, 1);
        var inicioSemana1 = enero1.AddDays(-(int)enero1.DayOfWeek);
        var inicio = inicioSemana1.AddDays((semana - 1) * 7);
        return (inicio, inicio.AddDays(6));
    }

    /// <summary>Normaliza la etapa recibida; null si no es una de las dos válidas.</summary>
    public static string? NormalizarEtapa(string? etapa)
    {
        var e = etapa?.Trim().ToLowerInvariant();
        return e is EtapaLevante or EtapaProduccion ? e : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Participación
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalcula <c>Part</c> = saldo de hembras del lote / Σ saldo de hembras.
    ///
    /// ⚠️ OBLIGATORIO después de recortar filas por alcance del usuario: la
    /// función SQL calcula la participación con una ventana sobre TODAS las
    /// filas que devuelve, así que si el backend elimina lotes que el usuario
    /// no puede ver, las participaciones que quedan ya no suman 1 y los
    /// promedios ponderados salen mal.
    ///
    /// Con Σ saldo = 0 (o sin filas) deja <c>Part</c> en null, no en 0: no hay
    /// participación definida, y un 0 haría que el lote desaparezca de los
    /// ponderados en vez de marcar «sin dato».
    /// </summary>
    public static void RecalcularParticipacionLevante(IReadOnlyList<ResumenSemanalLevanteRow> filas)
    {
        var total = filas.Sum(f => f.SaldoHembras);
        foreach (var f in filas)
            f.Part = total > 0 ? f.SaldoHembras / total : null;
    }

    /// <inheritdoc cref="RecalcularParticipacionLevante"/>
    public static void RecalcularParticipacionProduccion(IReadOnlyList<ResumenSemanalProduccionRow> filas)
    {
        var total = filas.Sum(f => f.SaldoHembras);
        foreach (var f in filas)
            f.Part = total > 0 ? f.SaldoHembras / total : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Totales
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Promedio PONDERADO por saldo de hembras. Los lotes sin valor no aportan
    /// ni al numerador ni al denominador (no cuentan como 0). Devuelve null si
    /// ningún lote aporta valor o si el peso total es 0.
    /// </summary>
    public static double? PromedioPonderado<T>(
        IReadOnlyList<T> filas, Func<T, double?> valor, Func<T, double> peso)
    {
        double acum = 0, pesos = 0;
        foreach (var f in filas)
        {
            var v = valor(f);
            var p = peso(f);
            if (v is null || p <= 0) continue;
            acum += v.Value * p;
            pesos += p;
        }
        return pesos > 0 ? acum / pesos : null;
    }

    public static ResumenSemanalTotalesDto TotalesLevante(IReadOnlyList<ResumenSemanalLevanteRow> filas)
    {
        double Peso(ResumenSemanalLevanteRow f) => f.SaldoHembras;
        return new ResumenSemanalTotalesDto
        {
            Lotes = filas.Count,
            SaldoHembras = filas.Sum(f => f.SaldoHembras),
            SaldoMachos = filas.Sum(f => f.SaldoMachos),
            Ponderados = new Dictionary<string, double?>
            {
                ["mortHembrasPct"]         = PromedioPonderado(filas, f => f.MortHembrasPct, Peso),
                ["retiroAcumHembrasPct"]   = PromedioPonderado(filas, f => f.RetiroAcumHembrasPct, Peso),
                ["retiroAcumHembrasGuia"]  = PromedioPonderado(filas, f => f.RetiroAcumHembrasGuia, Peso),
                ["difConsumoHembrasPct"]   = PromedioPonderado(filas, f => f.DifConsumoHembrasPct, Peso),
                ["difPesoHembrasPct"]      = PromedioPonderado(filas, f => f.DifPesoHembrasPct, Peso),
                ["uniformidadHembras"]     = PromedioPonderado(filas, f => f.UniformidadHembras, Peso),
                ["cvHembras"]              = PromedioPonderado(filas, f => f.CvHembras, Peso),
                ["mortMachosPct"]          = PromedioPonderado(filas, f => f.MortMachosPct, Peso),
                ["retiroAcumMachosPct"]    = PromedioPonderado(filas, f => f.RetiroAcumMachosPct, Peso),
                ["retiroAcumMachosGuia"]   = PromedioPonderado(filas, f => f.RetiroAcumMachosGuia, Peso),
                ["difConsumoMachosPct"]    = PromedioPonderado(filas, f => f.DifConsumoMachosPct, Peso),
                ["difPesoMachosPct"]       = PromedioPonderado(filas, f => f.DifPesoMachosPct, Peso),
                ["uniformidadMachos"]      = PromedioPonderado(filas, f => f.UniformidadMachos, Peso),
                ["cvMachos"]               = PromedioPonderado(filas, f => f.CvMachos, Peso)
            }
        };
    }

    public static ResumenSemanalTotalesDto TotalesProduccion(IReadOnlyList<ResumenSemanalProduccionRow> filas)
    {
        double Peso(ResumenSemanalProduccionRow f) => f.SaldoHembras;
        return new ResumenSemanalTotalesDto
        {
            Lotes = filas.Count,
            SaldoHembras = filas.Sum(f => f.SaldoHembras),
            SaldoMachos = filas.Sum(f => f.SaldoMachos),
            Ponderados = new Dictionary<string, double?>
            {
                ["produccionPct"]          = PromedioPonderado(filas, f => f.ProduccionPct, Peso),
                ["produccionPctGuia"]      = PromedioPonderado(filas, f => f.ProduccionPctGuia, Peso),
                ["difProduccionPct"]       = PromedioPonderado(filas, f => f.DifProduccionPct, Peso),
                ["htaa"]                   = PromedioPonderado(filas, f => f.Htaa, Peso),
                ["htaaGuia"]               = PromedioPonderado(filas, f => f.HtaaGuia, Peso),
                ["hiaa"]                   = PromedioPonderado(filas, f => f.Hiaa, Peso),
                ["hiaaGuia"]               = PromedioPonderado(filas, f => f.HiaaGuia, Peso),
                ["aprovSemPct"]            = PromedioPonderado(filas, f => f.AprovSemPct, Peso),
                ["aprovSemPctGuia"]        = PromedioPonderado(filas, f => f.AprovSemPctGuia, Peso),
                ["grHuevoInc"]             = PromedioPonderado(filas, f => f.GrHuevoInc, Peso),
                ["mortHembrasPct"]         = PromedioPonderado(filas, f => f.MortHembrasPct, Peso),
                ["retiroAcumHembrasPct"]   = PromedioPonderado(filas, f => f.RetiroAcumHembrasPct, Peso),
                ["retiroAcumHembrasGuia"]  = PromedioPonderado(filas, f => f.RetiroAcumHembrasGuia, Peso),
                ["mortMachosPct"]          = PromedioPonderado(filas, f => f.MortMachosPct, Peso),
                ["retiroAcumMachosPct"]    = PromedioPonderado(filas, f => f.RetiroAcumMachosPct, Peso),
                ["retiroAcumMachosGuia"]   = PromedioPonderado(filas, f => f.RetiroAcumMachosGuia, Peso),
                ["pesoMachoSobreHembra"]   = PromedioPonderado(filas, f => f.PesoMachoSobreHembra, Peso)
            }
        };
    }
}
