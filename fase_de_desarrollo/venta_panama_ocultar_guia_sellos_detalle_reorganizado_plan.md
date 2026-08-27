# Plan: Ocultar Guía/Sellos en venta Panamá + reorganizar modal de detalle

## Contexto

En "Datos del despacho" del modal de venta Panamá (pollo engorde) aparecen los campos "Guía"
y "Sellos" (`formControlName="guiaAgrocalidad"` / `"sellos"`). Ese campo nació para la guía de
**Agrocalidad**, la agencia fito-zoosanitaria de **Ecuador** — Panamá no la usa. Se pide
ocultarlos, confirmado con el usuario en el chat: **solo para Panamá, Ecuador no se toca**
(sigue mostrando Guía Agrocalidad + Sellos donde ya los muestra hoy). Se pide además
reorganizar visualmente el modal de detalle compartido (Ecuador + Panamá), hoy una lista plana
de pares clave/valor a lo largo de toda la pantalla ("muy plano a lo largo").

## Alcance confirmado con el usuario

- Los campos a ocultar son SOLO de Panamá. Ecuador **no se toca**.
- La ocultación usa `CountryFilterService.isPanama()` (mismo servicio que ya decide en
  `movimientos-pollo-engorde-list.component.ts` si se abre el modal dedicado de Panamá) — no se
  introduce un flag de empresa nuevo en BD, sigue el patrón ya establecido en este módulo.
- La reorganización visual del detalle es transversal (Ecuador + Panamá), es solo estructura/CSS.

## Archivos a modificar

1. `frontend/src/app/features/movimientos-pollo-engorde/components/modal-venta-panama/modal-venta-panama.component.html`
   - Quitar los dos `.vp-field` de Guía y Sellos (líneas 108-115). Sin cambios en el `.ts`: los
     FormControls quedan definidos pero sin input que los llene → siempre `null`, valor ya
     válido hoy (ninguno de los dos tiene `Validators.required`). Sin cambio de DTO/backend.

2. `frontend/src/app/features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component.ts`
   - Inyectar `CountryFilterService` (ya existe, ya usado en el componente lista hermano).
   - Getter `ocultarGuiaYSellos(): boolean` → `this.countryFilter.isPanama()`.

3. `frontend/src/app/features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component.html`
   - Vista de detalle (solo lectura): envolver las filas "Guía Agrocalidad" y "Sellos" con
     `@if (!ocultarGuiaYSellos)`.
   - Formulario crear/editar, sección "Datos de despacho (salida/venta)": envolver los mismos
     dos `form-group` con `@if (!ocultarGuiaYSellos)`. Cubre el caso de un usuario Panamá que
     entra al flujo estándar (`hasLoteSelected`), no solo el modal dedicado.
   - Reorganización visual de la vista de detalle (solo estructura/CSS, mismos datos):
     - Envolver las 3 secciones cortas (Datos generales / Origen y destino / Cantidades) en un
       contenedor `.detail-columns` (grid responsive: lado a lado en desktop, apiladas en mobile).
     - Cambiar `.detail-grid` de "lista de 2 columnas fija" a grilla de tarjetas de campo
       (`<div class="detail-item">` envolviendo cada `dt`/`dd` — válido en HTML5 dentro de
       `<dl>`), mismo patrón visual que `.vp-grid`/`.vp-field` del modal Panamá y `.form-grid`
       del propio formulario de este componente — no se inventa un patrón nuevo.
     - Las secciones pasan de "separador de línea" a tarjeta (fondo, borde, radio) para cortar
       la sensación de lista plana.

4. `frontend/src/app/features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component.scss`
   - Estilos nuevos: `.detail-columns`, `.detail-item`, tarjeta en `.detail-section`. Reusa
     variables `--ital-*` ya centralizadas (sin colores nuevos hardcodeados). Ajustar el media
     query de 768px existente (hoy fuerza `.detail-grid` a 1 columna) al nuevo markup.

## Reglas de negocio

- Ningún cambio de datos, validación ni payload. Es ocultar inputs/filas de UI y reorganizar
  layout. El backend sigue recibiendo/aceptando `guiaAgrocalidad`/`sellos` igual que hoy
  (nullable en `VentaPanamaDespachoDto`/`MovimientoPolloEngordeDto`); para Panamá simplemente
  nunca se completan desde la UI (ya eran opcionales, así que un `null` no es un caso nuevo).
- `CountryFilterService.isPanama()` lee el país activo de la sesión — mismo criterio que ya
  decide qué modal de creación abrir en este módulo, no un check nuevo de `if (pais==X)`
  disperso.

## Casos de prueba (smoke manual, dev server)

1. Usuario Ecuador: crear/editar una venta con el modal estándar → Guía Agrocalidad y Sellos
   siguen visibles y funcionando igual que hoy. Ver detalle de una venta Ecuador → igual.
2. Usuario Panamá: abrir "Nueva venta Panamá" → Guía y Sellos NO aparecen en "Datos del despacho".
   Guardar una venta sin esos campos → éxito (ya eran opcionales).
3. Usuario Panamá con `hasLoteSelected` (flujo estándar) → sección "Datos de despacho" tampoco
   muestra Guía Agrocalidad/Sellos.
4. Ver detalle de una venta creada por Panamá → sin filas de Guía Agrocalidad/Sellos; resto de
   "Datos de despacho" intacto.
5. Detalle (Ecuador y Panamá): layout en tarjetas/columnas, responsive en mobile (<768px) sigue
   legible (columnas colapsan a 1).
