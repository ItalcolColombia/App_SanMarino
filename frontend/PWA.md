# PWA de ItalGranja — operación

Qué hay, cómo se prueba y qué hacer cuando algo se rompe en campo.
Diseño y fundamentos: [`pwa_f1_shell_plan.md`](../fase_de_desarrollo/pwa_f1_shell_plan.md) (shell) y
[`pwa_f2_consulta_offline_plan.md`](../fase_de_desarrollo/pwa_f2_consulta_offline_plan.md) (consulta offline).

---

## Qué funciona sin conexión (y qué no)

| | Sin red |
|---|---|
| Abrir la app, navegar, ver la pantalla de diagnóstico | ✅ el shell está precacheado |
| **Volver a ver una consulta ya hecha** (lotes, seguimientos, inventario, movimientos…) | ✅ hasta **16 h** después |
| Consultar algo que nunca se abrió con red | ❌ **requiere conexión** |
| Reportes, costos y liquidaciones | ❌ **nunca se guardan** (ver abajo) |
| Guardar cualquier cosa | ❌ **requiere conexión** |
| Cuentas de **super admin** o con **varias empresas** | ❌ **no guardan nada** (ver abajo) |

### Quién puede usar la consulta offline

**Las cuentas con alcance global o multiempresa no guardan consultas en el dispositivo** (decisión D6).
No es una restricción de comodidad: la partición de la caché evita que una sesión **lea** lo de otra,
pero no evita que el mismo equipo **acumule** los datos de todas las empresas que ese usuario visita, y
el dato en reposo **no está cifrado** (decisión D3). Un super admin con la app instalada terminaría con
el snapshot de la operación completa en una tablet de granja.

Basta una señal (`isSuperAdmin`, `hasMultipleCompanies`, o más de un id en `companyIds`/`companies`)
para bloquear: las llena el backend por caminos distintos y el criterio conservador es el correcto
cuando la pregunta es "¿bajo datos de más?". Al detectar una cuenta no elegible **se purga** lo que
tuviera guardado de antes.

El operario de una sola empresa —que es el destinatario de todo esto— funciona igual que siempre.

### Almacenamiento persistente

La app pide `navigator.storage.persist()` **cuando hay sesión**, para que el navegador no pueda
desalojar la base ante presión de disco. Sin esa concesión el desalojo es **silencioso**: sin error ni
log, la pantalla aparece vacía en la granja como si nunca se hubiera consultado nada.

En `/diagnostico`, el campo **«Se pidió persistencia»** distingue *«el navegador la negó»* de *«todavía
no»* — ante un reporte de campo llevan a diagnósticos opuestos. Chrome la concede automáticamente si la
app está **instalada**, así que el alistamiento en oficina (instalar y entrar una vez con red) es lo que
la asegura.

**Que no se pueda GUARDAR sin red es deliberado, no una limitación pendiente de pulir.** La captura offline (outbox +
sincronización diferida) está bloqueada por el backend: no tiene idempotencia, ni control de
concurrencia, ni tombstones, y todos los saldos son contadores read-modify-write con `Math.Max(0, …)`
—o sea, no reversibles aritméticamente—. Encolar escrituras sobre ese modelo multiplicaría por N
dispositivos un problema de integridad que hoy ya es explotable con dos pestañas. El detalle, medido
contra el código, está en `fase_de_desarrollo/pwa_offline_first_plan.md` §4.A y §4.B; son las fases
F0.A/F0.B — de los cuales **A1 y A2 ya están hechos** (`44b2400`) y A3-A10 no. Son prerrequisito de
F3 (escritura offline); la lectura offline (F2) ya está entregada y no depende de ellos, porque no
escribe nada.

---

## Piezas

| Archivo | Qué es |
|---|---|
| `ngsw-config.json` | Qué precachea el Service Worker. **Sin `dataGroups`** — ver abajo |
| `src/manifest.webmanifest` | Hace instalable la app. Iconos en `src/assets/pwa/` |
| `scripts/generar-iconos-pwa.ps1` | Regenera los 5 iconos desde la marca del repo. Si cambia la marca, se corre esto |
| `scripts/verificar-ngsw.js` | Gate de integridad. Corre en el build y lo **hace fallar** |
| `src/app/core/pwa/` | Servicios de actualización, instalación y conexión + funciones puras con tests |
| `/diagnostico` | Pantalla de soporte. Sin `authGuard`, sin datos de negocio |
| `src/app/shared/offline/` | Consulta offline (F2): caché de GET en IndexedDB, particionada por `{userId, companyId, paisId}` |
| `scripts/verificar-lista-cacheable.js` | Contrasta la lista blanca contra los endpoints que la app pide de verdad |

### Consulta offline (F2)

