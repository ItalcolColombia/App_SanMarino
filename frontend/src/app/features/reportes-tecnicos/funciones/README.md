# `funciones/` — Reporte Técnico

Convención del repo (ver `CLAUDE.md` §🧩): **una función grande / de botón por archivo**, y cada
una es **PURA** — sin `this`, sin DI, sin service, sin toast, sin estado. Recibe parámetros y
devuelve un resultado. Los componentes y páginas quedan como orquestadores delgados que juntan
estado/inputs, llaman la función y manejan HTTP/UI.

Los tipos que estas funciones necesitan viven en `../models/`, nunca inline en un componente (evita
el import circular componente ↔ función).

## Reutilización — esto NO es «el reporte de una empresa»

Las dos funciones de acá deciden **qué columnas tienen algo real que mostrar**, en función de cómo
está configurada la empresa del reporte. Ninguna nombra un tenant ni un país, y ninguna debe
hacerlo (`CLAUDE.md` §🏢: la señal vive en `companies` como columna tipada nombrada por el
comportamiento, jamás `if (empresa == 'X')`).

| Archivo | Qué decide | Señal que la gobierna |
|---|---|---|
| `normalizar-guia-disponibilidad.funcion.ts` | Qué columnas GUÍA se pintan | `guiaMetricasDisponibles` del DTO (backend: `GuiaMetricasDisponiblesCalculos`) |
| `columnas-huevo-reporte.funcion.ts` | Qué columnas de huevo se pintan | `clasificacionHuevoPorItems` del DTO (`companies.clasificacion_huevo_por_items`) |

**Las dos son fail-open hacia el comportamiento histórico**: si el dato que las gobierna no llega,
se pinta todo, que es lo que se pintaba antes. Ocultar de más por un campo ausente sería peor que
mostrar una celda vacía.

## Espejos que hay que mantener a la par

- `columnas-huevo-reporte.funcion.ts` clasifica en Primera / Pnc / Otros con la misma regla que
  `resumir-huevo-items-por-tipo.funcion.ts` (`features/lote-produccion/funciones/`) y que
  `HuevoItemsResumenCalculos` en el backend. **Si cambia la regla, cambian los tres.**
