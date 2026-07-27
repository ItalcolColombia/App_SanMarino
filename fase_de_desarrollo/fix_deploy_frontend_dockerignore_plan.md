# Fix — el deploy del frontend muere en el build de Docker (`MODULE_NOT_FOUND`)

Run fallido: **82085199647** (2026-07-27 17:46 UTC), job `Frontend — Build & Deploy`, paso 7 `Build imagen Docker`.
El backend y el gate de tests pasaron; solo cayó el frontend, antes de publicar nada en ECR.

## Síntoma

```
#25 [build 10/10] RUN node scripts/build-version.js prepare && yarn build --configuration docker ...
Error: Cannot find module '/app/scripts/build-version.js'
ERROR: failed to build: ... exit code: 1
```

## Causa raíz

`frontend/.dockerignore` tenía:

```
scripts/*
!scripts/inject-version.js
```

El commit `76a2903` (Fase 0.C de PWA) **renombró** `scripts/inject-version.js` → `scripts/build-version.js`
y cambió el Dockerfile para invocar el nombre nuevo, pero la lista blanca del `.dockerignore` quedó
apuntando al archivo borrado. Resultado: el contexto de build llega con `scripts/` **vacío**.

Por qué no se vio antes de llegar a CI:

1. `COPY scripts ./scripts` **no falla** con un directorio vacío — en el log sale `#24 ... DONE 0.0s`,
   indistinguible de una copia exitosa.
2. En local `yarn build` no pasa por el contexto de Docker, así que el script siempre está ahí.
3. El error real aparece recién en el `RUN` siguiente, ~40 s después, como un `MODULE_NOT_FOUND` de Node
   que no menciona ni Docker ni `.dockerignore`.

Es un fallo silencioso de contexto, no un problema del script ni del build de Angular.

## Cambios

| Archivo | Cambio | Por qué |
|---|---|---|
| `frontend/.dockerignore` | `!scripts/inject-version.js` → `!scripts/build-version.js` + comentario que ata la lista blanca al Dockerfile | Arregla la causa raíz: el archivo vuelve a entrar al contexto |
| `frontend/Dockerfile` | `COPY scripts ./scripts` → `COPY scripts/build-version.js ./scripts/build-version.js` | Convierte el fallo silencioso en uno ruidoso e inmediato: si el archivo no está en el contexto, el build muere en el `COPY` nombrando el archivo, no minutos después |

Sin cambio de comportamiento: el único archivo de `scripts/` que el build usaba (y el único que la
lista blanca dejaba pasar) es el sellador de versión.

## Auditoría de lo que sigue en el pipeline

El job nunca pasó del paso 7, así que del paso 8 en adelante **nada se ejecutó nunca en CI**. Revisado
archivo por archivo para no gastar otro ciclo de deploy:

- **`yarn build --configuration docker`** — la configuración `docker` de `angular.json` es más
  permisiva que `production` (budget inicial: error a 2.5 mb vs 2 mb). Como `yarn build` local
  (configuración `production`, la default) pasa con solo el warning de budget, el build de la imagen
  también pasa.
- **Assets** — `angular.json` los toma de `src/favicon.ico` y `src/assets`, ambos dentro de `src`, que
  el Dockerfile sí copia. No hay carpeta `public/` ni `ngsw-config.json` todavía (el Service Worker no
  está habilitado; Fase 0.C solo preparó el borde), así que no falta ningún archivo más en el contexto.
- **Paso "Validar nginx y política de caché del borde"** — guarda nueva de `76a2903`, con `exit 1` antes
  del push a ECR. En su commit se validó solo con `bash -n` (sintaxis), nunca corriendo contra una
  imagen real. Se ejecuta local, tal cual está en el workflow, contra la imagen construida.
  Puntos revisados a mano contra `nginx.conf`: los cuatro 404 esperados caen en bloques con
  `try_files $uri =404`; `version.json` lo produce `build-version.js emit`; el asset de referencia lo
  extrae de `index.html` con `grep -oE 'src="[^"]+\.js"'`, que matchea `polyfills-*.js` (verificado
  contra el `dist/browser` local) — importa porque si saliera vacío, el `[ -n "$JS" ] && check ...` se
  evalúa como falso y, con el `bash -e` que GitHub Actions usa por defecto, **el paso entero aborta**.

## Casos de prueba (local, antes de re-desplegar)

1. `docker build --target build` desde `frontend/` con el mismo `--platform linux/amd64` del workflow
   → el `COPY` del script pasa y `prepare` / `ng build` / `emit` completan.
2. La imagen final tiene `/usr/share/nginx/html/version.json` con un `buildId`.
3. Correr el script del paso "Validar nginx…" tal cual, contra la imagen construida → 0 fallos.
4. Bajar todo lo levantado (`docker rm -f`), sin contenedores huérfanos.

## Re-despliegue

El fix va a `main`; el pipeline dispara con push a `main-produccion`. El merge/push lo pide el usuario
explícitamente (no se hace desde acá).
