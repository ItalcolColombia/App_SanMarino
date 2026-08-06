# Plan — las 10 líneas de gasto con `concepto = 'insumo'` (item 57 · AV0351)

**Fecha:** 2026-08-05 · **Módulo:** Gastos de inventario (Ecuador, empresa 3 ItalcolEcuador)
**Antecedente:** [`normalizacion_concepto_inventario_plan.md`](normalizacion_concepto_inventario_plan.md)
(sesión paralela, rama `claude/priceless-bhabha-c60ee5`, commit `84bf74f`), que dejó este caso
**fuera de alcance a propósito** por considerarlo una hipótesis.

---

## 0. El hallazgo original

10 filas de `inventario_gasto_detalle` con `concepto = 'insumo'` apuntan al item 57
(`AV0351 · AV. LIV 52 PROTEC 5 LTR`, empresa 3), cuyo `concepto` en el catálogo es `Otros insumos`.

El desplegable de Concepto del módulo se arma **desde el catálogo**
(`InventarioGastoService.GetConceptosAsync`, línea 49) pero el filtro compara con **igualdad exacta**
sobre el snapshot (`fn_inventario_gastos_search.sql` ~112 y `InventarioGastoService.cs` ~201).
Como el catálogo ya no ofrece `insumo`, esas 10 líneas son **infiltrables por concepto** y salen con
una etiqueta distinta a la de su ítem en la tabla y en el Excel del reporte.

---

## 1. Investigación del origen — CERRADA, con evidencia

### 1.1 No fue una carga ni un seed: entró por la pantalla, y el writer hizo lo correcto

- **Un solo escritor.** `inventario_gasto_detalle` se escribe únicamente en
  `InventarioGastoService.CreateAsync` (líneas 491/503). No hay carga masiva, ni seed, ni `INSERT`
  crudo: los dos `.sql` del módulo (`fn_inventario_gastos_search`, `fn_inventario_gastos_existencias`)
  solo leen, y la única migración que nombra la tabla es su `CREATE TABLE`.
- **La auditoría confirma la UI.** Las 10 cabeceras tienen su `Crear` en `inventario_gasto_auditoria`,
  con payload de pantalla, repartidas en **8 días** (2026-07-14 → 2026-07-27) y **4 usuarios**
  distintos (`968091594`, `36869593`, `223963310`, `428835595`). Una incluso registra
  `Eliminar` con motivo `"Eliminación desde UI (gasto #135)"`.
- **El writer nunca cambió.** `git log -S` sobre `Concepto = item.Concepto` y sobre el mensaje
  `"no pertenece al concepto seleccionado"` devuelve un único commit: `b6f5d16` (**2026-03-25**), el
  que crea el módulo. Es decir, desde marzo el `CreateAsync`:
  1. valida `item.Concepto == req.Concepto` normalizado (línea 447) y **rechaza** si difieren, y
  2. guarda `Concepto = item.Concepto` (línea 495).

  Con el código de hoy esas 10 filas **son imposibles de crear**.

### 1.2 Conclusión forzosa: el `concepto` del ítem 57 SÍ fue distinto — era `insumo`

Si el writer valida y copia desde el ítem, la única forma de que el snapshot diga `insumo` es que el
**catálogo dijera `insumo`** en ese momento. Y hay un testigo independiente que lo prueba:

> **La migración `20260717192803_SeedItemInventarioPanamaDesdeEcuador`** clona el catálogo de la
> empresa 3 a la 5 con un `INSERT ... SELECT src.concepto ... WHERE src.company_id = 3`, sin
> transformar nada. Corrió el **2026-07-17 15:34:43** (es el `created_at` idéntico de los 148 ítems
> de la empresa 5) — **en plena ventana** de las 10 filas.
>
> Su copia de AV0351 (**item 356**) sigue hoy con `concepto = 'insumo'`, y es la **única divergencia
> de concepto entre los 148 códigos compartidos** por los dos catálogos.

Item 356 es, literalmente, una foto de lo que decía el item 57 el 2026-07-17. Decía `insumo`.

