using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixVentaEngordeNetoCeroBrutoIgualTara : Migration
    {
        // Ventas de pollo engorde que cuentan las aves y aportan 0 kg, porque el operario digitó el
        // MISMO número en `peso_bruto` y en `peso_tara`. Medido el 9-sep-2026: 206 de las 207 ventas
        // de ItalcolPanama; `peso_bruto` promedia 2,357 kg/ave — el peso NETO del pollo, no el de un
        // camión cargado (ItalcolEcuador, que usa los campos bien, promedia 7,21 en bruto y 2,836 en
        // neto, y tiene 0 filas con bruto = tara). Planta entrega UNA sola cifra de kilos y el
        // formulario pide dos, así que se repetía el número.
        //
        // Tres piezas:
        //   1) El detector MOV_SIN_PESO deja de mirar sólo NULL: `COALESCE(peso_neto,0) = 0` cubre el
        //      peso ausente Y el neto 0. La red de seguridad que existía justo para «cuentan aves,
        //      0 kg» era ciega a este caso: la auditoría del lote 163 (20 despachos, 45.479 aves,
        //      0 kg) no devolvía un solo hallazgo de peso.
        //   2) `fn_aplicar_correccion_despachos_sin_peso` toma el MISMO criterio (son un par: el
        //      detector reporta y la fn corrige; separarlos deja hallazgos que el botón no puede
        //      resolver).
        //   3) Backfill: mueve el valor digitado a `peso_neto` y deja `peso_tara = 0`. No inventa
        //      kilos — reubica los que ya estaban. Respaldo previo en `_backup_mpe_peso_neto_cero`.
        //
        // El gate de escritura que impide que vuelva a pasar vive en
        // MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta (con tests).
        //
        // NO re-congela liquidaciones. 4 lotes de Panamá (161/163/164/165, 176.930 aves) ya están
        // liquidados y su tabla diaria sale de la copia CONGELADA, que seguirá en 0 kg hasta que se
        // re-congele con `fn_recongelar_liquidacion_engorde`. Eso se dejó FUERA a propósito: medido,
        // re-congelar mueve 57 de esas 171 filas en columnas que no son la de kilos (saldo de aves,
        // saldo de alimento, consumo, mortalidad) porque la fórmula avanzó de v13/v15 a v18. Reescribir
        // una liquidación aprobada es decisión de operación, no efecto colateral de un deploy.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FN_AUDITORIA_SQL, suppressTransaction: true);
            migrationBuilder.Sql(FN_CORRECCION_SQL, suppressTransaction: true);
            migrationBuilder.Sql(BACKFILL_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sólo se revierte el DATO, que es lo único que este Down puede deshacer sin adivinar:
            // los pesos vuelven exactamente a como estaban (respaldo fila a fila). El criterio de los
            // dos detectores queda ampliado a propósito — no escriben nada, sólo reportan, y volver a
            // filtrar por NULL re-escondería el caso.
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string FN_AUDITORIA_SQL = @"-- ============================================================================
-- fn_auditoria_liquidacion_engorde — Verificador de Liquidación Pollo Engorde EC
-- ----------------------------------------------------------------------------
-- Recibe el alcance (company/granja/núcleo/código de lote) y los valores
-- ""correctos"" del Excel (JSONB clave→valor del TOTAL de la corrida) y devuelve
-- UN jsonb armado con:
--   * reconciliacion: sistema vs Excel por indicador (clase 'dato'|'definicion')
--   * hallazgos: detectores de datos con los registros exactos afectados
--   * simulacion: resultado corregido vs Excel (¿cuadra?)
-- Toda la lógica vive en BD para que el back sea delgado (solo parsea el Excel).
-- NO escribe nada (solo diagnostica). Reusa fn_indicadores_pollo_engorde.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_auditoria_liquidacion_engorde(
    p_company_id  INT,
    p_granja_id   INT,
    p_nucleo_id   TEXT,
    p_lote_codigo TEXT,
    p_excel       JSONB
)
RETURNS JSONB
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    v_lotes         INT[];
    v_granja_nombre TEXT;
    v_enc INT; v_sac INT; v_mort INT;
    v_cons NUMERIC; v_kg NUMERIC; v_prod NUMERIC;
    v_merma_u INT; v_merma_kg NUMERIC; v_hay_merma BOOLEAN;
    v_edad NUMERIC; v_dias NUMERIC;
    v_peso NUMERIC; v_conv NUMERIC; v_superv NUMERIC; v_consave NUMERIC;
    v_recon JSONB;
    v_hallazgos JSONB := '[]'::jsonb;
    v_sim JSONB;
    -- detectores
    v_sinpeso JSONB; v_sinpeso_n INT; v_sinpeso_aves INT;
    v_avg_peso NUMERIC; v_impacto NUMERIC;
    v_anulado JSONB; v_anulado_aves INT;
    v_multilote JSONB;
    v_ajuste JSONB;
    -- validación del Excel
    v_excel_enc NUMERIC; v_excel_sac NUMERIC; v_excel_valido BOOLEAN;
    -- simulación
    v_excel_prod NUMERIC; v_gap NUMERIC;
    v_prod_corr NUMERIC; v_peso_corr NUMERIC; v_conv_corr NUMERIC;
BEGIN
    -- 1) Lotes en alcance (mismos criterios que el reporte; sin filtro de cerrados)
    SELECT array_agg(l.lote_ave_engorde_id ORDER BY l.lote_nombre), max(f.name)
      INTO v_lotes, v_granja_nombre
    FROM public.lote_ave_engorde l
    JOIN public.farms f ON f.id = l.granja_id
    WHERE l.company_id = p_company_id
      AND l.granja_id  = p_granja_id
      AND (p_nucleo_id   IS NULL OR l.nucleo_id   = p_nucleo_id)
      AND (p_lote_codigo IS NULL OR l.lote_nombre LIKE p_lote_codigo || '%')
      AND l.deleted_at IS NULL;

    IF v_lotes IS NULL THEN
        RETURN jsonb_build_object(
            'error', 'No se encontraron lotes en el alcance indicado.',
            'scope', jsonb_build_object('companyId',p_company_id,'granjaId',p_granja_id,
                       'nucleoId',p_nucleo_id,'loteCodigo',p_lote_codigo));
    END IF;

    -- 2) Agregados del sistema (definiciones autoritativas, una fila por lote)
    SELECT sum(aves_encasetadas), sum(aves_sacrificadas), sum(mortalidad),
           sum(consumo_total_alimento_kg), sum(kg_carne_pollos), sum(produccion_kilo_en_pie),
           sum(CASE WHEN merma_unidades IS NOT NULL OR merma_kilos IS NOT NULL
                    THEN coalesce(merma_unidades,0) ELSE 0 END),
           sum(coalesce(merma_kilos,0)),
           bool_or(merma_unidades IS NOT NULL OR merma_kilos IS NOT NULL),
           CASE WHEN sum(aves_sacrificadas) > 0
                THEN sum(edad_promedio * aves_sacrificadas) / sum(aves_sacrificadas) ELSE 0 END,
           avg(dias_engorde)
      INTO v_enc, v_sac, v_mort, v_cons, v_kg, v_prod,
           v_merma_u, v_merma_kg, v_hay_merma, v_edad, v_dias
    FROM unnest(v_lotes) AS t(id),
         LATERAL public.fn_indicadores_pollo_engorde(t.id, 2.7, 4.5);

    v_peso    := CASE WHEN v_sac  > 0 THEN v_prod / v_sac          ELSE 0 END;
    v_conv    := CASE WHEN v_prod > 0 THEN v_cons / v_prod         ELSE 0 END;
    v_superv  := CASE WHEN v_enc  > 0 THEN (v_enc-v_mort)::numeric / v_enc * 100 ELSE 0 END;
    v_consave := CASE WHEN v_sac  > 0 THEN v_cons / v_sac          ELSE 0 END;

    -- Validación del Excel: valores clave deben venir presentes y > 0 (si no, es plantilla/archivo
    -- equivocado o con fórmulas en error #DIV/0!). 0 se trata como ""sin dato válido"".
    v_excel_enc  := NULLIF((p_excel->>'aves_encasetadas')::numeric, 0);
    v_excel_sac  := NULLIF((p_excel->>'aves_sacrificadas')::numeric, 0);
    v_excel_prod := NULLIF((p_excel->>'produccion_kilo_en_pie')::numeric, 0);
    v_excel_valido := v_excel_enc IS NOT NULL AND v_excel_sac IS NOT NULL AND v_excel_prod IS NOT NULL;

    -- 3) Reconciliación sistema vs Excel
    WITH ind(orden,clave,label,unidad,sistema,excel,dec,clase) AS (
      VALUES
        (1, 'aves_encasetadas','Aves encasetadas','aves',
            v_enc::numeric, (p_excel->>'aves_encasetadas')::numeric, 0, 'dato'),
        (2, 'aves_sacrificadas','Aves sacrificadas','aves',
            v_sac::numeric, (p_excel->>'aves_sacrificadas')::numeric, 0, 'dato'),
        (3, 'mortalidad','Mortalidad (unidades)','aves',
            v_mort::numeric, (p_excel->>'mortalidad')::numeric, 0, 'dato'),
        (4, 'mortalidad_pct','Mortalidad (%)','%',
            CASE WHEN v_enc>0 THEN v_mort::numeric/v_enc*100 ELSE 0 END,
            (p_excel->>'mortalidad_pct')::numeric, 2, 'dato'),
        (5, 'merma_unidades','Merma (unidades)','aves',
            v_merma_u::numeric, (p_excel->>'merma_unidades')::numeric, 0, 'dato'),
        (6, 'merma_kilos','Merma (kilos)','kg',
            v_merma_kg, (p_excel->>'merma_kilos')::numeric, 2, 'dato'),
        (7, 'ajuste_aves','Ajuste en aves','aves',
            (v_enc-v_sac-v_mort)::numeric, (p_excel->>'ajuste_aves')::numeric, 0, 'dato'),
        (8, 'porcentaje_ajuste','Porcentaje de ajuste','%',
            CASE WHEN v_enc>0 THEN (v_enc-v_sac-v_mort)::numeric/v_enc*100 ELSE 0 END,
            (p_excel->>'porcentaje_ajuste')::numeric, 2, 'dato'),
        (9, 'supervivencia','Supervivencia (%)','%',
            v_superv, (p_excel->>'supervivencia')::numeric, 2, 'dato'),
        (10,'consumo_total','Consumo total alimento','kg',
            v_cons, (p_excel->>'consumo_total')::numeric, 0, 'dato'),
        (11,'consumo_ave','Consumo ave','kg',
            v_consave, (p_excel->>'consumo_ave')::numeric, 2, 'dato'),
        (12,'produccion_kilo_en_pie','Producción kilo en pie','kg',
            v_prod, (p_excel->>'produccion_kilo_en_pie')::numeric, 0, 'dato'),
        (13,'total_kilos_despachados_cliente','Total kilos despachados a cliente','kg',
            (v_prod - v_merma_kg), (p_excel->>'total_kilos_despachados_cliente')::numeric, 0, 'dato'),
        (14,'peso_promedio','Peso promedio','kg',
            v_peso, (p_excel->>'peso_promedio')::numeric, 2, 'dato'),
        (15,'conversion','Conversión','',
            v_conv, (p_excel->>'conversion')::numeric, 2, 'dato'),
        (16,'eficiencia_americana','Eficiencia Americana','',
            CASE WHEN v_conv>0 THEN (v_peso/v_conv)*100 ELSE 0 END,
            (p_excel->>'eficiencia_americana')::numeric, 2, 'dato'),
        (17,'productividad','Productividad','',
            CASE WHEN v_conv>0 THEN (v_peso/v_conv)/v_conv*100 ELSE 0 END,
            (p_excel->>'productividad')::numeric, 2, 'dato'),
        -- diferencias de definición (no son falla de dato)
        (18,'merma_pct','Merma (%)','%',
            CASE WHEN v_sac>0 THEN v_merma_u::numeric/v_sac*100 ELSE 0 END,
            (p_excel->>'merma_pct')::numeric, 2, 'definicion'),
        (19,'dias_engorde','Días de engorde','días',
            v_dias, (p_excel->>'dias_engorde')::numeric, 0, 'definicion'),
        (20,'edad_ponderada','Edad ponderada','días',
            v_edad, (p_excel->>'edad_ponderada')::numeric, 2, 'definicion')
    )
    SELECT jsonb_agg(jsonb_build_object(
        'clave', clave, 'label', label, 'unidad', unidad, 'clase', clase,
        'sistema', round(sistema, dec),
        'excel',   round(excel, dec),
        'tieneExcel', excel IS NOT NULL,
        'diferencia', CASE WHEN excel IS NOT NULL THEN round(sistema-excel, dec) END,
        'difPct', CASE WHEN excel IS NOT NULL AND excel<>0 THEN round((sistema-excel)/excel*100, 2) END,
        -- tolerancia = 1 unidad en el último decimal mostrado (dec 0 ⇒ ±1; dec 2 ⇒ ±0.01).
        -- aves/días exactos (sin redondeo de báscula).
        'cuadra', (excel IS NOT NULL AND abs(round(sistema,dec) - round(excel,dec)) <=
                   CASE WHEN unidad IN ('aves','días') THEN 0 ELSE power(10::numeric, -dec) END)
    ) ORDER BY orden)
    INTO v_recon
    FROM ind;

    -- 4a) DETECTOR: despachos sin peso (cuentan aves, 0 kg)
    SELECT coalesce(jsonb_agg(jsonb_build_object(
              'id',id,'numero',numero,'lote',lote,'aves',aves,
              'fecha',fecha,'placa',placa,'edad',edad) ORDER BY fecha), '[]'::jsonb),
           count(*), coalesce(sum(aves),0)
      INTO v_sinpeso, v_sinpeso_n, v_sinpeso_aves
    FROM (
      SELECT id, numero_movimiento numero, lote_ave_engorde_origen_id lote,
             (cantidad_hembras+cantidad_machos+cantidad_mixtas) aves,
             fecha_movimiento::date fecha, placa, edad_aves edad
      FROM public.movimiento_pollo_engorde
      WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
        AND estado='Completado' AND deleted_at IS NULL
        AND tipo_movimiento IN ('Venta','Despacho','Retiro')
        -- Criterio: el despacho APORTA 0 kg. Cubre el peso ausente (NULL) y el peso
        -- declarado que da neto 0 — el caso de Panamá (9-sep-2026): bruto y tara con el
        -- MISMO número en 206 de 207 ventas. Filtrar sólo por NULL dejaba ese caso invisible.
        AND COALESCE(peso_neto, 0) = 0
    ) x;

    v_avg_peso := CASE WHEN (v_sac - v_sinpeso_aves) > 0 THEN v_prod/(v_sac - v_sinpeso_aves) ELSE 0 END;
    v_impacto  := round(v_sinpeso_aves * v_avg_peso, 2);

    IF v_sinpeso_aves > 0 THEN
      v_hallazgos := v_hallazgos || jsonb_build_array(jsonb_build_object(
        'codigo','MOV_SIN_PESO','severidad','critico','tipo','dato',
        'titulo','Despachos sin peso registrado',
        'descripcion', format('%s despacho(s) con %s aves no aportan kilos (peso neto ausente o en 0): se cuentan las aves pero aportan 0 kg, lo que baja producción kilo en pie, peso promedio y sube la conversión. Cargar el tiquete de báscula de esos movimientos.', v_sinpeso_n, v_sinpeso_aves),
        'impactoKgEstimado', v_impacto,
        'pesoAvePromedioResto', round(v_avg_peso,4),
        'registros', v_sinpeso));
    END IF;

    -- 4b) DETECTOR: ventas 'Anulado' activas (no borradas) que se contarían
    SELECT coalesce(jsonb_agg(jsonb_build_object('id',id,'numero',numero_movimiento,
              'lote',lote_ave_engorde_origen_id,'estado',estado,
              'aves',cantidad_hembras+cantidad_machos+cantidad_mixtas)), '[]'::jsonb),
           coalesce(sum(cantidad_hembras+cantidad_machos+cantidad_mixtas),0)
      INTO v_anulado, v_anulado_aves
    FROM public.movimiento_pollo_engorde
    WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
      AND estado='Anulado' AND deleted_at IS NULL
      AND tipo_movimiento IN ('Venta','Despacho','Retiro');

    IF v_anulado_aves > 0 THEN
      v_hallazgos := v_hallazgos || jsonb_build_array(jsonb_build_object(
        'codigo','ANULADO_ACTIVO','severidad','alerta','tipo','dato',
        'titulo','Movimientos anulados sin marcar como borrados',
        'descripcion','Hay ventas en estado Anulado con deleted_at NULL: podrían contarse en aves/kg. Verificar su borrado lógico.',
        'registros', v_anulado));
    END IF;

    -- 4c) DETECTOR: merma esperada por Excel pero no registrada en ningún lote
    IF (NOT v_hay_merma) AND coalesce((p_excel->>'merma_kilos')::numeric,0) > 0 THEN
      v_hallazgos := v_hallazgos || jsonb_build_array(jsonb_build_object(
        'codigo','MERMA_NO_REGISTRADA','severidad','alerta','tipo','dato',
        'titulo','Merma del Excel no registrada en el sistema',
        'descripcion', format('El Excel reporta merma (%s kg) pero ningún lote de la corrida tiene merma registrada. Registrarla (una vez por corrida).', p_excel->>'merma_kilos')));
    END IF;

    -- 4d) DETECTOR: despachos multi-lote (báscula duplica si se suma por línea)
    -- Agrupa por (placa, peso_neto_global) = un mismo pesaje de camión (robusto a zona horaria,
    -- a diferencia de agrupar por ::date que puede partir un viaje en dos fechas).
    SELECT coalesce(jsonb_agg(jsonb_build_object('placa',placa,'fecha',fecha,
              'lotes',lotes,'lineas',lineas,'netoCamion',neto_g)), '[]'::jsonb)
      INTO v_multilote
    FROM (
      SELECT placa, max(fecha_movimiento::date) fecha, peso_neto_global neto_g,
             string_agg(DISTINCT lote_ave_engorde_origen_id::text, ',') lotes, count(*) lineas
      FROM public.movimiento_pollo_engorde
      WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
        AND estado='Completado' AND deleted_at IS NULL
        AND tipo_movimiento IN ('Venta','Despacho','Retiro')
        AND peso_neto_global IS NOT NULL
      GROUP BY placa, peso_neto_global
      HAVING count(DISTINCT lote_ave_engorde_origen_id) > 1
    ) m;

    IF jsonb_array_length(v_multilote) > 0 THEN
      v_hallazgos := v_hallazgos || jsonb_build_array(jsonb_build_object(
        'codigo','DESPACHO_MULTILOTE','severidad','info','tipo','validacion',
        'titulo','Despachos que cargan de varios galpones',
        'descripcion','Camiones que despachan de >1 lote. El sistema usa peso_neto prorrateado (correcto). Si el Excel suma báscula (bruto−tara) por línea, duplica estos camiones.',
        'registros', v_multilote));
    END IF;

    -- 4e) DETECTOR: ajuste alto por lote (|enc-sac-mort|/enc > 1%)
    SELECT coalesce(jsonb_agg(jsonb_build_object('lote',id,'encasetadas',enc,
              'ajuste',ajuste,'porcentaje',round(pct,2)) ORDER BY id), '[]'::jsonb)
      INTO v_ajuste
    FROM (
      SELECT t.id, f.aves_encasetadas enc, (f.aves_encasetadas - f.aves_sacrificadas - f.mortalidad) ajuste,
             CASE WHEN f.aves_encasetadas>0
                  THEN abs(f.aves_encasetadas - f.aves_sacrificadas - f.mortalidad)::numeric/f.aves_encasetadas*100 ELSE 0 END pct
      FROM unnest(v_lotes) AS t(id), LATERAL public.fn_indicadores_pollo_engorde(t.id,2.7,4.5) f
    ) a
    WHERE pct > 1.0;

    IF jsonb_array_length(v_ajuste) > 0 THEN
      v_hallazgos := v_hallazgos || jsonb_build_array(jsonb_build_object(
        'codigo','AJUSTE_ALTO','severidad','alerta','tipo','dato',
        'titulo','Ajuste de aves alto en uno o más lotes',
        'descripcion','Lotes con |encasetadas − vendidas − mortalidad| > 1%: posibles aves no registradas (mortalidad/ventas) o conteo inicial.',
        'registros', v_ajuste));
    END IF;

    -- 4f) DETECTOR: Excel incompleto / archivo equivocado (valores clave vacíos o en cero).
    -- Se antepone (es el más importante): explica por qué ""no cuadra"" antes que la reconciliación ruidosa.
    IF NOT v_excel_valido THEN
      v_hallazgos := jsonb_build_array(jsonb_build_object(
        'codigo','EXCEL_INCOMPLETO','severidad','critico','tipo','excel',
        'titulo','El Excel cargado parece incompleto o no es el correcto',
        'descripcion', format('Valores clave del Excel vacíos o en cero (encasetadas=%s, sacrificadas=%s, producción=%s). Suele pasar con plantillas o archivos cuyas fórmulas dan error (#DIV/0!, #VALUE!). Suba el archivo de liquidación ya calculado y con valores.',
            coalesce(v_excel_enc, 0), coalesce(v_excel_sac, 0), coalesce(v_excel_prod, 0)))
      ) || v_hallazgos;
    END IF;

    -- 5) Simulación de corrección (atribuye el gap a los despachos sin peso)
    -- v_excel_prod ya viene con NULLIF(...,0): 0 = sin dato válido (evita cuadre falso).
    v_gap        := CASE WHEN v_excel_prod IS NOT NULL THEN v_excel_prod - v_prod END;
    v_prod_corr  := coalesce(v_excel_prod, v_prod + v_impacto);
    v_peso_corr  := CASE WHEN v_sac  > 0 THEN v_prod_corr / v_sac  ELSE 0 END;
    v_conv_corr  := CASE WHEN v_prod_corr > 0 THEN v_cons / v_prod_corr ELSE 0 END;

    v_sim := jsonb_build_object(
      'supuesto','Se carga el peso faltante de los despachos sin peso. El faltante para cuadrar con el Excel se atribuye a esos registros.',
      'gapKg', round(v_gap,2),
      'atribuibleASinPeso', (v_gap IS NOT NULL AND v_gap > 0 AND v_sinpeso_aves > 0),
      'pesoAveImplicito',
          CASE WHEN v_sinpeso_aves>0 AND v_gap IS NOT NULL AND v_gap>0 THEN round(v_gap/v_sinpeso_aves,4) END,
      'pesoAvePromedioResto', round(v_avg_peso,4),
      'impactoSiPesaranComoResto', v_impacto,
      'nota', CASE
                WHEN v_sinpeso_aves=0 THEN 'No hay despachos sin peso; revisar otras causas del gap.'
                WHEN v_gap IS NULL THEN 'El Excel no trae producción kilo en pie para comparar.'
                WHEN v_gap <= 0 THEN 'El sistema no está por debajo del Excel en producción.'
                WHEN round(v_gap/NULLIF(v_sinpeso_aves,0),2) < 2.0 THEN
                     format('El gap (%s kg) se atribuye a los despachos sin peso, pero el peso implícito por ave (%s kg) es bajo: confirmar el tiquete físico de báscula.', round(v_gap,0), round(v_gap/NULLIF(v_sinpeso_aves,0),2))
                ELSE format('El gap (%s kg) se atribuye a los despachos sin peso (peso implícito %s kg/ave).', round(v_gap,0), round(v_gap/NULLIF(v_sinpeso_aves,0),2))
              END,
      'indicadores', jsonb_build_array(
        jsonb_build_object('label','Producción kilo en pie','clave','produccion_kilo_en_pie',
          'sistemaActual',round(v_prod,0),'corregido',round(v_prod_corr,0),
          'excel',round(v_excel_prod,0),
          'cuadra', v_excel_prod IS NOT NULL AND abs(round(v_prod_corr,0)-round(v_excel_prod,0))<=1),
        jsonb_build_object('label','Total kilos despachados a cliente','clave','total_kilos_despachados_cliente',
          'sistemaActual',round(v_prod - v_merma_kg,0),'corregido',round(v_prod_corr - v_merma_kg,0),
          'excel',round((p_excel->>'total_kilos_despachados_cliente')::numeric,0),
          'cuadra', (p_excel->>'total_kilos_despachados_cliente') IS NOT NULL
                    AND abs(round(v_prod_corr - v_merma_kg,0)-round((p_excel->>'total_kilos_despachados_cliente')::numeric,0))<=1),
        jsonb_build_object('label','Peso promedio','clave','peso_promedio',
          'sistemaActual',round(v_peso,2),'corregido',round(v_peso_corr,2),
          'excel',round((p_excel->>'peso_promedio')::numeric,2),
          'cuadra', (p_excel->>'peso_promedio') IS NOT NULL
                    AND round(v_peso_corr,2)=round((p_excel->>'peso_promedio')::numeric,2)),
        jsonb_build_object('label','Conversión','clave','conversion',
          'sistemaActual',round(v_conv,2),'corregido',round(v_conv_corr,2),
          'excel',round((p_excel->>'conversion')::numeric,2),
          'cuadra', (p_excel->>'conversion') IS NOT NULL
                    AND round(v_conv_corr,2)=round((p_excel->>'conversion')::numeric,2)),
        jsonb_build_object('label','Eficiencia Americana','clave','eficiencia_americana',
          'sistemaActual', round(CASE WHEN v_conv>0 THEN (v_peso/v_conv)*100 ELSE 0 END,2),
          'corregido',     round(CASE WHEN v_conv_corr>0 THEN (v_peso_corr/v_conv_corr)*100 ELSE 0 END,2),
          'excel',round((p_excel->>'eficiencia_americana')::numeric,2),
          'cuadra', (p_excel->>'eficiencia_americana') IS NOT NULL
                    AND round(CASE WHEN v_conv_corr>0 THEN (v_peso_corr/v_conv_corr)*100 ELSE 0 END,2)
                        =round((p_excel->>'eficiencia_americana')::numeric,2)),
        jsonb_build_object('label','Productividad','clave','productividad',
          'sistemaActual', round(CASE WHEN v_conv>0 THEN (v_peso/v_conv)/v_conv*100 ELSE 0 END,2),
          'corregido',     round(CASE WHEN v_conv_corr>0 THEN (v_peso_corr/v_conv_corr)/v_conv_corr*100 ELSE 0 END,2),
          'excel',round((p_excel->>'productividad')::numeric,2),
          'cuadra', (p_excel->>'productividad') IS NOT NULL
                    AND round(CASE WHEN v_conv_corr>0 THEN (v_peso_corr/v_conv_corr)/v_conv_corr*100 ELSE 0 END,2)
                        =round((p_excel->>'productividad')::numeric,2))
      )
    );

    -- 6) Ensamblar resultado
    RETURN jsonb_build_object(
      'scope', jsonb_build_object('companyId',p_company_id,'granjaId',p_granja_id,
                 'granjaNombre',v_granja_nombre,'nucleoId',p_nucleo_id,
                 'loteCodigo',p_lote_codigo,'lotes',to_jsonb(v_lotes)),
      'resumen', jsonb_build_object(
          'excelValido', v_excel_valido,
          'indicadoresComparados', (SELECT count(*) FROM jsonb_array_elements(v_recon) e WHERE (e->>'tieneExcel')::boolean),
          'fallasDato', (SELECT count(*) FROM jsonb_array_elements(v_recon) e
                         WHERE (e->>'clase')='dato' AND (e->>'tieneExcel')::boolean AND NOT (e->>'cuadra')::boolean),
          'difDefinicion', (SELECT count(*) FROM jsonb_array_elements(v_recon) e
                         WHERE (e->>'clase')='definicion' AND (e->>'tieneExcel')::boolean AND NOT (e->>'cuadra')::boolean),
          'hallazgos', jsonb_array_length(v_hallazgos)),
      'reconciliacion', v_recon,
      'hallazgos', v_hallazgos,
      'simulacion', v_sim,
      'generadoEn', now()
    );
