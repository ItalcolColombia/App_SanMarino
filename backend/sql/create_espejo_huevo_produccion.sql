-- =============================================================================
-- TABLA ESPEJO HUEVO PRODUCCIÓN
-- Espejo de tipos de huevos por lote_postura_produccion.
-- Histórico: solo suma (producción diaria). Dinámico: suma y resta (movimientos).
-- Un registro por lote_postura_produccion.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.espejo_huevo_produccion (
    lote_postura_produccion_id   INTEGER NOT NULL PRIMARY KEY,
    company_id                   INTEGER NOT NULL,

    -- Totales (todo el tiempo del lote en producción)
    -- Histórico: acumulado desde seguimiento_diario, solo suma
    -- Dinámico: suma entradas, resta salidas (movimientos de huevos)
    huevo_tot_historico          INTEGER NOT NULL DEFAULT 0,
    huevo_tot_dinamico           INTEGER NOT NULL DEFAULT 0,
    huevo_inc_historico          INTEGER NOT NULL DEFAULT 0,
    huevo_inc_dinamico           INTEGER NOT NULL DEFAULT 0,
    huevo_limpio_historico       INTEGER NOT NULL DEFAULT 0,
    huevo_limpio_dinamico        INTEGER NOT NULL DEFAULT 0,
    huevo_tratado_historico      INTEGER NOT NULL DEFAULT 0,
    huevo_tratado_dinamico       INTEGER NOT NULL DEFAULT 0,
    huevo_sucio_historico        INTEGER NOT NULL DEFAULT 0,
    huevo_sucio_dinamico         INTEGER NOT NULL DEFAULT 0,
    huevo_deforme_historico      INTEGER NOT NULL DEFAULT 0,
    huevo_deforme_dinamico       INTEGER NOT NULL DEFAULT 0,
    huevo_blanco_historico       INTEGER NOT NULL DEFAULT 0,
    huevo_blanco_dinamico        INTEGER NOT NULL DEFAULT 0,
    huevo_doble_yema_historico   INTEGER NOT NULL DEFAULT 0,
    huevo_doble_yema_dinamico    INTEGER NOT NULL DEFAULT 0,
    huevo_piso_historico         INTEGER NOT NULL DEFAULT 0,
    huevo_piso_dinamico          INTEGER NOT NULL DEFAULT 0,
    huevo_pequeno_historico      INTEGER NOT NULL DEFAULT 0,
    huevo_pequeno_dinamico       INTEGER NOT NULL DEFAULT 0,
    huevo_roto_historico         INTEGER NOT NULL DEFAULT 0,
    huevo_roto_dinamico          INTEGER NOT NULL DEFAULT 0,
    huevo_desecho_historico      INTEGER NOT NULL DEFAULT 0,
    huevo_desecho_dinamico       INTEGER NOT NULL DEFAULT 0,
    huevo_otro_historico         INTEGER NOT NULL DEFAULT 0,
    huevo_otro_dinamico          INTEGER NOT NULL DEFAULT 0,

    -- Historial semanal (edad del lote por semana)
    -- {"26":{"semana":26,"huevo_tot":100,"huevo_inc":80,...},"27":{...}}
    historico_semanal            JSONB NULL DEFAULT '{}',

    created_at                   TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at                   TIMESTAMPTZ NULL,

    CONSTRAINT fk_ehp_lote_postura_produccion
        FOREIGN KEY (lote_postura_produccion_id)
        REFERENCES public.lote_postura_produccion(lote_postura_produccion_id) ON DELETE CASCADE,
    CONSTRAINT fk_ehp_company
        FOREIGN KEY (company_id)
        REFERENCES public.companies(id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_espejo_huevo_produccion_company
    ON public.espejo_huevo_produccion(company_id);

CREATE INDEX IF NOT EXISTS ix_espejo_huevo_produccion_historico_semanal
    ON public.espejo_huevo_produccion USING gin(historico_semanal);

COMMENT ON TABLE public.espejo_huevo_produccion IS
    'Espejo de huevos por lote postura producción. Histórico: suma desde seguimiento_diario. Dinámico: suma/resta con movimientos.';
COMMENT ON COLUMN public.espejo_huevo_produccion.huevo_tot_historico IS
    'Acumulado total de huevo_tot registrados en seguimiento_diario (solo suma)';
COMMENT ON COLUMN public.espejo_huevo_produccion.huevo_tot_dinamico IS
    'Saldo dinámico: suma producción, resta salidas (ventas/traslados)';
COMMENT ON COLUMN public.espejo_huevo_produccion.historico_semanal IS
    'JSONB por semana: { "26": { "semana": 26, "huevo_tot": 100, "huevo_inc": 80, ... } }';
