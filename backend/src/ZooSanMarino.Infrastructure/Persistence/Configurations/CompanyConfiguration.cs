// file: src/ZooSanMarino.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Identifier).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();

        builder.Property(x => x.Address).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.Country).HasMaxLength(80);
        builder.Property(x => x.State).HasMaxLength(80);
        builder.Property(x => x.City).HasMaxLength(80);
        // text[] en PostgreSQL
        builder.Property(x => x.VisualPermissions).HasColumnType("text[]");

        builder.Property(x => x.MobileAccess).HasDefaultValue(false);

        // Flag tipado por comportamiento: campos ERP avícolas (bodega/C.O./instalación/ubicación/
        // centro de costo) visibles en granja, núcleo, galpón y lote.
        builder.Property(x => x.ManejaCodigosErpAvicola)
            .HasColumnName("maneja_codigos_erp_avicola")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: clasificación de huevos POR ÍTEMS del catálogo
        // (Primera/Pnc) en el seguimiento diario de producción, en vez de las 11 columnas fijas.
        builder.Property(x => x.ClasificacionHuevoPorItems)
            .HasColumnName("clasificacion_huevo_por_items")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: captura de la clasificación de huevos en el seguimiento
        // diario de LEVANTE desde la semana 14, con arrastre del acumulado a producción al liquidar.
        builder.Property(x => x.CapturaHuevosEnLevante)
            .HasColumnName("captura_huevos_en_levante")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: traslado de aves ENTRE ETAPAS (Levante → Producción)
        // desde el seguimiento diario, conservando la edad de las aves recibidas (cohortes).
        builder.Property(x => x.PermiteTrasladoAvesCrossEtapa)
            .HasColumnName("permite_traslado_aves_cross_etapa")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: el peso báscula de la venta de engorde llega al día
        // siguiente ⇒ la venta se registra sin peso y el peso se carga al confirmarla.
        builder.Property(x => x.VentaEngordePesoDiferido)
            .HasColumnName("venta_engorde_peso_diferido")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: el pollo engorde no se maneja por sexo tras salir de
        // reproductora ⇒ la plantilla de carga masiva habla de «Mixta/Mixto» en vez de H/M.
        builder.Property(x => x.SeguimientoEngordeMixto)
            .HasColumnName("seguimiento_engorde_mixto")
            .HasDefaultValue(false)
            .IsRequired();

        // Parámetro operativo: ventana previa al encaset cuyo alimento cuenta como del lote. Cortar
        // exactamente en el encaset dejaba fuera el preiniciador, que siempre llega antes.
        builder.Property(x => x.DiasAlimentoPrevioEncaset)
            .HasColumnName("dias_alimento_previo_encaset")
            .HasDefaultValue(10)
            .IsRequired();

        // Flag tipado por comportamiento: la hora de llegada decide el primer día con registro.
        builder.Property(x => x.PrimerRegistroSegunHoraLlegada)
            .HasColumnName("primer_registro_segun_hora_llegada")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: el Reporte de Costos toma alimento de las fuentes reales
        // (ingresos del histórico + consumo del seguimiento) en vez del snapshot jsonb incompleto.
        builder.Property(x => x.ReporteCostosAlimentoDesdeFuentesReales)
            .HasColumnName("reporte_costos_alimento_desde_fuentes_reales")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: los lotes de engorde se programan (lote base obligatorio,
        // nombre por corrida y gasto de inventario contra lote programado).
        builder.Property(x => x.ProgramacionLotesEngorde)
            .HasColumnName("programacion_lotes_engorde")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: el nombre del lote lleva el sufijo de corrida desde la
        // primera apertura (Panamá) o es el nombre del lote base tal cual (Ecuador).
        builder.Property(x => x.NombreLoteIncluyeCorrida)
            .HasColumnName("nombre_lote_incluye_corrida")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: el inventario se ubica en SILOS/BODEGAS de la granja, no
        // en el galpón (ingreso, traslado y consumo exigen silo_id y dejan núcleo/galpón en NULL).
        builder.Property(x => x.ManejaInventarioPorSilo)
            .HasColumnName("maneja_inventario_por_silo")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: los reportes Contable y Técnico leen el alimento del
        // módulo unificado. Default false = la consulta de siempre contra la tabla vieja.
        builder.Property(x => x.ReportesAlimentoDesdeInventarioUnificado)
            .HasColumnName("reportes_alimento_desde_inventario_unificado")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: los seguimientos diarios separan al guardar y descuentan al
        // validar. Default false = se descuenta al guardar, como siempre.
        builder.Property(x => x.RequiereValidacionSeguimientoDiario)
            .HasColumnName("requiere_validacion_seguimiento_diario")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: el seguimiento diario no captura consumo de alimento de
        // machos (producción ni levante). Nace de Santa Reyes.
        builder.Property(x => x.ConsumoAlimentoSoloHembras)
            .HasColumnName("consumo_alimento_solo_hembras")
            .HasDefaultValue(false)
            .IsRequired();

        // Flag tipado por comportamiento: oculta la columna Machos en mortalidad/selección/peso/
        // uniformidad/traslados/ventas y retira error de sexaje del registro diario (solo UI).
        builder.Property(x => x.OcultaMachosEnPostura)
            .HasColumnName("oculta_machos_en_postura")
            .HasDefaultValue(false)
            .IsRequired();

        // Parámetro operativo: última semana con huevo de primera postura habilitado. Null = la
        // empresa no usa el concepto.
        builder.Property(x => x.HuevoPrimeraPosturaHastaSemana)
            .HasColumnName("huevo_primera_postura_hasta_semana");

        // Flag tipado por comportamiento: la etapa del ciclo de vida (alistamiento/levante/levante
        // en producción/postura) se calcula por semana y por raza en vez de los cortes fijos.
        builder.Property(x => x.SemanasCicloPosturaPorRaza)
            .HasColumnName("semanas_ciclo_postura_por_raza")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(x => x.Identifier);
    }
}
