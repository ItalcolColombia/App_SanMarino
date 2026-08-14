# -*- coding: utf-8 -*-
"""Extrae de las transcripciones de Claude Code (jul-ago 2026) una fila por SESION:
inicio, fin, duracion activa (huecos >30 min descontados), primer pedido del usuario
y archivos tocados (Edit/Write). Salida: sesiones.json en el mismo directorio."""
import json, os, sys, glob, datetime as dt

DIR = r"C:\Users\SAN MARINO\.claude\projects\C--Users-SAN-MARINO-Desktop-App-SanMarino"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sesiones.json")
GAP = dt.timedelta(minutes=30)

def parse_ts(s):
    try:
        return dt.datetime.fromisoformat(s.replace("Z", "+00:00"))
    except Exception:
        return None

def texto_user(msg):
    c = msg.get("content")
    if isinstance(c, str):
        return c
    if isinstance(c, list):
        partes = [p.get("text", "") for p in c if isinstance(p, dict) and p.get("type") == "text"]
        return "\n".join(partes)
    return ""

sesiones = []
for path in sorted(glob.glob(os.path.join(DIR, "*.jsonl"))):
    sid = os.path.basename(path)[:-6]
    ts = []
    prompts = []
    archivos = set()
    n_user = 0
    n_tool = 0
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                e = json.loads(line)
            except Exception:
                continue
            t = parse_ts(e.get("timestamp", ""))
            if t:
                ts.append(t)
            tipo = e.get("type")
            if tipo == "user" and not e.get("isMeta") and not e.get("isSidechain"):
                msg = e.get("message") or {}
                txt = texto_user(msg).strip()
                if txt and not txt.startswith("<") and "tool_result" not in txt[:40]:
                    n_user += 1
                    if len(prompts) < 12:
                        prompts.append(txt[:600])
            elif tipo == "assistant":
                msg = e.get("message") or {}
                for blk in (msg.get("content") or []):
                    if isinstance(blk, dict) and blk.get("type") == "tool_use":
                        n_tool += 1
                        inp = blk.get("input") or {}
                        fp = inp.get("file_path") or inp.get("notebook_path")
                        if isinstance(fp, str):
                            archivos.add(fp.replace("\\", "/"))
    if not ts:
        continue
    ts.sort()
    activa = dt.timedelta()
    # Segmentos de actividad continua: una ventana de sesión puede quedar abierta días
    # (la del 01-jul se retomó el 25-jul). Atribuir commits por la ventana completa hace que
    # esa sesión se lleve el trabajo de todas las demás.
    segmentos, seg_ini = [], ts[0]
    for a, b in zip(ts, ts[1:]):
        d = b - a
        if d < GAP:
            activa += d
        else:
            segmentos.append([seg_ini.isoformat(), a.isoformat()])
            seg_ini = b
    segmentos.append([seg_ini.isoformat(), ts[-1].isoformat()])
    sesiones.append({
        "sessionId": sid,
        "inicio": ts[0].isoformat(),
        "fin": ts[-1].isoformat(),
        "horas_activas": round(activa.total_seconds() / 3600.0, 2),
        "horas_span": round((ts[-1] - ts[0]).total_seconds() / 3600.0, 2),
        "segmentos": segmentos,
        "mensajes_usuario": n_user,
        "tool_calls": n_tool,
        "prompts": prompts,
        "archivos": sorted(a for a in archivos if "App_SanMarino" in a),
    })

sesiones.sort(key=lambda s: s["inicio"])
with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(sesiones, fh, ensure_ascii=False, indent=1)

print("sesiones:", len(sesiones))
if sesiones:
    print("rango:", sesiones[0]["inicio"], "->", sesiones[-1]["fin"])
    print("horas activas totales:", round(sum(s["horas_activas"] for s in sesiones), 1))
