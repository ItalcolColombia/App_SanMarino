# Plan — Rutas enmascaradas: el borde solo responde las entradas públicas (login)

**Fecha:** 2026-09-28
**Origen:** pedido del usuario después del arreglo `/env` → 404 (PR #119, desplegado 28-sep 16:12Z):
*"que las rutas no sean públicas, enmascararlas para mostrar, y que la única ruta pública sea el login"*.
**Relacionado:** `borde_rutas_spa_404_plan.md` (lista blanca top-level, ya en producción),
`respuesta_auditoria_ciberseguridad_2026-09.md` §7.

---

## 1. Qué pasa hoy (medido en producción el 28-sep-2026 16:13Z, build `2026-09-28T16:08:03Z`)

| Ruta | Respuesta |
|---|---|
| `/env`, `/aws`, `/swagger`, `/actuator/env`… (inventadas) | **404** con `404.html` (arreglo anterior) |
| `/config`, `/dashboard`, `/daily-log/seguimiento`, `/tickets/9`… (las 39 top-level del router) | **200** con el `index.html` |
| `/.env`, `/.aws/*`, `/admin` | 403 (WAF) · `/.git/*` 403 (nginx) |

Dos cosas siguen a la vista:

1. **Enumeración desde afuera.** Un anónimo distingue un módulo real (200) de uno inventado (404):
   con una lista de palabras se reconstruye qué módulos tiene la app sin tener cuenta.
2. **La barra de direcciones muestra la ruta interna** (`/config/users`, `/reporte-contable/…`), que
   además queda en el historial del navegador, en capturas de pantalla y en los logs de proxies.

## 2. Objetivo

1. **Anónimo, sin Service Worker:** solo responden 200 `/`, `/login`, `/password-recovery` y
   `/reset-password`. Todo lo demás da el **mismo 404** que una ruta inventada ⇒ un módulo real y uno
   inventado son indistinguibles.
2. **Adentro de la app:** la barra muestra siempre `https://<host>/`. Ninguna ruta interna en la barra,
   en el historial del navegador ni en el `href` de los enlaces del menú.
3. **Sin romper:** atrás/adelante, recargar (F5) sin perder la pantalla, guards, redirects, PWA sin
   conexión, correos (restablecer contraseña, login, tickets) y atajos del manifest.

## 3. Enfoque

### 3.1 Enmascarar la URL — `LocationStrategy` propia

El router de Angular escribe la barra a través de una `LocationStrategy` reemplazable.
`RutaEnmascaradaLocationStrategy` extiende `PathLocationStrategy`:

- **`pushState` / `replaceState`**: si la ruta es interna, escribe `/` en la barra y guarda la ruta real
  dentro de `history.state` (clave propia). Si es pública, la escribe tal cual.
- **`path()`**: devuelve la ruta real guardada en `history.state`; si la entrada no tiene (llegada por
  carga completa), la física.
- **`prepareExternalUrl()`**: el `href` que arma `routerLink` para una ruta interna también es `/` (al
  pasar el mouse no se ve la ruta; Ctrl+clic abre el inicio).
- **`getState()` / `onPopState()`**: le sacan la clave propia al estado que ve el router, para que no
  se filtre a `extras.state` ni a `history.state` de los componentes.

**Por qué alcanza:** `history.state` es por entrada del historial y **sobrevive a la recarga**;
atrás/adelante disparan `popstate` y el `Location` de Angular llama a `path()` justo ahí, con el estado
de la entrada nueva. El router no se entera del enmascarado: ve siempre la ruta real, así que guards,
`router.url`, `ActivatedRoute` y `routerLinkActive` no cambian. Verificado en el código del router
(v22.0.5): `setBrowserUrl` usa `location.isCurrentPathEqualTo(path)` y `initialNavigation()` arranca de
`location.path(true)` ⇒ ambos pasan por `path()`.

**Descartadas:**
- `skipLocationChange` en cada navegación: cientos de llamadas, y rompe atrás y F5.
- `HashLocationStrategy` (`/#/config`): la ruta se sigue viendo en la barra.
- Cookie de sesión revisada por nginx: no enmascara la barra y le agrega estado al borde.
- `browserUrl` del router: hay que pasarlo en cada `navigate`/`routerLink`.

### 3.2 Entradas por carga completa → `/?ir=<código>`

El borde deja de servir las rutas internas, así que lo que hoy las abre con una carga completa pasa a
entrar por la raíz:

| Origen | Hoy | Queda |
|---|---|---|
| Correos de tickets (4 botones, `TicketEmailTemplates`) | `/tickets` | `/?ir=tickets` |
| Atajo del manifest «Seguimiento» | `/daily-log/seguimiento` | `/?ir=seguimiento` |
| Atajo del manifest y 3 enlaces «Diagnóstico» (barra PWA, fila de captura) | `/diagnostico` | `/?ir=diagnostico` |
| Login, último recurso si el router falla | `window.location.href = '/home'` | `'/'` |

La raíz (`''`) pasa de `redirectTo: 'home'` a una **función de redirección pura**: código conocido →
su ruta; cualquier otro o ninguno → `/home` (lo de hoy). **Lista cerrada**: no se aceptan rutas
arbitrarias en `ir` ⇒ no hay redirección abierta. Los guards siguen aplicando (`/?ir=tickets` sin sesión
→ `/tickets` → `authGuard` → `/login`, igual que hoy con `/tickets`). El redirect del router descarta la
query original (`createQueryParams` solo copia la del destino), así que `ir` no queda colgando.

`/login` y `/reset-password?token=` siguen llegando tal cual desde `CorreosCuenta`: son públicas.

### 3.3 Borde — la lista blanca pasa a ser la de entradas públicas

`RUTAS_PUBLICAS` (`src/app/core/navegacion/rutas-publicas.ts`) es la **única fuente**: la importa la
estrategia (qué rutas se muestran en la barra) y la lee `scripts/rutas-spa-nginx.js` con el compilador
de TypeScript (qué rutas sirve nginx). **Fail-closed**: si la lista no es un array literal de textos,
está vacía, repite una ruta, nombra una ruta que no existe top-level en `app.config.ts` o una ruta con
hijos, el script corta con el motivo (y con él el job `tests` del deploy).

El `map` de nginx queda en `/` + `^/(?:login|password-recovery|reset-password)/?$`. El resto del borde
(bloque 4 con el `if`, `404.html`, Dockerfile, `.dockerignore`) no cambia: cambia qué lista se genera.

## 4. Qué cambia para el usuario (consecuencia directa del pedido)

- La barra muestra `/` siempre, salvo en el login y en la recuperación de contraseña.
- **Abrir un enlace del menú en pestaña nueva** (Ctrl+clic / clic central) abre el **inicio**, no esa
  pantalla. Un favorito o una URL copiada de una pantalla interna lleva al inicio.
- **Favorito viejo** de una ruta interna: con la PWA instalada sigue abriendo la pantalla (el Service
  Worker sirve la app); sin ella, página 404 con «Ir al inicio».
- Atrás/adelante y F5: **igual que hoy**.
- Anónimo: `/config`, `/dashboard`, etc. → 404, igual que `/env`.

## 5. Qué NO cambia

- Health checks: `/` (TaskDef y target group) y `/health` (HEALTHCHECK del Dockerfile).
- Backend (salvo el enlace de los correos de tickets), API, auth, BD, migraciones.
- Service Worker (`navigationUrls`): con la PWA instalada las navegaciones las responde el SW.
- **Los nombres de ruta siguen dentro del JavaScript** (toda SPA los publica en su bundle). Esto quita
  la enumeración desde el servidor y la ruta de la barra; la protección de los datos sigue siendo la API
  (JWT + firma de plataforma).

## 6. Archivos

| Archivo | Cambio |
|---|---|
| `frontend/src/app/core/navegacion/rutas-publicas.ts` | **Nuevo.** `RUTAS_PUBLICAS` y `URL_VISIBLE`. |
| `frontend/src/app/core/navegacion/funciones/ruta-enmascarada.funcion.ts` (+ spec, README) | **Nuevo.** Puras: `esRutaPublica`, `leerRutaReal`, `conRutaReal`, `sinRutaReal`, `rutaDeEntrada`, `redirigirDesdeLaRaiz`. |
| `frontend/src/app/core/navegacion/ruta-enmascarada.location-strategy.ts` (+ spec de integración) | **Nuevo.** La estrategia. |
| `frontend/src/app/app.config.ts` | Provider de `LocationStrategy` + raíz con `redirigirDesdeLaRaiz`. |
| `frontend/src/app/features/auth/login/login.component.ts` | Último recurso `'/home'` → `'/'`. |
| `frontend/src/app/shared/components/pwa-barra-estado/*.html`, `fila-captura-pendiente/*.html` | `href="/diagnostico"` → `href="/?ir=diagnostico"`. |
| `frontend/src/manifest.webmanifest` | Atajos → `/?ir=seguimiento`, `/?ir=diagnostico`. |
| `frontend/scripts/rutas-spa-nginx.js` | Lista blanca = raíz + `RUTAS_PUBLICAS` validadas contra el router. |
| `frontend/scripts/tests/rutas-spa-nginx.test.js` | Casos nuevos (ver §7). |
| `frontend/scripts/servir-pwa-local.js` | Misma lista que nginx. |
| `frontend/nginx.conf` | Solo comentarios (criterios del bloque 4). |
| `backend/src/ZooSanMarino.Infrastructure/Services/TicketEmailTemplates.cs` (+ test) | Enlace → `/?ir=tickets`. |
| `.github/workflows/deploy-production.yml` | C2/C5: la «ruta del SPA» pasa a `/login`; C7: internas → 404, entradas → 200. |

## 7. Casos de prueba

**Unitarios (Karma):**
1. `esRutaPublica`: `/login`, `/login/`, `/login?x=1`, `/reset-password?token=a`, `/password-recovery`
   → sí; `/`, `/home`, `/config/users`, `/login/x`, `/loginx`, `/Login`, `''` → no.
2. `rutaDeEntrada`: `tickets`, `seguimiento`, `diagnostico` → su ruta; `undefined`, `''`, `'/config'`,
   `'//evil.test'`, `'https://evil.test'`, `'toString'`, `['tickets']` → `null`.
3. `redirigirDesdeLaRaiz`: sin `ir` → `/home`; `ir=tickets` → `/tickets`; desconocido → `/home`.
4. `conRutaReal`/`sinRutaReal`/`leerRutaReal`: no mutan, conservan las claves del router, toleran
   `null`/`undefined`/no-objetos, ignoran una clave propia que no sea una ruta.

**Integración (Router real + estrategia + `MockPlatformLocation`):**
5. Navegar a `/config/users` → barra `/`; `router.url` y `location.path()` = `/config/users`.
6. Navegar a `/login` → barra `/login`, sin clave propia en el estado.
7. Atrás/adelante → el router vuelve a la ruta real de cada entrada; la barra sigue en `/`.
8. «Recargar» (router nuevo sobre la misma entrada) → abre la ruta guardada.
9. Query interna (`/reportes?lote=12`) → barra `/`, ruta real con la query.
10. Redirect (`/viejo` → `/nuevo`) → barra `/`, ruta real `/nuevo`.
11. Guard que rechaza y manda a `/login` → barra `/login`.
12. `href` de `routerLink`: interna → `/`; `/login` → `/login`.
13. La clave propia no llega a `location.getState()` ni al `extras.state` de la navegación al volver atrás.
14. `/?ir=diagnostico` → `/diagnostico` con la barra en `/`.

**Node (`node --test`, job `tests`):**
15. Borde: `/`, `/login`, `/login/`, `/password-recovery`, `/reset-password` → SPA; **todas** las demás
    top-level del router → 404; sondas de reconocimiento → 404.
16. Fail-closed de `RUTAS_PUBLICAS`: pública inexistente, con hijos, vacía, repetida, no literal → lanza.
17. Ningún `href="/…"` de los templates ni atajo del manifest apunta a una ruta que el borde no sirve
    (evita volver a abrir con carga completa algo que ahora da 404).
18. Cableado nginx/Dockerfile/`.dockerignore`/`404.html` (se conservan).

**Backend (xUnit):**
19. Los 4 correos de tickets enlazan `{applicationUrl}/?ir=tickets` (sin barra doble).

**Navegador (build de producción + `servir-pwa-local.js`, réplica del borde):**
20. `/config` y `/dashboard` → 404; `/`, `/login` → 200.
21. `/?ir=diagnostico` → Diagnóstico con la barra en `/`; F5 → sigue en Diagnóstico; atrás/adelante.
22. Login → «¿Olvidaste tu contraseña?» → `/password-recovery` visible; atrás → `/login`.

**Gate del borde (CI):** C2/C5 usan `/login`; C7 suma las internas → 404 y las entradas → 200.

## 8. Riesgos y reversión

- **Un error en la estrategia rompe la navegación de todos.** Mitiga: integración con el Router real
  (§7.5-14), prueba en navegador sobre el build de producción y el gate C7 antes del push a ECR.
- **Código que lea `window.location.pathname` para decidir algo** vería `/`. Barrido hecho: nadie lo usa
  para lógica (`diagnostico` solo lo copia al informe; `reload()` recarga la entrada actual, que
  conserva la ruta en `history.state`).
- **Reversión:** revertir el commit. Sin estado, datos ni migraciones.
