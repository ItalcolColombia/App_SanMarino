# Plan — PWA offline-first con sincronización diferida

**Fecha:** 2026-07-26
**Estado:** ANÁLISIS COMPLETO / DISEÑO PROPUESTO — pendiente de decisiones del usuario antes de implementar
**Alcance pedido:** que los módulos operativos funcionen sin red y sincronicen al recuperar conexión; PWA autoactualizable; **no** app móvil nativa; que lo que se construya de ahora en más nazca sirviendo para los dos modos.

**Módulos operativos nombrados por el usuario:** gestión de lotes · seguimiento levante · seguimiento producción · pollo engorde · reproductora pollo engorde · gestión de lote pollo engorde · gestión de inventario · gastos de inventario · ventas · ventas de aves · movimientos de huevos · movimientos de aves · gestión de granjas.

---

## 0. Método del análisis

14 agentes en paralelo sobre el repo (981 lecturas de código): 8 de inventario por área (postura, engorde, inventario, movimientos/ventas, granjas/catálogos, auth/sesión, plataforma backend, build/hosting), 3 de riesgo (volumetría medida contra la BD local, clasificación de reglas de negocio, precedentes de carga diferida) y 3 de crítica adversarial contra la arquitectura candidata (correctitud de datos, seguridad/multi-tenant, entrega/operación).

La volumetría **no está estimada**: se midió con `octet_length(row_to_json(x)::text)` fila por fila contra `sanmarinoapplocal:5433` (refresh de dump de prod), en modo solo lectura.

---

## 1. Punto de partida (verificado)

| Dimensión | Estado real |
|---|---|
| Infraestructura PWA | **Cero.** Sin `@angular/service-worker`, sin `ngsw-config.json`, sin manifest, sin iconos, sin registro en `main.ts`. Grep de `indexedDB|idb|Dexie|localForage` sobre `frontend/src` = 0 resultados |
| Capa de datos en el front | Ninguna. Todo va directo a `HttpClient` vía `core/services/base-http.service.ts` |
| Autoactualización | `core/services/version-check.service.ts`: polling de `index.html` cada 5 min + `window.location.reload()` forzado a 1 s, sin confirmación |
| App móvil previa | `zootecnicoapp/` es un scaffold Flutter vacío (1 commit, mayo-2026, `pubspec.yaml` sin dependencias). No hay nada que preservar |
| Escala | 99 entidades de dominio · 80 controllers · 44 features en el front |
| Idempotencia en el backend | **Ninguna.** Sin `Idempotency-Key`, sin clave de deduplicación, sin correlation id en ninguna entidad operativa |
| Control de concurrencia | **Ninguno.** `grep IsConcurrencyToken|RowVersion|xmin` sobre `backend/src` = 0 resultados |
| Tombstones / soft delete en tablas operativas | No existen. Los borrados de seguimiento e inventario son físicos |

**Conclusión del punto de partida:** es greenfield en el front (ventaja: no hay que convivir con un esquema local previo) y es terreno hostil en el backend (todo lo que una sincronización necesita —idempotencia, concurrencia, tumbas, ids del cliente— hay que construirlo).

---

## 2. Los cinco hallazgos que definen la arquitectura

### 2.1 La sesión se autodestruye al perder red (bloqueador #1, y es client-side)

`core/auth/session-timeout.service.ts`: heartbeat a `GET /api/session/heartbeat` cada 90 s; con `status === 0` (sin red) cuenta fallos y a los **2 fallos** (`MAX_HEARTBEAT_FAILS = 2`, ~3 min) llama `endSession('sin_conexion')`, que **borra el storage** y redirige al login. Además `IDLE_LIMIT_MS = 5 min` cierra sesión por inactividad, y `auth.interceptor.ts:77-79` convierte **cualquier** 401 en fin de sesión.

Efecto: en granja sin señal el usuario queda deslogueado en ~3 minutos, y el mismo camino que lo expulsa **borra el almacenamiento donde viviría la cola offline**.

Agravantes: JWT de 60 min con `ClockSkew = 0` y **sin refresh token** (grep de `refresh|jti|revoke|blacklist` sobre `AuthService.cs` = 0); reCAPTCHA obligatorio en producción, o sea que no hay re-login sin alcance a Google; `authGuard` decodifica el `exp` localmente y hace logout destructivo al navegar.

### 2.2 Todos los saldos son contadores mutables read-modify-write, sin concurrencia

- `InventarioGestionService.RegistrarConsumoAsync` (`:1209-1214`): `FirstOrDefaultAsync(stock)` → `if (stock.Quantity < req.Quantity) throw` → `stock.Quantity -= req.Quantity`. Sin transacción propia, sin `SELECT FOR UPDATE`, sin `CHECK quantity >= 0`.
- El saldo de aves de **levante** es un contador (`AplicarDescuentoLevanteAsync`, con `Math.Max(0, ...)`): **no idempotente y no reversible aritméticamente** por el clamp.
- El índice de la clave natural de `inventario_gestion_stock` **no es único** (`InventarioGestionStockConfiguration.cs:30`) y el upsert es buscar-o-insertar → dos escrituras concurrentes crean dos filas de stock para la misma ubicación, y todas las lecturas toman la primera con `FirstOrDefault`: **la segunda queda invisible**. Esto ya es explotable hoy con dos pestañas.

