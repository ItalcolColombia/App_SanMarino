-- Saldo de alimento (kg) al cierre del día, calculado desde lote_registro_historico_unificado.
-- Ejecutar si no aplica migración EF. PostgreSQL.

ALTER TABLE public.seguimiento_diario_aves_engorde
    ADD COLUMN IF NOT EXISTS saldo_alimento_kg NUMERIC(18, 3) NULL;

COMMENT ON COLUMN public.seguimiento_diario_aves_engorde.saldo_alimento_kg IS
    'Saldo alimento (kg) al cierre del día: ingresos + traslados entrada − traslados salida − consumos (INV_*).';
