-- =============================================================================
-- Usuario solo lectura para DWH / consultas (BI, reporting)
-- =============================================================================
-- Rol: "usrDWH" (entre comillas para conservar mayúsculas en PostgreSQL)
--
-- ANTES DE EJECUTAR:
-- 1) Conéctese como superusuario o rol con privilegio CREATEROLE (ej. admin RDS).
-- 2) Sustituya la contraseña en la variable pwd abajo (mínimo 10 caracteres,
--    mezcla de mayúsculas, minúsculas, números y símbolos).
-- 3) No commitee la contraseña en el repositorio.
--
-- Ejecución (psql):
--   \i create_user_dwh_readonly.sql
-- O pegue el bloque DO $$ ... $$ en pgAdmin / DBeaver.
-- =============================================================================

DO $$
DECLARE
  pwd text := '__CAMBIE_ESTA_CONTRASENA_10_CHARS__';  -- <<<<<<<<<< EDITAR
BEGIN
  IF length(pwd) < 10 OR pwd LIKE '%__CAMBIE%' THEN
    RAISE EXCEPTION 'Defina una contraseña segura de al menos 10 caracteres en la variable pwd.';
  END IF;

  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'usrDWH') THEN
    RAISE NOTICE 'El rol usrDWH ya existe; solo se actualizan contraseña y privilegios de lectura.';
    EXECUTE format('ALTER ROLE "usrDWH" WITH LOGIN PASSWORD %L', pwd);
  ELSE
    EXECUTE format(
      'CREATE ROLE "usrDWH" WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION CONNECTION LIMIT -1',
      pwd
    );
  END IF;

  EXECUTE format(
    'GRANT CONNECT ON DATABASE %I TO "usrDWH"',
    current_database()
  );

  EXECUTE 'GRANT USAGE ON SCHEMA public TO "usrDWH"';

  -- Tablas y vistas existentes
  EXECUTE 'GRANT SELECT ON ALL TABLES IN SCHEMA public TO "usrDWH"';
  EXECUTE 'GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO "usrDWH"';

  -- Objetos nuevos creados en public por el dueño actual (típico en migraciones)
  EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO "usrDWH"';
  EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO "usrDWH"';

  RAISE NOTICE 'Usuario "usrDWH" listo: solo lectura en schema public de la base %.', current_database();
END$$;

COMMENT ON ROLE "usrDWH" IS 'Solo consulta (DWH/reporting); sin escritura.';
