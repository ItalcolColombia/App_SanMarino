#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# SoporteBot — CLI-puente al módulo de Tickets (ZooSanMarino)
# Portable: Git Bash (Windows local) y Linux (cron `/schedule`). Solo depende de `curl`.
# Los comandos de transporte devuelven JSON crudo por stdout; el loop lo parsea.
# Los comandos `save-*` decodifican Base64 a archivo (requieren python3).
#
# Config por variables de entorno (NUNCA hardcodear):
#   SOPORTE_BOT_BASE_URL   ej. https://api.tu-dominio.com   (local: http://localhost:5002)
#   SOPORTE_BOT_PAT        el token sk_...  (emitido por POST /api/service-tokens)
#
# Uso:
#   soporte-bot.sh whoami
#   soporte-bot.sh list [--all] [--estado ABIERTO] [--tipo SOPORTE] [--anio 2026] [--page 1] [--pageSize 20]
#   soporte-bot.sh get <id>
#   soporte-bot.sh imagenes <id>
#   soporte-bot.sh save-imagen <id> <imagenId> <archivoSalida>
#   soporte-bot.sh adjuntos <id>
#   soporte-bot.sh save-doc <id> <adjuntoId> <archivoSalida>
#   soporte-bot.sh tomar <id>
#   soporte-bot.sh nota <id> "texto" [--interna]
#   soporte-bot.sh estado <id> <NUEVO_ESTADO> [--nota "texto"] [--solucion "descripcion"]
#   soporte-bot.sh catalogos
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

die() { echo "soporte-bot: $*" >&2; exit 1; }

[ -n "${SOPORTE_BOT_BASE_URL:-}" ] || die "falta SOPORTE_BOT_BASE_URL"
[ -n "${SOPORTE_BOT_PAT:-}" ]      || die "falta SOPORTE_BOT_PAT"
command -v curl >/dev/null 2>&1    || die "curl no está instalado"

BASE="${SOPORTE_BOT_BASE_URL%/}"
AUTH="Authorization: Bearer ${SOPORTE_BOT_PAT}"

# python3 realmente ejecutable (en Windows el stub de la Store aparece en PATH pero falla)
_has_python3() { command -v python3 >/dev/null 2>&1 && python3 -c 'import sys' >/dev/null 2>&1; }

# _api METHOD PATH [JSON_BODY]
# Si SOPORTE_BOT_SECRET_UP está seteada, agrega el header X-Secret-Up (gate PlatformSecret del backend).
_api() {
  local method="$1" path="$2" body="${3:-}"
  local -a args=(-sS -X "$method" "${BASE}${path}" -H "$AUTH")
  [ -n "${SOPORTE_BOT_SECRET_UP:-}" ] && args+=(-H "X-Secret-Up: ${SOPORTE_BOT_SECRET_UP}")
  if [ -n "$body" ]; then args+=(-H "Content-Type: application/json" --data "$body"); fi
  curl "${args[@]}" -w $'\n<<HTTP:%{http_code}>>'
}

# escapa una cadena para incrustarla como valor JSON (python3 si funciona; si no, escape básico)
_json_str() {
  if _has_python3; then
    python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"
  else
    printf '"%s"' "$(printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g')"
  fi
}

# extrae un campo Base64 del JSON y lo decodifica a archivo.
# Preferimos python3 (parseo JSON robusto); si no hay, fallback a sed + base64 -d.
_save_b64_field() {
  local field="$1" outfile="$2" input; input="$(cat)"
  input="${input%%<<HTTP:*}"                       # descarta el sufijo de http_code
  if _has_python3; then
    printf '%s' "$input" | python3 - "$field" "$outfile" <<'PY'
import sys, json, base64
field, outfile = sys.argv[1], sys.argv[2]
obj = json.loads(sys.stdin.read())
val = next((v for k, v in obj.items() if k.lower() == field.lower()), None)
if val is None:
    sys.exit(f"campo '{field}' no encontrado en la respuesta")
with open(outfile, "wb") as f:
    f.write(base64.b64decode(val))
print(f"guardado {outfile} ({len(val)} bytes b64)")
PY
  elif command -v base64 >/dev/null 2>&1; then
    # el valor Base64 no contiene comillas ni backslash → extracción por sed es segura
    local b64
    b64="$(printf '%s' "$input" | sed -n "s/.*\"$field\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/Ip")"
    [ -n "$b64" ] || die "campo '$field' no encontrado en la respuesta"
    printf '%s' "$b64" | base64 -d > "$outfile"
    echo "guardado $outfile ($(printf '%s' "$b64" | wc -c) bytes b64)"
  else
    die "save-* requiere python3 o base64"
  fi
}