END;
$$;
";

        private const string FN_CORRECCION_SQL = @"-- ============================================================================
-- fn_aplicar_correccion_despachos_sin_peso — Aplica la corrección sugerida por el
-- verificador de liquidación: carga el peso faltante en los despachos que aportan
-- 0 kg (peso neto ausente O en 0) de una corrida, distribuyendo p_kg_total entre
-- ellos proporcional a las aves. Escribe peso_neto + peso_neto_global y audita
-- (updated_at / updated_by_user_id). NO es STABLE (modifica datos).
-- Pensada para llamarse desde un endpoint gateado por el permiso
-- 'liquidacion.aplicar_correccion'. Transaccional (un solo UPDATE).
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_aplicar_correccion_despachos_sin_peso(
    p_company_id  INT,
    p_granja_id   INT,
    p_nucleo_id   TEXT,
    p_lote_codigo TEXT,
    p_kg_total    NUMERIC,
    p_user_id     INT
)
RETURNS JSONB
LANGUAGE plpgsql
AS $$
DECLARE
    v_lotes      INT[];
    v_total_aves INT;
    v_aplicados  JSONB;
BEGIN
    IF p_kg_total IS NULL OR p_kg_total <= 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'El total de kg a aplicar debe ser mayor a 0.');
    END IF;

    SELECT array_agg(lote_ave_engorde_id)
      INTO v_lotes
    FROM public.lote_ave_engorde
    WHERE company_id = p_company_id
      AND granja_id  = p_granja_id
      AND (p_nucleo_id   IS NULL OR nucleo_id   = p_nucleo_id)
      AND (p_lote_codigo IS NULL OR lote_nombre LIKE p_lote_codigo || '%')
      AND deleted_at IS NULL;

    IF v_lotes IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'No se encontraron lotes en el alcance indicado.');
    END IF;

    -- Aves de los despachos que aportan 0 kg (mismo criterio que el detector MOV_SIN_PESO:
    -- peso neto ausente O en 0; el segundo caso es el de Panamá, bruto == tara)
    SELECT coalesce(sum(cantidad_hembras + cantidad_machos + cantidad_mixtas), 0)
      INTO v_total_aves
    FROM public.movimiento_pollo_engorde
    WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
      AND estado = 'Completado' AND deleted_at IS NULL
      AND tipo_movimiento IN ('Venta','Despacho','Retiro')
      AND COALESCE(peso_neto, 0) = 0;

    IF v_total_aves = 0 THEN
        RETURN jsonb_build_object('ok', false,
            'error', 'No hay despachos sin peso para corregir en este alcance (puede que ya se haya aplicado).');
    END IF;

    -- Distribuir p_kg_total proporcional a las aves y escribir peso_neto (auditado)
    WITH objetivo AS (
        SELECT id, (cantidad_hembras + cantidad_machos + cantidad_mixtas) AS aves
        FROM public.movimiento_pollo_engorde
        WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
          AND estado = 'Completado' AND deleted_at IS NULL
          AND tipo_movimiento IN ('Venta','Despacho','Retiro')
          AND COALESCE(peso_neto, 0) = 0
    ),
    upd AS (
        UPDATE public.movimiento_pollo_engorde m
        SET peso_neto          = round((p_kg_total * o.aves / v_total_aves)::numeric, 2),
            peso_neto_global   = round((p_kg_total * o.aves / v_total_aves)::numeric, 2),
            updated_at         = now(),
            updated_by_user_id = p_user_id
        FROM objetivo o
        WHERE m.id = o.id
        RETURNING m.id, o.aves, m.peso_neto
    )
    SELECT jsonb_agg(jsonb_build_object('id', id, 'aves', aves, 'pesoAsignado', peso_neto) ORDER BY id)
      INTO v_aplicados
    FROM upd;

    RETURN jsonb_build_object(
        'ok',          true,
        'kgTotal',     p_kg_total,
        'avesTotales', v_total_aves,
        'movimientos', jsonb_array_length(v_aplicados),
        'aplicados',   v_aplicados
    );
