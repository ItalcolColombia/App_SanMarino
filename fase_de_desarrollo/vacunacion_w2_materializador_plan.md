# Vacunación W2 — el materializador: la plantilla baja al cronograma de los lotes

**Fecha:** 2026-08-17 · **Continúa:** [`vacunacion_cronograma_vivo_plantillas_plan.md`](vacunacion_cronograma_vivo_plantillas_plan.md) §3.2 y §4 (fase W2)
**Antecedente:** W1.1-W1.4 entregados (`a19807b`, `bd935cb`). Hoy una empresa puede cargar su plan
sanitario, verlo en pantalla y preguntar *«¿cuál le toca a este lote?»* — y la respuesta es correcta,
auditable… y **no se traduce en una sola fila de cronograma**. `vacunacion_cronograma_item` se sigue
llenando a mano, lote por lote, exactamente como antes de W1.

**Riesgo declarado: MEDIO.** Es la primera fase que **escribe en datos de lotes vivos**. Todo el
diseño de abajo está subordinado a eso.

---

## 0. Alcance

| Pieza | Entra | No entra |
|---|---|---|
| Columnas de origen en `vacunacion_cronograma_item` | ✅ W2.1 | — |
| `VacunacionMaterializadorCalculos` (puro) + tests | ✅ W2.2 | — |
| Servicio idempotente + preview de impacto | ✅ W2.3 | — |
| Endpoints y pantalla (un lote y masivo) | ✅ W2.4 | — |
| Enganche automático al crear el lote | ✅ W2.4 (fail-soft, 3 caminos) | carga masiva · puente Panamá · migraciones masivas |
| Bandeja de pendientes / scoping fino | ❌ | **W3 / W4** |
| Borrar ítems de cronograma huérfanos | ❌ | se **reportan**, no se tocan (§3.5) |

**Regla que gobierna el bloque:** una empresa **sin plantillas se comporta byte a byte como hoy**.
Con plantillas, **nada se escribe sin que alguien lo pida y vea antes el impacto**.

---

## 1. Enfoque arquitectónico

- **Servicio nuevo, no un partial de los dos que ya hay.** `VacunacionPlantillaService` administra el
  plan de la empresa; `VacunacionCronogramaService` administra el cronograma de un lote. El
  materializador es el **puente**: lee el primero y escribe el segundo. Meterlo en cualquiera de los
  dos le daría a esa clase dos sujetos y —peor— le daría a un servicio de lectura de plan la
  capacidad de escribir cronogramas de lotes.
- **Partición por responsabilidad** (CLAUDE.md §🧩): ancla con campos/ctor/resolución de lote, y
  `Funciones/` con `Planificar` (lectura + preview) y `Aplicar` (escritura en transacción).
  Namespace plano `ZooSanMarino.Infrastructure.Services`.
- **La decisión de qué crear / actualizar / preservar es pura**, en
  `Application/Calculos/VacunacionMaterializadorCalculos.cs`, sin EF ni `_ctx`. El servicio trae las
  dos listas, llama a la función y ejecuta el plan que le devuelve. Esto es lo que hace que
  «materializar 2× no duplica» y «un aplicado nunca se pisa» sean **testeables sin base de datos**.
- **Empresa por dato, fail-closed**: se reusa `ResolverLoteAsync` filtrando por
  `CompanyId == _currentUser.CompanyId`. Un lote de otra empresa no es «sin plantilla»: es *no existe*.
- **El preview y la aplicación comparten la misma función pura.** Si se calcularan por caminos
  distintos, el preview mentiría el día que uno de los dos cambie — y un preview que miente es peor
  que no tenerlo, porque es el gate en el que el usuario apoya la decisión.

---

## 2. Archivos

**Backend**

