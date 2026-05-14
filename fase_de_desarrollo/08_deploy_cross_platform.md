# Plan de Desarrollo — Deploy Cross-Platform (Mac + Windows)
**ID:** 08  
**Feature:** Compatibilidad de scripts de despliegue y Makefile en Mac y Windows  
**Estado:** Pendiente de implementación  
**Fecha:** 2026-05-14

---

## Contexto y Problema

Los scripts de deploy (`deploy-backend-ecs.sh`, `deploy-frontend-ecs.sh`) y el `Makefile`
fueron escritos originalmente para **macOS**. Al correrlos en **Windows** con GnuWin32 make
+ Git Bash aparecen los siguientes problemas:

| # | Archivo | Problema | Mac | Windows |
|---|---------|----------|-----|---------|
| P1 | `Makefile` | Abrir navegador | `open` | `start` |
| P2 | `Makefile` | Verificar puerto libre | `lsof -i :PORT` | `netstat -ano` |
| P3 | `Makefile` | Emojis garbled en consola | UTF-8 nativo | Requiere `chcp 65001` |
| P4 | `deploy-backend-ecs.sh` | `sed -i` sin comillas falla en Mac | `sed -i ''` | `sed -i` |
| P5 | `deploy-backend-ecs.sh` | `docker buildx` con cert corporativo | Funciona | Falla TLS |
| P6 | `deploy-frontend-ecs.sh` | Idem P4 (ya parcialmente corregido) | `sed -i ''` | `sed -i` |
| P7 | General | Bash no nativo en Windows | Nativo | Requiere Git Bash en PATH |

**Solución actual (temporal):**
- P3: resuelto con `chcp 65001` en perfil de PowerShell
- P5: resuelto cambiando `buildx build --push` → `docker build` + `docker push`
- P4/P6: pendiente

---

## Alcance de la Implementación

### Tarea 1 — Makefile cross-platform

**Archivo:** `Makefile`

Detectar OS dentro del Makefile usando la variable de entorno `OS` (Windows la define
como `Windows_NT`; Mac/Linux no la tiene):

```makefile
# Detectar SO
ifeq ($(OS),Windows_NT)
    OPEN_CMD  = start
    CHECK_PORT = (netstat -ano | findstr :5050 > NUL 2>&1 && echo Puerto en uso && exit 1) || echo Puerto libre
else
    OPEN_CMD  = open
    CHECK_PORT = lsof -i :5050 >/dev/null && echo "Puerto en uso" && exit 1 || echo "Puerto libre"
endif
```

**Targets afectados:**
- `open` → reemplazar `open http://...` por `$(OPEN_CMD) http://...`
- `check-port` → reemplazar el bloque `lsof` por `$(CHECK_PORT)`

---

### Tarea 2 — deploy-backend-ecs.sh: sed portable

**Archivo:** `backend/scripts/deploy-backend-ecs.sh`

El `sed -i` en macOS requiere un sufijo de backup explícito (`''`). En Linux/Git Bash no
se necesita. Ya existe este patrón correcto en el script de frontend (líneas 96-102).
Aplicarlo igual al backend (actualmente usa `sed -i.bak` que deja archivos `.bak`):

```bash
# Reemplazar la línea de sed actual:
sed -i.bak "s/\\(.*\"image\":.*backend:\\)[^\\\"]*\\(.*\\)/\\1${TAG}\\2/" ...

# Por:
if [[ "$OSTYPE" == "darwin"* ]]; then
    sed -i '' "s|${ECR_URI}:[^\"}]*|${ECR_URI}:${TAG}|g" ecs-taskdef-new-aws.json
else
    sed -i "s|${ECR_URI}:[^\"}]*|${ECR_URI}:${TAG}|g" ecs-taskdef-new-aws.json
fi
```

---

### Tarea 3 — Verificar Git Bash disponible en Windows

Los scripts `.sh` se invocan desde el Makefile con `bash scripts/deploy-*.sh`.  
En Windows, `bash` debe estar en PATH (viene con Git for Windows).

Agregar verificación al inicio de cada script `.sh`:

```bash
# Ya al inicio del script, antes de set -e:
if [[ "$OS" == "Windows_NT" ]] && ! command -v bash &>/dev/null; then
    echo "[ERROR] Git Bash no encontrado. Instala Git for Windows."
    exit 1
fi
```

En realidad esta verificación es automática porque si no hay bash, el script no corre.
**Acción:** documentar en README que Windows requiere Git for Windows instalado.

---

### Tarea 4 — Limpiar builder buildx obsoleto

En el intento fallido anterior se creó el builder `sanmarino-builder` con driver
`docker-container`. Ya no se usa (reemplazado por `docker build` directo). Eliminarlo
para no confundir:

```bash
docker buildx rm sanmarino-builder 2>/dev/null || true
```

Se puede agregar como paso en los scripts o ejecutar manualmente una vez.

---

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Makefile` | Variables `OPEN_CMD` / `CHECK_PORT` según OS |
| `backend/scripts/deploy-backend-ecs.sh` | `sed` portable (darwin vs linux), limpiar `.bak` |
| `frontend/scripts/deploy-frontend-ecs.sh` | Revisar que el `sed` portable ya esté completo ✅ |

## Archivos NO modificar

| Archivo | Razón |
|---------|-------|
| `Dockerfile` (backend y frontend) | Sin cambios necesarios |
| `ecs-taskdef-new-aws.json` | Configuración de infra, sin cambios |
| Scripts de Angular / .NET | Sin relación con deploy |

---

## Criterio de Aceptación

- [ ] `make help` muestra emojis correctamente en Windows y Mac
- [ ] `make check-port` funciona en Windows (netstat) y Mac (lsof)
- [ ] `make open` abre el navegador en Windows (`start`) y Mac (`open`)
- [ ] `make deploy-backend` completa en Windows sin errores de sed ni buildx
- [ ] `make deploy-backend` completa en Mac sin errores de sed
- [ ] `make deploy-frontend` idem backend
- [ ] No quedan archivos `.bak` tras el deploy

---

## Orden de Implementación

1. Tarea 4 — Limpiar builder buildx (1 comando manual, no requiere código)
2. Tarea 2 — Fix `sed` en backend deploy script
3. Tarea 1 — Makefile cross-platform (`open` + `check-port`)
4. Tarea 3 — Documentar requisito Git Bash en Windows (README o CLAUDE.md)
5. QA manual: correr `make deploy-all` desde Windows y desde Mac
