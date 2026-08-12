# Plan — El gate del borde del frontend bloquea el deploy desde que la PWA existe

**Fecha:** 2026-08-11
**Run que falló:** `85573900056` (push a `main-produccion`, merge PR #66, SHA `67147f1`)
**Síntoma en Actions:** `##[error]La imagen del frontend no cumple 2 criterio(s) del borde. No se publica.` → `exit code 1`

---

## 1. Qué pasó exactamente

El job **Frontend — Build & Deploy** construyó la imagen bien (`nginx -t` OK, `verificar-ngsw.js`
OK, imagen etiquetada) y **falló en el paso siguiente**, `Validar nginx y política de caché del
borde`, que corre **antes** del `docker push`. Dos criterios en rojo:

```
FALLA ngsw.json ausente -> 404          (respondió 200, application/json,      no-cache)
FALLA manifest.webmanifest aus. -> 404  (respondió 200, application/manifest+json, no-cache)
```

Todo lo demás pasó: 404 del chunk inexistente, 200 de la ruta del SPA, no-cache de
`version.json`/`index.html`, `immutable` del asset hasheado, CSP/HSTS/worker-src/reCAPTCHA en
todas las respuestas.

**Consecuencia:** la imagen **nunca se subió a ECR** y el servicio de ECS del front nunca se
actualizó. El backend del mismo run sí desplegó (`rolloutState=COMPLETED`), así que producción
quedó con **backend nuevo y frontend viejo**.

## 2. Causa raíz — el gate quedó viejo, la imagen está bien

| Fecha | Commit | Qué cambió |
|---|---|---|
| 2026-07-27 | `76a2903` | Nace el gate (PWA Fase 0.C). En ese momento el build **no** emitía `ngsw.json` ni `manifest.webmanifest`, así que pedirles **404** era la forma de probar el bloque 3 de `nginx.conf`: *un recurso no navegable que no existe devuelve 404, nunca el `index.html`*. Las etiquetas lo dicen: «ngsw.json **ausente** → 404». |
| 2026-08-09 | `8ecb7c6` | La app se vuelve PWA: `angular.json` gana `"serviceWorker": "ngsw-config.json"` en la configuración `docker` y `src/manifest.webmanifest` entra en `assets`. Desde ahí el build **sí** emite los dos archivos. |

O sea: el gate afirma «estos dos archivos no existen» y desde el 09-ago **sí existen, a propósito**.
No hay nada roto en `nginx.conf` ni en la imagen — el que quedó desactualizado es el criterio.
Esto explica la nota de campo del 10-ago (*la PWA está construida y no desplegada; `ngsw.json` da
404 en prod*): el gate es justo lo que la venía frenando.

## 3. Enfoque de la corrección

No relajar la compuerta: **actualizarla para que verifique el invariante real**, que sigue siendo el
mismo que motivó el bloque 3 de nginx — *un Service Worker nunca puede recibir HTML donde espera
JSON*. Con la PWA encendida eso se comprueba en dos mitades:

1. **Lo inexistente sigue dando 404** (lo que el gate probaba antes). Se conserva con rutas que
   nunca van a existir, en vez de con archivos que hoy son parte del build:
   `chunk-inexistente.js`, `no-existe-1234.json`, `no-existe-1234.webmanifest`.
2. **Lo que la PWA necesita publicado, se publica bien**: `ngsw.json`, `ngsw-worker.js`,
   `safety-worker.js` y `manifest.webmanifest` responden **200**, con el **Content-Type correcto**
   (nunca `text/html`) y **`no-cache`**.

El punto 2 cubre un agujero que hoy nadie vigila en el borde: si `ngsw-worker.js` o
`safety-worker.js` cayeran en la regla `immutable` de assets (bloque 2 de `nginx.conf`, riesgo que
el propio archivo documenta en su cabecera), el navegador se quedaría con un Service Worker viejo
cacheado **un año** y el kill switch sería inservible. `verificar-ngsw.js` valida el **contenido del
build**; este gate valida **cómo lo sirve nginx**. Son cosas distintas y ninguna reemplaza a la otra.

## 4. Archivos a modificar

- `.github/workflows/deploy-production.yml` — paso *Validar nginx y política de caché del borde*
  del job `deploy-frontend` (bloques «C2» y uno nuevo «C4»).

Sin cambios en `nginx.conf`, en el Dockerfile ni en código de la app: la imagen que se construyó ya
cumple lo que el gate corregido pide (el propio log del run lo muestra en el volcado de headers).

## 5. Reglas de negocio / invariantes que el gate debe seguir sosteniendo

- Un recurso **no navegable inexistente** (`.js`, `.json`, `.webmanifest`, `.map`, …) devuelve
  **404**, jamás `index.html` con `Content-Type: text/html`.
- Una **ruta del SPA** (sin extensión) devuelve **200** con el `index.html`.
- Archivos de control (`ngsw.json`, `ngsw-worker.js`, `safety-worker.js`, `manifest.webmanifest`,
  `version.json`, `index.html`) → **`no-cache`**. Los assets hasheados → **`immutable`**.
- CSP/HSTS en **todas** las respuestas, con `worker-src 'self'` y los orígenes de reCAPTCHA.

## 6. Casos de prueba

Sobre la imagen construida localmente con el mismo Dockerfile, corriendo el script del gate tal cual
queda en el workflow:

| # | Caso | Esperado |
|---|---|---|
| 1 | `GET /chunk-inexistente.js` | 404 |
| 2 | `GET /no-existe-1234.json` | 404 |
| 3 | `GET /no-existe-1234.webmanifest` | 404 |
| 4 | `GET /lotes/detalle/9` | 200 (fallback del SPA) |
| 5 | `GET /ngsw.json` | 200 · `application/json` · `no-cache` |
| 6 | `GET /ngsw-worker.js` | 200 · `no-cache` (**no** `immutable`) |
| 7 | `GET /safety-worker.js` | 200 · `no-cache` |
| 8 | `GET /manifest.webmanifest` | 200 · `application/manifest+json` · `no-cache` |
| 9 | `version.json` / `index.html` | `no-cache` (sin regresión) |
| 10 | asset hasheado (`polyfills-*.js`) | `immutable` (sin regresión) |
| 11 | CSP/HSTS/worker-src/reCAPTCHA | presentes en `/`, en el asset y en la ruta del SPA |
| 12 | Total | `fallos=0` → «Borde OK.» y exit 0 |

## 7. Qué queda fuera de este cambio

- **No** se despliega nada: este arreglo habilita el deploy, no lo ejecuta. Volver a correr el
  workflow (push a `main-produccion`) es decisión del usuario.
- Cuando ese deploy corra, el frontend que llegue a prod **es el de la PWA** (F1+F2, instalable,
  con Service Worker). Es el primer deploy del front desde el 07-ago; conviene verificarlo con
  `/version.json` contra el ALB después.