| Archivo | Qué |
|---|---|
| `Domain/Entities/Vacunacion/VacunacionCronogramaItem.cs` | +2 propiedades (`OrigenPlantillaItemId`, `GeneradoAutomatico`) |
| `Persistence/Configurations/Vacunacion/VacunacionCronogramaItemConfiguration.cs` | mapeo + FK `SET NULL` + índice único parcial |
| Migración `…_AddOrigenPlantillaAVacunacionCronograma` | idempotente, aditiva pura |
| `Application/Calculos/VacunacionMaterializadorCalculos.cs` | **nuevo**, puro |
| `Application/DTOs/Vacunacion/VacunacionMaterializadorDtos.cs` | preview e informe de aplicación |
| `Application/Interfaces/IVacunacionMaterializadorService.cs` | contrato |
| `Infrastructure/Services/Vacunacion/VacunacionMaterializadorService.cs` | ancla |
| `…/Vacunacion/Funciones/VacunacionMaterializadorService.Planificar.cs` | resolver plantilla + armar el plan (lectura) |
| `…/Vacunacion/Funciones/VacunacionMaterializadorService.Aplicar.cs` | ejecutar el plan (escritura, en tx) |
| `…/Vacunacion/Funciones/VacunacionMaterializadorService.Lotes.cs` | lotes vivos de la empresa por línea |
| `API/Controllers/VacunacionMaterializadorController.cs` | 4 endpoints |
| `API/Program.cs` | DI |
| `Services/LoteService.cs` · `LotePosturaLevanteService.cs` · `LoteAveEngordeService.cs` | enganche fail-soft |
| `Services/Vacunacion/Funciones/VacunacionCronogramaService.Crud.cs` | `Update` emancipa el ítem (§3.4) |
| `tests/…/VacunacionMaterializadorCalculosTests.cs` | xUnit |

**Frontend**

| Archivo | Qué |
|---|---|
| `features/vacunacion/models/vacunacion-materializador.model.ts` | tipos 1:1 con los DTOs |
| `features/vacunacion/services/vacunacion.service.ts` | +4 métodos |
| `features/vacunacion/funciones/resumir-impacto-materializacion.funcion.ts` | PURA: el impacto en castellano |
| `features/vacunacion/components/modal-aplicar-plantilla/` | preview + confirmación |
| `features/vacunacion/pages/plantillas/` | botón «Aplicar a los lotes» |
| `features/vacunacion/pages/cronograma/` | aviso «este lote tiene N vacunas del plan sin aplicar» |

---

## 3. Diseño

### 3.1 Las dos columnas (W2.1)

```
vacunacion_cronograma_item
  + origen_plantilla_item_id  integer NULL   -- FK → vacunacion_plan_plantilla_item (id), ON DELETE SET NULL
  + generado_automatico       boolean NOT NULL DEFAULT false
```

- **`ON DELETE SET NULL`, no `RESTRICT` ni `CASCADE`.** `vacunacion_plan_plantilla_item` cascadea
  desde la plantilla; si algún día una plantilla se borra duro, con `CASCADE` se llevaría puesto el
  cronograma de lotes reales y con `RESTRICT` el borrado fallaría. El ítem del lote es **historia
  sanitaria**: sobrevive y sólo pierde el vínculo con el plan del que salió.
- **`DEFAULT false` es el valor que hace que la migración sea neutra:** todas las filas que ya existen
  quedan marcadas como *hechas a mano*, que es exactamente lo que son. El materializador no las va a
  tocar nunca.

**Idempotencia garantizada por la base, no por el código:**

```sql
CREATE UNIQUE INDEX IF NOT EXISTS ux_vci_origen_plantilla_item
ON public.vacunacion_cronograma_item (
    COALESCE(lote_postura_levante_id, 0),
    COALESCE(lote_postura_produccion_id, 0),
    COALESCE(lote_ave_engorde_id, 0),
    origen_plantilla_item_id)
WHERE origen_plantilla_item_id IS NOT NULL;
```

> ⚠️ El `COALESCE` no es adorno: en Postgres `NULL` no es igual a `NULL`, y los tres FK de línea son
> excluyentes (dos de los tres siempre vienen en `NULL`). Sin envolverlos, el índice **no bloquearía
> ni un duplicado** — el mismo golpe que ya está documentado en el índice único de stock de
> inventario. El índice es parcial, así que al crearse cubre **cero filas** (la columna nace toda en
> `NULL`): no puede fallar sobre datos existentes.

### 3.2 El cálculo puro (W2.2)

