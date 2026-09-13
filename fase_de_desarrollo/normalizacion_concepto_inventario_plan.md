# Plan — El desplegable «Concepto» lista el mismo concepto dos veces (Gestión de Inventario)

**Fecha:** 2026-08-05
**Módulo:** `gestion-inventario` (front) + `InventarioGestionService` / catálogo `item_inventario_ecuador` (back)
**Novedad del usuario:** en las pestañas Stock, Ingresos, Traslados y Catálogo ítems el `<select>` de
Concepto muestra dos opciones idénticas a la vista (`Otros insumos` / `Otros Insumos`) que devuelven
exactamente las mismas filas.

---

## 1. Diagnóstico (verificado contra la BD local, dump tipo-prod)

**Causa raíz: calidad de datos del catálogo + una lista armada con comparación sensible a mayúsculas.**

El filtro es **case-insensitive de punta a punta** — `GetStockAsync` (`itemType`), el filtro de concepto y
el de tipo de ítem del histórico y el `filterByConcept` del front normalizan todos a minúsculas. En cambio
las **listas de opciones** se arman con igualdad exacta:

| Desplegable | Dónde se arma | Cómo |
|---|---|---|
| Concepto (Stock · Ingresos · Traslados · Catálogo ítems) | [`gestion-inventario-page.component.ts:1184`](../frontend/src/app/features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.ts) | `new Set(...)` sobre el string crudo |
| Concepto · Tipo de ítem · Unidad (Histórico) | [`InventarioGestionService.GetHistoricoFiltrosAsync`](../backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestionService.cs) ~282-317 | `Distinct()` en SQL sobre el string crudo |

⇒ dos capitalizaciones = dos opciones que hacen lo mismo.

### Datos reales del catálogo (`item_inventario_ecuador`, 20 combinaciones concepto × tipo_item)

Las empresas **3** y **5** tienen catálogos clonados (la 5 se sembró el 2026-07-17 desde la 3):

| Empresa | Duplicado visible | Ítems |
|---|---|---|
| 3 | `Otros insumos` (36) vs `Otros Insumos` (6) | 42 |
| 5 | `Otros insumos` (34) vs `Otros Insumos` (6) | 40 |

**El duplicado `alimento` / `Alimento` NO se ve en pantalla**: el catálogo está acotado por empresa
(fail-closed, `InventarioCatalogoScopeCalculos`) y ninguna empresa tiene las dos variantes. `Alimento` (16)
vive en las empresas 3 y 5; `alimento` (1, ítem *Alimento ERP*) en la 4, junto a los 167 ítems con
`concepto IS NULL` que caen a `tipo_item = 'alimento'` por el `Concepto ?? TipoItem`. Consecuencia
importante para el diseño: **capitalizar ese único ítem a `Alimento` CREARÍA un duplicado en la empresa 4**
(`Alimento` del ítem + `alimento` del fallback de los 61 NULL). Por eso la normalización se hace
**por empresa**, no global.

### El `insumo` suelto (1 ítem)

`AV0351 · AV. LIV 52 PROTEC 5 LTR` tiene `concepto = 'insumo'` en la empresa 5 y `concepto = 'Otros insumos'`
en la 3 — **es la única divergencia entre los dos catálogos clonados** (verificado por comparación
código a código). Es un valor mal cargado, no una categoría propia: `insumo` es un `tipo_item`, y como
concepto lo usa **1 solo ítem de toda la base** frente a los 82 de `Otros insumos`.

### Hallazgos al pasar (auditoría del alcance del cambio)

1. 🔴 **`inventario_gasto_detalle.concepto` es un snapshot con comparación EXACTA.** El módulo Gastos de
   inventario ofrece el desplegable desde `item_inventario_ecuador` (`GetConceptosAsync`) pero filtra
   con `dc.concepto = p_concepto` ([`fn_inventario_gastos_search.sql:112`](../backend/sql/fn_inventario_gastos_search.sql)
   y `InventarioGastoService:201`). Si se normaliza el catálogo **sin** tocar el snapshot, en producción
   las líneas cuyo snapshot quedó con la capitalización vieja dejan de ser filtrables. ⇒ la migración
   **debe** sincronizar también esa columna.
2. 🟠 **Deuda preexistente, fuera de alcance:** 10 líneas de gasto tienen `concepto = 'insumo'` apuntando
   al ítem **57** (empresa 3), cuyo concepto siempre fue `Otros insumos`. Hoy ya son infiltrables
   (el desplegable nunca ofrece `insumo` en esa empresa). Corregirlas sería reescribir una
   categorización histórica sobre una hipótesis ⇒ se documenta y se deja fuera.
3. 🟠 **La columna `unit` del histórico tiene el mismo defecto** (`und` / `UND` en los movimientos) ⇒ el
   desplegable «Unidad» del Histórico también duplica. El filtro de unidad ya es case-insensitive, así
   que entra en la misma corrección de listas. `estado` y `movement_type` NO tienen variantes (los
   escribe el código con literales fijos) ⇒ no se tocan.
4. ✅ **Ninguna lógica depende del texto exacto del concepto** en gestión de inventario: `IsAlimento`
   (backend, `OrdinalIgnoreCase`), `esFilaAlimento` / `isAlimentoConcept` / `filterByConcept` (front,
   `toLowerCase()`) y los cuatro filtros del API comparan normalizado. La única igualdad exacta viva es
   la del punto 1.

---

## 2. Enfoque

