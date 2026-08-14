# -*- coding: utf-8 -*-
"""Envuelve el SQL de la migracion en una FUNCION para poder alinear produccion a mano.

Motivo: DbStudioSqlCalculos.ContainsMultipleStatements rechaza cualquier ';' interno, asi que
un bloque DO no entra por la consola. Como funcion, el cuerpo se crea una sola vez (con psql o
pgAdmin) y despues 'SELECT fn()' si pasa el guard de una sentencia.

Salida: backend/sql/bitacora_italjira_jul_ago_2026_prod.sql"""
import os, re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SEED = os.path.join(REPO, r"backend\src\ZooSanMarino.Infrastructure\Migrations\20260814010000_SeedBitacoraSesionesJulAgo2026.Seed.cs")
OUT = os.path.join(REPO, r"backend\sql\bitacora_italjira_jul_ago_2026_prod.sql")

cs = open(SEED, encoding="utf-8").read()
m = re.search(r'private const string SEED_SQL = @"(.*?)";\n', cs, re.S)
sql = m.group(1).replace('""', '"')

# El SQL de la migracion es: <comentario> DO $$ DECLARE ... BEGIN ... END $$;
i = sql.index("DO $$")
encabezado = sql[:i].rstrip()
cuerpo = sql[i + len("DO $$"):]
cuerpo = cuerpo.rstrip()
assert cuerpo.endswith("END $$;"), cuerpo[-40:]
cuerpo = cuerpo[: -len("END $$;")].rstrip()

# Fail-open: en una funcion que devuelve tabla, el RETURN pelado deja el resultado vacio y no
# se entiende. Se devuelve una fila que dice por que no hizo nada.
cuerpo = cuerpo.replace(
    "        RAISE NOTICE 'ItalJira bitácora: no existe moiesbbuga@gmail.com en este entorno; omitida.';\n        RETURN;",
    "        RETURN QUERY SELECT 'OMITIDO: no existe moiesbbuga@gmail.com en este entorno'::text, 0::bigint;\n        RETURN;")
cuerpo = cuerpo.replace("    RAISE NOTICE 'ItalJira bitácora jul-ago 2026: sembrada.';", "")

REPORTE = """
    -- Reporte de cierre: lo que quedó en la base después de correr todo.
    RETURN QUERY
    SELECT 'tareas enriquecidas (horas + bitácora)'::text,
           count(*) FROM public.ticket_tareas
     WHERE codigo LIKE 'HIS-2026-%' AND horas_estimadas IS NOT NULL
    UNION ALL
    SELECT 'tareas nuevas de sesión (SES-*)'::text,
           count(*) FROM public.ticket_tareas WHERE codigo LIKE 'SES-2026%'
    UNION ALL
    SELECT 'subtareas BUG (una por commit fix)'::text,
           count(*) FROM public.ticket_tareas WHERE codigo LIKE 'BUG-%'
    UNION ALL
    SELECT 'historias con horas'::text,
           count(*) FROM public.historias WHERE horas_estimadas IS NOT NULL
    UNION ALL
    SELECT 'horas estimadas totales'::text,
           COALESCE(round(sum(horas_estimadas)), 0)::bigint FROM public.historias;
"""

CABECERA = """-- ═══════════════════════════════════════════════════════════════════════════════
-- Bitácora ItalJira de julio y agosto 2026 — alineación MANUAL de producción
-- ═══════════════════════════════════════════════════════════════════════════════
-- Equivale, línea por línea, a la migración 20260814010000_SeedBitacoraSesionesJulAgo2026.
-- Generado por fase_de_desarrollo/generadores/italjira_bitacora/generar_sql_prod.py — no editar
-- a mano: si hay que cambiar algo, se cambia la migración y se regenera este archivo.
--
-- ¿POR QUÉ UNA FUNCIÓN? La consola de DB Studio rechaza cualquier ';' interno
-- (DbStudioSqlCalculos.ContainsMultipleStatements), así que un bloque DO no entra. El PASO 1
-- (crear la función) hay que correrlo con psql / pgAdmin / DBeaver una sola vez; el PASO 2 es
-- una sentencia sola y esa sí entra por DB Studio.
--
-- ES IDEMPOTENTE: correrlo dos veces no cambia una sola fila la segunda vez. Y no choca con el
-- despliegue: cuando la migración corra al arrancar la app, encontrará todo hecho y no tocará
-- nada — EF solo registrará la migración en __EFMigrationsHistory.
--
-- NO PISA TRABAJO HUMANO: el UPDATE exige que la descripción siga siendo exactamente la que
-- escribió el seed del 07ago. Cualquier tarjeta editada a mano queda intacta.
--
-- ───────────────────────────────────────────────────────────────────────────────
-- PASO 0 (opcional, una sentencia — sirve para DB Studio). Verificar el punto de partida:
--
--   SELECT (SELECT count(*) FROM public.historias WHERE codigo LIKE 'HIS-2026-%') AS historias,
--          (SELECT count(*) FROM public.ticket_tareas WHERE codigo LIKE 'HIS-2026-%') AS tareas_sembradas,
--          (SELECT count(*) FROM public.ticket_tareas WHERE codigo LIKE 'SES-2026%') AS ya_aplicado
--
--   Esperado ANTES de correr: historias = 20, tareas_sembradas = 203, ya_aplicado = 0.
--   Si tareas_sembradas = 0, falta el seed del 07ago y este script solo creará las 39 tareas
--   nuevas: hay que desplegar antes.
--
-- PASO 1 — crear la función (psql / pgAdmin / DBeaver; es todo lo que sigue en este archivo).
-- PASO 2 — ejecutarla (una sentencia sola; entra por DB Studio):
--
--   SELECT * FROM public.fn_bitacora_italjira_jul_ago_2026()
--
-- PASO 3 — opcional, soltar la función cuando ya no haga falta:
--
--   DROP FUNCTION public.fn_bitacora_italjira_jul_ago_2026()
-- ───────────────────────────────────────────────────────────────────────────────
"""

salida = (CABECERA + "\n" + encabezado + """

CREATE OR REPLACE FUNCTION public.fn_bitacora_italjira_jul_ago_2026()
RETURNS TABLE (metrica text, valor bigint)
LANGUAGE plpgsql
AS $fn$""" + cuerpo + REPORTE + """
END
$fn$;
""")

os.makedirs(os.path.dirname(OUT), exist_ok=True)
open(OUT, "w", encoding="utf-8", newline="\n").write(salida)
print("escrito:", OUT, "|", len(salida), "bytes")
