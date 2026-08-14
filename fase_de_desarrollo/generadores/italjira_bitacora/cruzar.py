# -*- coding: utf-8 -*-
"""Cruza: (a) las historias/tareas YA sembradas por 20260807160000_SeedHistorialDesarrolloItalJira,
(b) las sesiones de jul-ago 2026, (c) los commits reales de git en esa ventana.
Salida: cruce.json + resumen por consola."""
import json, os, re, subprocess, sys, datetime as dt

REPO = r"C:\Users\SAN MARINO\Desktop\App_SanMarino"
HERE = os.path.dirname(os.path.abspath(__file__))
SEED = os.path.join(REPO, r"backend\src\ZooSanMarino.Infrastructure\Migrations\20260807160000_SeedHistorialDesarrolloItalJira.Seed.cs")

# ── (a) historias y tareas ya sembradas ──────────────────────────────────────
sql = open(SEED, encoding="utf-8").read()

historias = []
for m in re.finditer(r"SELECT '(HIS-2026-\d{4})', v_pais, '((?:[^']|'')*)', '((?:[^']|'')*)', '(\w+)', '(\w+)',\s*\n\s*v_user_guid, (\d+), DATE '([\d-]+)', DATE '([\d-]+)',[\s\S]{0,200}?'((?:[^']|'')*)', v_company", sql):
    historias.append({"codigo": m.group(1), "titulo": m.group(2).replace("''", "'"),
                      "orden": int(m.group(6)), "ini": m.group(7), "fin": m.group(8),
                      "etiquetas": m.group(9)})

tareas = []
for m in re.finditer(
    r"SELECT NULL, v_hist, '(HIS-2026-(\d{4})-T\d+)', '(\w+)', '(\w+)', '(\w+)',\s*\n\s*'((?:[^']|'')*)', '((?:[^']|'')*)', v_user_guid, (\d+), DATE '([\d-]+)', DATE '([\d-]+)'", sql):
    desc = m.group(7).replace("''", "'")
    plan = None
    pm = re.search(r"fase_de_desarrollo/([^\s'\"]+\.md)", desc)
    if pm:
        plan = pm.group(1)
    tareas.append({"codigo": m.group(1), "historia": "HIS-2026-" + m.group(2), "tipo": m.group(3),
                   "titulo": m.group(6).replace("''", "'"), "plan": plan,
                   "ini": m.group(9), "fin": m.group(10)})

