# -*- coding: utf-8 -*-
"""Genera el SQL de la migracion SeedCasosCerradosBitacora: un CASO (ticket) por cada trabajo de
la bitacora de julio-agosto, en estado CERRADO y enlazado a su tarea de ItalJira.

Reusa items.json y el archivo de horas: los mismos 137 trabajos, la misma fuente. Correr despues
de armar_items.py."""
import json, os, re, datetime as dt

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
HORAS = os.path.join(REPO, r"fase_de_desarrollo\italjira_bitacora_sesiones_jul_ago_2026_horas.json")
OUT = os.path.join(REPO, r"backend\src\ZooSanMarino.Infrastructure\Migrations\20260814030000_SeedCasosCerradosBitacora.Seed.cs")

items = json.load(open(os.path.join(HERE, "items.json"), encoding="utf-8"))
horas = json.load(open(HORAS, encoding="utf-8"))
H_EXIST, H_NUEVA = horas["tareas_existentes"], horas["tareas_nuevas"]

MARCA = "[Bitácora jul-ago 2026]"


def q(s):
    return (s or "").replace("'", "''")


def limpiar(txt, maxlen):
    txt = (txt or "").replace("\r", " ").replace("\n", " ").replace("\t", " ")
    txt = re.sub(r'@"[^"]*"', "", txt)
    txt = re.sub(r"https?://\S+", "", txt)
    txt = re.sub(r"[\x00-\x1f]", " ", txt)
    txt = re.sub(r"\s+", " ", txt).strip(" -·|")
    if len(txt) > maxlen:
        txt = txt[:maxlen].rsplit(" ", 1)[0] + "…"
    return txt.strip()


def num(x):
    return ("%.2f" % x).replace(",", ".")