`VacunacionMaterializadorCalculos.Planificar(itemsPlantilla, itemsCronograma)` → **4 listas**:

| Lista | Qué es | Qué hace el servicio |
|---|---|---|
| `Faltantes` | ítem de plantilla sin fila en el lote | `INSERT` con `generado_automatico = true` |
| `Actualizables` | fila derivada del plan cuyo objetivo/franja/vacuna cambió | `UPDATE` de esos campos |
| `Preservados` | fila que **no se toca**, con el motivo | nada — se muestra |
| `Sobrantes` | fila derivada de un ítem de plantilla que ya no está | nada — se reporta (§3.5) |

**Los tres motivos de preservación**, en el orden en que mandan:

1. **`YaAplicado`** — la fila tiene registro de aplicación. Invariante duro: *un ítem con registro no
   se modifica ni se borra jamás*. Cambiarle la semana objetivo a algo ya aplicado reescribiría la
   desviación y el `Incumplido` de un hecho pasado; el reporte de cumplimiento pasaría a decir otra
   cosa sobre algo que ya ocurrió.
2. **`Manual`** — `generado_automatico = false`. Nunca salió del plan, o se emancipó (§3.4).
3. **`SinCambios`** — derivada del plan y ya idéntica. Se separa de las otras dos a propósito: en el
   preview, «12 preservados» sin discriminar no le dice nada al usuario; «10 ya estaban bien, 2 ya
   fueron aplicadas» sí.

La función es **total y determinista**: mismas entradas ⇒ misma salida, sin importar el orden en que
la base devuelva las filas (se ordena por `Orden, ValorObjetivo, Id`, igual que `MapDetalleAsync`).

### 3.3 Qué se copia y qué no

`VacunacionPlanPlantillaItem` espeja a propósito los campos de `VacunacionCronogramaItem`
(`UnidadObjetivo` / `ValorObjetivo` / `RangoDias*`), así que materializar es **copiar tal cual**. Lo
que **no** viene del plan y lo resuelve el servicio en el momento:

| Campo | De dónde |
|---|---|
| `GranjaId` / `NucleoId` / `GalponId` | del lote, denormalizado (mismo patrón que `CreateAsync`) |
| FK de línea | el que corresponda a `LineaProductiva` |
| `CompanyId` / `PaisId` | de `_currentUser` |
| `FechaObjetivo` | **siempre `NULL`** — la plantilla no puede tener unidad `'Fecha'` (CHECK) |
| `Activo` | `true` |
| `CreatedByUserId` | quien materializa; en el enganche automático, quien crea el lote |

**Ubicación: se copia al materializar y no se re-sincroniza.** Si el lote se traslada de galpón, sus
ítems de cronograma quedan con el galpón viejo — que es el comportamiento que ya tiene el módulo
para los ítems cargados a mano. Alinear eso es otra tarea y toca filas que este bloque no está
mirando.

### 3.4 El ítem que se edita a mano se emancipa del plan

Hoy `VacunacionCronogramaService.UpdateAsync` edita cualquier ítem. Con W2, un ítem generado del plan
que alguien corrige a mano volvería a ser pisado en la próxima materialización, **en silencio**.

**Regla:** editar un ítem con `generado_automatico = true` lo pasa a `false`. Conserva
`origen_plantilla_item_id` —así el índice único sigue impidiendo que se cree un duplicado— pero pasa
a `Preservados/Manual` para siempre. Una corrección a mano es una decisión explícita sobre **ese**
lote; el plan de la empresa no la puede deshacer sin que nadie se entere.

### 3.5 Lo que el materializador NO hace: borrar

Si se quita una vacuna de la plantilla, las filas ya materializadas quedan como `Sobrantes`. **No se
borran.** Dos razones: (a) pueden tener registro de aplicación, y borrar la fila se lleva el registro
por cascada —o sea, borra la prueba de que la vacuna se puso—; (b) quitar algo del plan a futuro no
es lo mismo que declarar que no hubo que ponerlo en los lotes que ya estaban corriendo. Se listan en
el preview con nombre y lote, y se borran a mano desde el cronograma si corresponde.

### 3.6 Disparadores

