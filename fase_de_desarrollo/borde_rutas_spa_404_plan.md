# Plan — Solo las rutas reales del SPA responden 200 en el borde (hallazgo `/env` → 200)

**Fecha:** 2026-09-28
**Origen:** hallazgo de la validación de seguridad de producción: *"Responde ELB de AWS, código 200 en
`/env`"*. Se va a re-validar en producción.
**Relacionado:** `respuesta_auditoria_ciberseguridad_2026-09.md` (§3, dotfiles → 403),
`gate_borde_front_pwa_plan.md` (gate del borde), `gates_hallazgos_auditoria_2026-09_plan.md`.

---

## 1. Qué pasa hoy (medido contra producción el 28-sep-2026, solo status/tipo/tamaño/hash)

| Ruta | Código | Tipo | Bytes | Hash del cuerpo |
|---|---|---|---|---|
| `/` | 200 | text/html | 2234 | `2eca1c9cfaf2` |
| `/env`, `/environment`, `/aws`, `/config`, `/actuator/env`, `/swagger`, `/backup`, `/server-status`, `/no-existe-1234` | **200** | text/html | 2234 | **`2eca1c9cfaf2`** (el mismo) |
| `/.env`, `/wp-admin` | 403 | text/html | 118 | WAF (`awselb/2.0`) |
| `/api/env` | 401 | application/json | 118 | filtro `platform-secret` |

**No se filtra ningún archivo**: todas esas rutas devuelven el **`index.html` del SPA** (mismo hash que
`/`). Es el fallback `try_files $uri $uri/ /index.html` del bloque 4 de `frontend/nginx.conf`, que
contesta 200 a **cualquier** ruta sin extensión. Pero el analista tiene razón en el fondo: un 200 a una
ruta que no existe (a) hace que cada sonda de un escáner parezca un hallazgo, (b) le confirma al
reconocimiento que "algo" responde en cualquier path y (c) tapa los hallazgos reales entre falsos
positivos. **Una ruta que no existe tiene que responder 404.**

## 2. Enfoque

**Lista blanca de rutas del SPA en nginx, derivada del código (no a mano).**

1. `frontend/scripts/rutas-spa-nginx.js` lee `src/app/app.config.ts` con el **compilador de
   TypeScript** (no con regex: hay `children` anidados con `path:` propios) y toma las rutas
   **top-level** de `provideRouter([...])`:
   - Ruta hoja (`component`/`loadComponent`, sin hijos) o redirect `pathMatch: 'full'` → **exacta**
     (`/login`, `/login/`).
   - Ruta con `children`/`loadChildren` o redirect `prefix` → **prefijo** (`/config`, `/config/users`,
     `/traslados-aves/historial/9`).
   - `''` → la raíz `/`. `**` → se ignora: es justamente el comodín que el borde deja de imitar.
   - En las rutas de prefijo, el **último segmento no puede tener punto** (`/config/database.yml` → 404).
     Es la misma regla que ya usa el Service Worker (`navigationUrls: "!/**/*.*"`): una URL con
     extensión en el último segmento no es una navegación.
   - **Fail-closed**: si encuentra algo que no sabe traducir (ruta top-level con parámetro en el primer
     segmento, `matcher`, spread `...otrasRutas`, `path` no literal, `''` con `loadChildren`, cero o
     dos `provideRouter`) **falla** con un mensaje que dice qué tocar. Nunca genera una lista vacía ni
     un comodín.
2. Genera `rutas-spa.conf` con un `map $uri $ruta_spa { default 0; ... }` (contexto `http`).
3. **El Dockerfile lo genera en el build** (etapa `build`, antes de `ng build`) y lo copia a
   `/etc/nginx/rutas-spa.conf`. No hay archivo que se desincronice: una ruta top-level nueva queda
   habilitada en el mismo deploy que la crea.
4. `nginx.conf`: `include /etc/nginx/rutas-spa.conf;` arriba del `server` y, en el bloque 4
   (`location /`), `if ($ruta_spa = 0) { return 404; }` antes del `try_files`. `if` + `return` es
   uno de los dos usos seguros de `if` dentro de `location`. El resto de los bloques no se toca
   (orden, dotfiles → 403, assets `immutable`, archivos de control `no-cache`).
5. El 404 de navegación sirve `404.html` (estático, sin scripts, con los headers de seguridad): la
   persona que abre un enlace viejo ve "Esta página no existe · Ir al inicio" en vez del error pelado
   de nginx. `location = /404.html` es `internal` (pedirlo directo también da 404).

**Por qué no una lista negra** (`/env`, `/aws`, `/actuator`…): los escáneres prueban miles de rutas;
cada una que falte vuelve a ser un 200. La lista blanca es finita y la dicta el router.

**Por qué no un árbol completo de rutas** (incluidos los hijos de `loadChildren`): exige parsear ~25
archivos de rutas y reproducir la semántica del router de Angular (parámetros con `/` codificado,
matrix params…); un error ahí rompe un enlace real al recargar. El prefijo top-level ya cierra la
superficie del hallazgo con riesgo mínimo.

## 3. Qué NO cambia (verificado)

