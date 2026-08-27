# `funciones/` — lógica pura del módulo guia-genetica-santa-reyes

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de dependencias).
Cada archivo agrupa una "acción grande" del módulo —típicamente la lógica detrás de un botón— para
que sea **fácil de encontrar, testear y reutilizar**.

Sigue la convención canónica del repo:
[`features/movimientos-pollo-engorde/funciones/`](../../../movimientos-pollo-engorde/funciones).

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, ni estado
  del componente.
- La página (`pages/`) queda como **orquestador delgado**: arma los parámetros, llama la función y
  maneja estado / HTTP / UI.
- Los tipos compartidos viven en [`../models/`](../models), no acá (evita imports circulares).

## Índice

| Archivo | Qué hace |
|---|---|
| `construir-filas-tabla.funcion.ts` | `construirFilasTablaGuia`: DTOs → filas del grid ya formateadas. `formatearMetricaGuia` deja en `—` lo que es `null`. |
| `normalizar-filtros.funcion.ts` | `normalizarFiltrosGuia` / `hayFiltrosActivos` / `describirFiltrosGuia`: limpia el filtro y **corrige el rango de semanas invertido**. |
| `exportar-guia-excel.funcion.ts` | `exportarGuiaExcel` / `construirFilasExportGuia`: arma y descarga el `.xlsx`, con los encabezados de la plantilla. |
| `validar-formulario-guia.funcion.ts` | `validarFormularioGuia` + `construirCreateDtoGuia` / `construirUpdateDtoGuia`: valida el alta/edición y arma el DTO. |
| `resumir-import.funcion.ts` | `resumirImportGuia` + `validarArchivoImportGuia`: traduce el resultado del import y chequea el archivo antes de subirlo. |

## Tres decisiones que conviene no deshacer

**1. `null` no es `0`.** En esta guía «sin dato» y «cero» son cosas distintas: la raza Criolla
tiene 40 semanas (101–140) con `prod_porcentaje` legítimamente nulo. Por eso el grid pinta `—`, el
formulario deja el campo **vacío** y el export escribe una celda vacía. Cualquier atajo que
convierta `null` en `0` le está diciendo al usuario que esa semana la línea no produce.

**2. El `.xlsx` exportado es un `.xlsx` importable.** `exportarGuiaExcel` no pone fila de título ni
de filtros: los encabezados quedan en la fila 1, con los mismos nombres que la plantilla del backend
(`raza`, `anio_guia`, `edad`, `prod_porcentaje`, `retiro_ac_h`, `gr_ave_dia_h`). Bajar la guía,
corregirla en Excel y volver a subirla es el camino real de trabajo de este módulo, y el import lee
la fila 1 como encabezados — un título decorativo arriba rompería justamente eso.

**3. La raza es texto libre.** No hay `<select>` de razas ni acá ni en la página. Ése es el
*deadlock de arranque* que hoy vuelve inservible la pantalla de Ecuador: sin guía cargada no hay
raza que elegir, así que no se puede crear la primera línea.

## Nota de reutilización

Esta capa no tiene nada específico de una empresa: el módulo se llama «Santa Reyes» porque hoy es
la única con perfil de guía `reducida`, pero la selección va por la columna tipada
`companies.guia_genetica_perfil`, nunca por el nombre de la empresa. Una segunda empresa con el
mismo modelo de datos reutiliza estas funciones tal cual, sin tocar una línea.