# ── (b) commits jul-ago con archivos ─────────────────────────────────────────
raw = subprocess.run(["git", "log", "--since=2026-07-01", "--until=2026-08-15",
                      "--date=iso-strict", "--name-only",
                      "--pretty=format:@@@%H|%ad|%s|%b###"],
                     cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
commits = []
for chunk in raw.split("@@@"):
    if not chunk.strip():
        continue
    head, _, rest = chunk.partition("###")
    parts = head.split("|", 3)
    if len(parts) < 3:
        continue
    sha, fecha, subject = parts[0], parts[1], parts[2]
    body = parts[3] if len(parts) > 3 else ""
    files = [f.strip().replace("\\", "/") for f in rest.strip().splitlines() if f.strip()]
    commits.append({"sha": sha[:7], "fecha": fecha, "subject": subject.strip(),
                    "body": body.strip(), "files": files})
commits.sort(key=lambda c: c["fecha"])

# ── (c) sesiones ─────────────────────────────────────────────────────────────
ses = [s for s in json.load(open(os.path.join(HERE, "sesiones.json"), encoding="utf-8"))
       if s["inicio"] >= "2026-07-01"]

def rel(p):
    p = p.replace("\\", "/")
    i = p.find("App_SanMarino/")
    return p[i + len("App_SanMarino/"):] if i >= 0 else p

for s in ses:
    s["rel"] = sorted({rel(a) for a in s["archivos"]})
    s["planes"] = sorted({r.split("fase_de_desarrollo/")[1] for r in s["rel"] if "fase_de_desarrollo/" in r})

# atribucion commit -> sesion: ventana temporal + solape de archivos (hay sesiones en paralelo)
def ts(x):
    return dt.datetime.fromisoformat(x)

for s in ses:
    s["commits"] = []
ventana = dt.timedelta(hours=2)
CERCA = dt.timedelta(hours=6)     # sin solape de archivos, el commit tiene que estar PEGADO al cierre
sin_atribuir = []

def segmentos(s):
    return [(ts(a), ts(b)) for a, b in s.get("segmentos") or [[s["inicio"], s["fin"]]]]

def dentro(s, tc):
    """El commit cae dentro de un SEGMENTO de actividad (+2 h de gracia), no de la ventana
    completa de la sesión: hay sesiones abiertas durante semanas."""
    return any(a - dt.timedelta(minutes=10) <= tc <= b + ventana for a, b in segmentos(s))

def distancia(s, tc):
    return min(min(abs(tc - a), abs(tc - b)) for a, b in segmentos(s))

for c in commits:
    tc = ts(c["fecha"]).astimezone(dt.timezone.utc)
    cands = [s for s in ses if dentro(s, tc)]
    fset = set(c["files"])
    def solape(s):
        return len(fset & set(s["rel"]))
    # 1) el que comparte archivos con el commit gana (resuelve las sesiones en paralelo)
    con_solape = [s for s in cands if solape(s) > 0]
    if con_solape:
        elegido = max(con_solape, key=lambda s: (solape(s), -distancia(s, tc).total_seconds()))
    else:
        # 2) sin solape, solo cuenta la sesión que estaba CERRANDO ahí cerca. Sin esto, una
        #    ventana de sesión larga se lleva commits de todas las demás (113 en una sola).
        cerca = [s for s in cands if distancia(s, tc) <= CERCA]
        if not cerca:
            sin_atribuir.append(c)
            continue
        elegido = min(cerca, key=lambda s: distancia(s, tc))
    elegido["commits"].append({"sha": c["sha"], "fecha": c["fecha"][:10],
                               "subject": c["subject"], "body": c["body"][:400]})

# 2ª pasada: un commit puede caer FUERA de toda ventana de actividad (se commiteó horas
# después, o la transcripción de esa sesión ya no existe). Se recupera por solape fuerte de
# archivos dentro de ±3 días; si no, queda sin atribuir y no se inventa un dueño.
rescatados = 0
resto = []
for c in sin_atribuir:
    tc = ts(c["fecha"]).astimezone(dt.timezone.utc)
    fset = set(c["files"])
    cerca = [s for s in ses
             if abs((tc - ts(s["inicio"])).total_seconds()) <= 3 * 86400
             or abs((tc - ts(s["fin"])).total_seconds()) <= 3 * 86400]
    mejor, best = None, 1
    for s in cerca:
        n = len(fset & set(s["rel"]))
        if n > best:
            mejor, best = s, n
    if mejor is None:
        resto.append(c)
        continue
    mejor["commits"].append({"sha": c["sha"], "fecha": c["fecha"][:10],
                             "subject": c["subject"], "body": c["body"][:400]})
    rescatados += 1
sin_atribuir = resto

# ── join sesion -> tarea ya sembrada (por plan) ──────────────────────────────
por_plan = {}
for t in tareas:
    if t["plan"]:
        por_plan.setdefault(t["plan"], []).append(t)

sin_tarea, con_tarea = [], []
for s in ses:
    s["tareas_existentes"] = sorted({t["codigo"] for p in s["planes"] for t in por_plan.get(p, [])})
    (con_tarea if s["tareas_existentes"] else sin_tarea).append(s)

out = {"historias": historias, "tareas_sembradas": tareas, "sesiones": ses,
       "commits_total": len(commits)}
json.dump(out, open(os.path.join(HERE, "cruce.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)

print("historias sembradas:", len(historias))
for h in historias:
    print("  ", h["codigo"], h["titulo"], "|", h["etiquetas"])
print("tareas sembradas:", len(tareas), "| con plan:", sum(1 for t in tareas if t["plan"]))
print("commits jul-ago:", len(commits), "| atribuidos:", sum(len(s["commits"]) for s in ses),
      "| rescatados:", rescatados, "| sin atribuir:", len(sin_atribuir))
print("sesiones jul-ago:", len(ses), "| con tarea existente:", len(con_tarea), "| sin tarea:", len(sin_tarea))
print("sesiones sin commits:", sum(1 for s in ses if not s["commits"]))
