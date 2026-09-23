# Plan — Catálogo de ítems Santa Reyes: cierre de brecha + código ERP + Código Guía obligatorio

## Contexto / auditoría (hecha antes de escribir código)

El usuario adjuntó `Items.xlsx` (fuente ERP) con dos hojas:
- **Items Alimento** (45 filas): `Item` (código interno ERP), `Referencia`, `Desc. item`, `Tipo inventario`, `Desc. tipo inventario`.
- **Items Insumos** (246 filas): `Tipo de Inventario`, `Item`, `Desc. item` (combustibles, desinfectantes, vacunas, medicamentos, empaques, mantenimiento, materia prima, insumos varios).

Auditado contra la BD (Santa Reyes = `companies.id = 6`, `pais_id = 1`):

1. **`catalogo_items`** (alimenta `/api/catalogo-alimentos`, el selector de alimento de Levante/Producción y la pantalla admin del catálogo): Santa Reyes ya tiene 317 filas.
   - Los 45 de "Items Alimento" coinciden **byte a byte** (código, `metadata.referencia`, `metadata.tipoInventario`, `metadata.categoria`). Nada que migrar acá.
   - De los 246 de "Items Insumos", 244 ya están cargados igual (código + item_type + `metadata.categoria`). Los otros 2 (código 1307 `CAMPESINO SR H`, código 1268 `POLLA CRECIMIENTO SR H SIN COCC`) **no son un hueco**: son productos terminados que el ERP repite también en la hoja de insumos bajo `PRODUCTO TERMINADO`, y ya están correctamente cargados como `item_type='alimento'`.
   - Hay además 28 ítems `item_type='huevo'` en BD que no están en ninguna de las dos hojas (carga previa de otra sesión, fuera de alcance de este Excel).

2. **`item_inventario`** (alimenta stock, movimientos de Gestión de Inventario y el módulo **Gastos de Inventario**, que filtra `tipo_item <> 'alimento'`): Santa Reyes **solo tiene los 45 de alimento**. Los 244 de insumos nunca se copiaron acá. **Este es el hueco real**: hoy Gastos de Inventario le muestra 0 ítems a Santa Reyes y no se puede registrar entrada/consumo de nada que no sea alimento.

Decisiones ya confirmadas con el usuario:
- **Unidad de medida** (el Excel no la trae para insumos): inferirla del texto de la descripción (parseo de tokens como `KG`, `LT`, `GR`, `ML`, `GAL`, `DS`, `SACO`, `UND`…), con default por `item_type` cuando el texto no alcanza (validado en Python contra las 244 descripciones reales: 209 resueltas por texto, 35 por default de categoría — ninguna a ciegas).
- **Código Guía obligatorio**: en TODA empresa y todo tipo de ítem (Ingreso y Traslado), no solo Santa Reyes ni solo alimento — es la misma pantalla compartida y cierra además el hueco de detección de remisión duplicada, que hoy solo corre si el campo viene lleno.
- **Código de referencia ERP en vez del código interno**: en los 3 puntos donde hoy se muestra `codigo` de un ítem — selector de alimento (Levante/Producción), selector de Ingreso/Traslado/Histórico (Gestión de Inventario), listado/detalle del catálogo — con fallback al código interno cuando no hay referencia (dato-dependiente, sin ramificar por empresa/país).

## Alcance de este plan

### A. Migración EF — backfill `item_inventario` (Santa Reyes)
- Nueva migración `AddItemInventarioInsumosSantaReyes`.
- `Up`: `INSERT INTO item_inventario (...) SELECT ... FROM catalogo_items ci JOIN (VALUES (codigo, unidad_inferida), ...) u ON u.codigo = ci.codigo WHERE ci.company_id=6 AND ci.pais_id=1 AND ci.item_type NOT IN ('alimento','huevo') ON CONFLICT (company_id, pais_id, codigo) DO NOTHING`. `concepto` = `metadata->>'categoria'`; `referencia`/`tipo_inventario_codigo`/`descripcion_tipo_inventario` quedan NULL (los insumos no traen esas claves en `metadata`, igual que en el Excel).
- `Down`: `DELETE FROM item_inventario WHERE company_id=6 AND pais_id=1 AND item_type NOT IN ('alimento','huevo') AND codigo IN (<244 códigos>)`.
- La unidad de cada uno de los 244 códigos viaja embebida en el `VALUES` (calculada una sola vez, auditable en el diff de la migración).