| # | Disparador | Estado |
|---|---|---|
| a | Botón **«Aplicar el plan a los lotes»** (uno o todos), con preview obligatorio | ✅ el camino principal |
| b | Al **crear el lote** (3 caminos, fail-soft) | ✅ §3.7 |
| c | ~~Lazy al abrir el cronograma de un lote sin ítems~~ | ❌ **descartado** |

**Por qué se descarta (c)**, que el plan madre listaba: sería un `GET` que escribe. Las filas nacerían
con `created_by_user_id` de quien pasó a mirar la pantalla, y el módulo entero existe para poder
decir quién programó qué y cuándo. En su lugar, el `GET` del cronograma **avisa** (`plantillaEfectiva`
+ `itemsFaltantes`) y la pantalla ofrece el botón: el mismo click de distancia, con autoría real.

### 3.7 El enganche al crear el lote

Se engancha en los **tres** caminos de creación normal, ya identificados en el código:

| Línea | Punto |
|---|---|
| Levante | `LoteService.CreateAsync` (`new LotePosturaLevante`) |
| Producción | `LotePosturaLevanteService` (transición levante → producción) |
| Engorde | `LoteAveEngordeService.CreateAsync` |

**Fail-soft, sin excepción:** la materialización va en `try/catch` con log. Un plan sanitario que no
se pudo copiar **no puede impedir que se cree un lote** — el lote es el hecho operativo, el plan es
derivado, y el botón masivo lo recupera después.

**Declarado, no omitido en silencio:** carga masiva, puente Panamá y migraciones masivas **no** se
enganchan. Son caminos de importación con su propia semántica de fechas y su propio volumen; meterles
escritura de cronograma sin medirlo es exactamente el tipo de cambio que este plan quiere evitar.
Sus lotes se cubren con el botón masivo.

### 3.8 «Lotes vivos» para el masivo

Se usa la misma base que `fn_vacunacion_filter_data` (company + `deleted_at IS NULL`) y **se excluye
lo cerrado**:

| Línea | Filtro |
|---|---|
| Levante / Producción | `estado_cierre IS DISTINCT FROM 'Cerrado'` |
| Engorde | `estado_operativo_lote IS DISTINCT FROM 'Cerrado'` |

> ⚠️ **`<> 'Cerrado'`, nunca `= 'Abierto'`.** El vocabulario del dato está partido: `LoteService`
> escribe `'Abierto'` y `LotePosturaLevanteService` escribe **`'Abierta'`**. Un filtro por igualdad
> saltearía en silencio a todos los lotes creados por el segundo camino, y el síntoma sería el peor
> posible: el masivo diciendo «listo» sobre la mitad de los lotes.

Además, un lote sin `FechaEncaset` sólo puede tomar plantillas sin vigencia — eso ya lo resuelve
`ResolverEfectiva` y no se toca acá.

### 3.9 Permisos

Escribir exige **las dos** claves: `vacunacion.plantillas.administrar` **y**
`vacunacion.cronograma.administrar`. La acción es literalmente las dos cosas a la vez (leer el plan de
la empresa y escribir el cronograma de N lotes). Hoy la población es idéntica —la migración de W1.3
le dio `plantillas.administrar` exactamente a los roles que ya tenían `cronograma.administrar`—, así
que **nadie gana ni pierde acceso**; mañana la distinción existe. El preview pide sólo las de lectura.

### 3.10 Endpoints

| Verbo | Ruta | Qué |
|---|---|---|
| `GET` | `/api/VacunacionMaterializador/preview?lineaProductiva=&loteId=` | impacto en **un** lote |
| `POST` | `/api/VacunacionMaterializador/lote` | aplicar a un lote |
| `GET` | `/api/VacunacionMaterializador/preview-masivo?plantillaId=` | impacto en todos los lotes vivos que resuelven a esa plantilla |
| `POST` | `/api/VacunacionMaterializador/plantilla/{id}/aplicar` | aplicar el masivo |

El masivo corre **una transacción por lote**: un lote que falle (una vacuna del plan borrada del
catálogo, por ejemplo) no puede dejar a los otros 40 a medio materializar. El informe devuelve el
detalle por lote, con los que fallaron y por qué.

