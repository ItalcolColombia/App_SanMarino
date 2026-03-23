-- Tipos de movimiento (movement_type) — módulo inventario gestión EC/PA.
-- Tabla: public.inventario_gestion_movimiento  (singular).
--
-- Valores usados en código (VARCHAR(30) en EF):
--   Ingreso, TrasladoSalida, TrasladoEntrada, Consumo,
--   TrasladoInterGranjaPendiente  (solicitud; no descuenta origen hasta recepción),
--   TrasladoInterGranjaSalida, TrasladoInterGranjaEntrada,
--   TrasladoInterGranjaRechazado
--
-- Si la columna es más corta que 30 caracteres:
ALTER TABLE public.inventario_gestion_movimiento
    ALTER COLUMN movement_type TYPE VARCHAR(40);
