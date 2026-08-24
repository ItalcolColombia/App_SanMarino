# F7.3 — Ítems de huevo permitidos por lote + filas fijas en el diario de producción

**Decisión del cliente (21-ago-2026, en sesión):** desbloquea `TK-2026-000180` / `SR-DEF-2`.

> «cuando yo creo un lote puedo seleccionar los tipos de huevos que me dará el lote. Necesito que
> esos tipos de huevos solo me aparezcan en la fase de producción, no todos los huevos. Y en el
> seguimiento diario de producción ya no tendrá que ser un select, sino que aparecerían por defecto
> los huevos permitidos para que coloquen su cantidad.»
>
> **Fail-closed, confirmado explícitamente:** «no, si no tiene asignado no aparece; ahí el usuario
> tiene que editar el lote para agregarle los tipos de huevos, así controlamos mejor todo.»

---

## 1. Estado auditado (6 cortes en paralelo, 21-ago-2026)

**El flujo de Santa Reyes nunca se ejercitó.** 0 lotes, 0 seguimientos con `huevoItems` en toda la
base. Las otras 4 empresas tienen `clasificacion_huevo_por_items = false`, así que **todo lo que se
construya detrás de ese flag tiene radio de impacto CERO sobre datos existentes.**

**Lo que ya está bien:** el guardado rechaza con 400 un ítem que no sea `item_type='huevo'` o que
sea de otra empresa, y resuelve la empresa efectiva por `farms.company_id` (no por el token), como
manda CLAUDE.md §Features por EMPRESA regla 3.

**Lo que NO existe:** ni tabla, ni columna, ni código de «ítems permitidos del lote». Verificado por
`information_schema`, por grep y por lectura de `ValidarHuevoItemsAsync` (recibe `loteId` pero solo
lo usa para resolver la empresa).

### Los 6 defectos que se arreglan en el mismo trabajo

| # | Defecto | Dónde |
|---|---|---|
| D1 | Editando un registro legacy con el flag ON, la pantalla dice **«Total de huevos: 0»** aunque el registro tenga huevos: las 11 columnas quedan ocultas y el total por ítems arranca vacío | `modal-seguimiento-diario.component.ts:1332`, `:1416` |
| D2 | El ítem `HUEVO RECUPERACION BOLSA KIL` (id 673, `um='KIL'`) se **pesa**, pero el contrato es `int` y el front hace `Math.round` **en silencio**: 12,5 kg se guardan como 13 | `HuevoItemSeguimientoDto.cs:23`, `modal-seguimiento-diario.component.ts:846` |
| D3 | `gruposHuevoItems` queda **contaminado entre aperturas** del modal (el componente no se destruye): `resetForm` limpia `huevoItemsGuardados` pero no rearma los grupos → ítems fantasma de un lote anterior | `modal-seguimiento-diario.component.ts:979` vs `:725` |
| D4 | El backend valida el ítem **sin mirar `activo`**; el front solo ofrece activos → los dos gates divergen | `ProduccionService.cs:376` vs `CatalogItemService.cs:311` |
| D5 | La vigencia de primera postura (F7.4) **no tiene un solo llamador en backend**: es 100 % UI. La fecha es editable dentro del modal, así que se elige el ítem en semana 21 y se guarda con fecha de semana 30 | `HuevoPrimeraPosturaCalculos.cs:32` (0 callers en `src/`) |
| D6 | El alta de **traslado de huevos no valida catálogo ni flag** — solo lo frena la disponibilidad, y un ítem con `Cantidad = 0` la pasa | `TrasladoHuevosService.cs:52` |

---

## 2. Alcance: SOLO producción (decidido con evidencia, no por preferencia)

Levante **no tiene modelo de ítems para huevos en ninguna capa**:

- `seguimiento_diario_levante` tiene las 11 columnas fijas; su `metadata` solo lleva alimento.
- Santa Reyes tiene `captura_huevos_en_levante = false`, y además `SeguimientoLoteLevanteService.cs:98`
  **excluye explícitamente** a las empresas de clasificación por ítems.
