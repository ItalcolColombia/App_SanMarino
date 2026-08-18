# V11 · Cierre de los smokes pendientes + limpieza del tracker (17-ago-2026)

Pedido del usuario: **«continuá con el track y limpiá lo que completó»**.

Estado de partida: **56 pendientes** en `tracker_estado.md` (2.326 líneas, 24 bloques). El triage de
V9.0 ya había separado los que esperan una decisión del usuario / un admin externo / un deploy. Lo que
queda **accionable sin dependencias externas** son exactamente **dos smokes** y una **revalidación**:

| id | pendiente | bloque |
|---|---|---|
| V9.5.17 | smoke HTTP del ciclo Implementación ↔ ItalJira | V9 |
| — | 2 smokes en pantalla del rango de fechas de gastos | Gastos rango de fechas |
| V3.X | «Disponible = stock − reservas» — sospecha de que ya está cerrado por V5 + V9.2 | Bitácora agosto |

**V8 (descuadres de alimento de Panamá) sigue RESERVADA para otra sesión — no se toca.**

---

## 1. Enfoque arquitectónico

### 1.1 El problema que bloqueaba el smoke de V9.5.17

La BD local `sanmarinoapplocal:5433` está en `20260814130000` y al código le faltan **12 migraciones**
por aplicar (de otras sesiones + las de vacunación de hoy). Entre ellas,
`20260815000000_AddImplementacionFirmaManuscritaYItalJira`, que es **justo la que crea las dos columnas
del vínculo** (`implementacion_planes.historia_id`, `implementacion_tareas.ticket_tarea_id`).
Verificado: hoy **no existen** en la BD.

O sea que el smoke es imposible sin schema, y aplicar migraciones a la BD compartida **no es decisión
de esta sesión** (es exactamente lo que V9.6.9 declaró y no hizo).

### 1.2 La salida: BD clon descartable, la compartida NO se toca

```sql
CREATE DATABASE sanmarinoapplocal_smokev11 TEMPLATE sanmarinoapplocal;   -- 46 MB, instantáneo
```

Sobre el clon se levanta el backend de smoke, se hace todo el ciclo y al terminar `DROP DATABASE`.

Precondición verificada: **0 conexiones** a `sanmarinoapplocal` (`pg_stat_activity`), que es lo que
`CREATE DATABASE ... TEMPLATE` exige.

#### 🔴 Corrección — cómo se apunta el backend al clon (esto salió mal la primera vez)

> El plan decía «la compartida queda intacta **por construcción**». **Era falso**, y se pagó.

`ConnectionStrings__ZooSanMarinoContext` **NO sirve** para mover un backend local de base:
`Program.cs:112-123` vuelve a leer `appsettings.Development.json` con un `ConfigurationBuilder`
aparte y **pisa** lo que traiga el entorno cuando `EnvironmentName == "Development"` — a propósito,
«para que la conexión local no sea sobrescrita por env vars». `PORT` sí se respeta, así que el
backend arranca en el puerto pedido **y en la base equivocada**, sin una sola señal.

Consecuencia real de este intento: el smoke corrió contra `sanmarinoapplocal` y, con
`Database:RunMigrations = true`, le aplicó las **12 migraciones pendientes**. Se revirtieron las filas
del smoke; las migraciones quedaron aplicadas **por decisión del usuario** (es el estado al que llega
cualquier `make back` sobre `main`, y revertirlas exigía 12 `Down()` destructivos).

**La forma que sí aísla en Development** — porque `SetBasePath(builder.Environment.ContentRootPath)`
es lo que decide qué `appsettings.Development.json` gana:

```bash
cp appsettings.json appsettings.Development.json  <scratch>/contentroot/     # y ahí se edita el Database=
dotnet <artifacts>/ZooSanMarino.API.dll --contentRoot <scratch>/contentroot  # PORT=5501
```

Y **se verifica antes de correr nada**, no se asume:

```sql
SELECT datname, count(*) FROM pg_stat_activity WHERE datname LIKE 'sanmarinoapp%' GROUP BY 1;
```

Bonus del mismo content root: `AllowedOrigins` vive en `appsettings.json`, así que agregar
`http://localhost:4300` para el smoke de UI **tampoco toca el repo**.

Beneficio lateral que sí se cumplió: aplicar las 12 migraciones sobre datos reales **probó que
corren**, que es la misma compuerta que el deploy pasa al arrancar en ECS.

---

## 2. Archivos / componentes tocados

**Código funcional: ninguno previsto.** Los dos smokes son verificación de código ya escrito y
commiteado. Si alguno destapa un bug (que es a lo que van: V10.2 y W2.5.2 fueron bugs que sólo
aparecían ejecutando), el arreglo se hace acá con su test.