- **Health checks**: la TaskDef del front pega a `curl -f http://localhost/` y el target group del ALB
  a `/` (`backend/documentacion/aws-infrastructure/CONFIGURACION_ALB.md`). `/` sigue en 200.
  `location /health` no se toca.
- **Entradas por carga completa** (las únicas que llegan a nginx; la navegación interna es del
  router): correos (`/login`, `/reset-password?token=…`, logos en `/assets/…`), `window.location.href
  = '/home'`, `<a href="/diagnostico">`, manifest (`start_url: /?fuente=pwa`, atajos
  `/daily-log/seguimiento` y `/diagnostico`). Todas son top-level o hijas de una top-level de prefijo.
- **PWA instalada**: el Service Worker responde las navegaciones desde su caché, así que para quien
  tiene la app instalada una URL inventada sigue cayendo en el `**` → `/login` del router. El 404 lo ve
  el que llega sin Service Worker (un escáner, un navegador nuevo).
- `dist/browser` no tiene archivos sin extensión: nada legítimo dependía del `try_files $uri` del
  bloque 4 salvo el `index.html`.
- Backend, BD, migraciones: **sin cambios**.

**Cambio de comportamiento aceptado:** una URL inventada abierta en un navegador sin Service Worker
antes redirigía a `/login`; ahora muestra la página 404 con "Ir al inicio".

## 4. Archivos

| Archivo | Cambio |
|---|---|
| `frontend/scripts/rutas-spa-nginx.js` | **Nuevo.** Generador + funciones exportadas (`extraerRutasTopLevel`, `construirPatron`, `generarConfNginx`, `esRutaSpa`). |
| `frontend/scripts/tests/rutas-spa-nginx.test.js` | **Nuevo.** `node --test`: contra el `app.config.ts` real y contra fuentes sintéticas (fail-closed). |
| `frontend/nginx.conf` | `include` del map + `if ($ruta_spa = 0) { return 404; }` + `error_page 404 /404.html` en el bloque 4 + `location = /404.html` interna. |
| `frontend/Dockerfile` | `COPY` del generador, lo corre antes de `ng build`, copia `rutas-spa.conf` al runtime. |
| `frontend/.dockerignore` | `!scripts/rutas-spa-nginx.js` (lista blanca por nombre, ver memoria del 27-jul). |
| `frontend/src/404.html` + `frontend/angular.json` | Página 404 estática, en `assets` del build. |
| `frontend/scripts/servir-pwa-local.js` | Replica la lista blanca y el 404 (misma función). |
| `.github/workflows/deploy-production.yml` | Job `tests`: gate nuevo (`node --test`). Gate del borde: la "ruta del SPA" pasa a ser una real (`/lotes/detalle/9` no existe en el router y con la lista blanca da 404) y bloque **C7** (sondas → 404 sin el index, con CSP; entradas reales → 200). |
| `fase_de_desarrollo/respuesta_auditoria_ciberseguridad_2026-09.md` | Sección del hallazgo y cómo verificarlo. |

## 5. Casos de prueba

**Unitarios (`node --test`, corren en el job `tests`):**
1. Con el `app.config.ts` real: `/`, `/login`, `/login/`, `/home`, `/reset-password`,
   `/password-recovery`, `/selector-usuario`, `/diagnostico`, `/daily-log/seguimiento`,
   `/config`, `/config/users`, `/traslados-aves/historial/12`, `/indicador-ecuador/x`, `/tickets/9` → SPA.
2. `/env`, `/environment`, `/aws`, `/actuator/env`, `/swagger`, `/server-status`, `/backup`,
   `/wp-admin`, `/no-existe-1234`, `/login/env`, `/home/env`, `/configx`, `/config.php`,
   `/config/database.yml`, `/tickets/web.config` → **no** SPA.
3. Fuentes sintéticas: hoja → exacta; `children`/`loadChildren` → prefijo; redirect `full` → exacta;
   redirect sin `pathMatch` → prefijo; `''` con `children` inline → sus hijos pasan a top-level.
4. Fail-closed: parámetro en el primer segmento, `matcher`, spread, `path` no literal, `''` con
   `loadChildren`, sin `provideRouter`, dos `provideRouter` → **lanza**.
5. Cableado: `nginx.conf` incluye el map y corta con 404; el Dockerfile genera y copia el archivo; el
   `.dockerignore` deja pasar el script.

**nginx real (`nginx:1.27-alpine`, la imagen del runtime) con `dist/browser` local:** `nginx -t` OK y
el script del gate del borde completo (C2–C7) en verde. Prueba negativa: sin el `if`, C7 falla.

**Build:** `yarn build` 0 errores (único warning aceptado: budget preexistente) y `404.html` en
`dist/browser`.

**Post-deploy (producción, solo lectura):** `/env` → 404 sin el index; `/`, `/login`,
`/daily-log/seguimiento` → 200; `/version.json` con el `buildId` del deploy.

## 6. Riesgos y reversión

- **Ruta top-level nueva**: queda habilitada sola (se genera en el build). Si el generador no entiende
  la forma nueva, falla el job `tests` antes de desplegar nada, con el motivo.
- **Config de nginx rota**: el gate del borde corre `nginx -t` y las sondas **antes** del push a ECR:
  la imagen no se publica y ECS no se entera.
- **Reversión**: revertir el commit. No hay estado ni datos involucrados.
