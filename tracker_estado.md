# Tracker — Unidad `qq` (quintal) en alimento del seguimiento pollo engorde

Plan: [fase_de_desarrollo/qq_a_kg_alimento_seguimiento_engorde_plan.md](fase_de_desarrollo/qq_a_kg_alimento_seguimiento_engorde_plan.md)

## Frontend (solo front — sin BD ni backend)
- [x] `inventario-calculos.funcion.ts`: `KG_POR_QUINTAL = 45.36` + `toKg` entiende `qq`
- [x] `mapear-seguimiento-dto.funcion.ts`: `construirItemsSeguimiento` normaliza `qq`→kg antes de enviar
- [x] Componente: helper `unidadAlimentoPorDefecto()` (qq en Panamá) + preview `consumoKgDeFila()`
- [x] Componente: usar el default en `agregarItemHembras`/`agregarItemMachos`
- [x] HTML: opción `qq (quintal)` (solo Panamá) + hint "Se guardará en consumo: X kg"
- [x] `README.md` de `funciones/` actualizado
- [x] Spec pura `inventario-calculos.funcion.spec.ts`

## Validación
- [x] `yarn build`: los archivos tocados por esta tarea compilan **sin errores**
      (verificado: ningún error del build referencia mis archivos).
- [ ] `yarn build` **global** falla por trabajo en curso **ajeno a esta tarea**
      (`lote-engorde-list.component.ts/.html` — lote_base_engorde, cambios sin commitear).
      No lo toqué; se resuelve al terminar ese módulo.
- [ ] `yarn test` (Karma) — bloqueado por lo mismo (compila todo el proyecto).

## Nota
El tracker previo (lote_base_engorde) se reinició por el workflow de CLAUDE.md; es recuperable por git
y su plan sigue en `fase_de_desarrollo/lote_base_engorde_por_granja_plan.md`.
