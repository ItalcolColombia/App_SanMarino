#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# SoporteBot — runner de una iteración del loop.
# `prepare <id> <workdir>`: toma el ticket (si está ABIERTO) y arma el "packet"
# de análisis en <workdir>: ticket.json + imágenes + documentos (ARCHIVO) + links.txt.
# La CLASIFICACIÓN y las acciones (nota/estado/PR) las decide Claude según RUNBOOK.md.
#
# Requiere las mismas env vars que soporte-bot.sh (SOPORTE_BOT_BASE_URL, SOPORTE_BOT_PAT).
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
BOT="$HERE/soporte-bot.sh"
die() { echo "loop: $*" >&2; exit 1; }

strip_http() { sed 's/<<HTTP:[0-9]*>>//'; }

cmd="${1:-}"; shift || true
case "$cmd" in
  prepare)
    [ $# -ge 2 ] || die "prepare <id> <workdir>"
    id="$1"; wd="$2"; mkdir -p "$wd"

    # 1) tomar (idempotente; si ya está tomado/avanzado no rompe)
    echo "· tomar $id"; bash "$BOT" tomar "$id" >/dev/null 2>&1 || true

    # 2) detalle
    echo "· detalle → $wd/ticket.json"
    bash "$BOT" get "$id" | strip_http > "$wd/ticket.json"

    # 3) imágenes
    bash "$BOT" imagenes "$id" | strip_http > "$wd/imagenes.json"
    for imgId in $(grep -oE '"id":[0-9]+' "$wd/imagenes.json" | grep -oE '[0-9]+'); do
      ext="png"; grep -q '"contentType":"image/jpeg"' "$wd/imagenes.json" && ext="jpg"
      echo "· imagen $imgId → $wd/img_${imgId}.${ext}"
      bash "$BOT" save-imagen "$id" "$imgId" "$wd/img_${imgId}.${ext}" || echo "  (falló imagen $imgId)"
    done

    # 4) adjuntos: documentos (ARCHIVO) se bajan; LINK se listan
    bash "$BOT" adjuntos "$id" | strip_http > "$wd/adjuntos.json"
    for adjId in $(grep -oE '"id":[0-9]+,"tipo":"ARCHIVO"' "$wd/adjuntos.json" | grep -oE '^"id":[0-9]+' | grep -oE '[0-9]+'); do
      echo "· documento $adjId → $wd/doc_${adjId}"
      bash "$BOT" save-doc "$id" "$adjId" "$wd/doc_${adjId}" || echo "  (falló doc $adjId)"
    done
    grep -oE '"url":"[^"]+"' "$wd/adjuntos.json" | sed 's/^"url":"//; s/"$//' > "$wd/links.txt" || true

    echo "--- packet listo en $wd ---"
    echo "estado:   $(grep -oE '"estado":"[^"]+"' "$wd/ticket.json" | head -1 | sed 's/.*://; s/"//g')"
    echo "tipo:     $(grep -oE '"tipo":"[^"]+"'   "$wd/ticket.json" | head -1 | sed 's/.*://; s/"//g')"
    echo "paisId:   $(grep -oE '"paisId":[0-9]+'  "$wd/ticket.json" | head -1 | sed 's/.*://')"
    echo "archivos: $(ls -1 "$wd" | tr '\n' ' ')"
    [ -s "$wd/links.txt" ] && { echo "links:"; sed 's/^/  - /' "$wd/links.txt"; }
    ;;
  ""|-h|--help)
    echo "uso: loop.sh prepare <ticketId> <workdir>"
    ;;
  *) die "comando desconocido: $cmd" ;;
esac
