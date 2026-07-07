# SoporteBot — CLI-puente al módulo de Tickets

Puente delgado que autentica con un **PAT de servicio** (`sk_...`) y envuelve los endpoints de tickets, para que el loop de soporte (local o cron `/schedule`) opere con una sola ruta testeada. Portable: **Git Bash** (Windows) y **Linux** (cron en la nube). Única dependencia dura: `curl` (los comandos `save-*` requieren además `python3`).

## Configuración (variables de entorno — nunca hardcodear)

```bash
export SOPORTE_BOT_BASE_URL="http://localhost:5002"     # local; en prod: la URL pública de la API
export SOPORTE_BOT_PAT="sk_xxxxxxxxxxxxxxxxxxxxxxxxxxxx" # emitido por POST /api/service-tokens
```

## Emitir / revocar el PAT

Con una sesión JWT normal (rol admin/global), desde la app o Swagger:

```
POST /api/service-tokens      { "name": "soporte-bot", "scopes": "tickets:read tickets:write", "expiresAt": null }
    → devuelve el token plano UNA sola vez (guardalo en el secreto del cron)
DELETE /api/service-tokens/{id}   → revoca
```

El PAT solo autoriza `api/tickets/**` (+ `api/auth/ping`); en cualquier otra ruta el backend lo rechaza.

## Comandos

| Comando | Hace |
|---|---|
| `whoami` | Verifica que el PAT funciona (`/api/auth/ping`) |
| `list [--all] [--estado E] [--tipo T] [--anio A] [--pais P] [--page/--pageSize]` | Sin `--all`: mis tickets asignados. Con `--all`: bandeja global (dev global) |
| `get <id>` | Detalle del ticket (notas + metadata de imágenes/adjuntos) |
| `imagenes <id>` | Metadata de las fotos del ticket (sin Base64) |
| `save-imagen <id> <imagenId> <archivo>` | Descarga y decodifica una foto a archivo |
| `adjuntos <id>` | Lista documentos y links del ticket |
| `save-doc <id> <adjuntoId> <archivo>` | Descarga y decodifica un documento (Excel/PDF) a archivo |
| `tomar <id>` | Toma el ticket: ABIERTO → EN_ANALISIS (idempotente) |
| `nota <id> "texto" [--interna]` | Agrega nota/respuesta a la bitácora |
| `estado <id> <NUEVO> [--nota "t"] [--solucion "d"]` | Avanza el estado (`--solucion` obligatorio al pasar a SOLUCIONADO) |
| `catalogos` | Tipos y estados válidos |

Toda respuesta imprime el body y un sufijo `<<HTTP:code>>` para que el loop verifique el status.

## Ejemplo de una vuelta del loop

```bash
soporte-bot.sh list --estado ABIERTO                 # ¿qué tengo abierto?
soporte-bot.sh tomar 12                               # lo tomo (EN_ANALISIS)
soporte-bot.sh get 12                                 # leo descripción + notas
soporte-bot.sh save-imagen 12 3 /tmp/tk12_img3.png    # bajo la foto para analizarla
soporte-bot.sh save-doc   12 5 /tmp/tk12_doc5.xlsx    # bajo el documento adjunto
# ... análisis / fix / PR ...
soporte-bot.sh nota 12 "Diagnóstico: ... (SoporteBot)"
soporte-bot.sh estado 12 SOLUCIONADO --solucion "Se corrigió X; ver PR #123"
```

## Notas de seguridad
- El PAT es equivalente a tu sesión sobre tickets: tratalo como secreto (variable de entorno / secreto del cron, nunca en git).
- Los guardrails de deploy (migración = gate humano; deploy que no estabiliza = auto-rollback + ticket NO SOLUCIONADO) viven en el **loop**, no en este CLI. El CLI es solo transporte.
- Ver el plan: [`fase_de_desarrollo/soporte_bot_loop_tickets_plan.md`](../../fase_de_desarrollo/soporte_bot_loop_tickets_plan.md).
