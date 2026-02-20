-- Tabla de historial de lotes de pollo engorde (Lote Ave Engorde y Lote Reproductora Ave Engorde).
-- Objetivo: registrar con cuántas aves inició cada lote para reportes (inicio, vendidas, actuales).
-- tipo_lote: 'LoteAveEngorde' | 'LoteReproductoraAveEngorde'
-- tipo_registro: 'Inicio' (al crear el lote) | opcionalmente otros en el futuro.

CREATE TABLE IF NOT EXISTS public.historial_lote_pollo_engorde (
    id                              SERIAL PRIMARY KEY,
    company_id                      INTEGER NOT NULL,
    tipo_lote                       VARCHAR(32) NOT NULL,
    lote_ave_engorde_id             INTEGER NULL,
    lote_reproductora_ave_engorde_id INTEGER NULL,
    tipo_registro                   VARCHAR(24) NOT NULL DEFAULT 'Inicio',
    aves_hembras                    INTEGER NOT NULL DEFAULT 0,
    aves_machos                     INTEGER NOT NULL DEFAULT 0,
    aves_mixtas                     INTEGER NOT NULL DEFAULT 0,
    fecha_registro                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    movimiento_id                   INTEGER NULL,
    created_at                      TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),

    CONSTRAINT fk_hlpe_company FOREIGN KEY (company_id)
        REFERENCES public.companies (id) ON DELETE RESTRICT,
    CONSTRAINT fk_hlpe_lote_ave_engorde FOREIGN KEY (lote_ave_engorde_id)
        REFERENCES public.lote_ave_engorde (lote_ave_engorde_id) ON DELETE CASCADE,
    CONSTRAINT fk_hlpe_lote_reproductora_ave_engorde FOREIGN KEY (lote_reproductora_ave_engorde_id)
        REFERENCES public.lote_reproductora_ave_engorde (id) ON DELETE CASCADE,
    CONSTRAINT fk_hlpe_movimiento FOREIGN KEY (movimiento_id)
        REFERENCES public.movimiento_pollo_engorde (id) ON DELETE SET NULL,
    CONSTRAINT ck_hlpe_tipo_lote CHECK (tipo_lote IN ('LoteAveEngorde', 'LoteReproductoraAveEngorde')),
    CONSTRAINT ck_hlpe_tipo_registro CHECK (tipo_registro IN ('Inicio', 'Ajuste')),
    CONSTRAINT ck_hlpe_lote_ref CHECK (
        (tipo_lote = 'LoteAveEngorde' AND lote_ave_engorde_id IS NOT NULL AND lote_reproductora_ave_engorde_id IS NULL)
        OR (tipo_lote = 'LoteReproductoraAveEngorde' AND lote_ave_engorde_id IS NULL AND lote_reproductora_ave_engorde_id IS NOT NULL)
    ),
    CONSTRAINT ck_hlpe_aves_nonneg CHECK (
        aves_hembras >= 0 AND aves_machos >= 0 AND aves_mixtas >= 0
    )
);

CREATE INDEX IF NOT EXISTS ix_hlpe_company_id ON public.historial_lote_pollo_engorde (company_id);
CREATE INDEX IF NOT EXISTS ix_hlpe_tipo_lote ON public.historial_lote_pollo_engorde (tipo_lote);
CREATE INDEX IF NOT EXISTS ix_hlpe_lote_ave_engorde_id ON public.historial_lote_pollo_engorde (lote_ave_engorde_id) WHERE lote_ave_engorde_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_hlpe_lote_reproductora_id ON public.historial_lote_pollo_engorde (lote_reproductora_ave_engorde_id) WHERE lote_reproductora_ave_engorde_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_hlpe_fecha_registro ON public.historial_lote_pollo_engorde (fecha_registro);
CREATE INDEX IF NOT EXISTS ix_hlpe_movimiento_id ON public.historial_lote_pollo_engorde (movimiento_id) WHERE movimiento_id IS NOT NULL;

COMMENT ON TABLE public.historial_lote_pollo_engorde IS 'Historial de lotes pollo engorde: aves con que inicia cada lote (Lote Ave Engorde o Lote Reproductora) para reportes inicio/vendidas/actuales.';
COMMENT ON COLUMN public.historial_lote_pollo_engorde.tipo_lote IS 'LoteAveEngorde o LoteReproductoraAveEngorde.';
COMMENT ON COLUMN public.historial_lote_pollo_engorde.tipo_registro IS 'Inicio = registro al crear el lote; Ajuste = ajuste posterior.';