END;
$$;
";

        private const string BACKFILL_SQL = @"-- ============================================================================
-- backfill_venta_engorde_neto_cero_bruto_igual_tara.sql
-- ESPEJO de la migración FixVentaEngordeNetoCeroBrutoIgualTara (el vehículo es la migración).
-- ----------------------------------------------------------------------------
-- QUÉ CORRIGE
--   Ventas de pollo engorde donde el operario digitó el MISMO número en `peso_bruto` y en
--   `peso_tara` ⇒ `peso_neto = 0`: la venta cuenta las aves y aporta 0 kg al seguimiento diario,
--   al informe semanal, a la liquidación y a los indicadores.
--
-- POR QUÉ ESE NÚMERO ES EL NETO (medido 9-sep-2026 sobre la copia de prod del 3-sep)
--   ItalcolPanama: 206 de 207 ventas con `peso_bruto = peso_tara`; `peso_bruto` promedia
--   2,357 kg/ave — el peso de un pollo de 32-42 días, NO el de un camión cargado. La empresa que
--   usa los campos como corresponde (ItalcolEcuador, 0 de 1.472 con bruto = tara) promedia
--   7,21 kg/ave en bruto y 2,836 en neto. Planta entrega UNA sola cifra de kilos y el formulario
--   pide dos, así que se repetía el número.
--
-- QUÉ HACE
--   Mueve ese valor a `peso_neto` y deja `peso_tara = 0` («no se reportó tara»), que es la verdad
--   de lo que hay: no inventa kilos, reubica los que ya estaban digitados.
--
-- SELECCIÓN POR PATRÓN, NO POR EMPRESA
--   El WHERE no nombra ninguna company_id: describe el defecto (bruto = tara > 0 con neto 0). Hoy
--   sólo Panamá cae, pero los ids de empresa difieren local↔prod y una regla por tenant no escala.
--
-- MULTI-LÍNEA
--   `peso_bruto` es el peso del camión CLONADO en cada línea de la factura; el individual correcto
--   es el prorrateo por aves. Se replica MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea:
--   redondeo a 3 decimales y residuo a la línea con más aves. Con tara 0, bruto prorrateado == neto.
--   (Las 206 filas de hoy son facturas de UNA línea ⇒ el prorrateo es la identidad y el residuo 0.)
--
-- IDEMPOTENTE
--   Tras correr, las filas tienen `peso_neto > 0` y dejan de cumplir el WHERE ⇒ re-ejecutar es no-op.
--   El respaldo se llena una sola vez por fila (NOT EXISTS).
--
-- EL ESPEJO HISTÓRICO SE ACTUALIZA SOLO
--   `trg_movimiento_pollo_engorde_lote_hist` escucha `UPDATE OF … peso_neto, peso_tara_real,
--   promedio_peso_ave …` ⇒ `lote_registro_historico_unificado` se reescribe sin tocarlo a mano.
-- ============================================================================

