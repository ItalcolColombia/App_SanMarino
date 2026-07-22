# Plan — Seguimiento Diario Reproductora Pollo Engorde: fechas y edición

**Módulo:** `frontend/src/app/features/seguimiento-diario-lote-reproductora` + `backend/.../Services/SeguimientoDiarioLoteReproductoraService.cs`
**Fecha:** 2026-07-22

## Contexto / síntomas reportados
1. El **primer registro muestra un día distinto** entre la tabla y el modal de detalle; "el primer consumo no se sincroniza y no cuadra".
2. Debe **impedirse** cargar una fecha **anterior a la del encasetamiento** del lote reproductora.
3. Al **reabrir un seguimiento** debe poder editarse la **fecha y algunos campos** (detalle); pero si se cambia **alimento o mortalidad** hay que **eliminar el registro** para que el sistema **retorne aves + consumo** y recargue.

## Diagnóstico (causas raíz verificadas en código)
- **Off-by-one tabla vs modal:** la tabla usa `{{ s.fechaRegistro | date:'dd/MM/yyyy':'UTC' }}` (correcto, regla `fechas-puras`). El **modal de detalle** (`modal-detalle-seguimiento-reproductora.component.ts` → `formatDate`) usa `new Date(d).toLocaleDateString('es-ES')` → convierte a zona local → para datos guardados a medianoche UTC muestra el **día anterior**. Ese es el desajuste tabla↔modal.
- **"El primer consumo no cruza":** `fn_cruce_reproductora_a_engorde` cruza **solo edades 1..7** (`edad = fecha_registro::date − fecha_encaset::date`). Un primer registro cargado **el mismo día del encaset (edad 0)** nunca cruza (caso LOTE 32). LOTE 35 (primer registro en encaset+1 = edad 1) sí cruza. → El primer registro debe quedar en **edad ≥ 1**.

## Decisiones (confirmadas con el usuario)
1. **Fecha mínima = encaset + 1 (edad 1).** No se toca la función SQL de cruce.
2. **Editables en sitio (edición):** fecha, ciclo, observaciones, pesos promedio (H/M) y agua. **Alimento/consumo y mortalidad/selección/error de sexaje quedan bloqueados** → para cambiarlos, eliminar el registro y recrear (el delete ya retorna aves + consumo y recalcula el cruce por el trigger).
3. **Solo prevenir a futuro** — no se tocan datos existentes ni el cruce en prod.
4. (Guarda coherente añadida, vetos posibles) **Fecha máxima = encaset + 7 (edad 7)**: mismo modelo de edad; evita el bug simétrico (edad > 7 tampoco cruza). Marcado en tracker por si se quiere quitar.

## Cambios

### Frontend
- **F1 — Fix modal detalle** `pages/modal-detalle-seguimiento-reproductora/modal-detalle-seguimiento-reproductora.component.ts`
  - `formatDate()` deja de usar `new Date(d).toLocaleDateString()`; usa `ymdSinTz(d)` y formatea `dd/MM/yyyy` desde el `YYYY-MM-DD` literal. Import `ymdSinTz` de `shared/utils/format`.
  - Resultado: modal == tabla == fecha ingresada.
- **F2 — Validación de fecha (crear/editar)** `pages/modal-seguimiento-reproductora/modal-seguimiento-reproductora.component.ts` + `.html`
  - Nuevo `@Input() fechaEncasetamiento: string | Date | null`.
  - Getters `minFechaYmd` (encaset+1) / `maxFechaYmd` (encaset+7) usando `ymdSinTz` + suma de días UTC.
  - Validator de rango en el control `fecha` (comparación de strings `YYYY-MM-DD`): errores `fechaMin` / `fechaMax`.
  - `ngOnChanges`: al abrir o cambiar el encaset, `fecha.updateValueAndValidity()`.
  - HTML: `[min]`/`[max]` en el input date, hint con la fecha mínima y mensajes de error.
- **F3 — Split de campos en edición** `modal-seguimiento-reproductora.component.ts` + `.html`
  - `get isEdit()`.
  - `populateForm()` (edición): `disable({emitEvent:false})` de los controles **duros**: `alimentoId, consumoHembrasQty, unidadHembras, consumoMachosQty, unidadMachos, mortalidadH, mortalidadM, selH, selM, errorH, errorM`.
  - `resetForm()` (creación): `enable()` de esos controles (el componente es singleton → limpiar estado de una edición previa).
  - `onSave()`: usar `this.form.getRawValue()` (incluye deshabilitados) para **preservar** consumo/mortalidad/metadata sin cambios en el update (diff de inventario = 0, saldo sin cambio).
  - HTML: banner informativo en edición + candado visual en las secciones bloqueadas, apuntando a "eliminar y recrear".
- **F4 — Wire del Input** `pages/seguimiento-diario-lote-reproductora-list/...-list.component.html`
  - `[fechaEncasetamiento]="selectedReproductoraDetail?.fechaEncasetamiento ?? null"` en `<app-modal-seguimiento-reproductora>`.

### Backend
- **B1 — Lógica pura** `Application/Calculos/ReproductoraEngordeCalculos.cs`
  - `EdadSeguimientoDias(DateTime encaset, DateTime fecha)` = `(fecha.Date − encaset.Date).Days`.
  - `EsEdadSeguimientoValida(int edad, int dias = 7)` = `edad ∈ [1, dias]`.
- **B2 — Validación en servicio** `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs`
  - `CreateAsync`: tras cargar `loteRep`, si hay `FechaEncasetamiento`, calcular edad de la fecha anclada y lanzar `InvalidOperationException` si `edad < 1` (anterior al día siguiente del encaset) o `edad > 7`.
  - `UpdateAsync`: obtener `FechaEncasetamiento` del lote reproductora del registro y aplicar la misma validación (defensa en profundidad; el registro confirmado ya está bloqueado).
- **B3 — Tests** `tests/ZooSanMarino.Application.Tests/ReproductoraEngordeCalculosTests.cs`
  - `EdadSeguimientoDias` y `EsEdadSeguimientoValida`: encaset 13/07 → 13/07 edad 0 inválida · 14/07 edad 1 válida · 20/07 edad 7 válida · 21/07 edad 8 inválida.

## Validación
- Front: `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget).
- Back: `cd backend && dotnet build` (0 errores/sin nuevas advertencias) + `dotnet test`.
- Verificación funcional en preview: crear/editar seguimiento, ver que tabla y modal coinciden, que no deja fecha < encaset+1, y que en edición los campos duros están bloqueados.

## Fuera de alcance / notas
- No se modifica `fn_cruce_reproductora_a_engorde` ni datos de prod (decisión "solo prevenir a futuro").
- El modal gemelo de pollo engorde (`aves-engorde`) no tiene el bug de `toLocaleDateString` (verificado) → no se toca.
- No se agrega prevención de edades duplicadas al mover la fecha en edición (fuera de lo pedido); la validación de rango [1,7] sí aplica.
