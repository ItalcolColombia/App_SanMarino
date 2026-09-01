// src/ZooSanMarino.Application/DTOs/ReporteSemanalGalponDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila semanal de reporte de producción para un galpón específico.
/// Incluye datos reales agregados por semana + valores guía STANDARD.
/// </summary>
public record ReporteSemanalGalponDto(
    int    LotePosturaProduccionId,
    string GalponId,
    string GalponNombre,
    string LoteNombre,
    int    Semana,
    DateTime FechaInicioSemana,
    DateTime FechaFinSemana,
    int EdadSemanas,
    // Saldo
    int SaldoInicioHembras,
    int SaldoInicioMachos,
    int SaldoFinHembras,
    int SaldoFinMachos,
    // Mortalidad
    int    MortalidadHembrasSemanal,
    int    MortalidadMachosSemanal,
    double PorcMortalidadSemanal,
    // Consumo
    double ConsKgHSemanal,
    double ConsKgMSemanal,
    // Huevos
    int    HuevoTotSemanal,
    int    HuevoIncSemanal,
    double PorcentajePosturaPromedio,
    double PorcentajeIncubablesPromedio,
    // Peso
    double  PesoHuevoPromedio,
    double? PesoHPromedio,
    double? PesoMPromedio,
    // Calidad
    double? UniformidadPromedio,
    double? HtaaSemanal,
    // GUIA
    double? PorcentajePosturaGuia,
    double? PesoHuevoGuia,
    double? HtaaGuia,
    double? UniformidadGuia,
    // Diferencias
    double? DifPostura,
    double? DifPesoHuevo,
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
