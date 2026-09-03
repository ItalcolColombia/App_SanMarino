# Consolidación de traslados/ventas de aves y huevos — Fase 0 + Propuesta B

> **Decisión del usuario (3-sep-2026):** ejecutar **Fase 0** (borrar código muerto) +
> **Propuesta B** (consolidar el front SIN tocar el motor de backend). Las propuestas A
> (un solo formulario + un solo camino backend) y C (especializar por intención) quedan
> archivadas para más adelante.

## Contexto medido

El movimiento de aves tenía **4 puntos de entrada de escritura vivos** en el front (no 3), y los
huevos **3**. Detrás hay 5 endpoints backend, de los cuales sólo 3 tienen llamador real.

Mediciones contra la BD local (`:5433`, Postgres nativo):
- `movimiento_aves`: 19 filas (12 Camino C `TSD-%`, 7 Camino A/D).
- **0 filas `TSD-%` tienen fila de `inventario_aves`** que se pudiera inflar al anular ⇒ el hueco
  de diseño existe en el código pero **no hay daño consumado**.
- `/movimientos-aves/lista` → **10 roles**; `/traslados-huevos/lista` → **9 roles** (vivos).
- `/traslados-huevos/nuevo` → **0 roles** (ruta fantasma, confirmada).

## Enfoque arquitectónico

**Refactor ≠ cambio de comportamiento.** Los caminos backend A (`POST /api/traslados/aves`),
C (`POST /api/traslados/aves-desde-seguimiento`) y D (`POST /api/MovimientoAves`) **no cambian
su contrato ni su aritmética**. Sólo se borra lo que no tiene llamador (B1/B2) y se reorganiza
el front.

Metodología `funciones/` + `models/` de CLAUDE.md (referencia canónica:
`movimientos-pollo-engorde`) para todo lo que se mueva del god-service.

---

## Fase 0 — Borrar código muerto confirmado

| # | Qué | Dónde | Verificación previa |
|---|---|---|---|
| F0.1 | Modal inline "Traslado" del dashboard (`abrirModalTraslado`/`cerrarModalTraslado`/`procesarTraslado`/`limpiarFormularioTraslado`/`getTotalAves`/`trasladarTodasLasHembras`/`trasladarTodosLosMachos`/`trasladarTodo`/`navegarATraslados`/`navegarANuevoTraslado` + estado + su bloque HTML + SCSS) | `traslados-aves/pages/inventario-dashboard/` | Ningún `(click)` del template lo abre |
| F0.2 | `RegistrosTrasladosComponent` completo (ts + html + scss + spec) | `traslados-aves/pages/registros-traslados/` | Sin ruta en `app.config.ts`; sólo lo referencia su propio spec |
| F0.3 | `TrasladoNavigationListComponent` + `TrasladoNavigationCardComponent` | `traslados-aves/components/` | Cadena huérfana: la `list` no la usa nadie, la `card` sólo la `list` |
| F0.4 | Caminos **B1/B2** backend: `EjecutarVentaAsync`/`EjecutarTrasladoAsync`, sus endpoints `ejecutar-venta`/`ejecutar-traslado`, las firmas de `IMovimientoAvesService` y los DTOs `EjecutarVentaAvesRequest`/`EjecutarTrasladoAvesRequest` si quedan huérfanos | `MovimientoAvesController.cs`, `MovimientoAvesService.EjecucionDirecta.cs`, `IMovimientoAvesService.cs` | Re-verificado por grep: 0 llamadores en todo el repo |
| F0.5 | Huérfanos derivados: imports, tipos, specs que sólo testeaban lo borrado | — | `yarn build` + `dotnet build` |

⚠️ **`ejecutar-traslado-cierre-levante` (B3) NO se toca**: lo llama
`seguimiento-lote-levante-list.component.ts:1184` en el cierre de levante.

---

## Propuesta B — Consolidación del front

### B1 · Partir `TrasladosAvesService` (747 líneas) por dominio
El service mezcla 5 dominios. Se parte dejando en `traslados-aves/services/` sólo traslados:
- `inventario-aves.service.ts` → `/InventarioAves/*`
- `historial-inventario.service.ts` → `/HistorialInventario/*`
- `traslados-aves.service.ts` (queda) → `/traslados/*`, cohortes, `Lote/trasladar`, `MovimientoAves` puntual
Los tipos compartidos van a `traslados-aves/models/`. **Re-exportar** desde el service viejo para
no romper imports externos (regla de CLAUDE.md).

