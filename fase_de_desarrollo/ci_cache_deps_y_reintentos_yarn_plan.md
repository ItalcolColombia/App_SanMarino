# CI — Caché real de dependencias + reintentos en `yarn install`

> Origen: el deploy `89219049283` (26-ago-2026) murió en el job del front. Backend desplegado
> (`rolloutState=COMPLETED`), front nunca llegó a ECR ⇒ producción quedó con back nuevo y front viejo.

## 1. Diagnóstico (medido, no supuesto)

**Disparador.** `registry.npmjs.org` devolvió **503** al servir `karma-6.4.4.tgz`, en
`[2/4] Fetching packages`. El `[1/4] Resolving packages` (API de metadata) había pasado bien. Es una
caída del lado de npm; nada del repo la provocó.

**Por qué un 503 aislado alcanzó para tumbar el deploy — dos condiciones que se suman:**

1. **Yarn 1 no reintenta ante un error HTTP.** `--network-timeout 100000` sólo acota cuánto espera,
   no reintentos. Cualquier non-2xx aborta el install entero. Yarn classic está en mantenimiento.
2. **El caché de capas nunca pega.** Conteo de layers `CACHED` en el run 89219049283:

   | build | layers `CACHED` |
   |---|---|
   | frontend | **0** |
   | backend  | **0** |

   El `#7 importing cache manifest from ...:latest` termina en `0.0s` porque no hay nada que importar.
   Consecuencia: **cada deploy del front rebaja los 763 paquetes del lockfile desde cero**, aunque
   `yarn.lock` no haya cambiado (último cambio: `8ecb7c6`, varios commits atrás). 763 tarballs por
   deploy = 763 chances de topar un 503.

## 2. Por qué `BUILDKIT_INLINE_CACHE=1` **solo** NO alcanza

Primera hipótesis descartada antes de implementar. El inline cache es cache **`mode=min`**: exporta
únicamente las capas de la **imagen resultante**. Ambos Dockerfiles son multi-stage y el paso que toca
la red vive en una etapa **intermedia** que nunca llega a la imagen final:

| | etapa con red | llega a la imagen final |
|---|---|---|
| `frontend/Dockerfile:21` | `deps` → `yarn install` | ❌ (sólo se copia `node_modules` a `build`) |
| `backend/Dockerfile:55`  | `restore` → `dotnet restore` | ❌ (`final` viene de `base`) |

Agregar el build-arg y nada más habría dejado el `yarn install` exactamente igual de sin caché.

## 3. Enfoque elegido

**Publicar la etapa de dependencias como imagen propia** (`--target`) con inline cache, y sembrar el
build completo desde ella. Con `--target deps`, esa etapa **es** la imagen resultante ⇒ sus capas sí
se exportan en `mode=min` ⇒ el build siguiente las reusa vía `--cache-from`.

Descartado migrar a `buildx` + `docker/build-push-action` con `cache-to mode=max`: cachearía todas las
etapas, pero (a) obliga al driver `docker-container`, y la **guarda del borde** del front
(`deploy-production.yml:346`) hace `docker run` sobre la imagen recién construida, que con ese driver
no queda en el daemon local sin `load: true`; (b) `cache-to type=registry` contra ECR exige
`image-manifest=true,oci-mediatypes=true`. Más superficie de riesgo sobre un pipeline de producción
para el mismo resultado.

**Reintentos** en el `RUN yarn install`: cubre las veces que `yarn.lock` **sí** cambia y el caché
legítimamente no puede pegar. No enmascara errores reales — si falla los 3 intentos, corta igual.

## 4. Archivos a modificar

| Archivo | Cambio |
|---|---|
| `.github/workflows/deploy-production.yml` (job backend, ~L195) | paso previo `docker build --target restore` → tag `:deps-cache`; `--build-arg BUILDKIT_INLINE_CACHE=1` + `--cache-from :deps-cache` en el build completo; push del tag |
| `.github/workflows/deploy-production.yml` (job frontend, ~L330) | ídem con `--target deps` |
| `frontend/Dockerfile` (L21) | `RUN yarn install` → loop de 3 intentos con backoff 20s/40s |

**No se toca:** ninguna etapa del Dockerfile fuera del `RUN`, la guarda del borde, el orden de los
pasos, las tags `:sha` / `:latest`, ni el despliegue a ECS. El artefacto que llega a producción es
byte a byte el mismo.

## 5. Reglas / invariantes

- El `RUN yarn install` conserva **las mismas flags** (`--frozen-lockfile` incluido): el lockfile
  sigue siendo la fuente de verdad, los reintentos no relajan la resolución de dependencias.
- Fallo tras 3 intentos ⇒ `exit 1`. El gate no se ablanda.
- `:deps-cache` es una tag más en el **mismo repo ECR** (no crea repos). Ojo con lifecycle policies
  que expiren por antigüedad: la tag se re-pushea en cada deploy, así que se mantiene fresca.
- El primer deploy tras el cambio **todavía baja todo** (la imagen actual en ECR no tiene manifiesto
  de caché). El beneficio arranca desde el segundo.

## 6. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | El YAML del workflow parsea | sin error de sintaxis; los 3 jobs y sus `needs` intactos |
| 2 | Loop con `yarn` que **falla 3 veces** | 3 intentos, mensajes numerados, `exit 1` |
| 3 | Loop con `yarn` que falla 2 y anda a la 3ª | `ok=1`, sale 0, corre `yarn cache clean` |
| 4 | Loop con `yarn` que anda a la 1ª | 1 sola invocación, sin sleeps (no alarga el caso feliz) |
| 5 | Sintaxis del `RUN` bajo `sh` de busybox (alpine) | `sh -n` sin error; `$((...))` soportado |
| 6 | Deploy real | 2º deploy consecutivo sin cambios en `yarn.lock` ⇒ layers `CACHED` > 0 |

## 7. Validación

- Casos 1–5, locales, antes de commitear.
- Caso 6 sólo se puede confirmar **en el pipeline**, comparando el conteo de `CACHED` del run
  siguiente contra el 0/0 de este. Queda anotado como verificación pendiente, no como hecho.