- 🔴 El arrastre de levante escribe las 11 columnas fijas y `AplicarTotalesHuevoPorItems`
  (`ProduccionService.cs:467-482`) **las pone en cero**. Son incompatibles hoy.

Llevar la lista blanca a levante no es un parámetro: es construir el modo por ítems en levante,
reescribir el arrastre y unificar dos gates divergentes. **Queda fuera, documentado.**

---

## 3. Modelo de datos — replica exacta de `lote_silos`

`lote_silos` es el patrón canónico de N:M por lote y está **probado en producción**. Cuelga de
`lotes.lote_id` (el maestro), **no** de los espejos de etapa — verificado que sobrevive el cierre de
levante (lote 13 K345A: levante `Cerrado` + producción `Abierta`, ambos al mismo `lote_id`).

```
lote_huevo_items
  id                  serial PK
  company_id          int NOT NULL
  lote_id             int NOT NULL  FK lotes(lote_id)        ON DELETE CASCADE
  catalog_item_id     int NOT NULL  FK catalogo_items(id)    ON DELETE RESTRICT
  activo              bool NOT NULL DEFAULT true
  created_at          timestamptz NOT NULL DEFAULT now()
  created_by_user_id  uuid NULL
  UNIQUE (lote_id, catalog_item_id)   -- ux_lote_huevo_items_lote_item
```

**Sin columna `orden` a propósito:** el orden de las filas fijas sale del catálogo
(`agruparItemsHuevoPorTipo`: Primera → Pnc → resto, y por label dentro de cada grupo), que ya existe
y ya está testeado. Una columna `orden` sería un segundo dueño del mismo número.

---

## 4. La regla, fail-closed, en cálculo puro

`HuevoItemsCalculos.ValidarPermitidos(items, permitidos)` — **función nueva**, no se toca `Validar`
(que la comparten traslados y carga masiva; cambiarla sería un cambio de comportamiento en 3
caminos a la vez).

| Caso | Resultado |
|---|---|
| Lote **sin** ítems asignados y llegan huevoItems | ❌ rechaza — «este lote no tiene tipos de huevo asignados; asignalos en el lote» |
| Ítem que **no está** en la lista del lote | ❌ rechaza, nombrando el ítem |
| Todos los ítems en la lista | ✅ pasa |
| Lista de items **vacía o null** (el request no trae clasificación) | ✅ pasa — no hay nada que validar, y es el caso «no tocar» de la edición |

Se aplica en los **dos** caminos de escritura de producción, que hoy ya divergen:

1. `ProduccionService.ValidarHuevoItemsAsync` (alta y edición manual)
2. `MigracionService.LeerHojaHuevosPosturaAsync` (carga masiva por Excel)

**NO** se aplica en traslados: un traslado mueve lo que YA se produjo. Si la lista blanca cambia
después, bloquear el traslado dejaría huevos reales atrapados.

---

## 5. Cambios por capa

### Backend

| Archivo | Acción |
|---|---|
| `Domain/Entities/LoteHuevoItem.cs` | crear (espeja `LoteSilo`) |
| `Infrastructure/Persistence/Configurations/LoteHuevoItemConfiguration.cs` | crear |
| `Infrastructure/Persistence/ZooSanMarinoContext.cs` | `DbSet<LoteHuevoItem>` |
| `Application/DTOs/LoteHuevoItemDtos.cs` | crear: `LoteHuevoItemDto`, `AsignarHuevoItemsDto` |
| `Application/Interfaces/ILoteHuevoItemService.cs` | crear |
| `Infrastructure/Services/LoteHuevoItemService.cs` | crear (espeja `LoteSiloService`) |
| `API/Controllers/LoteHuevoItemController.cs` | crear (espeja `LoteSiloController`) |
| `API/Program.cs` | registrar el service en DI |
| `Application/Calculos/HuevoItemsCalculos.cs` | `ValidarPermitidos` (nuevo, puro) |
| `Application/Calculos/HuevoPrimeraPosturaCalculos.cs` | `MensajeFueraDeVigencia` (nuevo, puro) — **D5** |
| `Infrastructure/Services/ProduccionService.cs` | `ValidarHuevoItemsAsync`: lista blanca + `Activo` (**D4**) + vigencia (**D5**) |
| `Infrastructure/Services/Migracion/Funciones/MigracionService.HuevosPostura.cs` | mismo gate en el Excel |
| `Infrastructure/Services/TrasladoHuevosService.cs` | validar catálogo + flag (**D6**) |
| migración EF | tabla + índice, idempotente |