def tstz(iso):
    return dt.datetime.fromisoformat(iso).astimezone(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S+00")


bloques = []
n_cerrados = n_abiertos = 0
horas_total = 0.0

for it in items:
    es_nueva = it["key"].startswith("N:")
    sid8 = it["key"][2:10]
    cfg = H_NUEVA.get(sid8) if es_nueva else None
    if es_nueva and cfg is None:
        continue

    if es_nueva:
        est = float(cfg["h"])
        titulo = cfg["titulo"]
        prioridad = "ALTA" if cfg["tipo"] == "BUG" else "MEDIA"
        estado_tarea = cfg.get("estado") or ("LISTO" if (it["commits"] or it["bugs"] or it["archivos"] >= 3) else "ANALISIS")
        f = it["ini"][:10]
        cod_tarea = "SES-%s-%s" % (f[:4] + f[5:7] + f[8:10], sid8[:4])
    else:
        cod = it["key"][2:]
        if cod not in H_EXIST:
            continue
        est = float(H_EXIST[cod])
        titulo = it["titulo"]
        prioridad = "ALTA" if it["tipo"] == "BUG" else "MEDIA"
        estado_tarea = "LISTO"          # el seed del 07ago las dejó todas terminadas
        cod_tarea = cod

    # Un caso solo se da por CERRADO si su tarea está terminada. Las 5 sesiones que quedaron en
    # análisis entran como EN_ANALISIS y SIN fecha de solución: cerrar lo que no se cerró sería
    # exactamente la clase de dato falso que esta bitácora trata de evitar.
    cerrado = estado_tarea == "LISTO"
    estado = "CERRADO" if cerrado else "EN_ANALISIS"
    if cerrado:
        n_cerrados += 1
    else:
        n_abiertos += 1
    horas_total += est

    desc = ["%s · tarea %s" % (MARCA, cod_tarea)]
    pedido = limpiar(it["prompts"][0] if it["prompts"] else "", 900)
    if pedido:
        desc.append("Pedido del usuario: «%s»" % pedido)
    desc.append("Registrado desde el trabajo real del área de desarrollo (sesión %s, %s)."
                % (", ".join(s[:8] for s in it["sesiones"]), it["ini"][:10]))

    sol = []
    if it["commits"]:
        n = len(it["commits"])
        sol.append("Qué se hizo (%d %s): %s" % (
            n, "commit" if n == 1 else "commits",
            "; ".join(limpiar(c["subject"], 130) for c in it["commits"][:8])))
    else:
        sol.append("Qué se hizo: el cambio quedó en el repositorio dentro del trabajo de la misma "
                   "línea (sin commit propio atribuible a esta sesión).")
    if it["bugs"]:
        sol.append("Bugs encontrados en el camino: %d — cada uno queda como subtarea BUG de la tarea %s."
                   % (len(it["bugs"]), cod_tarea))
    else:
        sol.append("Bugs encontrados en el camino: 0.")
    ev = []
    if it["archivos"]:
        ev.append("%d archivos tocados" % it["archivos"])
    if it["horas_reales"]:
        ev.append(("%.1f h de sesión real" % it["horas_reales"]).replace(".", ","))
    shas = [c["sha"] for c in (it["commits"] + it["bugs"])][:10]
    if shas:
        ev.append("commits " + ", ".join(shas))
    sol.append("Evidencia: " + " · ".join(ev))
    sol.append("Estimación: %s h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)"
               % ("%g" % est).replace(".", ","))
    if not cerrado:
        sol.append("Estado real: quedó en análisis, no se cerró.")

    bloques.append("""
    -- ── {cod} ──────────────────────────────────────────────────────────────────
    v_ticket := NULL;
    INSERT INTO public.tickets (codigo, pais_id, tipo, estado, titulo, descripcion,
        assigned_to_user_guid, created_by_user_guid, fecha_primera_apertura, fecha_solucion,
        solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo,
        prioridad, orden_tablero, horas_estimadas, fecha_inicio_plan, fecha_fin_plan,
        historia_id, status, company_id, created_by_user_id, created_at)
    SELECT 'TK-2026-' || lpad(v_next::text, 6, '0'), v_pais, 'DESARROLLO', '{estado}',
           '{titulo}', '{desc}',
           v_user_guid, v_user_guid, TIMESTAMPTZ '{ini}', {fsol},
           '{sol}', {fcierre}, {cerrado_por}, false,
           '{prio}', v_orden, {horas}, DATE '{fini}', DATE '{ffin}',
           t.historia_id, 'A', v_company, v_cedula, TIMESTAMPTZ '{ini}'
    FROM public.ticket_tareas t
    WHERE t.codigo = '{cod}' AND t.ticket_id IS NULL AND t.deleted_at IS NULL
    RETURNING id INTO v_ticket;

    IF v_ticket IS NOT NULL THEN
        -- La tarea y sus bugs pasan a colgar del caso. Se hace SIEMPRE junto al INSERT: es lo que
        -- vuelve idempotente al bloque (en la 2ª pasada la tarea ya tiene ticket_id y no entra).
        UPDATE public.ticket_tareas x
           SET ticket_id = v_ticket
         WHERE x.deleted_at IS NULL
           AND (x.codigo = '{cod}'
                OR x.parent_tarea_id = (SELECT id FROM public.ticket_tareas WHERE codigo = '{cod}'));
        v_next  := v_next + 1;
        v_orden := v_orden + 1;
    END IF;""".format(
        cod=cod_tarea, estado=estado, titulo=q(limpiar(titulo, 200)),
        desc=q("\n".join(desc)), sol=q("\n".join(sol)),
        ini=tstz(it["ini"]),
        fsol=("TIMESTAMPTZ '%s'" % tstz(it["fin"])) if cerrado else "NULL",
        fcierre=("TIMESTAMPTZ '%s'" % tstz(it["fin"])) if cerrado else "NULL",
        cerrado_por="v_cedula" if cerrado else "NULL",
        prio=prioridad, horas=num(est), fini=it["ini"][:10], ffin=it["fin"][:10]))

CAB = """-- ─────────────────────────────────────────────────────────────────────────────
-- Un CASO (ticket) por cada trabajo de la bitácora de julio-agosto 2026.
-- Mismo origen que 20260814010000 (sesiones reales + commits): esto no agrega información
-- nueva, la publica en el módulo de Tickets, que es donde el usuario espera ver el trabajo
-- solucionado y cerrado. Cada caso queda ENLAZADO a su tarea de ItalJira (ticket_tareas.ticket_id).
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_user_guid uuid;
    v_cedula    integer;
    v_company   integer;
    v_pais      integer;
    v_ticket    bigint;
    v_next      integer;
    v_orden     integer;
BEGIN
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'Casos de la bitácora: no existe moiesbbuga@gmail.com; omitido.';
        RETURN;
    END IF;

    SELECT t.created_by_user_id INTO v_cedula
    FROM public.tickets t WHERE t.created_by_user_guid = v_user_guid ORDER BY t.id DESC LIMIT 1;
    IF v_cedula IS NULL THEN
        SELECT CASE WHEN u.cedula ~ '^[0-9]{1,9}$' THEN u.cedula::integer ELSE 0 END
          INTO v_cedula FROM public.users u WHERE u.id = v_user_guid;
    END IF;
    v_cedula := COALESCE(v_cedula, 0);

    SELECT t.company_id, t.pais_id INTO v_company, v_pais
    FROM public.tickets t ORDER BY t.id DESC LIMIT 1;
    IF v_company IS NULL THEN
        SELECT c.id INTO v_company FROM public.companies c ORDER BY c.id LIMIT 1;
    END IF;
    v_company := COALESCE(v_company, 1);
    v_pais    := COALESCE(v_pais, 1);

    -- El correlativo arranca donde quedó el de la base: local y producción NO están en el mismo
    -- número, así que jamás se puede hardcodear.
    SELECT COALESCE(MAX(NULLIF(regexp_replace(codigo, '^TK-[0-9]{4}-', ''), '')::integer), 0) + 1
      INTO v_next
    FROM public.tickets
    WHERE codigo ~ '^TK-[0-9]{4}-[0-9]+$';

    SELECT COALESCE(MAX(orden_tablero) + 1, 0) INTO v_orden
    FROM public.tickets WHERE estado = 'CERRADO' AND deleted_at IS NULL;
"""

PIE = """

    RAISE NOTICE 'Casos de la bitácora jul-ago 2026: sembrados.';
END $$;
"""

DOWN = """-- Revierte SOLO los casos de esta migración, identificados por la marca de la descripción.
-- ⚠️ El DESENLACE VA PRIMERO: fk_ticket_tareas_tickets_ticket_id es ON DELETE CASCADE, así que
-- borrar los casos con las tareas todavía enlazadas se llevaría por delante las 137 tareas de
-- ItalJira y sus 99 subtareas BUG.
DO $$
BEGIN
    UPDATE public.ticket_tareas SET ticket_id = NULL
     WHERE ticket_id IN (SELECT id FROM public.tickets WHERE descripcion LIKE '{marca}%');

    DELETE FROM public.tickets WHERE descripcion LIKE '{marca}%';
END $$;
""".format(marca=MARCA)

sql = CAB + "\n".join(bloques) + PIE


def cs(s):
    return s.replace('"', '""')


open(OUT, "w", encoding="utf-8").write('''using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo del seed de casos de la bitácora. SQL GENERADO: ver el <c>remarks</c> de la
    /// migración y <c>fase_de_desarrollo/generadores/italjira_bitacora/generar_seed_casos.py</c>.
    /// </summary>
    public partial class SeedCasosCerradosBitacora
    {
        private const string SEED_SQL = @"''' + cs(sql) + '''";

        private const string DOWN_SQL = @"''' + cs(DOWN) + '''";
    }
}
''')

print("casos CERRADO:", n_cerrados, "| casos EN_ANALISIS:", n_abiertos,
      "| horas:", horas_total, "| bytes:", len(sql))
print("->", OUT)
