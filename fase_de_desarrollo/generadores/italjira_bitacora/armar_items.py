# -*- coding: utf-8 -*-
"""Convierte el cruce en ITEMS de trabajo:
  E:<codigo tarea>  -> tarea ya sembrada que se ENRIQUECE (puede agregar varias sesiones)
  N:<sessionId>     -> tarea NUEVA (sesion sin tarea sembrada)
Cada item lleva: pedido real, commits (solucion), bugs (fix), horas reales, historia destino.
Salida: items.json + tabla_revision.txt (para asignar horas por juicio)."""
import json, os, re, unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
d = json.load(open(os.path.join(HERE, "cruce.json"), encoding="utf-8"))
ses = d["sesiones"]
tareas = {t["codigo"]: t for t in d["tareas_sembradas"]}

HISTORIAS = {
    "0001": "tickets", "0002": "implementacion", "0003": "vacunacion", "0004": "seguridad",
    "0005": "usuarios", "0006": "carga-masiva", "0007": "inventario", "0008": "liquidacion",
    "0009": "reproductoras", "0010": "movimientos", "0011": "guia-genetica", "0012": "engorde",
    "0013": "levante", "0014": "produccion", "0015": "reportes", "0016": "multiempresa",
    "0017": "ux", "0018": "integraciones", "0019": "plataforma", "0020": "italjira",
}

# reglas de clasificacion: (historia, [patrones]) — se evalua en orden, gana el de mas aciertos
REGLAS = [
    ("0020", [r"italjira", r"historias", r"roadmap"]),
    ("0001", [r"ticket", r"soporte"]),
    ("0002", [r"implementaci", r"cronograma"]),
    ("0003", [r"vacunaci"]),
    ("0004", [r"\bauth\b", r"login", r"jwt", r"sesion desliz", r"contrase", r"seguridad", r"permis"]),
    ("0005", [r"usuario", r"\broles?\b", r"menu", r"granja", r"nucleo", r"galpon"]),
    ("0006", [r"carga.?masiv", r"migracion.?masiv", r"migraciones-masivas", r"plantilla"]),
    ("0007", [r"inventario", r"gasto", r"stock", r"silo", r"alimento"]),
    ("0008", [r"liquidaci", r"cierre de lote"]),
    ("0009", [r"reproductora"]),
    ("0010", [r"traslado", r"movimiento", r"venta"]),
    ("0011", [r"guia.?genetica", r"genetic", r"uniformidad"]),
    ("0012", [r"engorde", r"pollo"]),
    ("0013", [r"levante"]),
    ("0014", [r"produccion", r"postura"]),
    ("0015", [r"reporte", r"informe", r"indicador", r"tablero", r"excel", r"powerbi", r"power bi"]),
    ("0016", [r"panama", r"ecuador", r"santa ?reyes", r"multi.?empresa", r"multipais", r"multi.?pais", r"company"]),
    ("0017", [r"dise.?o", r"\bux\b", r"\bui\b", r"filtro", r"modal", r"toast"]),
    ("0018", [r"correo", r"email", r"graph", r"integracion", r"\bpwa\b", r"whatsapp"]),
    ("0019", [r"deploy", r"ci/cd", r"docker", r"ecs", r"backup", r"db.?studio", r"build", r"make ", r"refactor", r"upgrade", r"angular 22"]),
]

def norm(s):
    s = unicodedata.normalize("NFD", s or "")
    return "".join(c for c in s if unicodedata.category(c) != "Mn").lower()

def clasificar(texto):
    t = norm(texto)
    mejor, best = "0019", 0
    for hist, pats in REGLAS:
        n = sum(1 for p in pats if re.search(p, t))
        if n > best:
            mejor, best = hist, n
    return mejor

def limpiar(txt, maxlen):
    txt = (txt or "").replace("\r", " ").replace("\n", " ")
    txt = re.sub(r'@"[^"]*"', "", txt)                 # rutas arrastradas al chat
    txt = re.sub(r"https?://\S+", "", txt)
    txt = re.sub(r"\s+", " ", txt).strip(" -·")
    if len(txt) > maxlen:
        txt = txt[:maxlen].rsplit(" ", 1)[0] + "…"
    return txt.strip()

ES_BUG = re.compile(r"^(fix|hotfix)\b", re.I)
ES_DOC = re.compile(r"^docs?\b", re.I)
ES_MEJ = re.compile(r"^(refactor|perf|style|chore|ux)\b", re.I)
PROMPT_BUG = re.compile(r"(error|falla|bug|no funciona|no me deja|descuadre|no trae|no carga|mal |incorrect|se queda)", re.I)

items = {}

def nuevo(key, historia):
    return {"key": key, "historia": historia, "sesiones": [], "commits": [], "bugs": [],
            "prompts": [], "horas_reales": 0.0, "archivos": 0, "ini": None, "fin": None,
            "tarea": tareas.get(key[2:]) if key.startswith("E:") else None}

