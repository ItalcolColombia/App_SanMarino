// src/ZooSanMarino.Application/DTOs/ReporteDiarioGalponDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila diaria de reporte de producción para un galpón específico.
/// Incluye datos reales + valores guía STANDARD + diferencias.
/// </summary>
public record ReporteDiarioGalponDto(
    int    LotePosturaProduccionId,
    string GalponId,
    string GalponNombre,
    string LoteNombre,
    DateTime Fecha,
    int    SemanaRelativa,
    int    EdadDias,
    // Saldo
    int SaldoHembras,
    int SaldoMachos,
    // Mortalidad
    int    MortalidadHembras,
    int    MortalidadMachos,
    double PorcMortalidad,
    // Consumo
    double ConsKgH,
    double ConsKgM,
    // Huevos
    int    HuevoTot,
    int    HuevoInc,
    double PorcentajePostura,
    double PorcentajeIncubables,
    // Peso
    double  PesoHuevo,
    double? PesoH,
    double? PesoM,
    // Calidad
    double? Uniformidad,
    double? Htaa,
    // GUIA (tabla STANDARD / ProduccionAvicolaRaw)
    double? PorcentajePosturaGuia,
    double? PesoHuevoGuia,
    double? HtaaGuia,
    double? UniformidadGuia,
    // Diferencias Real − Guía
    double? DifPostura,
    double? DifPesoHuevo,
    string? Observaciones,
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