cmd="${1:-}"; shift || true
case "$cmd" in
  whoami)
    _api GET "/api/tickets/catalogos" >/dev/null && _api GET "/api/auth/ping"
    ;;

  list)
    all=0; qs=""
    while [ $# -gt 0 ]; do
      case "$1" in
        --all)      all=1; shift ;;
        --estado)   qs+="&estado=$2"; shift 2 ;;
        --tipo)     qs+="&tipo=$2";   shift 2 ;;
        --anio)     qs+="&anio=$2";   shift 2 ;;
        --pais)     qs+="&paisId=$2"; shift 2 ;;
        --page)     qs+="&page=$2";   shift 2 ;;
        --pageSize) qs+="&pageSize=$2"; shift 2 ;;
        *) die "list: opción desconocida $1" ;;
      esac
    done
    if [ "$all" = "1" ]; then _api GET "/api/tickets/global?${qs#&}"
    else _api GET "/api/tickets/asignados?${qs#&}"; fi
    ;;

  get)        [ $# -ge 1 ] || die "get <id>"; _api GET "/api/tickets/$1" ;;
  imagenes)   [ $# -ge 1 ] || die "imagenes <id>"; _api GET "/api/tickets/$1/imagenes" ;;
  adjuntos)   [ $# -ge 1 ] || die "adjuntos <id>"; _api GET "/api/tickets/$1/adjuntos" ;;
  catalogos)  _api GET "/api/tickets/catalogos" ;;
  tomar)      [ $# -ge 1 ] || die "tomar <id>"; _api POST "/api/tickets/$1/tomar" ;;

  save-imagen)
    [ $# -ge 3 ] || die "save-imagen <id> <imagenId> <archivoSalida>"
    _api GET "/api/tickets/$1/imagenes/$2" | _save_b64_field "ImagenBase64" "$3"
    ;;

  save-doc)
    [ $# -ge 3 ] || die "save-doc <id> <adjuntoId> <archivoSalida>"
    _api GET "/api/tickets/$1/adjuntos/$2/descargar" | _save_b64_field "ContenidoBase64" "$3"
    ;;

  nota)
    [ $# -ge 2 ] || die 'nota <id> "texto" [--interna]'
    id="$1"; texto="$2"; shift 2
    interna="false"; [ "${1:-}" = "--interna" ] && interna="true"
    _api POST "/api/tickets/$id/notas" "{\"nota\":$(_json_str "$texto"),\"esInterna\":$interna}"
    ;;

  estado)
    [ $# -ge 2 ] || die 'estado <id> <NUEVO> [--nota "t"] [--solucion "d"]'
    id="$1"; nuevo="$2"; shift 2
    nota="null"; sol="null"
    while [ $# -gt 0 ]; do
      case "$1" in
        --nota)     nota="$(_json_str "$2")"; shift 2 ;;
        --solucion) sol="$(_json_str "$2")";  shift 2 ;;
        *) die "estado: opción desconocida $1" ;;
      esac
    done
    _api PATCH "/api/tickets/$id/estado" \
      "{\"estado\":$(_json_str "$nuevo"),\"nota\":$nota,\"solucionDescripcion\":$sol}"
    ;;

  ""|-h|--help|help)
    sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'
    ;;

  *) die "comando desconocido: $cmd (usá --help)" ;;
esac
echo
