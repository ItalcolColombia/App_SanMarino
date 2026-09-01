// src/ZooSanMarino.Application/DTOs/ReporteGeneralDiarioDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila diaria consolidada (todos los galpones sumados) del reporte general de producción.
/// </summary>
public record ReporteGeneralDiarioDto(
    DateTime Fecha,
    int    SemanaRelativa,
    int    EdadDias,
    // Saldo consolidado
    int SaldoTotalHembras,
    int SaldoTotalMachos,
    // Mortalidad
    int    MortalidadTotalHembras,
    int    MortalidadTotalMachos,
    double PorcMortalidadPromedio,
    // Consumo
    double ConsKgHTotalKg,
    double ConsKgMTotalKg,
    // Huevos
    int    HuevosTotTotal,
    int    HuevosIncTotal,
    double PorcentajePosturaPromedio,
    double PesoHuevoPromedio,
    double? UniformidadPromedio,
    // GUIA
    double? PorcentajePosturaGuia,
    double? PesoHuevoGuia,
    double? HtaaGuia,
    // Diferencia
    double? DifPostura,
    // ── Clasificación de huevo POR ÍTEMS ────────────────────────────────────────────────────────
    // Empresas con `companies.clasificacion_huevo_por_items`: el desglose real vive en
    // `metadata.huevoItems` y `huevo_inc` se escribe en 0 A PROPÓSITO (postura comercial, no
    // incuba). Estos tres reemplazan a Incubable/%Incubables en la vista. Default 0 ⇒ ningún
    // constructor posicional existente se rompe y la empresa sin el flag no ve diferencia.
    int HuevoPrimera = 0,
    int HuevoPnc = 0,
    int HuevoOtros = 0,
    // Semana con la que se cruzó la GUÍA. Con guía compartida es la misma `Semana`/`SemanaRelativa`
    // de siempre; con guía propia (indexada por semana de vida) es la edad del ave. Ver
    // `SemanaGuiaProduccionCalculos`.
    int SemanaGuia = 0,
    // Etapa del ciclo de vida (`SemanasCicloPosturaCalculos`) para empresas con
    // `semanas_ciclo_postura_por_raza`. `null` = la empresa no usa cortes por raza, o la raza no se
    // reconoce: no se adivina.
    string? EtapaCiclo = null
);