### Frontend

| Archivo | Acción |
|---|---|
| `features/lote/services/lote-huevo-items.service.ts` | crear |
| `features/lote/components/modal-asignar-huevo-items/` | crear (espeja `modal-asignar-silos`) |
| `features/lote/components/lote-list/lote-list.component.*` | botón «Tipos de huevo», gateado por `clasificacionHuevoPorItems` |
| `features/lote-produccion/pages/modal-seguimiento-diario/*` | **filas fijas** + D1 + D2 + D3 |
| `features/lote-produccion/funciones/items-huevo-catalogo.funcion.ts` | `construirFilasFijas` (pura) |

> ⚠️ El formulario **vivo** de lote es `features/lote/components/lote-list/` — `features/lote/page/lote-list/`
> y `modal-create-edit-lote` son **huérfanos que declaran el MISMO selector `app-lote-list`**. Es una
> trampa activa: tocar el archivo equivocado compila y no hace nada.

---

## 6. UX de las filas fijas

- Una fila por ítem permitido, **agrupada por Primera / Pnc** con encabezado de grupo.
- La fila muestra código + nombre + unidad; el único control es la cantidad.
- Sin `<select>`, sin botón «agregar», sin botón «quitar» — el conjunto lo define el lote.
- Total en vivo por grupo + total general.
- **D2:** los ítems con `um = 'KIL'` aceptan decimales (`step="0.01"`); los de unidades siguen enteros.
- **D5:** un ítem de primera postura fuera de vigencia se muestra **deshabilitado y explicado**, no
  oculto — que desaparezca una fila sin decir por qué es peor que verla en gris.
- Lote **sin ítems asignados** → mensaje accionable, no una tabla vacía.

---

## 7. Casos de prueba

**Puros (xUnit) — `HuevoItemsCalculosTests` / `HuevoPrimeraPosturaCalculosTests`:**
1. `ValidarPermitidos` con lista vacía + items → rechaza (fail-closed).
2. `ValidarPermitidos` con item fuera de la lista → rechaza nombrándolo.
3. `ValidarPermitidos` con todos dentro → `null`.
4. `ValidarPermitidos` con `items` null/vacío → `null` aunque no haya lista.
5. `MensajeFueraDeVigencia`: vigente semana 22, no vigente 23, fail-open sin límite o sin semana.

**Front (Karma):** `construirFilasFijas` — orden Primera→Pnc, hidratación de cantidades guardadas,
ítem guardado que ya no está en la lista (se conserva, marcado).

**Multipaís:** el gate de CLAUDE.md **no aplica** — no se toca `fn_seguimiento_diario_*`,
`fn_cuadre_alimento_*` ni `*SaldoAlimento*`. Se verifica igual que `dotnet test` y `ng test` quedan
verdes y que Sanmarino/Demo (flag OFF) no cambian.

**Reportes (pedido explícito del usuario):** el contable y los otros 4 **no leen `huevoItems`** —
consumen `huevo_tot` / las 11 columnas. `AplicarTotalesHuevoPorItems` deja `huevo_tot` = suma de los
ítems, así que el total tiene que cuadrar. Se verifica con SQL: `huevo_tot` == suma de
`metadata->huevoItems`, y que el espejo y la fn semanal den el mismo número.