**Consecuencia para el diseño:** la idempotencia por `client_op_id` protege contra el **reintento del mismo comando**; no protege contra **comandos distintos concurrentes** sobre el mismo saldo, que es justo lo que offline multiplica. Dos galponeros que capturan consumos offline del mismo galpón producen *lost update* garantizado con dos `client_op_id` perfectamente válidos.

### 2.3 Buena parte de la lógica vive en PostgreSQL, no en C#

10 triggers se disparan en cada escritura; varios son **acumulativos por delta** (no idempotentes bajo reenvío):

| Trigger / función | Qué hace | Riesgo offline |
|---|---|---|
| `tr_espejo_huevo_produccion_aiud` | Mantiene `espejo_huevo_produccion` sumando/restando NEW/OLD | Un reenvío **infla el histórico de huevos** y habilita despachar huevo inexistente |
| `trg_lotes_sync_lote_postura_levante` | En la rama UPDATE hace `aves_h_actual = NEW.hembras_l` | **Editar un lote resetea el saldo de aves.** Ya causó un incidente: `fix_datos_postura_verenice_jul26.sql:45-59` tuvo que hacer backup/restore de saldos antes de un simple fix de fecha |
| `fn_cruce_reproductora_a_engorde` | Escribe los primeros 7 días del lote de engorde consolidando N lotes reproductora | El cliente no puede simularlo ni anticiparlo |
| `fn_acumulado_entradas_alimento` | Acumula con `WHERE h.id <= p_hasta_id` | Acumula por **orden de inserción**, no por fecha: la cola sincronizada tarde produce reportes falsos |
| `fn_lote_ave_engorde_id_desde_ubicacion` | `ORDER BY lote_ave_engorde_id DESC LIMIT 1` | Imputa el consumo al **lote más reciente de la ubicación**: el consumo del lote saliente se carga al lote entrante |
| `fn_seguimiento_diario_engorde` | 561 líneas: la tabla diaria de engorde **no existe como datos**, se calcula en SQL | No replicable en el navegador |

### 2.4 Los identificadores y los códigos los genera el servidor, después del INSERT

- PKs: todo es `int identity`. `seguimiento_diario_produccion.id` usa `UseIdentityAlwaysColumn()` → Postgres **rechaza** cualquier id del cliente, incluso con `OVERRIDING`.
- Códigos de documento: `numero_movimiento = MPE-{yyyyMMdd}-{Id:D6}` se calcula con un **segundo `SaveChanges`** tras el INSERT, sobre columna `NOT NULL UNIQUE`. Igual `MOV-`, `HUE-`, `numero_traslado`.
- Consecutivos: el "siguiente número de despacho" es `MAX(id)+1` leído en línea. `numero_corrida` de Panamá también.
- `TransferGroupId` de traslados y `FacturaId` de venta engorde son `Guid.NewGuid()` **del servidor**.
- `seguimiento_diario_levante.lote_id` es un **string que guarda un int** (comparaciones `l.LoteId.ToString() == loteId`): la referencia no es una FK tipada sino texto.

**Consecuencia:** la idea de "ids negativos temporales + remapeo en el cliente" es inviable — habría que reescribir el id dentro de columnas de texto, dentro de `metadata` jsonb y dentro de referencias ya materializadas por triggers en `lote_registro_historico_unificado`.

### 2.5 El tamaño **no** es el problema

Volumetría medida (bytes por fila del JSON tal como viaja hoy):

| Tabla | B/fila | Sin nulls |
|---|---|---|
| `seguimiento_diario_levante` | 1.863 | 685 (**63 % del payload son nulls**) |
| `seguimiento_diario_produccion` | 1.654 | 1.009 |
| `seguimiento_diario_aves_engorde` | 1.167 | 813 |
| `lote_registro_historico_unificado` | 683 | 494 |
| `inventario_gestion_movimiento` | 442 | 342 |

Derivado: un lote de postura de **ciclo completo** = 477 filas ≈ **790 KB**. Un lote de engorde completo ≈ **120 KB**. La granja más cargada de todo el sistema (SAN GUILLERMO) = **4 MB** con 10 meses de historia.

Snapshot simulado contra `user_farms` real:

| Perfil | Granjas | JSON | En IndexedDB (×1,5-2,5) |
|---|---|---|---|
| Operario de campo típico | 1-3 | 2-4 MB | 5-10 MB |
| Peor caso medido (90 d) | 10 | 8,2 MB | ~20 MB |
| Peor caso sin ventana | 10 | 15 MB | ~40 MB |

