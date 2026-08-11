# Plan — PWA F1: shell instalable, autoactualizable y con kill switch

**Fecha:** 2026-08-09
**Estado:** EN EJECUCIÓN
**Depende de:** `pwa_offline_first_plan.md` (§5.1 y §8, fase **F1**). F0.C está cerrada (`76a2903`) y F0.B parcial (`f139dfd`).

---

## 1. Qué se entrega y qué NO

Lo que el repo tiene hoy es **toda la infraestructura de borde para sostener un Service Worker y ningún
Service Worker**: `nginx.conf` ya sirve `ngsw.json`, `ngsw-worker.js`, `safety-worker.js`,
`worker-basic.min.js` y `manifest.webmanifest` con `no-cache` y `try_files $uri =404`; la CSP ya declara
`worker-src 'self'` y `manifest-src 'self'`; `scripts/build-version.js` ya está partido en `prepare`/`emit`
justo para no mutar el output después del build. Falta la pieza que todo eso protege.

### ✅ Alcance de esta entrega (F1)

| # | Entrega | Criterio de aceptación |
|---|---|---|
| 1 | `@angular/service-worker` registrado, solo en builds de producción | `navigator.serviceWorker.controller` no nulo tras el segundo load; `ngsw.json` presente en `dist/browser` |
| 2 | `ngsw-config.json` con **assetGroups únicamente** | Grep: **cero** `dataGroups`. Ver §3 (riesgo multi-tenant) |
| 3 | `manifest.webmanifest` + iconos 192/512 `any` y `maskable` + apple-touch 180 | Instalable en Chrome Android (criterios de instalabilidad completos) |
| 4 | Shell operable sin red | Con red cortada, `/` carga la app y `/diagnostico` responde |
| 5 | Actualización **no destructiva** por `SwUpdate` + banner | El usuario decide cuándo aplicar; sin `reload()` forzado |
| 6 | `VersionCheckService` **eliminado en el mismo cambio** | Grep = 0. Dos autoridades de recarga producen bucles |
| 7 | `safety-worker.js` (kill switch) publicado desde el día uno | Desregistra el SW y borra CacheStorage; **NO toca IndexedDB** |
| 8 | Pantalla **Diagnóstico** sin red y sin datos de negocio | `/diagnostico` muestra build, estado del SW, cuota, persistencia |
| 9 | Verificador de integridad de `ngsw.json` en el build | El build **falla** si un SHA1 declarado no coincide con el archivo en disco |
| 10 | Indicador de conexión | Banner al perder/recuperar red |

### ⛔ Fuera de alcance (y por qué, explícito)

**Escritura offline (outbox + push) NO entra.** No es una decisión de tiempo: `pwa_offline_first_plan.md`
§4.A/§4.B documenta que el backend no tiene idempotencia (`Idempotency-Key` no existe), ni control de
concurrencia (`grep IsConcurrencyToken|RowVersion|xmin` = 0), ni tombstones (los borrados son físicos), y
que todos los saldos son contadores read-modify-write con `Math.Max(0,...)` — o sea **no reversibles
aritméticamente**. Encolar escrituras sobre ese modelo multiplica por N dispositivos un problema de
integridad que ya es explotable hoy con dos pestañas (A1). F2 (snapshot/pull) y F3 (outbox) siguen
bloqueadas por F0.A y F0.B.

**Consecuencia honesta:** al terminar F1 la app es una PWA completa e instalable cuyo **shell** funciona
sin red; los **datos** siguen requiriendo conexión. Eso es lo máximo que se puede entregar sin tocar la
integridad de los datos.

---

## 2. Archivos

**Nuevos**
```
frontend/ngsw-config.json
frontend/src/manifest.webmanifest
frontend/src/safety-worker.js
frontend/src/assets/pwa/{icon-192,icon-512,icon-maskable-192,icon-maskable-512,apple-touch-icon-180}.png
frontend/scripts/generar-iconos-pwa.ps1          # reproducible, no arte a mano
frontend/scripts/verificar-ngsw.js               # gate de integridad (§9 del plan madre)
frontend/src/app/core/pwa/pwa-actualizacion.service.ts
frontend/src/app/core/pwa/pwa-instalacion.service.ts
frontend/src/app/core/pwa/conexion.service.ts
frontend/src/app/core/pwa/funciones/{decidir-actualizacion,formatear-bytes,resumir-estado-sw}.funcion.ts
frontend/src/app/core/pwa/models/pwa.model.ts
frontend/src/app/shared/components/pwa-barra-estado/pwa-barra-estado.component.{ts,html,scss}
frontend/src/app/features/diagnostico/diagnostico-page.component.{ts,html}
frontend/src/tests/pwa/*.spec.ts
```