Las respuestas **GET** de los endpoints operativos se guardan en IndexedDB y se sirven **solo cuando
la petición falla por falta de red** (`status === 0`). Nunca se sirve caché habiendo conexión, y un
4xx/5xx tampoco la activa: son respuestas del servidor, y taparlas escondería el problema real.

- **Partición `{userId, companyId, paisId}`, fail-closed.** Sin los tres, no se guarda ni se lee.
- **TTL duro de 16 h.** Vencida no se sirve; se propaga el error de red.
- **Se purga** en logout (todo) y al cambiar de empresa (esa partición).
- **Lista blanca.** Al agregar un módulo, corré `node scripts/verificar-lista-cacheable.js`: un nombre
  mal escrito no rompe nada y solo se nota en la granja. Fuera de la lista quedan, por escrito, el
  dinero (costos, liquidaciones, contabilidad), la identidad (auth/users/roles/permisos), los
  reportes y las herramientas internas.

### ⛔ Prohibido: `dataGroups` sobre `/api/**`

La caché del Service Worker **indexa por URL e ignora los headers**. La empresa activa viaja en
`X-Active-Company`, así que un `dataGroup` le serviría al operario de la empresa B la respuesta
cacheada de la empresa A: fuga entre empresas, silenciosa y sin rastro. `verificar-ngsw.js` corta el
build si aparece alguno. Por eso la consulta offline (F2) vive en IndexedDB **particionada por
`{userId, companyId, paisId}`**, donde la clave la elegimos nosotros y sí se puede aislar.

---

## Probar en local (build de producción)

El dev server **no** registra el Service Worker (`enabled: !isDevMode()`). Para probar la PWA de punta
a punta hace falta servir un build de producción — `localhost` cuenta como contexto seguro, así que no
hace falta desplegar:

```bash
cd frontend && yarn build && node scripts/verificar-ngsw.js && npx http-server dist/browser -p 4400 -c-1
```

Chequeos que tienen que dar:

1. DevTools → Application → Service Workers: **activated and is running**.
2. Recargar: `navigator.serviceWorker.controller` deja de ser `null`.
3. Application → Manifest: sin errores, iconos 192/512 resueltos.
4. Network → **Offline** → recargar: la app carga igual y `/diagnostico` responde.
5. `/chunk-que-no-existe.js` → **404**, nunca el `index.html`.

---

## 🔴 Kill switch — `make pwa-panic`

**Cuándo:** un deploy dejó el Service Worker sirviendo un bundle roto. Los dispositivos de campo se
quedan con esa versión y **no se los puede alcanzar desde el servidor**: el SW responde desde su propia
caché antes de tocar la red.

**Cómo funciona:** se reemplaza `ngsw-worker.js` por `safety-worker.js` en el contenedor. El navegador
detecta que el contenido del worker registrado cambió, lo instala, y ese se desregistra a sí mismo.
nginx sirve ambos con `Cache-Control: no-cache` (`nginx.conf`, bloque 1), así que se propaga en el
siguiente arranque de la app en cada dispositivo.

```bash
make pwa-panic       # imprime el procedimiento y los comandos exactos
```

**`safety-worker.js` lo emite `@angular/service-worker`, no este repo.** Hubo una versión propia acá y
se eliminó: el builder escribe el suyo **encima** del asset, *después* de haberlo hasheado para
`ngsw.json` → SHA1 divergente → el SW arranca en safe mode y se desactiva solo, en silencio. Lo detectó
`verificar-ngsw.js` la primera vez que corrió. El de Angular hace exactamente lo que hace falta:
`unregister()` + borrar **solo** las cachés `ngsw:`.

### La regla que no se puede romper

**El kill switch NUNCA debe borrar IndexedDB.** Hoy la base local está vacía, pero la fase F3 guarda
ahí el outbox: las capturas que el galponero hizo sin red y todavía no se sincronizaron. El servidor
nunca las vio; borrarlas es destruir trabajo de campo real e irrecuperable. CacheStorage sí se puede
borrar —se reconstruye bajando la app de nuevo—. Si alguna vez aparece un `indexedDB.deleteDatabase`
en ese camino, es un bug, no una mejora.

---

## Si un operario dice "no me anda"

1. Que abra **`/diagnostico`** (funciona sin red y sin sesión) y toque **Copiar diagnóstico**.
2. El campo a mirar primero es **Modo sin conexión**:
   - *Activo y controlando la app* → el SW está bien; el problema es otro.
   - *Instalándose* → normal en la primera visita; que recargue.
   - ***Registrado pero NO controla*** → safe mode. Casi siempre significa que un archivo del build
     cambió después de que se calculó su hash. Verificar con `verificar-ngsw.js` sobre la imagen
     desplegada; si se confirma, `make pwa-panic` y re-deploy.
3. `buildId` del diagnóstico contra `curl -sk https://<alb>/version.json` dice si el dispositivo tiene
   la versión publicada o quedó en una vieja.