# Sesiones que se conservan aunque no toquen archivos del repo (p. ej. la que produjo los
# documentos Word de la auditoría de Panamá): están declaradas a mano en el archivo de horas.
HORAS = os.path.join(r"C:\Users\SAN MARINO\Desktop\App_SanMarino",
                     r"fase_de_desarrollo\italjira_bitacora_sesiones_jul_ago_2026_horas.json")
DECLARADAS = set(json.load(open(HORAS, encoding="utf-8"))["tareas_nuevas"])

for s in ses:
    trivial = (not s["commits"]) and len(s["rel"]) < 3 and s["sessionId"][:8] not in DECLARADAS
    if trivial:
        continue
    destinos = ["E:" + c for c in s["tareas_existentes"]] or ["N:" + s["sessionId"]]
    principal = destinos[0]
    for k in destinos:
        if k not in items:
            hist = tareas[k[2:]]["historia"][-4:] if k.startswith("E:") else clasificar(
                " ".join([s["prompts"][0] if s["prompts"] else ""] +
                         [c["subject"] for c in s["commits"]] + s["rel"][:40]))
            items[k] = nuevo(k, hist)
        it = items[k]
        it["sesiones"].append(s["sessionId"])
        if s["prompts"]:
            it["prompts"].append(s["prompts"][0])
        # Ventana EFECTIVA: si la sesión quedó abierta días (span > 72 h), vale el primer
        # segmento de actividad — es cuando se hizo el trabajo, no cuando se cerró la ventana.
        segs = s.get("segmentos") or [[s["inicio"], s["fin"]]]
        fin_ef = segs[0][1] if s["horas_span"] > 72 else s["fin"]
        it["ini"] = min(x for x in [it["ini"], s["inicio"]] if x)
        it["fin"] = max(x for x in [it["fin"], fin_ef] if x)
    # commits, bugs y horas SOLO al destino principal (no se duplican)
    it = items[principal]
    it["horas_reales"] += s["horas_activas"]
    it["archivos"] += len(s["rel"])
    for c in s["commits"]:
        (it["bugs"] if ES_BUG.match(c["subject"]) else it["commits"]).append(c)

# tipo/estado: SOLO se calculan para las tareas NUEVAS. Las ya sembradas conservan el tipo y el
# estado del seed (están LISTO porque se hicieron); enriquecer no es reclasificar.
for k, it in items.items():
    if it["tarea"]:
        it["tipo"] = it["tarea"]["tipo"]
        it["estado"] = None          # no se toca
        it["titulo"] = it["tarea"]["titulo"]
        it["horas_reales"] = round(it["horas_reales"], 2)
        continue
    subs = [c["subject"] for c in it["commits"]]
    prompt = " ".join(it["prompts"])
    if it["bugs"] and not it["commits"]:
        it["tipo"] = "BUG"
    elif subs and all(ES_DOC.match(x) for x in subs):
        it["tipo"] = "DOCUMENTACION"
    elif subs and all(ES_MEJ.match(x) or ES_DOC.match(x) for x in subs):
        it["tipo"] = "MEJORA"
    elif not subs and not it["bugs"]:
        it["tipo"] = "DOCUMENTACION"
    elif PROMPT_BUG.search(prompt or "") and len(it["bugs"]) >= len(it["commits"]):
        it["tipo"] = "BUG"
    else:
        it["tipo"] = "TAREA"
    it["estado"] = "LISTO" if (it["commits"] or it["bugs"]) else "ANALISIS"
    it["titulo"] = (it["tarea"]["titulo"] if it["tarea"] else
                    limpiar(it["prompts"][0] if it["prompts"] else "Sesión de trabajo", 120))
    it["horas_reales"] = round(it["horas_reales"], 2)

orden = sorted(items.values(), key=lambda i: (i["ini"] or "", i["key"]))
json.dump(orden, open(os.path.join(HERE, "items.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=1)

with open(os.path.join(HERE, "tabla_revision.txt"), "w", encoding="utf-8") as fh:
    for i in orden:
        fh.write("%s | %s | H%s | %s | real %.2fh | %dc %db %df\n" % (
            i["key"], (i["ini"] or "")[:10], i["historia"], i["tipo"], i["horas_reales"],
            len(i["commits"]), len(i["bugs"]), i["archivos"]))
        fh.write("   T: %s\n" % limpiar(i["titulo"], 150))
        if i["prompts"]:
            fh.write("   P: %s\n" % limpiar(i["prompts"][0], 220))
        for c in (i["commits"] + i["bugs"])[:6]:
            fh.write("   c %s %s\n" % (c["sha"], limpiar(c["subject"], 110)))
        fh.write("\n")

print("items:", len(orden), "| enriquecer:", sum(1 for i in orden if i["key"].startswith("E:")),
      "| nuevos:", sum(1 for i in orden if i["key"].startswith("N:")))
print("bugs:", sum(len(i["bugs"]) for i in orden), "| commits solucion:", sum(len(i["commits"]) for i in orden))
print("horas reales totales:", round(sum(i["horas_reales"] for i in orden), 1))
import collections
print("por tipo:", collections.Counter(i["tipo"] for i in orden))
print("por historia:", sorted(collections.Counter(i["historia"] for i in orden).items()))