Catálogos + estructura + 1 guía genética = **~0,4 MB**. Ítems de inventario: 45-148 por empresa. `master_list_options`: 70 filas totales.

**Regla de ventana derivada de los datos:** la ventana correcta **no es temporal**. Un lote de producción vive 301 días; cortarlo a 90 rompe los acumulados que el formulario diario necesita (saldo de alimento, mortalidad acumulada, edad). La unidad es **el ciclo de vida completo del lote ACTIVO** — y los activos son pocos (114 abiertos vs 20 cerrados; 8,1 abiertos por granja).

Un ahorro gratis: `Program.cs` no configura `DefaultIgnoreCondition`, así que los DTOs viajan con todos los nulls. Activar `JsonIgnoreCondition.WhenWritingNull` recorta el snapshot ~30 % sin tocar una sola tabla.

**El cuello de botella real no es el espacio, son:** la eviction de 7 días de Safari iOS (que puede borrar capturas sin sincronizar si la PWA no está instalada), el tiempo de sincronización inicial sobre red mala, y la cadena acumulada de saldo de alimento de engorde.

---

## 3. Decisión de alcance: dos velocidades

El pedido es "todos los módulos offline". La respuesta honesta que sale del análisis es: **la lectura sí puede ir amplia desde temprano; la escritura tiene que ser deliberadamente angosta al principio.** No por conservadurismo, sino porque hay operaciones que estructuralmente no se pueden encolar.

### 3.1 CONSULTA offline — amplia (todos los módulos operativos)

Snapshot de solo lectura por alcance del usuario. Riesgo de integridad: **cero**. Cubre lo que más se usa en campo: ver el lote, el saldo de aves, el stock, el histórico, la guía genética, los movimientos recientes.

### 3.2 CAPTURA offline — lista blanca (v1)

| Operación | Por qué es encolable |
|---|---|
| Seguimiento diario **levante** | Clave natural (lote, fecha); una sola parte; sin contraparte remota |
| Seguimiento diario **producción** | Ídem (con la salvedad del índice único, §4.3) |
| Seguimiento diario **engorde** | Ídem |
| Seguimiento diario **reproductora engorde** | Ídem |
| **Consumo de alimento** asociado al seguimiento | Efecto secundario del mismo comando |
| **Gastos de inventario** (registro simple) | Una parte, sin contraparte |

### 3.3 ONLINE-ONLY — lista negra (la UI las deshabilita con "requiere conexión", **no** las encola)

| Operación | Por qué no se puede encolar |
|---|---|
| Cerrar / abrir lote (levante y producción) | Transacción multi-entidad: crea el LPP, cambia estado, ejecuta el arrastre de huevos, con dos `SaveChanges` |
| Traslado de aves entre granjas | Escribe **las dos patas** (origen y destino) en una transacción; la contraparte no está en el dispositivo |
| Traslado de inventario entre granjas | Workflow de **dos pasos con dos usuarios** y `TransferGroupId` generado por el servidor |
| Traslado de huevos | Ídem; además hoy el destino nunca recibe contrapartida |
| Ventas con reserva de aves | La validación anti-sobreventa resta movimientos en estado `Pendiente` **de otros usuarios** |
| Creación de lotes y de granjas | IDs, códigos ERP, consecutivos y `user_farms` los genera el servidor; el CRUD de ubicación son 3 funciones SQL sobre ~13 tablas |
| Cuadres y backfills | Por definición |

**Fundamento:** encolar una operación multi-parte cuya contraparte no participa del dispositivo es una fábrica de descuadres. Ejemplo concreto verificado: se envía alimento de la granja A (con red) a la B (sin red); B no puede ni ver el pendiente porque el `TransferGroupId` no existe en su dispositivo, y si dos personas de B "reciben offline", el `AnyAsync` anti-doble-recepción no los detecta hasta el push — el segundo revienta después de que el primero ya sumó el stock.

**Ventas y movimientos entran en v2**, una vez que el backend tenga la infraestructura de §4.

---

## 4. FASE 0 — Saneamiento previo (no negociable)

Las tres críticas adversariales coinciden en lo mismo desde ángulos distintos: **la PWA no agrega una funcionalidad, multiplica por N dispositivos un modelo de confianza y de integridad que ya está roto en un solo navegador.** Estos son los prerrequisitos, agrupados por frente. Ninguno requiere escribir una línea de PWA y todos tienen valor por sí solos.

### 4.A — Integridad de datos (backend + BD)

