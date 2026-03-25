-- Borra TODOS los registros de histórico de movimientos (Gestión de Inventario EC/PA).
-- No modifica inventario_gestion_stock ni item_inventario_ecuador.
-- Reinicia el contador SERIAL de id.
--
-- Ejecutar en PostgreSQL (psql, DBeaver, etc.):
--   psql -U ... -d ... -f inventario_gestion_movimiento_borrar_todo.sql

TRUNCATE TABLE public.inventario_gestion_movimiento RESTART IDENTITY;

-- Si TRUNCATE no está permitido por permisos, usar en su lugar:
-- DELETE FROM public.inventario_gestion_movimiento;
-- SELECT setval(pg_get_serial_sequence('public.inventario_gestion_movimiento', 'id'), 1, false);
