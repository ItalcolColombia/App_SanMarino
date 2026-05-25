-- Agrega 6 columnas de traslado de aves a produccion_seguimiento
-- Requerimiento R3: Traslado de Aves desde Seguimiento Diario (Producción)

ALTER TABLE produccion_seguimiento
    ADD COLUMN IF NOT EXISTS traslado_hembras         INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS traslado_machos          INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS lote_destino_id          INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS granja_destino_id        INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS fecha_traslado           DATE          NULL,
    ADD COLUMN IF NOT EXISTS traslado_observaciones   VARCHAR(500)  NULL;