| # | Acción | Por qué |
|---|---|---|
| A1 | `CREATE UNIQUE INDEX` en la clave natural de `inventario_gestion_stock` (`farm_id, item, COALESCE(nucleo_id,''), COALESCE(galpon_id,'')`) previa consolidación de duplicados, + reemplazar buscar-o-insertar por `INSERT ... ON CONFLICT DO UPDATE` | **Bug explotable hoy con dos pestañas.** Sin esto ningún esquema de sync es seguro |
| A2 | Descuento de stock como **UPDATE atómico condicional** (`SET quantity = quantity - @q WHERE ... AND quantity >= @q RETURNING`), tratando 0 filas afectadas como rechazo | Elimina el read-modify-write |
| A3 | Corregir `trigger_lotes_to_lote_postura_levante`: la rama UPDATE deja de tocar `aves_*_actual` | Bug preexistente que offline convierte en sistemático |
| A4 | **Una sola autoridad por columna de saldo.** Sacar el `SaveChangesAsync` escondido en `ProduccionService.ObtenerInformacionLoteAsync` (una **lectura que escribe** y bumpea `updated_at`) | Una lectura que escribe hace inviable cualquier cursor de sincronización, y compite con `AplicarDescuentoLppAsync` sobre la misma columna |
| A5 | `deleted_at` + soft delete en `seguimiento_diario_levante`, `_produccion`, `_aves_engorde`, `inventario_gestion_movimiento` + tabla `sync_tombstones` alimentada por trigger `AFTER DELETE` | Hoy los borrados son físicos y algunos borran la fila **de otro lote/granja**: invisibles para cualquier cursor `updated_at` |
| A6 | Alinear el índice único de producción a `(lote_postura_produccion_id, fecha)` en vez de `(lote_id, fecha)`, con migración que detecte colisiones históricas | Hoy dos galpones del mismo lote base colisionan al sincronizar, y sale como **500**, no como 400 |
| A7 | Consolidar los **dos servicios que escriben `seguimiento_diario_levante`** con semántica de saldo distinta (`Program.cs:217` vs `:232`) | El mismo comando produce dos estados distintos según el endpoint. Prerrequisito para portar reglas a TS |
| A8 | `FechaOperacion` en `InventarioGestionConsumoRequest` (hoy **no tiene campo de fecha**) + `fn_acumulado_entradas_alimento` ordenando por `(fecha_operacion, id)` | Sin esto, toda captura offline con >1 día de retraso produce reportes falsos |
| A9 | `lote_ave_engorde_id` explícito en el payload + `fn_lote_ave_engorde_id_desde_ubicacion` filtrando por rango de vida del lote, fail-closed ante ambigüedad | Hoy imputa al lote más reciente de la ubicación: el consumo del lote saliente se carga al entrante |
| A10 | Reemplazar `tr_espejo_huevo_produccion_aiud` (acumulativo) por el recálculo derivado que **ya existe** en `EspejoHuevoProduccionSyncService` | Un espejo recalculable es inmune a reenvíos; uno acumulativo no lo es nunca |

### 4.B — Seguridad y sesión

| # | Acción | Por qué |
|---|---|---|
| B1 | `jti` en el JWT + tabla `sesiones_activas(user_id, jti, device_id, last_seen_at, revoked_at)` consultada en cada push + **refresh token** rotativo | Hoy **no hay ninguna forma de revocar una sesión**. Una sesión offline extendida sin revocación es una ventana de acceso irrevocable |
| B2 | Reescribir `SessionTimeoutService`: suspender idle-logout y heartbeat-logout en modo offline; **nunca** cerrar sesión ni purgar cuando hay operaciones pendientes | §2.1 |
| B3 | Distinguir 401 de **autenticación** de 401 de **plataforma** (`PlatformSecretMiddleware`) con código tipado; el interceptor solo cierra sesión en el primero | Rotar un secreto hoy destruiría las colas pendientes de todos los dispositivos |
| B4 | Llevar a server-side los gates de escritura hoy **front-only**. La migración `20260721040445` lo documenta textual: *"el gate es 100 % frontend"* — hay ~46 usos de `*appHasPermission` y solo ~7 chequeos de permiso en controllers | Los permisos se leen de un JSON **editable por el usuario** en localStorage. La capa de repositorio del punto 9 institucionalizaría esa autorización del lado del cliente |
| B5 | El servidor estampa **siempre** el autor desde el token e **ignora** `dto.CreatedByUserId` (`SeguimientoDiarioService.cs:291` y `:889` hoy lo aceptan del cliente sin validar). Trazabilidad offline en campos separados y no autoritativos (`capturado_por`, `capturado_at_dispositivo`, `device_id`, `sync_by`, `sync_at`) | La autoría es falsificable hoy; el diseño offline la iba a usar como característica |
| B6 | Eliminar el **fallback silencioso** de empresa (`ActiveCompanyMiddleware.cs:129-136`: si el usuario no pertenece a la empresa del header, degrada al `company_id` del token) y dejar de tratar `X-Active-Pais` como autoritativo | Un outbox reproducido se escribiría en la empresa equivocada con 200 OK y sin rastro. El país además decide si la validación de stock **bloquea o se traga el error** |
| B7 | Corregir `ActiveCompanyService.setActiveCompany()`, que cambia solo el **nombre** y nunca `activeCompanyId`, mientras el middleware prefiere el id | Nombre e id pueden apuntar a empresas distintas |
| B8 | Rotar las 4 llaves de `environment.prod.ts:13-27` (están en texto plano y quemadas en la historia de git) y sacarlas a variables de build | La afirmación de que el storage está cifrado con AES **es falsa**: `token-storage.service.ts:23-26` guarda JSON plano, con "Recordarme" en `true` por defecto |
| B9 | Decidir explícitamente la política de dato en reposo (ver §7, decisión D3) | Cifrar IndexedDB con el `EncryptionService` actual sería teatro: llave pública en el bundle, salt fijo `'sanmarino-salt'`, AES-CBC sin MAC |
| B10 | Mover el super admin hardcodeado por email (`ActiveCompanyMiddleware.cs:52` y `:116`) a datos, reusando el patrón `roles.is_company_admin` | Atraviesa el aislamiento multiempresa y no se puede revocar sin deploy |