-- 1) Respaldo previo (para revertir sin adivinar).
CREATE TABLE IF NOT EXISTS public._backup_mpe_peso_neto_cero (
    id                  INTEGER PRIMARY KEY,
    peso_bruto          DOUBLE PRECISION,
    peso_tara           DOUBLE PRECISION,
    peso_bruto_global   DOUBLE PRECISION,
    peso_tara_global    DOUBLE PRECISION,
    peso_neto_global    DOUBLE PRECISION,
    peso_bruto_real     DOUBLE PRECISION,
    peso_tara_real      DOUBLE PRECISION,
    peso_neto           DOUBLE PRECISION,
    promedio_peso_ave   DOUBLE PRECISION,
    respaldado_en       TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public._backup_mpe_peso_neto_cero (
    id, peso_bruto, peso_tara, peso_bruto_global, peso_tara_global, peso_neto_global,
    peso_bruto_real, peso_tara_real, peso_neto, promedio_peso_ave)
SELECT m.id, m.peso_bruto, m.peso_tara, m.peso_bruto_global, m.peso_tara_global, m.peso_neto_global,
       m.peso_bruto_real, m.peso_tara_real, m.peso_neto, m.promedio_peso_ave
FROM public.movimiento_pollo_engorde m
WHERE m.deleted_at IS NULL
  AND m.tipo_movimiento = 'Venta'
  AND m.peso_bruto IS NOT NULL
  AND m.peso_bruto > 0
  AND m.peso_bruto = m.peso_tara
  AND COALESCE(m.peso_neto, 0) = 0
  AND (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) > 0
  AND NOT EXISTS (SELECT 1 FROM public._backup_mpe_peso_neto_cero b WHERE b.id = m.id);

-- 2) Corrección.
WITH objetivo AS (
    SELECT m.id,
           -- Sin factura, la línea es su propio despacho (no agrupar todos los NULL juntos).
           COALESCE(m.factura_id::text, 'mov-' || m.id) AS despacho,
           m.peso_bruto::numeric                        AS global_kg,
           (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas)::numeric AS aves
    FROM public.movimiento_pollo_engorde m
    WHERE m.deleted_at IS NULL
      AND m.tipo_movimiento = 'Venta'
      AND m.peso_bruto IS NOT NULL
      AND m.peso_bruto > 0
      AND m.peso_bruto = m.peso_tara
      AND COALESCE(m.peso_neto, 0) = 0
      AND (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) > 0
),
prorrateo AS (
    SELECT o.id, o.despacho, o.aves, o.global_kg,
           ROUND(o.global_kg * o.aves / SUM(o.aves) OVER (PARTITION BY o.despacho), 3) AS neto_base,
           ROW_NUMBER() OVER (PARTITION BY o.despacho ORDER BY o.aves DESC, o.id)      AS rn
    FROM objetivo o
),
residuo AS (
    SELECT p.despacho, MAX(p.global_kg) - SUM(p.neto_base) AS resto
    FROM prorrateo p
    GROUP BY p.despacho
),
final AS (
    SELECT p.id, p.aves, p.global_kg,
           ROUND(p.neto_base + CASE WHEN p.rn = 1 THEN r.resto ELSE 0 END, 3) AS neto
    FROM prorrateo p
    JOIN residuo r ON r.despacho = p.despacho
)
UPDATE public.movimiento_pollo_engorde m
   SET peso_tara         = 0,
       peso_tara_global  = 0,
       peso_tara_real    = 0,
       peso_bruto_global = f.global_kg::double precision,
       peso_bruto_real   = f.neto::double precision,   -- con tara 0, el bruto prorrateado ES el neto
       peso_neto_global  = f.global_kg::double precision,
       peso_neto         = f.neto::double precision,
       promedio_peso_ave = (f.neto / f.aves)::double precision,
       updated_at        = now()
  FROM final f
 WHERE m.id = f.id;
";

        private const string DOWN_SQL = @"
UPDATE public.movimiento_pollo_engorde m
   SET peso_bruto        = b.peso_bruto,
       peso_tara         = b.peso_tara,
       peso_bruto_global = b.peso_bruto_global,
       peso_tara_global  = b.peso_tara_global,
       peso_neto_global  = b.peso_neto_global,
       peso_bruto_real   = b.peso_bruto_real,
       peso_tara_real    = b.peso_tara_real,
       peso_neto         = b.peso_neto,
       promedio_peso_ave = b.promedio_peso_ave
  FROM public._backup_mpe_peso_neto_cero b
 WHERE m.id = b.id;

DROP TABLE IF EXISTS public._backup_mpe_peso_neto_cero;
";
    }
}