### B2 · Una sola fuente de disponibilidad
Las 4 pantallas vivas leen **sólo** `GET /traslados/lote/{id}/disponibilidad`.

🔴 **GATE DE VERIFICACIÓN OBLIGATORIO**: antes de sacar el fallback a `resumen-mortalidad` /
`avesHActual` del modal de seguimiento, comparar con **datos reales** (backend local + BD) que
las dos fuentes den el **mismo número** en 2-3 lotes. **Si divergen ⇒ PARAR y reportar.** No es
una decisión a tomar en esta tarea.

### B3 · Selector de lote destino compartido
Extraer la cascada Granja→Núcleo→Galpón→Lote de `modal-traslado-aves-seguimiento` a
`traslados-aves/components/selector-lote-destino/` y usarla en el dashboard, en
`/traslados-aves/nuevo` y en `movimientos-aves`, reemplazando los `<input type="text">` de
"ID del lote destino".

### B4 · Colapsar `/traslados-aves/nuevo` sobre el dashboard
La ruta del menú ("Nuevo Traslado" → `/traslados-aves/traslados` → redirect) debe seguir
funcionando. Opción más simple que no rompe el link.

### B5 · Huevos
- Borrar `/traslados-huevos/nuevo` + `TrasladoHuevosFormComponent` (0 roles, ruta fantasma).
- En el dashboard de aves, la pestaña Huevos deja de tener formulario propio
  (`procesarTrasladoHuevos()`, sin soporte de `huevoItems`) y monta
  `ModalTrasladoHuevosComponent` (que sí soporta ítems) con el lote preseleccionado.
- **Cierra el bloque F10 del tracker** (`- [!]` "4º lugar con el mismo bug").

### B6 · Primitivas obligatorias del sistema de diseño
- `window.prompt` de `anularMovimientoAves` → `ConfirmDialogService`.
- Errores/éxitos inline sueltos → `ToastService` donde falte.
- `changeDetection: ChangeDetectionStrategy.Eager` **explícito** en todo componente nuevo.

### B7 · (fuera de alcance) `.sql` de instrumentación
Queda para cuando se evalúe la Propuesta A.

---

## Reglas de negocio a preservar (no cambian)

1. El Camino C sigue siendo el único con gate `permite_traslado_aves_cross_etapa` y el único que
   escribe las dos patas (SALIDA + INGRESO) de la fila diaria.
2. El Camino A sigue pre-validando con `ValidarDisponibilidadAvesAsync`.
3. `TrasladoHuevosService`: con `HuevoItems` exige LPP; sin ellos, flujo legacy byte a byte igual.
4. La ventana de fecha (`PERMISO_FECHA_RETROACTIVA`) se conserva donde ya estaba.
5. `ocultaMachosEnPostura` y `clasificacionHuevoPorItems` siguen siendo fail-closed.

## Casos de prueba

- `yarn build` limpio (único warning aceptado: bundle budget preexistente).
- `dotnet build` 0 errores tras borrar B1/B2.
- Specs puntuales de lo tocado (no la suite completa).
- Rutas a probar con browser (las hace el coordinador): `/traslados-aves/dashboard`,
  `/traslados-aves/nuevo`, `/traslados-aves/movimientos`, `/traslados-aves/historial`,
  `/traslados-huevos/lista`, `/movimientos-aves/lista`, seguimiento diario levante y producción.

---

## Resultado de la ejecución (3-sep-2026)

**Fase 0 completa** y **Propuesta B completa salvo B2**, que quedó **bloqueada por su propio gate**.

### 🔴 B2 no se hizo: las dos fuentes de disponibilidad divergen estructuralmente

El gate del plan pedía verificar con datos reales antes de sacar el fallback. Se verificó
(`backend/sql/verificar_paridad_disponibilidad_aves.sql`, copia local): **9 de 15 lotes con
actividad divergen, hasta 19.385 aves**. No es redondeo — son 4 diferencias de fórmula:

| Dimensión | `GET /Lote/{id}/resumen-mortalidad` | `GET /traslados/lote/{id}/disponibilidad` |
|---|---|---|
| Base | `lote_etapa_levante.aves_inicio_hembras` ?? `lotes.hembras_l` | siempre `lotes.hembras_l` |
| Mortalidad de caja | la resta | **no** la resta |
| Bajas de **producción** | **no** las resta | las resta |
| Traslados | columnas acumuladas del **espejo** | filas de **`movimiento_aves`** |

Y **ninguna de las dos es correcta en todos los casos**:
- Lote 14: `disponibilidad` da 23 (el número correcto, ya medido en una sesión previa) y
  `resumen-mortalidad` daría 10.447 → gana `disponibilidad`.
- Lotes 116/124/128/129 (receptores de un traslado): `disponibilidad` da **0** aunque recibieron
  miles de aves, porque los `TSD-*` anteriores a ago-2026 tienen `lote_destino_id` **NULL** →
  gana `resumen-mortalidad`.

Unificar habría cambiado números que autorizan traslados. **Decide el usuario.**

### Lo que sí se hizo

- **Fase 0**: −3.400 líneas de front muerto (modal inline del dashboard, `RegistrosTraslados`,
  `TrasladoNavigationList/Card`) + Caminos B1/B2 del backend (endpoints, service, interfaz, DTOs).
- **B1**: `TrasladosAvesService` 747 → 144 líneas; 29 interfaces a `models/`; `InventarioAvesService`
  y `HistorialInventarioService` propios; `manejarErrorHttp` como función pura compartida;
  15 métodos sin llamador eliminados. Re-export desde el service viejo ⇒ ningún import externo cambió.
- **B3**: cascada Granja→Núcleo→Galpón→Lote (`app-filtro-select`) en el dashboard, en lugar del
  `<input type="text">` de "ID del lote destino".
- **B4**: `/traslados-aves/nuevo` y `/traslados-aves/traslados` redirigen al dashboard;
  `TrasladoAvesComponent` borrado. El ítem de menú sigue funcionando.
- **B5**: `/traslados-huevos/nuevo` borrado (0 roles); la pestaña Huevos del dashboard monta
  `ModalTrasladoHuevosComponent`. **Cierra el bloque F10 del tracker.**
  Detalle que casi se escapa: montar el modal solo con `loteId` **no** habría arreglado a Santa
  Reyes — el traslado por ítems exige `LotePosturaProduccionId` en `TrasladoHuevosService`. El
  backend ya devuelve ese id en la respuesta por lote, pero la interfaz del front
  (`DisponibilidadLoteDto` de `traslados-aves`) **no lo declaraba**, así que el dato llegaba y se
  descartaba. Se declaró y el dashboard lo resuelve antes de abrir el modal (fail-open al flujo
  legacy si la disponibilidad falla).
- **B6**: `ConfirmDialogService.askText()` (campo de texto opcional en `ConfirmationModalComponent`,
  aditivo y no-breaking) reemplaza el `window.prompt` de `anularMovimientoAves`.

### Deuda que queda anotada (fuera de alcance de esta sesión)

1. **4 `window.prompt` más** en el flujo de ajuste manual de inventario del dashboard
   (`inventario-dashboard.component.ts:596-612`): son 4 prompts encadenados; merecen un formulario
   propio, no una traducción 1:1 a `askText()`.
2. **`CreateAsync` sigue ignorando el resultado de `ProcesarMovimientoAsync`**
   (`MovimientoAvesService.Crud.cs:70-83`): el efecto ya es atómico, pero la API puede devolver
   **201 con el movimiento en `Pendiente`** sin avisarle al operario.
3. **Anular un `TSD-*` desde el dashboard** corre el motor de reversión del Camino A/D, que suma
   aves a `inventario_aves` — tabla que el Camino C nunca decrementa. Medido: **0 filas afectadas
   hoy** en la copia local, así que no hay daño consumado, pero el hueco sigue en el código.
4. **`FiltroSelectComponent` es polimórfico**: con `[filterDataUrl]` emite `lotePosturaProduccionId`
   y sin él emite el id de lote base. No es un bug hoy (cada consumidor usa el modo que necesita),
   pero es una trampa para quien lo reutilice sin leerlo.
