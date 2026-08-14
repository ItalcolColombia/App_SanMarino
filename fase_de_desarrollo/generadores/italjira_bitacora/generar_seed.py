# -*- coding: utf-8 -*-
"""Genera el SQL de la migracion SeedBitacoraSesionesJulAgo2026 (partial .Seed.cs).
Entradas: items.json (sesiones cruzadas con git y con el seed anterior) y el archivo de horas
versionado en fase_de_desarrollo/. Salida: el .Seed.cs listo para compilar."""
import json, os, re, datetime as dt

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = r"C:\Users\SAN MARINO\Desktop\App_SanMarino"
SEED_ANT = os.path.join(REPO, r"backend\src\ZooSanMarino.Infrastructure\Migrations\20260807160000_SeedHistorialDesarrolloItalJira.Seed.cs")
HORAS = os.path.join(REPO, r"fase_de_desarrollo\italjira_bitacora_sesiones_jul_ago_2026_horas.json")
OUT = os.path.join(REPO, r"backend\src\ZooSanMarino.Infrastructure\Migrations\20260814010000_SeedBitacoraSesionesJulAgo2026.Seed.cs")

items = json.load(open(os.path.join(HERE, "items.json"), encoding="utf-8"))
horas = json.load(open(HORAS, encoding="utf-8"))
H_EXIST, H_NUEVA = horas["tareas_existentes"], horas["tareas_nuevas"]

# descripcion ORIGINAL de cada tarea sembrada (para el guard exacto y para el Down)
sql_ant = open(SEED_ANT, encoding="utf-8").read()
DESC_ORIG, ETIQ = {}, {}
for m in re.finditer(
    r"SELECT NULL, v_hist, '(HIS-2026-\d{4}-T\d+)', '\w+', '\w+', '\w+',\s*\n\s*'(?:[^']|'')*', '((?:[^']|'')*)',[\s\S]{0,320}?'((?:[^']|'')*)', v_company", sql_ant):
    DESC_ORIG[m.group(1)] = m.group(2)     # tal cual esta en la BD, pero con '' escapado
    ETIQ[m.group(1)] = m.group(3)
ETIQ_HIST = {}
for m in re.finditer(r"SELECT '(HIS-2026-\d{4})',[\s\S]{0,420}?'((?:[^']|'')*)', v_company", sql_ant):
    ETIQ_HIST.setdefault(m.group(1), m.group(2))


def q(s):
    """Literal SQL: comilla simple duplicada. El @-string de C# se resuelve aparte."""
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


def fecha(iso):
    return iso[:10]


def tstz(iso):
    d = dt.datetime.fromisoformat(iso).astimezone(dt.timezone.utc)
    return d.strftime("%Y-%m-%d %H:%M:%S+00")


def bloque_bitacora(it, est, extra_pedido=None):
    """El texto que ve el usuario en la tarjeta: pedido, solución, bugs y evidencia."""
    ini = fecha(it["ini"])
    fin = fecha(it["fin"])
    rango = ini if ini == fin else "%s → %s" % (ini, fin)
    L = ["── Bitácora de la sesión (%s) ──" % rango]
    pedido = limpiar(extra_pedido or (it["prompts"][0] if it["prompts"] else ""), 700)
    if pedido:
        L.append("Pedido: «%s»" % pedido)
    if it["commits"]:
        n = len(it["commits"])
        L.append("Solución (%d %s): %s" % (
            n, "commit" if n == 1 else "commits",
            "; ".join(limpiar(c["subject"], 130) for c in it["commits"][:8])))
    else:
        L.append("Solución: el cambio quedó en el repositorio dentro del trabajo de la misma línea "
                 "(sin commit propio atribuible a esta sesión).")
    if it["bugs"]:
        L.append("Bugs encontrados: %d — cada uno queda como subtarea BUG con su causa." % len(it["bugs"]))
    else:
        L.append("Bugs encontrados: 0.")
    ev = []
    if it["archivos"]:
        ev.append("%d archivos tocados" % it["archivos"])
    if it["horas_reales"]:
        ev.append(("%.1f h de sesión real" % it["horas_reales"]).replace(".", ","))
    shas = [c["sha"] for c in (it["commits"] + it["bugs"])][:10]
    if shas:
        ev.append("commits " + ", ".join(shas))
    ev.append("sesión %s" % ", ".join(s[:8] for s in it["sesiones"]))
    L.append("Evidencia: " + " · ".join(ev))
    L.append("Estimación: %s h (por juicio — fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json)"
             % ("%g" % est).replace(".", ","))
    return "\n".join(L)


ins_tareas, ins_bugs, upd, down_upd = [], [], [], []
usados_codigo = set()
resumen = {"enriquecidas": 0, "nuevas": 0, "bugs": 0, "horas": 0.0}