### 1.3 `insumo` nunca fue un concepto: es un `tipo_item` en la columna equivocada

`insumo` es un valor legítimo de **`tipo_item`** — 29 ítems de la empresa 3 lo tienen — y no es un
concepto de negocio en ninguna empresa. El patrón sigue vivo: el item 467 (`AV0531 · PHARBIODEX`,
alta del 2026-08-04) tiene `tipo_item = 'insumo'` con `concepto = 'Otros insumos'`, que es la
combinación correcta. Al item 57 le quedó el `tipo_item` cargado en la columna `concepto`.

Mientras eso fue así, `GetConceptosAsync` **ofrecía `insumo`** en el desplegable de la empresa 3
(lee `i.Concepto` de los ítems activos): los usuarios no inventaron el valor, lo eligieron de la lista.

### 1.4 La corrección del catálogo ya ocurrió — el 2026-07-27, por fuera de la aplicación

- Última línea con `insumo`: gasto 175, creado **2026-07-27 08:17:07**.
- Primera línea del mismo ítem con `Otros insumos`: gasto 231, creado **2026-07-27 17:05:06**.
- ⇒ el `concepto` del item 57 se corrigió **entre esas dos horas del 2026-07-27**.
- Pero `item_inventario_ecuador.updated_at` del item 57 sigue en **2026-03-23 09:47:06**, el
  timestamp del seed masivo (compartido con otros 125 ítems).

`ItemInventarioService.UpdateAsync` (línea 177-178) y la importación por Excel (líneas 249-250)
**siempre** tocan `UpdatedAt`. Que no se haya movido significa que la corrección **no pasó por la
aplicación**: fue SQL crudo contra la base, sin auditoría. Es el patrón ya documentado
(`updated_at` viejo + valor nuevo + cero auditoría = escritura externa).

> `xmin` no sirve para datar acá: las 467 filas de `inventario_gasto_detalle` comparten `xmin = 52338`
> porque la BD local se restauró de un dump de prod en un solo bloque.

### 1.5 Línea de tiempo

| Fecha | Hecho | Evidencia |
|---|---|---|
| 2026-03-23 09:47 | Seed del catálogo empresa 3. Item 57 nace con `concepto = 'insumo'` | `created_at`/`updated_at` compartidos por 126 ítems |
| 2026-03-25 | `b6f5d16` crea el módulo con el guard y `Concepto = item.Concepto` | `git log -S` |
| 2026-07-14 13:46 → 2026-07-27 08:17 | **Las 10 líneas.** 4 usuarios, 8 días, todas por UI, todas de una sola línea sobre el item 57 | `inventario_gasto_auditoria` |
| 2026-07-17 15:34 | El seed a Panamá congela `insumo` en el item 356 | `20260717192803`, `created_at` empresa 5 |
| 2026-07-27 entre 08:17 y 17:05 | Alguien corrige el catálogo a `Otros insumos` **por SQL crudo** | `updated_at` sin mover |
| 2026-07-27 17:05 y 2026-07-29 | El mismo ítem se consume ya como `Otros insumos` (2 líneas) | `inventario_gasto_detalle` |

**Resultado:** no queda ninguna hipótesis. `insumo` fue un **defecto de carga del catálogo**
(un `tipo_item` en la columna `concepto`), no una categorización de negocio; el módulo lo propagó
fielmente al snapshot; y el catálogo ya fue corregido aguas arriba dejando el snapshot atrás.

---

## 2. La decisión

| | Qué hace | A favor | En contra |
|---|---|---|---|
| **(a) Corregir las 10 filas** a `Otros insumos` | Migración EF data-only idempotente, regla dinámica | Las vuelve filtrables y consistentes con su ítem y con las otras 2 líneas del mismo producto. No reescribe una categorización real: `insumo` nunca fue un concepto. Completa una corrección que ya se hizo en el catálogo | Toca datos históricos; el snapshot deja de reflejar *literalmente* lo que se guardó ese día |
| **(b) Ampliar el desplegable** con los conceptos presentes en el snapshot | Cambio de lectura, cero escritura | No toca historia | Deja `insumo` como opción **permanente** en la UI: expone un defecto de carga como si fuera una categoría, para siempre y en las 3 pantallas |