Las dos patas que propuso el usuario, porque resuelven cosas distintas:

- **Datos** (migración): elimina el duplicado de raíz y deja el catálogo consistente.
- **Listas** (código): red de seguridad que vale para cualquier empresa y para el estado de la BD
  **antes** de que la migración corra. Es lo que impide que el defecto vuelva con el próximo Excel de
  catálogo mal capitalizado.

**Regla canónica única, escrita una vez por lado (back C# / front TS) y espejada en el SQL:** para cada
grupo de valores que solo difieren en mayúsculas/espacios, **gana la variante más usada**; empate ⇒ la
que ordena primero. Así la etiqueta que muestra la pantalla y la que escribe la migración coinciden.

---

## 3. Cambios

### 3.1 Backend — listas del histórico

- **`Application/Calculos/EtiquetasFiltroInventarioCalculos.cs`** (NUEVO, puro, `static`)
  - `Normalizar(string?)` → `Trim().ToLowerInvariant()` (la misma clave que usan los filtros del API).
  - `EtiquetasUnicas(IEnumerable<(string Valor, int Usos)>)` → una etiqueta por grupo normalizado
    (mayoritaria; empate ⇒ `Ordinal` ascendente), descarta vacíos, ordena `OrdinalIgnoreCase`
    (**mismo orden que hoy**).
- **`InventarioGestionService.GetHistoricoFiltrosAsync`**: las tres consultas de `conceptos`,
  `tiposItem` y `unidades` pasan de `Distinct()` a `GROUP BY valor + count(*)` (para tener la frecuencia
  en SQL, sin traer filas) y delegan la lista final en el cálculo. `estados` y `movementTypes` quedan
  como están.
- **Tests xUnit** `EtiquetasFiltroInventarioCalculosTests` — gate CI.

### 3.2 Frontend — lista del catálogo

- **`features/gestion-inventario/funciones/conceptos-catalogo.funcion.ts`** (NUEVO, puro)
  - `conceptoEfectivo(item)` = `concepto ?? tipoItem` trimeado (el fallback que ya usa el módulo).
  - `normalizarConcepto(valor)` = `trim().toLowerCase()`.
  - `conceptosUnicos(items)` = misma regla canónica que el backend, ordenado con `localeCompare`
    (**mismo orden que hoy**).
- **`gestion-inventario-page.component.ts` → `loadCatalogItems`** delega en `conceptosUnicos`
  (orquestador delgado). Nada más cambia: `selectedConcept`, `filterByConcept` e `isAlimentoConcept`
  ya son case-insensitive.
- **Spec** `conceptos-catalogo.funcion.spec.ts` + índice del `funciones/README.md`.

### 3.3 Migración de datos (EF data-only, idempotente)

`20260805180000_NormalizarConceptoCatalogoInventario` + copia trazable en
`backend/sql/normalizar_concepto_catalogo_inventario.sql`. Tres reglas **dinámicas** (ninguna nombra
ids ni literales de negocio):

| Regla | Qué hace | Guardas |
|---|---|---|
| 1 | `item_inventario_ecuador`: por **empresa** y grupo normalizado, unifica a la variante mayoritaria (y trimea) | `IS DISTINCT FROM` ⇒ idempotente. Por empresa ⇒ no puede crear un duplicado nuevo contra el fallback `tipo_item` |
| 1b | `inventario_gasto_detalle`: alinea el snapshot con el concepto del ítem **solo cuando coinciden en minúsculas** (pura capitalización) | Nunca recategoriza: si el valor difiere de verdad, no entra |
| 2 | Mismo `codigo` con conceptos divergentes entre empresas ⇒ gana el concepto **más usado del catálogo** | El perdedor tiene que ser marginal (≤ 1 ítem) y el ganador usado por varios |

`Down()` **no-op deliberado** (no se puede restaurar una capitalización arbitraria; los valores previos
quedan en el `.sql` trazable).

### 3.4 Verificación

`backend/sql/verificar_conceptos_catalogo_inventario.sql` — reporte de 4 consultas (grupos duplicados
por empresa, códigos divergentes, snapshot desincronizado, conceptos huérfanos).

---

## 4. Casos de prueba

**Cálculo puro (back y front, mismos casos):**
1. Sin variantes ⇒ la lista sale idéntica a hoy (orden incluido).
2. `Otros insumos` (36) + `Otros Insumos` (6) ⇒ una sola opción, `Otros insumos`.
3. Empate de frecuencia ⇒ gana la que ordena primero (determinismo).
4. Vacíos / `null` / solo espacios ⇒ descartados.
5. Espacios alrededor ⇒ agrupan con la variante limpia.
6. (Front) `concepto = null` ⇒ cae a `tipoItem`; y `Alimento` del concepto agrupa con `alimento` del
   fallback en una sola opción.

**Migración (simulada en transacción + `ROLLBACK` antes de escribir nada):**
- Regla 1 alcanza **12 filas** (6 de la empresa 3 + 6 de la 5); regla 1b **0**; regla 2 **1** (el ítem 356).
- La empresa 4 (`alimento`) queda **intacta**.
- Cero grupos duplicados y cero códigos divergentes después; el conteo de ítems por empresa no cambia.
- Segunda pasada ⇒ `UPDATE 0` en las tres reglas.

**Validación:** `dotnet build` + `dotnet test`, `yarn build` + `yarn test`, y smoke por pantalla del
desplegable en las 5 pestañas.