### 4.C — Entrega (build, borde, CI)

| # | Acción | Por qué |
|---|---|---|
| C1 | **Eliminar la mutación post-build de `index.html`.** `Dockerfile:53-56` corre `yarn build && node scripts/inject-version.js`, que reescribe `dist/browser/index.html` | El builder calcula el SHA1 de index.html **antes** de esa mutación → el hash de `ngsw.json` no coincide → **el SW entra en safe mode y se desactiva solo, en silencio.** La PWA se despliega, el operario la instala, y en la granja no funciona nada |
| C2 | Fallback a `index.html` **solo para navegaciones**. Hoy `nginx.conf:93-100` hace `try_files $uri $uri/ /index.html` (por ahí caen `ngsw.json` y `manifest.webmanifest`) y CloudFront devuelve `index.html` con **200** ante 403/404, cacheado 300 s | El SW recibe HTML donde espera JSON/JS y lo rechaza por hash. Criterio de aceptación: `curl -i https://<host>/chunk-inexistente.js` debe devolver **404**, no 200 |
| C3 | `Cache-Control: no-cache` para `ngsw.json`, `ngsw-worker.js`, `safety-worker.js`, `manifest.webmanifest`, `index.html`, con `location =` **antes** del regex de nginx y behaviors CloudFront dedicados con TTL 0 | Hoy nginx marca **todo `.js`** como `immutable, max-age=1 año` (incluye el propio worker) y el borde tiene `DefaultTTL 86400` |
| C4 | **Definir cuál es el único origen del front** y borrar el otro del repo | Hay **dos caminos de despliegue vivos** con políticas de caché incompatibles: ECS+nginx (workflow y Makefile) y S3+CloudFront (README y `frontend/deploy/*.json`). La CSP existe solo en el camino nginx |
| C5 | Arreglar la herencia de headers de nginx (los `add_header` de `location` descartan los del `server`): hoy **index.html y todos los .js salen sin CSP ni HSTS** | Un SW amplía el radio de un XSS de "una sesión" a "toda la base offline + la cola" |
| C6 | **Gate de tests real en el pipeline.** `.github/workflows/deploy-production.yml` tiene 296 líneas y **cero** `dotnet test` / `yarn test`, pese a que CLAUDE.md afirma que el gate existe. Antes hay que arreglar el harness de Karma (compila 0 specs) | Sin gate no hay forma de detectar la divergencia C#↔TS de §6 |
| C7 | `deploy-frontend: needs: [deploy-backend]` | Hoy corren en paralelo. Con SW, la ventana de "cliente viejo contra backend nuevo" pasa de 5 minutos a **días** |
| C8 | `/api/sync/*` con política propia de rate limit **por usuario/device**, no por IP | `RateLimitingMiddleware.cs:39-43`: 100 req/min y al exceder bloquea **la IP completa 3 minutos para todas las rutas**. Cinco tablets de la misma granja drenando su cola detrás del mismo módem se autobloquean, tumbando el login de todos |

---

## 5. Arquitectura propuesta (post-Fase 0)

### 5.1 Shell PWA

- `@angular/service-worker` + `ngsw-config.json` con **dos grupos**: `app` en `prefetch` (shell + rutas de los módulos operativos offline) y `resto` en `lazy` (reportes, configuración, administración — no hay razón para precachear reportes en una tablet de granja).
- **Prohibido `dataGroups` sobre `/api/*`**: la caché del SW indexa por URL e ignora headers, y la empresa activa viaja en `X-Active-Company` → el operario de la empresa B recibiría la respuesta cacheada de la empresa A. Los datos van a IndexedDB por la capa de repositorio, donde **sí** se pueden particionar.
- `manifest.webmanifest` + iconos 192/512/180 propios + `apple-mobile-web-app-capable` + `theme-color` (hoy `index.html` no tiene ninguno).
- **`safety-worker.js` publicado desde el día uno** con `no-cache`, y un target `make pwa-panic` documentado y **cronometrado** (si tarda más de 15 min, no es un kill switch). Requisito crítico: el safety worker desregistra el SW **sin borrar la base del outbox**, y el arranque detecta cola huérfana y ofrece enviarla.
- **`VersionCheckService` se elimina en la MISMA PR** que registra el SW. Son dos implementaciones del mismo problema con criterios opuestos y su convivencia produce bucles de recarga.
- Actualización: `SwUpdate.versionUpdates` + banner no intrusivo + `activateUpdate()` disparado por el usuario. **La actualización NO se bloquea por cola pendiente** (ver §5.5).