**Modificados**
```
frontend/package.json          + @angular/service-worker
frontend/angular.json          serviceWorker en production y docker; assets manifest + safety-worker
frontend/src/index.html        link manifest, theme-color, apple-*
frontend/src/app/app.config.ts provideServiceWorker + ruta /diagnostico
frontend/src/app/app.component.{ts,html}  VersionCheckService -> PwaActualizacionService + barra
frontend/Dockerfile            COPY ngsw-config.json + verificar-ngsw
frontend/.dockerignore         lista blanca de scripts/ (regla de 76a2903)
Makefile                       target pwa-panic
```

**Eliminado**: `frontend/src/app/core/services/version-check.service.ts`

---

## 3. Decisiones de diseño (y su fundamento)

**D-A · Sin `dataGroups` sobre `/api/*`.** La caché del SW indexa por URL e **ignora headers**; la empresa
activa viaja en `X-Active-Company`. Un `dataGroup` le serviría al operario de la empresa B la respuesta
cacheada de la empresa A. Es el mismo fail-closed que `InventarioCatalogoScopeCalculos` en el backend.
Cuando llegue F2, los datos van a IndexedDB particionada por `{userId, companyId}`, no al SW.

**D-B · El SW se habilita con `!isDevMode()`, no con `BUILD_ID !== 'dev'`.** Así un build de producción
servido en `localhost` (que **es** contexto seguro) registra el SW y se puede probar en vivo, mientras el
dev server nunca lo hace. Atar el registro al `BUILD_ID` haría imposible probar la PWA sin desplegar.

**D-C · `registerWhenStable:30000`.** El registro espera a que la app quede estable; el tope de 30 s
garantiza que se registre igual si algo mantiene la zona ocupada (esta app tiene polling).

**D-D · La actualización NO recarga sola.** `VersionCheckService` hacía `window.location.reload()` a 1 s
sin preguntar: un galponero a mitad de un formulario perdía la captura. Ahora `SwUpdate.versionUpdates`
levanta un banner y **el usuario** aplica. `VERSION_INSTALLATION_FAILED` y `UnrecoverableStateError` se
tratan explícitamente (el segundo es el único caso que sí justifica recarga forzada, porque el SW ya no
puede servir).

**D-E · El kill switch no borra IndexedDB.** Requisito del plan madre §5.1: el día que exista el outbox,
un `pwa-panic` que borre la base destruiría capturas de campo no sincronizadas. Se escribe hoy, con la
base todavía vacía, para que la regla ya esté cuando importe.

**D-F · `/diagnostico` sin `authGuard`.** Es la pantalla a la que se recurre cuando *nada* funciona
(sesión vencida sin red, SW en safe mode). Solo expone build, estado del SW y cuota del dispositivo:
**cero datos de negocio**, así que no hay nada que proteger.

**D-G · `version.json` sigue existiendo y sigue fuera de todo assetGroup.** Es la sonda de despliegue de
`verificar-deploy-front-alb-version-json`. Lo consume ahora `PwaActualizacionService` como *fallback*
cuando el navegador no soporta SW, y **sin recargar**: solo levanta el mismo banner.

---

## 4. Casos de prueba

**Unitarios (Karma) — funciones puras**
1. `decidirActualizacion`: `VERSION_READY` ⇒ ofrecer; `VERSION_DETECTED` ⇒ nada; `VERSION_INSTALLATION_FAILED` ⇒ nada + log; hash igual ⇒ nada.
2. `decidirActualizacion` con `buildId` publicado == compilado ⇒ no ofrece (evita el bucle que tenía el servicio viejo).
3. `formatearBytes`: 0, <1 KB, MB, GB, `undefined`.
4. `resumirEstadoSw`: sin soporte / registrado sin controller / controlando / safe mode.

**En vivo (navegador, build de producción servido en localhost)**
5. Primer load ⇒ SW `activated`; segundo load ⇒ `controller` no nulo.
6. `ngsw.json` accesible, con `assetGroups` y **sin** `dataGroups`.
7. Manifest parseado por el navegador; iconos 192/512 resuelven 200.
8. **Red cortada** ⇒ recarga de `/` sirve la app desde caché; `/diagnostico` responde.
9. Un asset con hash inexistente ⇒ 404 (no el index).
10. `safety-worker.js` ⇒ desregistra y deja `controller` nulo, conservando una base IndexedDB de prueba.

**De build**
11. `verificar-ngsw.js` detecta un archivo mutado después del build (se simula tocando un byte).

---

## 5. Riesgos

| Riesgo | Mitigación |
|---|---|
| SW cacheando el bundle viejo tras un deploy | `ngsw.json`/`ngsw-worker.js` con `no-cache` (ya en nginx) + verificador de integridad + banner de actualización |
| SW en safe mode silencioso | `/diagnostico` lo muestra explícito; `verificar-ngsw.js` corta el build antes de publicar la causa más común |
| Fuga entre empresas por caché | Sin `dataGroups` (D-A) |
| Kill switch inutilizable | `safety-worker.js` publicado desde hoy con `no-cache` + `make pwa-panic` documentado |