---

## 4. Reglas de negocio (contrato de los tests)

1. **Idempotencia**: materializar N veces deja el mismo resultado. Garantizado dos veces — por la
   función pura (`Faltantes` vacío en la 2ª pasada) y por el índice único parcial.
2. **Un ítem con registro de aplicación nunca se modifica ni se borra.** Invariante duro.
3. **Un ítem `generado_automatico = false` nunca se pisa** (nació a mano o se emancipó al editarse).
4. **Sin plantilla efectiva ⇒ no se escribe nada.** Nunca se inventa un plan.
5. **El materializador no borra filas jamás**, ni siquiera las suyas.
6. **Empresa efectiva por dato.** Un lote de otra empresa es *no existe*, no *sin plan*.
7. **Preview y aplicación usan la misma función pura.** El preview no puede mentir.
8. **Con 0 plantillas, cero escrituras y cero cambios visibles.**

---

## 5. Casos de prueba

**xUnit puros (`VacunacionMaterializadorCalculosTests`)**

- Cronograma vacío ⇒ todos `Faltantes`; segunda pasada sobre el resultado ⇒ `Faltantes` vacío (idempotencia).
- Ítem con registro ⇒ `Preservados/YaAplicado`, **aunque la plantilla haya cambiado la semana**.
- Ítem `generado_automatico = false` con el mismo `origen` ⇒ `Preservados/Manual`.
- Ítem derivado idéntico ⇒ `Preservados/SinCambios` (no `Actualizables`: un `UPDATE` que no cambia
  nada igual ensucia `updated_at` y el conteo del preview).
- Cambio de semana / de franja / de vacuna ⇒ `Actualizables` con los valores nuevos.
- Ítem derivado cuyo ítem de plantilla ya no está ⇒ `Sobrantes`.
- Ítem del lote sin `origen` y sin choque ⇒ ni `Faltantes` ni `Sobrantes` (es del lote, no del plan).
- Determinismo: la misma entrada barajada da la misma salida.

**Integración / smoke HTTP** (backend propio en puerto libre, `Database__RunMigrations=false`)

- Lote sin ítems + plantilla de 3 vacunas ⇒ preview dice 3 faltantes ⇒ aplicar ⇒ 3 filas con
  `generado_automatico = true` y `origen_plantilla_item_id` poblado.
- **Aplicar de nuevo ⇒ 0 escrituras** (y el `updated_at` de las 3 filas sin cambiar).
- Registrar la aplicación de una ⇒ mover esa semana en la plantilla ⇒ aplicar ⇒ **esa fila intacta**,
  las otras dos actualizadas.
- Editar un ítem por el CRUD de cronograma ⇒ `generado_automatico` pasa a `false` ⇒ aplicar ⇒ intacto.
- Quitar una vacuna de la plantilla ⇒ aplicar ⇒ la fila sigue viva y aparece como sobrante.
- Lote de otra empresa ⇒ 400/404, sin filas.
- Masivo: N lotes vivos, los cerrados **no** entran; un lote con vacuna inválida falla solo y los
  demás quedan aplicados.
- **Regresión**: con la BD sin plantillas, crear un lote por los 3 caminos ⇒ cero filas de cronograma
  y el lote se crea igual.

---

## 6. Validación

- `dotnet build` 0 errores, sin advertencias nuevas · `dotnet test` verde (los 2.683 previos + los nuevos)
- `yarn build` 0 errores (único warning aceptado: el bundle budget preexistente)
- `node scripts/verificar-change-detection.js` — el modal nuevo con `changeDetection` explícito
- Smoke UI: el modal de preview abre y cierra **dos veces** sin colgarse
- Migración validada **por transacción** contra la BD local (que está atrasada respecto de `main` por
  migraciones de otras sesiones), con `ROLLBACK` y verificación de que no queda rastro
- Backend local apagado al terminar y puerto libre (`netstat`)

**Gate multipaís:** no aplica. Este bloque no toca `fn_seguimiento_diario_engorde`,
`fn_cuadre_alimento_engorde` ni ningún `*SaldoAlimento*` — no hay una sola línea de alimento ni de
aves en el camino.