### 5.2 Datos locales

`frontend/src/app/shared/offline/`: `db.ts` · `repositorio.ts` · `outbox.ts` · `sync.service.ts` · `red.service.ts` · `reglas/` · `diagnostico/`.

**Partición obligatoria por `{userId, companyId}`** en el nombre de la base o en índice de cada store, aplicada por el repositorio (fail-closed, mismo criterio que `InventarioCatalogoScopeCalculos` en el backend). En logout o cambio de empresa: si hay cola pendiente, **bloquear el cambio** con mensaje explícito; si no la hay, purgar la partición.

**Migraciones de esquema acumulativas obligatorias:** el handler debe iterar `for (v = oldVersion+1; v <= newVersion; v++)`. IndexedDB entrega un solo `upgradeneeded` de v1 a v5 y **nunca** ejecuta los pasos 2, 3 y 4 si están escritos como saltos consecutivos. Test en Karma que abra una base en v1, la lleve a vN de un salto y verifique el esquema.

### 5.3 Lectura (pull)

`GET /api/sync/pull?desde=<cursor>&granjas=...` devolviendo `{cambios, borrados, alcance_revocado}`.

- Ventana: **ciclo completo del lote activo**, no ventana temporal (§2.5).
- Requiere A5 (tombstones) — sin eso el delta sync es imposible: los dispositivos quedan con datos fantasma para siempre.
- Requiere **tombstones de alcance**: si a un usuario se le revoca una granja, el pull por `updated_at` no transporta esa revocación y el dispositivo sigue mostrando esa granja indefinidamente.
- **TTL duro por dataset**: pasado el plazo sin contacto con el servidor, la UI bloquea la lectura operativa en vez de mostrar datos viejos.
- Activar `JsonIgnoreCondition.WhenWritingNull` (−30 % de peso, gratis).

### 5.4 Escritura (outbox + push)

Cada mutación se escribe primero en IndexedDB como **documento inmutable y versionado**:

```
{ client_op_id: uuid, tipo, payload, payload_version, contract_version,
  company_id, pais_id, user_guid, device_id,
  capturado_at_dispositivo, lote_id, farm_id }
```

- `POST /api/sync/push` **por lotes** (arrancar en 25 ops / 1 MB), con respuesta **por operación** y **por efecto**, con códigos de error **tipados y estables** (no mensajes libres en español, que es lo que hoy devuelven los duplicados: HTTP 400 con texto, nunca 409).
- Tabla `sync_operaciones` con **UNIQUE sobre `client_op_id`** como **primera migración del proyecto**. Es lo único que hace seguro un 504 seguido de reintento — y CloudFront tiene `ConnectionAttempts: 3`, o sea que **puede reenviar el POST por su cuenta**.
- **El registro de idempotencia y el efecto van en la MISMA transacción.** Esto obliga a que el endpoint de push abra la transacción y los services participen de ella; hoy `SeguimientoDiarioService` e `InventarioGestionService` abren la suya y hacen varios `SaveChanges` sucesivos.
- Backoff exponencial con jitter y respeto de `Retry-After`; detenerse ante el primer 429.
- Si el procesamiento de un lote puede superar 20 s (`OriginReadTimeout` es 30 s y `RecalcularSaldoAlimentoPorLoteAsync` reescribe **todos** los registros del lote por cada seguimiento): modelo asíncrono `202 + batch_id` con consulta posterior. Además resuelve que iOS mate la app a mitad del envío.
- **Sin ids negativos.** Para las entidades que sí se crean offline: columna `client_entity_id uuid UNIQUE` (nullable, poblada solo por capturas offline) y el push envía el **grafo** de operaciones como unidad; el servidor resuelve las referencias por UUID dentro de la misma transacción y devuelve el mapa `uuid → id`. El cliente nunca reescribe referencias a posteriori.
- **El push ignora `X-Active-Company`** y usa el `company_id`/`pais_id` que la operación trae embebido, validándolos contra el JWT. Si el usuario ya no tiene acceso: rechazo explícito, **nunca** reasignación silenciosa.

### 5.5 Conflictos — el cambio de criterio más importante

**Un rechazo en campo casi nunca es un error de captura corregible; es un hecho físico ya ocurrido.**

Ejemplo verificado: el galponero registra offline el despacho de 4.200 aves el sábado; el domingo la oficina registra otra venta del mismo lote; el lunes sincroniza y recibe "aves disponibles insuficientes". Una bandeja con *reintentar / editar / descartar* no sirve: reintentar falla igual, editar significa inventar un número menor que el camión que ya salió, descartar borra una venta real.

Por eso se distinguen **dos clases**:

| Clase | Qué es | Tratamiento |
|---|---|---|
| **(a) Error de captura** | Fecha futura, campos inválidos, duplicado del mismo dispositivo, permiso revocado, contrato obsoleto | Bandeja con edición / reintento. Correcto |
| **(b) Divergencia con el mundo** | Stock insuficiente, aves insuficientes, lote cerrado | **NO rechazar.** Aplicar con marca `requiere_cuadre = true`, permitiendo saldo negativo, y generar tarea de cuadre para el supervisor con el detalle |

**Fundamento:** perder el dato de campo es peor que un saldo temporalmente negativo. Y esto es exactamente lo que **ya hace hoy** la rama Ecuador/Panamá (el consumo va en `try/catch` que solo loguea, el seguimiento se guarda igual) — pero sin registrar que pasó. Formalizarlo es una mejora, no un riesgo nuevo.

**Prohibido "reemplazar" como resolución de duplicado.** `SeguimientoDiarioService.UpdateAsync:548` sobrescribe `Metadata` completo y las columnas `traslado_*`. Escenario: se trasladan 3.000 hembras de A a B el martes; B captura offline su martes; la oficina ejecuta el traslado el miércoles creando la fila del martes en B con `traslado_ingreso_hembras=3000`; el jueves B sincroniza, "reemplaza", y se pisa el traslado con null y se borra el `metadata` (marca de arrastre de huevos, `huevoItems` de Santa Reyes). **Descuadre en dos granjas.**

En su lugar: el push de un seguimiento es un **patch con el conjunto explícito de campos que el usuario capturó**, y el servidor hace merge sin tocar jamás las columnas del sistema. Hay que formalizar en el contrato **qué columnas son "del galponero" y cuáles "del sistema"**; solo las primeras son sincronizables. Lo mismo para maestros: `camposModificados: ['loteNombre']`, nunca el objeto entero (un PUT de `lotes` sincronizado tarde resetea el saldo de aves por §2.3).

### 5.6 Cierre de lote con colas abiertas

El cierre del levante en oficina invalida en bloque el trabajo offline del galponero: se rechaza todo el batch por "lote cerrado", el LPP ya se creó con un saldo de aves que ignora esas capturas, el arrastre de huevos ya volcó su fila, y **reabrir está bloqueado** porque el LPP ya tiene registros.

Mínimo exigible: la UI de cierre muestra **"hay N dispositivos con capturas sin sincronizar de este lote"** y exige confirmación. Requiere que el servidor sepa qué dispositivos tienen cola abierta (telemetría de §5.7). Opción superior: ventana de gracia por `capturado_at` — el gate acepta operaciones cuya fecha de captura sea anterior a `estado_cierre_fecha` y recalcula el LPP.

### 5.7 Observabilidad de flota (sin esto no se opera una PWA offline, se la sufre)

Hoy no existe **ningún** controller de telemetría de cliente, la única marca de versión es un `<meta>` que solo lee el propio navegador, y los bloques de debug del interceptor están vaciados (o sea: ya se intentó depurar por consola y se abandonó).

- Pantalla **Diagnóstico** accesible sin red: versión del bundle, versión del esquema local, estado del SW (incluido si está en safe mode), `storage.estimate()`, fecha del último pull, tamaño de la cola y las últimas 20 operaciones con su error textual.
- Botón **Exportar diagnóstico** a JSON compartible (WhatsApp es el canal real de campo).
- `POST /api/sync/telemetria` en cada reconexión + tabla `dispositivos_sync` + vista para el equipo.
- Semáforo grande **"LISTO PARA TRABAJAR SIN RED / NO LISTO"** y procedimiento de **alistamiento en oficina con wifi**: instalar, esperar SW activo + snapshot completo + `navigator.storage.persist()` concedido, y recién ahí entregar el dispositivo.

---

## 6. Reglas de negocio: qué se puede validar offline

| Grupo | Qué es | Cuántas | Tratamiento |
|---|---|---|---|
| **A — Replicable** | Lógica pura ya aislada en `Application/Calculos/` (46 archivos, sin EF ni estado) | 46 archivos, 29 suites xUnit (~3.600 líneas) | Portar a `shared/offline/reglas/` para feedback inmediato |
| **B — Solo contra estado global** | Stock (12 puntos de bloqueo), saldo de aves/huevos, unicidad global, consecutivos, estado de cierre | — | **No** se validan offline. Autoridad del servidor, con el tratamiento de §5.5 |
| **C — Imposible** | 7 triggers de Postgres + 27 services con transacción multi-tabla | — | Fuera del alcance offline |

**Riesgo aritmético confirmado — y ya materializado hoy:** `Math.Round` de C# es **banker's rounding** (`MidpointRounding.ToEven`); los espejos TypeScript que **ya existen** en el repo usan half-up. **Divergen hoy, antes de cualquier trabajo offline.** Además `ToKg` tiene comportamiento distinto en C# y en TS (el front convierte quintales ×45,36 y el backend no), y el backend acumula en `decimal` (base 10) mientras JavaScript solo tiene `double` binario.