| archivo | cambio |
|---|---|
| `tracker_estado.md` | marcar lo verificado · **archivar los bloques 100 % cerrados y commiteados** |
| `fase_de_desarrollo/v11_cierre_smokes_y_limpieza_tracker_plan.md` | este plan |
| *(scratchpad)* | scripts de smoke — no entran al repo |

### Reglas de la limpieza del tracker

La guía dice: *«Tracker cerrado (todo `- [x]` y ya commiteado): recién ahí podés limpiarlo»*. Se
aplica **por bloque**, no al archivo entero:

1. Bloque con **al menos un `- [ ]`** ⇒ **se conserva entero**. Sin excepción (incluye V8, reservada).
2. Bloque **100 % `- [x]` y commiteado** ⇒ se reemplaza por **una línea** en un índice
   «Entregado y archivado», con fecha, commit y una frase de qué dejó. El texto completo sobrevive en
   git (`git show <commit>:tracker_estado.md`), que es donde tiene que estar la historia.
3. Nada se borra sin que su commit esté verificado con `git log`.

---

## 3. Casos de prueba

### 3.1 Smoke A — Implementación ↔ ItalJira (cierra V9.5.17)

Ciclo completo por HTTP, con JWT + `X-Secret-Up` minteados y permiso `tickets.gestionar`:

| # | paso | esperado |
|---|---|---|
| A1 | `POST /api/Implementacion/planes` + 3 puntos | 201 |
| A2 | `POST /planes/{id}/italjira` | historia creada, `puntosEnlazadosAhora = 3`, `puntosYaEnlazados = 0` |
| A3 | repetir A2 (idempotencia) | `historiaCreada = false`, `enlazadosAhora = 0`, `yaEnlazados = 3` |
| A4 | agregar un 4.º punto y repetir | enlaza **sólo** el nuevo (`1 / 3`) |
| A5 | mover la tarjeta del punto 1 a **LISTO** (por ItalJira) | el punto queda `completado = true` con fecha y autor |
| A6 | sacarla de LISTO | el punto vuelve a pendiente y **se limpia** el sello de fecha/autor |
| A7 | confirmar un punto y mover su tarjeta | el punto **CONFIRMADO no se toca** (candado de V9.5.9) |
| A8 | borrar la historia desde el tablero y repetir A2 | la vuelve a crear (autocura), no falla |
| A9 | borrar la tarjeta de un punto y repetir A2 | rehace **esa** tarjeta |
| A10 | limpieza | la BD clon se dropea entera |

### 3.2 Smoke B — rango de fechas en Gastos de inventario (2 pendientes)

Front `:4300` contra el backend del clon, empresa **3 (ItalcolEcuador)**, que es la que tiene gastos:

| # | paso | esperado |
|---|---|---|
| B1 | sin rango | tabla idéntica a hoy · nombre de archivo **sin** sufijo de rango |
| B2 | con rango aplicado | tabla acotada a esas fechas |
| B3 | Excel del rango | **mismas filas** que la tabla, subtítulo con el rango y sufijo en el nombre |
| B4 | rango invertido | aviso de rango inválido, sin consultar |
| B5 | «limpiar filtros» | vuelve a B1 exacto |

El Excel se inspecciona **sin abrirlo**, hookeando `URL.createObjectURL` + `click` (SheetJS escribe el
libro sin comprimir ⇒ el XML se lee con `blob.text()`).

### 3.3 Revalidación V3.X

Contra el código de hoy: `ReservadoPorItemAsync` eliminado (V5.Y/V9.2.6), `ReservadoDeAvesAsync` con
**dos** consumidores reales, `DisponibleKg` derivado y leído por el front (V5.6/V5.7). Si se confirma,
el pendiente se marca con su evidencia; no queda decisión que tomar.

---

## 4. Reglas de negocio y cambios de BD

- **Cambios de BD: cero.** Ninguna migración nueva. La única escritura vive en un clon que se destruye.
- La BD compartida se verifica **antes y después**: mismo head de `__EFMigrationsHistory` (279
  migraciones, la última `20260814130000`) y mismos conteos.
- Sin procesos huérfanos: `5002 / 5499 / 5501 / 4200 / 4300` libres al terminar.

## 5. Validación

- `dotnet build` 0 errores · `dotnet test` verde (2.745 + 1 de referencia) — sólo si se toca C#.
- `yarn build` 0 errores — sólo si se toca el front.
- Gates de front (`verificar-change-detection.js`, `verificar-lista-cacheable.js`) si hay componente
  o endpoint nuevo.
- Commit acotado, sin footer de atribución.
