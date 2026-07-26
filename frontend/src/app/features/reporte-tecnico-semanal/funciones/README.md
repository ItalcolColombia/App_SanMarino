# funciones/ — Reporte Técnico Semanal (Sanmarino postura)

Convención del repo (referencia canónica: `movimientos-pollo-engorde`): un archivo
por concern, funciones **PURAS** (sin `this`, sin DI, sin HTTP/toast/estado).
El componente orquesta (HTTP, estado, UI) y delega aquí la lógica grande.

| Archivo | Concern |
|---|---|
| `columnas-reporte-semanal.funcion.ts` | Especificación única de columnas (grupo, título, decimales, extractor) de los dos reportes (Levante / Producción). La consumen la tabla en pantalla y el Excel — una sola fuente de verdad. |
| `construir-aoa-reporte-semanal.funcion.ts` | Arma las hojas AOA del export `.xlsx` (una hoja por galpón + "Gral" consolidado) para `exportarAoaMultiHojaExcel`. |
| `construir-graficas-reporte-semanal.funcion.ts` | Arma las definiciones Chart.js de la vista Gráficas (8 de levante / 6 de producción por tab), réplica de las gráficas embebidas de los Excel oficiales. Real sólido / Guía punteada. |

Nota multi-empresa: el módulo está pensado para Sanmarino (postura Colombia) y
se habilita por menú (`company_menus`/`role_menus`), no por flag; las funciones
son igual de válidas para cualquier empresa con guía genética postura cargada.