**Por eso:** ninguna regla se porta a TS sin su fila en un **corpus de casos compartido** (JSON en el repo) que consuman xUnit **y** Karma. La equivalencia C#↔TS tiene que ser un test, no una intención. Y esto requiere C6 (gate de tests) primero.

---

## 7. Decisiones que necesito del usuario

| # | Decisión | Opciones | Recomendación |
|---|---|---|---|
| **D1** | Alcance de escritura en v1 | (a) Solo lista blanca §3.2 · (b) Sumar ventas y movimientos ya | **(a)** — ventas y movimientos entran en v2, cuando exista la infraestructura de §4 |
| **D2** | ¿Se hace la Fase 0 completa antes de la PWA, o se corre en paralelo un piloto de solo lectura? | (a) Fase 0 completa primero · (b) Fase 0.C (entrega) + piloto de **solo lectura** en paralelo, y Fase 0.A/0.B antes de la primera escritura | **(b)** — el piloto de solo lectura tiene riesgo de integridad cero y es donde se aprende iOS, cuota, purga e instalación en campo |
| **D3** | Dato en reposo en el dispositivo | (a) Cifrar con llave derivada de PIN/WebAuthn (`crypto.subtle`, salt aleatorio, AES-GCM, `CryptoKey` con `extractable:false`) · (b) **No cifrar** y minimizar el dato local (sin precios ni facturación) + TTL duro + purga | Depende de si las tablets tienen bloqueo de pantalla. **(b) es más honesto y más barato**; (a) solo si hay requisito formal |
| **D4** | Vigencia de la sesión offline | (a) Jornada (12-16 h) · (b) 7 días | **(a)** — con B1 (revocación) implementado. 7 días sin revocación es una ventana de acceso que nadie puede cerrar |
| **D5** | Dispositivos objetivo | Android / iOS / ambos | Define si hace falta el modo de sincronización explícita en primer plano (iOS **no tiene Background Sync** y mata la web app al pasar a segundo plano) |
| **D6** | Modo offline: ¿global o habilitado por rol y dispositivo? | (a) Global · (b) Opt-in del admin por rol y por dispositivo registrado | **(b)**, y **prohibido** para cuentas con alcance global/multiempresa (un super admin bajaría el snapshot de todas las empresas) |
| **D7** | ¿Cuál es el origen real del front hoy en producción? | ECS+nginx / S3+CloudFront | Hay que **verificarlo con `curl -I`** contra prod antes de tocar caché. Un repo con dos verdades de despliegue es un incidente pendiente |

---

## 8. Fases propuestas

| Fase | Contenido | Riesgo de integridad |
|---|---|---|
| **F0.C** | Higiene de entrega: C1-C8 | Ninguno (no toca funcionalidad) |
| **F0.B** | Sesión y seguridad: B1-B10 | Bajo |
| **F0.A** | Integridad de datos: A1-A10 (varias son **bugs de hoy**, con valor propio) | Medio (migraciones) — validar por módulo con `dotnet test` |
| **F1** | Shell PWA + manifest + kill switch + diagnóstico + telemetría. **Solo lectura**, un módulo piloto (seguimiento diario levante) | Ninguno |
| **F2** | Snapshot + pull + repositorio + reglas TS con corpus compartido. Consulta offline amplia | Bajo |
| **F3** | Outbox con **una sola** escritura (seguimiento levante), push por lotes, bandeja de conflictos con las dos clases de §5.5 | Alto — piloto en 1 granja |
| **F4+** | Resto de la lista blanca, un módulo por vez, cada uno con su test de equivalencia en el gate | Medio |
| **F5** | Regla hacia adelante: todo módulo operativo nuevo contra la capa de repositorio | — |

**Nota sobre la regla del §F5 (pedido explícito del usuario de "dejar alineado"):** conviene adoptarla **cuando F3 esté en producción con datos reales**. Congelar el desarrollo del negocio detrás de una abstracción que todavía no demostró funcionar es más caro que adaptar dos o tres módulos después.

---

## 9. Criterios de aceptación transversales

- `curl -i https://<host>/chunk-inexistente.js` → **404**, no 200.
- `curl -I` sobre los 5 archivos de control del SW → `Cache-Control: no-cache`.
- Step de CI que compare el SHA1 declarado en `ngsw.json` contra el archivo en disco y falle si difiere.
- Test de carga: 5 dispositivos × 500 operaciones desde la misma IP → ningún 429, ningún tercero bloqueado.
- Test en iPhone real: capturar 20 registros sin red, bloquear pantalla 30 min, reabrir → la cola sigue completa.
- Test de migración IndexedDB: abrir base en v1, saltar a vN, verificar esquema.
- Ensayo cronometrado del `make pwa-panic` en dispositivo real (< 15 min).
- Corpus de equivalencia C#↔TS verde en xUnit **y** en Karma.
