# -*- coding: utf-8 -*-
"""Envuelve el SQL de una migracion de la bitacora en una FUNCION, para poder alinear produccion
a mano cuando no se quiere esperar al despliegue.

Motivo: DbStudioSqlCalculos.ContainsMultipleStatements rechaza CUALQUIER ';' interno, asi que un
bloque DO no entra por la consola de DB Studio. Como funcion, el cuerpo se crea una sola vez (con
psql / pgAdmin / DBeaver) y despues 'SELECT * FROM fn()' ya es una sentencia sola y si entra.

Uso:  python generar_sql_prod.py [tareas|casos]"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))

CUAL = (sys.argv[1] if len(sys.argv) > 1 else "tareas").lower()

REPORTE_TAREAS = """
    RETURN QUERY
    SELECT 'tareas enriquecidas (horas + bitácora)'::text, count(*)
      FROM public.ticket_tareas WHERE codigo LIKE 'HIS-2026-%' AND horas_estimadas IS NOT NULL
    UNION ALL SELECT 'tareas nuevas de sesión (SES-*)'::text, count(*)
      FROM public.ticket_tareas WHERE codigo LIKE 'SES-2026%'
    UNION ALL SELECT 'subtareas BUG (una por commit fix)'::text, count(*)
      FROM public.ticket_tareas WHERE codigo LIKE 'BUG-%'
    UNION ALL SELECT 'historias con horas'::text, count(*)
      FROM public.historias WHERE horas_estimadas IS NOT NULL
    UNION ALL SELECT 'horas estimadas totales'::text,
           COALESCE(round(sum(horas_estimadas)), 0)::bigint FROM public.historias;
"""

REPORTE_CASOS = """
    RETURN QUERY
    SELECT 'casos creados por la bitácora'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%'
    UNION ALL SELECT 'de ellos, CERRADO'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%' AND estado = 'CERRADO'
    UNION ALL SELECT 'de ellos, EN_ANALISIS'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%' AND estado = 'EN_ANALISIS'
    UNION ALL SELECT 'con descripción de la solución'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%'
                            AND solucion_descripcion IS NOT NULL
    UNION ALL SELECT 'tareas/bugs enlazados a un caso'::text, count(*)
      FROM public.ticket_tareas WHERE ticket_id IS NOT NULL
    UNION ALL SELECT 'correos enviados (debe ser 0)'::text, count(*)
      FROM public.tickets WHERE descripcion LIKE '[Bitácora jul-ago 2026]%' AND notificado_correo;
