# Plan — Decimales en campos de peso/uniformidad del modal «Nuevo Lote de Engorde»

## Problema
En el modal **Nuevo/Editar Lote de Engorde** (`lote-engorde`), los campos numéricos no
aceptan decimales: al escribir `42,5` se guarda `43`. La causa es la directiva
`ThousandSeparatorDirective`, que en cada `input` hace `Math.round(...)` y formatea con
`maximumFractionDigits: 0`, descartando la parte decimal.

## Enfoque arquitectónico — EL CÓDIGO MANDA (contrato backend)
La verdad es el tipo de cada campo en el backend (entidad `LoteAveEngorde` + `CreateLoteAveEngordeDto`):

| Campo (form)            | Backend            | ¿Decimales? |
|-------------------------|--------------------|-------------|
| `pesoInicialH` (Peso llegada H g) | `double?` | ✅ sí |
| `pesoInicialM` (Peso llegada M g) | `double?` | ✅ sí |
| `pesoMixto` (Peso mixto g)        | `double?` | ✅ sí |
| `unifH` (Unif H)        | `double?`          | ✅ sí |
| `unifM` (Unif M)        | `double?`          | ✅ sí |
| `hembrasL`, `machosL`, `mixtas`, `mortCajaH`, `mortCajaM`, `avesEncasetadas` | `int?` | ❌ no (conteo de aves) |

⇒ El backend **ya acepta** decimales en los 5 campos `double`. El frontend es el único que
los trunca. Los conteos de aves siguen siendo enteros (no existe media ave).

## Restricción crítica — no romper el módulo `lote`
La directiva `ThousandSeparatorDirective` está **declarada en `lote/components/lote-list`** y la
**reusa `lote-engorde`**. El módulo `lote` quiere enteros a propósito
(nota en su HTML: «Usa números enteros»). Por lo tanto el soporte de decimales debe ser
**opt-in**, dejando el comportamiento por defecto (entero) **idéntico**.

## Archivos a modificar
1. `frontend/src/app/features/lote/components/lote-list/lote-list.component.ts`
   — Añadir a `ThousandSeparatorDirective` un `@Input() decimals = 0` (retrocompatible):
   - `decimals = 0` (default): comportamiento histórico EXACTO (`Math.round`, `maximumFractionDigits: 0`,
     `'.'`=miles / `','`=decimal descartado).
   - `decimals > 0`: redondeo a N decimales (no a entero), formateo con `maximumFractionDigits: N`,
     y `unformat` que toma el ÚLTIMO separador (`.` o `,`) como decimal y el resto como miles.
   - Importar `Input` de `@angular/core`.
2. `frontend/src/app/features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.html`
   — Añadir `[decimals]="2"` a los 5 inputs `double`: `pesoMixto`, `pesoInicialH`, `pesoInicialM`,
     `unifH`, `unifM`. Los demás campos quedan sin tocar (enteros).

## Reglas de negocio / aritmética
- Refactor ≠ cambio de comportamiento: con `decimals=0` la directiva produce **exactamente** lo
  mismo que hoy (verificado rama por rama: `unformat`, `Math.round`, formatter).
- Precisión elegida: **2 decimales** (suficiente para gramos y % de uniformidad). Trivial de ajustar.
- No se toca `save()`/`toNum()` (ya hace `Number(v)`, que preserva decimales) ni el DTO ni el backend.
- `actualizarEncasetadas()` suma `hembrasL + machosL` (enteros) → `avesEncasetadas` sigue entero.

## Casos de prueba
1. `cd frontend && yarn build` sin errores.
2. Peso llegada H = `42,5` → se conserva `42.5` (no `43`); al perder foco muestra `42,5`.
3. Unif H = `85,25` → conserva `85.25`.
4. `# Hembras` = `100` sigue entero; escribir `100,5` se redondea a `101` (sin cambio vs. hoy).
5. Editar un lote existente: los pesos con decimal se muestran y reenvían con su decimal.
6. Módulo `lote` (Nuevo Lote reproductora): sin cambios — sigue forzando enteros.