for it in items:
    es_nueva = it["key"].startswith("N:")
    sid8 = it["key"][2:10]
    cfg = H_NUEVA.get(sid8) if es_nueva else None
    if es_nueva and cfg is None:
        continue                                  # sesión trivial: no entra a la bitácora
    codigo_tarea = None
    # El título "de verdad": el de la tarea sembrada, o el que se le escribió a mano a la tarea
    # nueva (el crudo del item es el prompt recortado y no sirve para citar).
    titulo_efectivo = cfg["titulo"] if es_nueva else it["titulo"]

    if es_nueva:
        est = float(cfg["h"])
        hist = "HIS-2026-" + cfg["historia"]
        tipo = cfg["tipo"]
        estado = cfg.get("estado") or ("LISTO" if (it["commits"] or it["bugs"] or it["archivos"] >= 3) else "ANALISIS")
        titulo = cfg["titulo"]
        base = "SES-%s-%s" % (fecha(it["ini"])[:4] + fecha(it["ini"])[5:7] + fecha(it["ini"])[8:10], sid8[:4])
        codigo_tarea = base[:40]
        assert codigo_tarea not in usados_codigo, codigo_tarea
        usados_codigo.add(codigo_tarea)
        etiquetas = limpiar((ETIQ_HIST.get(hist, "").replace("''", "'") + ",bitacora"), 300)
        desc = bloque_bitacora(it, est)
        prioridad = "ALTA" if tipo == "BUG" else "MEDIA"
        ins_tareas.append("""    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, orden, fecha_inicio_plan, fecha_fin_plan,
        fecha_inicio_real, fecha_fin_real, horas_estimadas, etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, h.id, '{cod}', '{tipo}', '{estado}', '{prio}',
           '{titulo}', '{desc}', v_user_guid,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x
             WHERE x.historia_id = h.id AND x.estado = '{estado}'),
           DATE '{ini}', DATE '{fin}', TIMESTAMPTZ '{tini}', {tfin}, {horas}, '{etiq}',
           v_company, v_cedula, TIMESTAMPTZ '{tini}'
    FROM public.historias h
    WHERE h.codigo = '{hist}'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = '{cod}');""".format(
            cod=codigo_tarea, tipo=tipo, estado=estado, prio=prioridad,
            titulo=q(limpiar(titulo, 200)), desc=q(desc), ini=fecha(it["ini"]), fin=fecha(it["fin"]),
            tini=tstz(it["ini"]), tfin=("TIMESTAMPTZ '%s'" % tstz(it["fin"])) if estado == "LISTO" else "NULL",
            horas=num(est), etiq=q(etiquetas), hist=hist))
        resumen["nuevas"] += 1
    else:
        cod = it["key"][2:]
        if cod not in H_EXIST or cod not in DESC_ORIG:
            continue
        est = float(H_EXIST[cod])
        codigo_tarea = cod
        desc = DESC_ORIG[cod].replace("''", "'") + "\n\n" + bloque_bitacora(it, est)
        upd.append("""    UPDATE public.ticket_tareas
       SET horas_estimadas = {horas}, descripcion = '{desc}',
           updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
     WHERE codigo = '{cod}' AND horas_estimadas IS NULL AND descripcion = '{orig}';""".format(
            horas=num(est), desc=q(desc), cod=cod, orig=DESC_ORIG[cod]))
        down_upd.append("""    UPDATE public.ticket_tareas
       SET horas_estimadas = NULL, descripcion = '{orig}', updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo = '{cod}';""".format(orig=DESC_ORIG[cod], cod=cod))
        resumen["enriquecidas"] += 1

    resumen["horas"] += est

    for c in it["bugs"]:
        cod_bug = ("BUG-%s" % c["sha"])[:40]
        if cod_bug in usados_codigo:
            continue
        usados_codigo.add(cod_bug)
        cuerpo = limpiar(c["body"], 600)
        d_bug = "Bug detectado y corregido durante «%s».\nCommit %s (%s).%s" % (
            limpiar(titulo_efectivo, 120), c["sha"], c["fecha"],
            ("\nCausa/detalle registrado en el commit: " + cuerpo) if cuerpo else "")
        ins_bugs.append("""    INSERT INTO public.ticket_tareas (ticket_id, historia_id, codigo, tipo, estado, prioridad,
        titulo, descripcion, asignado_user_guid, parent_tarea_id, orden,
        fecha_inicio_plan, fecha_fin_plan, fecha_inicio_real, fecha_fin_real,
        etiquetas, company_id, created_by_user_id, created_at)
    SELECT NULL, p.historia_id, '{cod}', 'BUG', 'LISTO', 'ALTA',
           '{titulo}', '{desc}', v_user_guid, p.id,
           (SELECT COALESCE(MAX(x.orden) + 1, 0) FROM public.ticket_tareas x WHERE x.parent_tarea_id = p.id),
           DATE '{f}', DATE '{f}', TIMESTAMPTZ '{f} 12:00:00+00', TIMESTAMPTZ '{f} 18:00:00+00',
           'bitacora,bug', v_company, v_cedula, TIMESTAMPTZ '{f} 12:00:00+00'
    FROM public.ticket_tareas p
    WHERE p.codigo = '{padre}'
      AND NOT EXISTS (SELECT 1 FROM public.ticket_tareas t WHERE t.codigo = '{cod}');""".format(
            cod=cod_bug, titulo=q(limpiar(c["subject"], 200)), desc=q(d_bug),
            f=c["fecha"], padre=codigo_tarea))
        resumen["bugs"] += 1