### B. Cálculo puro — inferencia de unidad
- `Application/Calculos/InferenciaUnidadInventarioCalculos.cs`: `Inferir(string descripcion, string? tipoItem) : string`. Vocabulario de salida = el mismo de `UnidadInventarioCalculos` (`kg, und, l, ml, g, lb, saco, dosis, gal`).
- Tests xUnit (`tests/ZooSanMarino.Application.Tests/InferenciaUnidadInventarioCalculosTests.cs`): casos con token explícito (LT, KG, GR, ML, GAL, DS, SACO, UND, pegado a dígitos tipo `946ML`/`50KG`), casos sin token (fallback por `tipoItem`), y un par de las descripciones reales de Santa Reyes documentadas arriba.
- Esta función **solo la usa la migración** (dato precalculado); no participa de ningún camino de escritura en caliente.

### C. Código Guía obligatorio (backend)
- **En el CONTROLLER (`InventarioGestionController.RegistrarIngreso` / `RegistrarTraslado`), NO en el service.** Mismo patrón ya establecido ahí para el aviso de ventana de fecha y el de remisión duplicada ("así ningún llamador interno del service cambia de comportamiento"): `MigracionService.AlimentoEngorde` (carga masiva) llama a `RegistrarIngresoAsync`/`RegistrarTrasladoAsync` directamente con `Referencia` **opcional** para filas Ingreso/Traslado (solo la exige, con aviso, para Consumo — ver `MigracionService.AlimentoEngorde.cs:183-188`). Validar en el service habría roto la carga masiva existente.
- `RegistrarIngresoNivelGranjaAsync` (usado internamente por `ColombiaInventarioConsumoService` para devoluciones automáticas) **NO se toca** — no pasa por el controller.
- Nada cambia en edición (`ActualizarFechaIngresoAsync`/`ActualizarDestinoCicloIngresoAsync`): no tocan `Reference`, así que un movimiento viejo sin código guía sigue editable.

### D. Código Guía obligatorio (frontend)
- `gestion-inventario-page.component.ts`: `submitIngreso()` y `submitTraslado()` agregan el check de `ingresoReference`/`trasladoReference` no vacío antes de abrir el modal de confirmación (mismo patrón que las validaciones existentes de la función).
- `gestion-inventario-page.component.html` (líneas ~411 y ~639): agregar asterisco/indicación visual de obligatorio en la etiqueta "Código Guía".

### E. Mostrar código de referencia ERP (frontend)
- Nueva función pura `frontend/src/app/shared/utils/referencia-o-codigo.funcion.ts` (+ spec): `referenciaOCodigo(referencia, codigo): string` = referencia no vacía → referencia; si no, código; si no, `''`.
- Puntos a cambiar (reemplazan `item.codigo` por `referenciaOCodigo(...)` en la etiqueta mostrada al usuario; el `codigo` interno se sigue mandando igual en el payload, esto es solo de presentación):
  1. `lote-levante/pages/modal-create-edit/modal-create-edit.component.ts` → `getItemDisplayText()` (usa `item.metadata?.referencia`).
  2. `lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts` → `getItemDisplayText()` (ídem).
  3. `gestion-inventario-page.component.html` (4 selects/columnas: Ingreso, Traslado, filtro de Histórico, tabla del catálogo) + `gestion-inventario.service.ts` (agregar `referencia?: string | null` a `ItemInventarioDto` — el backend YA la manda, solo falta tiparla).
  4. `catalogo-alimentos-list.component.html` (columna Código de la tabla + detalle) — usa `item.metadata?.referencia`.

## Validación
- Backend: `dotnet build` + `dotnet test` (incluye los tests nuevos de inferencia de unidad).
- Migración: aplicar en BD local (`:5433`), verificar 244 filas nuevas en `item_inventario` para company 6, `unidad` razonable por muestreo, reejecución = 0 filas nuevas (idempotente), `Down` deja exactamente 45.
- Smoke doble (regla de features por empresa): una empresa SIN código de referencia (ej. Ecuador/Panamá) sigue viendo el código interno de siempre en los 4 puntos tocados — cero cambio visible; Santa Reyes ve la referencia ERP.
- Frontend: `yarn build` (0 errores) + Karma de los módulos tocados si existen specs.
- Código Guía: smoke manual de un Ingreso y un Traslado sin Código Guía → rechazado con mensaje claro, en front y en back (probar también pegándole directo al endpoint sin pasar por el front).