"""

CFG = {
    "tareas": {
        "seed": "20260814010000_SeedBitacoraSesionesJulAgo2026.Seed.cs",
        "out": "bitacora_italjira_jul_ago_2026_prod.sql",
        "fn": "fn_bitacora_italjira_jul_ago_2026",
        "migracion": "20260814010000_SeedBitacoraSesionesJulAgo2026",
        "que": "las horas, el pedido, la solución y los bugs sobre las historias y tareas de ItalJira",
        "previo": ("--   SELECT (SELECT count(*) FROM public.historias WHERE codigo LIKE 'HIS-2026-%') AS historias,\n"
                   "--          (SELECT count(*) FROM public.ticket_tareas WHERE codigo LIKE 'HIS-2026-%') AS tareas_sembradas,\n"
                   "--          (SELECT count(*) FROM public.ticket_tareas WHERE codigo LIKE 'SES-2026%') AS ya_aplicado\n"
                   "--\n"
                   "--   Esperado ANTES de correr: 20 / 203 / 0."),
        "reporte": REPORTE_TAREAS,
    },
    "casos": {
        "seed": "20260814030000_SeedCasosCerradosBitacora.Seed.cs",
        "out": "casos_cerrados_bitacora_prod.sql",
        "fn": "fn_casos_cerrados_bitacora",
        "migracion": "20260814030000_SeedCasosCerradosBitacora",
        "que": "un CASO (ticket) CERRADO por cada trabajo, enlazado a su tarea de ItalJira",
        "previo": ("--   SELECT (SELECT count(*) FROM public.tickets) AS casos,\n"
                   "--          (SELECT count(*) FROM public.ticket_tareas WHERE codigo LIKE 'SES-2026%') AS tareas_bitacora,\n"
                   "--          (SELECT count(*) FROM public.tickets WHERE descripcion LIKE '[Bitácora%') AS ya_aplicado\n"
                   "--\n"
                   "--   tareas_bitacora debe ser 39: esta migración NECESITA la anterior aplicada."),
        "reporte": REPORTE_CASOS,
    },
}[CUAL]

SEED = os.path.join(REPO, r"backend\src\ZooSanMarino.Infrastructure\Migrations", CFG["seed"])
OUT = os.path.join(REPO, "backend", "sql", CFG["out"])

cs = open(SEED, encoding="utf-8").read()
sql = re.search(r'private const string SEED_SQL = @"(.*?)";\n', cs, re.S).group(1).replace('""', '"')

# El SQL de la migracion es: <comentario> DO $$ DECLARE ... BEGIN ... END $$;
i = sql.index("DO $$")
encabezado, cuerpo = sql[:i].rstrip(), sql[i + len("DO $$"):].rstrip()
assert cuerpo.endswith("END $$;"), cuerpo[-40:]
cuerpo = cuerpo[: -len("END $$;")].rstrip()

# En una funcion que devuelve tabla, un RETURN pelado deja el resultado vacio y no se entiende por
# que. Se convierte el fail-open en una fila que lo dice, y se saca el NOTICE de cierre.
cuerpo = re.sub(r"RAISE NOTICE '([^']*(?:omitid|no existe)[^']*)';\s*\n(\s*)RETURN;",
                lambda m: ("RETURN QUERY SELECT 'OMITIDO: %s'::text, 0::bigint;\n%sRETURN;"
                           % (m.group(1).replace("'", "''"), m.group(2))),
                cuerpo, flags=re.IGNORECASE)
cuerpo = re.sub(r"\n\s*RAISE NOTICE '[^']*sembrad[^']*';", "", cuerpo, flags=re.IGNORECASE)

CABECERA = """-- ═══════════════════════════════════════════════════════════════════════════════
-- Bitácora de julio y agosto 2026 — alineación MANUAL de producción
-- Contenido: {que}.
-- ═══════════════════════════════════════════════════════════════════════════════
-- Equivale, línea por línea, a la migración {migracion}.
-- Generado por fase_de_desarrollo/generadores/italjira_bitacora/generar_sql_prod.py — no editar
-- a mano: se cambia la migración y se regenera.
--
-- ¿POR QUÉ UNA FUNCIÓN? La consola de DB Studio rechaza cualquier ';' interno
-- (DbStudioSqlCalculos.ContainsMultipleStatements), así que un bloque DO no entra. El PASO 1 hay
-- que correrlo con psql / pgAdmin / DBeaver una sola vez; el PASO 2 sí entra por DB Studio.
--
-- ES IDEMPOTENTE y no choca con el despliegue: cuando la migración corra al arrancar la app va a
-- encontrar todo hecho y no va a tocar nada — EF solo la registra en __EFMigrationsHistory.
--
-- ───────────────────────────────────────────────────────────────────────────────
-- PASO 0 (opcional, una sentencia). Verificar el punto de partida:
--
{previo}
--
-- PASO 1 — crear la función (todo lo que sigue en este archivo).
-- PASO 2 — ejecutarla (una sentencia sola):
--
--   SELECT * FROM public.{fn}()
--
-- PASO 3 — opcional, soltarla cuando ya no haga falta:
--
--   DROP FUNCTION public.{fn}()
-- ───────────────────────────────────────────────────────────────────────────────
""".format(que=CFG["que"], migracion=CFG["migracion"], previo=CFG["previo"], fn=CFG["fn"])

salida = (CABECERA + "\n" + encabezado + """

CREATE OR REPLACE FUNCTION public.{fn}()
RETURNS TABLE (metrica text, valor bigint)
LANGUAGE plpgsql
AS $fn$""".format(fn=CFG["fn"]) + cuerpo + CFG["reporte"] + """
END
$fn$;
""")

os.makedirs(os.path.dirname(OUT), exist_ok=True)
open(OUT, "w", encoding="utf-8", newline="\n").write(salida)
print("escrito:", OUT, "|", len(salida), "bytes")
