-- ============================================================================
-- fn_auditoria_liquidacion_engorde — Verificador de Liquidación Pollo Engorde EC
-- ----------------------------------------------------------------------------
-- Recibe el alcance (company/granja/núcleo/código de lote) y los valores
-- "correctos" del Excel (JSONB clave→valor del TOTAL de la corrida) y devuelve
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
    -- equivocado o con fórmulas en error #DIV/0!). 0 se trata como "sin dato válido".
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
        AND peso_neto IS NULL AND (peso_bruto IS NULL OR peso_tara IS NULL)
    ) x;

    v_avg_peso := CASE WHEN (v_sac - v_sinpeso_aves) > 0 THEN v_prod/(v_sac - v_sinpeso_aves) ELSE 0 END;
    v_impacto  := round(v_sinpeso_aves * v_avg_peso, 2);

    IF v_sinpeso_aves > 0 THEN
      v_hallazgos := v_hallazgos || jsonb_build_array(jsonb_build_object(
        'codigo','MOV_SIN_PESO','severidad','critico','tipo','dato',
        'titulo','Despachos sin peso registrado',
        'descripcion', format('%s despacho(s) con %s aves no tienen peso (peso_neto y báscula en NULL): se cuentan las aves pero aportan 0 kg, lo que baja producción kilo en pie, peso promedio y sube la conversión. Cargar el tiquete de báscula de esos movimientos.', v_sinpeso_n, v_sinpeso_aves),
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
    -- Se antepone (es el más importante): explica por qué "no cuadra" antes que la reconciliación ruidosa.
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
