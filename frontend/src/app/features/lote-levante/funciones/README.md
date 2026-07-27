# `funciones/` — Seguimiento Diario Levante

Convención del repo (ver `CLAUDE.md` § CLEAN CODE): una **función grande / de botón por archivo**,
`<accion>.funcion.ts`, **PURA** — sin `this`, sin DI, sin service/toast/estado. Los componentes y
páginas quedan como orquestadores delgados que juntan estado, llaman la función y manejan HTTP/UI.

Los tipos que estas funciones necesitan viven en `../models/` (nunca dentro de un componente) para
evitar imports circulares.

## Archivos

| Archivo | Qué hace |
|---|---|
| `totales-huevos-levante.funcion.ts` | Totales de la clasificadora fija de huevos (`incubables = limpio + tratado`; `totales = incubables + las 9 no incubables`) y la eficiencia de producción en %. |
| `semana-vida-levante.funcion.ts` | Semana de vida del lote (`floor((fecha − encaset)/7) + 1`, el día del encaset es la semana 1) y el gate `permiteHuevosEnLevante` (semana ≥ 14, fail-closed). |

## Nota de reutilización

La aritmética de `totales-huevos-levante` es **la misma** que usa el modal de producción y que el
cálculo puro del backend (`Application/Calculos/HuevosLevanteCalculos.cs`, con tests xUnit). Si hay
que cambiarla, hay que cambiarla en los tres lados a la vez — de lo contrario el total que ve el
usuario deja de coincidir con el que se persiste y con el que se arrastra a producción al liquidar.

`semana-vida-levante` replica la fórmula canónica de semana del backend. El **backend es el
autoritativo**: acá el cálculo sólo decide si se muestra el tab «Huevos». No copiar las variantes
`Math.floor(dias/7)` ni `Math.ceil(diff/7)` que existen en otros módulos del repo — dan una semana
de diferencia.