**Recomendación: (a).** El motivo por el que se dejó fuera de alcance —«sería reescribir una
categorización histórica sobre una hipótesis»— ya no aplica: está probado que `insumo` es el
`tipo_item` mal cargado del mismo producto, no una clasificación distinta. El snapshot debe congelar
*cómo estaba clasificado el consumo*, y la clasificación de ese producto siempre fue «Otros insumos»
— el catálogo solo lo decía mal.

> Si el usuario prefiere (b), el cambio es en `GetConceptosAsync` (unir catálogo ∪ snapshot de la
> empresa) y en la función pura del desplegable; no requiere migración.

---

## 3. Implementación de (a)

### 3.1 Migración `20260805190000_CorregirConceptoInsumoSnapshotGastos`
Data-only, `Designer` clonado, **sin tocar `ModelSnapshot`**. Ordena **después** de la
`20260805180000` de la sesión paralela (no depende de ella: las reglas no se solapan).

**Regla dinámica, sin ids ni etiquetas de negocio.** Alinea una línea de gasto con el concepto de su
ítem solo cuando se cumple TODO:
1. el concepto del snapshot **difiere en valor** del de su ítem (no solo en capitalización — ese caso
   es de la Regla 2 de la migración hermana);
2. el concepto del snapshot **no existe** como concepto en el catálogo de la empresa del gasto
   (⇒ es infiltrable: el desplegable no lo ofrece);
3. el concepto del snapshot **sí existe como `tipo_item`** en ese catálogo (⇒ es un `tipo_item`
   colado en la columna, que es el defecto probado, y no una categoría de negocio retirada);
4. el ítem tiene hoy un concepto no vacío al que alinearse.

`IS DISTINCT FROM` en el `UPDATE` ⇒ re-ejecutar da `UPDATE 0`. Si en producción los datos no
cumplen las cuatro condiciones, la fila **no entra** y la migración es un no-op.

`Down()`: no restaura (no hay dónde guardar el valor viejo); se documenta como irreversible por
diseño, igual que la migración hermana.

### 3.2 Validación
- Simulación `BEGIN; … ; ROLLBACK` midiendo filas afectadas **antes** de escribir.
- `backend/sql/verificar_conceptos_catalogo_inventario.sql` (consulta 4 = el detector): pasa de
  10 líneas a 0.
- Conteos por concepto del reporte **antes/después**: `Otros insumos` +10, `insumo` desaparece,
  el total de líneas **no cambia**.
- `dotnet build` + `dotnet test`.

### 3.3 Coordinación con la sesión paralela
- La BD local es **compartida**: durante esta investigación la rama `priceless-bhabha` aplicó y
  revirtió su migración `20260805180000` (verificado: no está en `__EFMigrationsHistory` y los
  duplicados de capitalización volvieron). Las mediciones de acá se tomaron sobre el estado
  **sin** esa migración aplicada.
- `verificar_conceptos_catalogo_inventario.sql` **vive en la rama hermana**, no en ésta: no se
  duplica acá para no crear conflicto de merge. Al integrar, su comentario de la consulta 4
  («Deuda conocida al 05-ago-2026: 10 líneas…») queda obsoleto y hay que actualizarlo.

---

## 4. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| T1 | Las 10 filas del item 57 | pasan a `Otros insumos` |
| T2 | Re-ejecutar la migración | `UPDATE 0` |
| T3 | Líneas que solo difieren en capitalización | **no** las toca (son de la migración hermana) |
| T4 | Línea cuyo concepto existe en el catálogo de su empresa | **no** la toca |
| T5 | Línea cuyo concepto no es un `tipo_item` del catálogo | **no** la toca (podría ser una categoría real retirada) |
| T6 | Total de líneas de `inventario_gasto_detalle` | invariante |
| T7 | Gastos en estado `Eliminado` (gasto 135) | se corrigen igual: el reporte ya los excluye por estado |
