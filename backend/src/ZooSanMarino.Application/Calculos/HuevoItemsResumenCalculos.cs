// src/ZooSanMarino.Application/Calculos/HuevoItemsResumenCalculos.cs
//
// Resume el desglose por ítems de UN registro diario en los totales Primera / Pnc / Otros que
// pintan los reportes técnicos de producción.
//
// 🔴 ESPEJO EXACTO de `resumir-huevo-items-por-tipo.funcion.ts` (frontend, usado por la grilla
// diaria de producción desde X18.6). Si cambia la regla, cambian los dos — igual que ya pasa con
// `HuevoItemsCalculos.OrdenTiposHuevo` y `ORDEN_TIPOS_HUEVO` del modelo de front.
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Totales por categoría comercial de un día. <c>Otros</c> son las cantidades con
/// <c>tipoHuevo</c> desconocido: no entran en Primera ni en Pnc, pero sí en el total.
/// </summary>
public readonly record struct ResumenHuevoPorTipo(int Primera, int Pnc, int Otros)
{
    public int Total => Primera + Pnc + Otros;

    /// <summary>¿Hay algún ítem de tipo desconocido? Decide si el reporte pinta la columna «Otros».</summary>
    public bool TieneOtros => Otros > 0;
}

public static class HuevoItemsResumenCalculos
{
    private const string TipoPrimera = "primera";
    private const string TipoPnc     = "pnc";

    /// <summary>
    /// Suma las cantidades por categoría. Las filas con cantidad ≤ 0 se descartan (misma guarda que
    /// el front), y el tipo se compara en minúsculas y sin espacios.
    ///
    /// <para>El total del día lo sigue mandando <c>huevo_tot</c> del registro, no este resumen: si
    /// alguna vez divergieran, manda la columna — es la que leen espejo, trigger, saldos e
    /// indicadores.</para>
    /// </summary>
    public static ResumenHuevoPorTipo Resumir(IEnumerable<HuevoItemSeguimientoDto>? items)
    {
        var primera = 0;
        var pnc = 0;
        var otros = 0;

        foreach (var item in items ?? Enumerable.Empty<HuevoItemSeguimientoDto>())
        {
            if (item is null) continue;

            var cantidad = item.Cantidad;
            if (cantidad <= 0) continue;

            var tipo = (item.TipoHuevo ?? string.Empty).Trim().ToLowerInvariant();
            if (tipo == TipoPrimera) primera += cantidad;
            else if (tipo == TipoPnc) pnc += cantidad;
            else otros += cantidad;
        }

        return new ResumenHuevoPorTipo(primera, pnc, otros);
    }

    /// <summary>Suma de varios registros (una semana, un galpón, el consolidado).</summary>
    public static ResumenHuevoPorTipo Sumar(IEnumerable<ResumenHuevoPorTipo>? resumenes)
    {
        var primera = 0;
        var pnc = 0;
        var otros = 0;

        foreach (var r in resumenes ?? Enumerable.Empty<ResumenHuevoPorTipo>())
        {
            primera += r.Primera;
            pnc     += r.Pnc;
            otros   += r.Otros;
        }

        return new ResumenHuevoPorTipo(primera, pnc, otros);
    }
}