CAB = """-- ─────────────────────────────────────────────────────────────────────────────
-- Bitácora REAL de julio y agosto 2026 en ItalJira.
-- Fuente: las 134 sesiones de trabajo del período (pedido textual del usuario, fechas y
-- duración medidas) cruzadas con los 447 commits del repositorio. Los bugs son los commits
-- fix(...) de cada ventana. La ÚNICA cifra estimada es horas_estimadas, asignada por juicio
-- (rúbrica y valor por ítem en fase_de_desarrollo/italjira_bitacora_sesiones_jul_ago_2026_horas.json).
-- ─────────────────────────────────────────────────────────────────────────────
DO $$
DECLARE
    v_user_guid uuid;
    v_cedula    integer;
    v_company   integer;
    v_pais      integer;
BEGIN
    -- Identidad POR EMAIL, nunca por guid fijo: los ids difieren entre local y producción.
    SELECT u.id INTO v_user_guid
    FROM public.users u
    JOIN public.user_logins ul ON ul.user_id = u.id
    JOIN public.logins l       ON l.id = ul.login_id
    WHERE lower(l.email) = 'moiesbbuga@gmail.com'
    LIMIT 1;

    -- Fail-open silencioso: sin el usuario no se siembra nada y la app arranca igual.
    IF v_user_guid IS NULL THEN
        RAISE NOTICE 'ItalJira bitácora: no existe moiesbbuga@gmail.com en este entorno; omitida.';
        RETURN;
    END IF;

    -- El int de auditoría del módulo NO es la cédula (3177120174 no entra en integer).
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

    -- ═══ 1) Enriquecer las tareas ya sembradas (horas + pedido + solución + evidencia) ═══
    -- El guard exige que la descripción siga siendo EXACTAMENTE la que escribió el seed
    -- anterior: si alguien la editó a mano, esta migración no la pisa.
"""

PIE_1 = """
    -- ═══ 2) Tareas nuevas: sesiones de trabajo que no tenían tarea sembrada ═══
"""
PIE_2 = """
    -- ═══ 3) Bugs encontrados: un commit fix(...) = una subtarea BUG de su tarea ═══
"""
PIE_3 = """
    -- ═══ 4) La historia agrega las horas de sus tareas (evita el doble conteo) ═══
    UPDATE public.historias h
       SET horas_estimadas = s.total, updated_by_user_id = v_cedula, updated_at = timezone('utc', now())
      FROM (SELECT t.historia_id, SUM(t.horas_estimadas) AS total
              FROM public.ticket_tareas t
             WHERE t.historia_id IS NOT NULL AND t.deleted_at IS NULL
             GROUP BY t.historia_id) s
     WHERE h.id = s.historia_id
       AND h.codigo ~ '^HIS-2026-[0-9]{4}$'
       AND h.horas_estimadas IS DISTINCT FROM s.total;

    RAISE NOTICE 'ItalJira bitácora jul-ago 2026: sembrada.';
END $$;
"""

sql = CAB + "\n".join(upd) + PIE_1 + "\n".join(ins_tareas) + PIE_2 + "\n".join(ins_bugs) + PIE_3

DOWN = """-- Revierte SOLO lo de esta migración.
DO $$
BEGIN
    DELETE FROM public.ticket_tareas WHERE codigo ~ '^BUG-[0-9a-f]{7}$';
    DELETE FROM public.ticket_tareas WHERE codigo ~ '^SES-2026[0-9]{4}-';
""" + "\n".join(down_upd) + """
    UPDATE public.historias SET horas_estimadas = NULL, updated_by_user_id = NULL, updated_at = NULL
     WHERE codigo ~ '^HIS-2026-[0-9]{4}$';
END $$;
"""

def cs(s):
    return s.replace('"', '""')

with open(OUT, "w", encoding="utf-8") as fh:
    fh.write('''using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuerpo del seed de la bitácora de julio-agosto 2026. Vive en su propio archivo (partial)
    /// porque es SQL GENERADO: la documentación de qué hace y por qué está en la migración.
    /// </summary>
    /// <remarks>
    /// No editar a mano: se regenera con los scripts del scratchpad (extraer_sesiones.py →
    /// cruzar.py → armar_items.py → generar_seed.py) a partir de las transcripciones de sesión,
    /// del historial de git y del archivo de horas versionado en <c>fase_de_desarrollo/</c>.
    /// </remarks>
    public partial class SeedBitacoraSesionesJulAgo2026
    {
        private const string SEED_SQL = @"''' + cs(sql) + '''";

        private const string DOWN_SQL = @"''' + cs(DOWN) + '''";
    }
}
''')

print("enriquecidas:", resumen["enriquecidas"], "| nuevas:", resumen["nuevas"],
      "| bugs:", resumen["bugs"], "| horas estimadas:", resumen["horas"])
print("bytes SQL:", len(sql), "->", OUT)
