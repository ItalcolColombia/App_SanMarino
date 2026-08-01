-- ============================================================================
-- CUADRE DE LECTURA — espejo_huevo_produccion vs sus fuentes (patrón «el cuadre
-- se mira, no se espera», análogo a fn_cuadre_alimento_engorde).
--
-- El espejo lo mantiene EXCLUSIVAMENTE el recálculo C# absoluto
-- (EspejoHuevoProduccionSyncService; D1 del plan seguimiento_produccion_fn_canonica,
-- trigger legacy retirado por 20260801071000). Ese recálculo corre en un SaveChanges
-- posterior al del dato: si el proceso muere entre ambos, el espejo queda viejo hasta
-- el próximo write. Este script delata ese estado: TODA fila debe salir con
-- descuadre_historico = 0 y descuadre_dinamico = 0.
--
-- Fórmula espejo (contrato del C#):
--   historico = Σ seguimiento_diario_produccion por LPP (clamp int)
--   dinamico  = max(0, historico − Σ traslado_huevos Completado no borrados del LPP)
-- ============================================================================
SELECT e.lote_postura_produccion_id                        AS lpp_id,
       c.name                                              AS empresa,
       e.huevo_tot_historico,
       s.tot                                               AS suma_produccion,
       e.huevo_tot_historico - s.tot                       AS descuadre_historico,
       e.huevo_tot_dinamico,
       GREATEST(0, s.tot - t.tot)                          AS dinamico_esperado,
       e.huevo_tot_dinamico - GREATEST(0, s.tot - t.tot)   AS descuadre_dinamico
  FROM espejo_huevo_produccion e
  JOIN lote_postura_produccion l ON l.lote_postura_produccion_id = e.lote_postura_produccion_id
  JOIN companies c ON c.id = e.company_id
  CROSS JOIN LATERAL (
      SELECT COALESCE(SUM(sp.huevo_tot), 0) AS tot
        FROM seguimiento_diario_produccion sp
       WHERE sp.lote_postura_produccion_id = e.lote_postura_produccion_id
  ) s
  CROSS JOIN LATERAL (
      SELECT COALESCE(SUM(th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio
                        + th.cantidad_deforme + th.cantidad_blanco + th.cantidad_doble_yema
                        + th.cantidad_piso + th.cantidad_pequeno + th.cantidad_roto
                        + th.cantidad_desecho + th.cantidad_otro), 0) AS tot
        FROM traslado_huevos th
       WHERE th.lote_postura_produccion_id = e.lote_postura_produccion_id
         AND th.company_id = e.company_id
         AND th.deleted_at IS NULL
         AND th.estado = 'Completado'
  ) t
 ORDER BY ABS(e.huevo_tot_historico - s.tot) DESC, e.lote_postura_produccion_id;
