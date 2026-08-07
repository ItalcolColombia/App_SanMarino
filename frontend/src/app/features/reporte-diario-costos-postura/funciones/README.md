# `funciones/` — Reporte Diario Área de Costos (POSTURA)

Convención del repo: **una función pura por archivo**, sin `this`, sin DI, sin servicios, sin toasts y
sin estado. La página (`pages/reporte-diario-costos-postura-main/`) es un orquestador delgado: junta
filtros, hace el HTTP y **delega** acá toda transformación de datos.

| Archivo | Qué hace |
|---|---|
| `expandir-filas-alimento.funcion.ts` | Expande cada día a **una fila por ítem** de alimento (decisión D4). Aparea hembras/machos por posición. |
| `construir-aoa-costos-postura.funcion.ts` | Arma las **3 hojas** del Excel (Aves · Alimento · Huevos) sobre el helper compartido `shared/utils/excel/`. |

## Lo que este reporte NO puede romper

1. **Es POSTURA, no engorde.** Existe un `reporte-diario-costos-engorde` con nombre casi idéntico y
   otras reglas. No se comparte código ni se copian sus fórmulas.

2. **La clasificación de huevo la hace el BACKEND, no el front.** `fértil / comercial / inservible`
   tiene un único dueño: `ReporteDiarioCostosPosturaCalculos.ClasificarHuevo` (C#, con tests). El front
   solo pinta `fila.huevo.*`. Si hiciera falta cambiar el criterio, se cambia allá — recalcularlo acá
   crearía la segunda implementación del mismo número.

3. **La hoja/pestaña Huevos solo aplica a producción.** En levante no hay postura que reportar; las
   filas de levante llegan con los huevos en 0 y la pestaña se oculta.

4. **Un día con varios alimentos del mismo sexo no se concatena.** El desglose por ítem es lo que
   permite costear por referencia; fusionarlo devolvería el reporte al dato que ya trae la columna
   `tipo_alimento` (que es exactamente lo que se quiso evitar).

## Reutilización

Las dos funciones son agnósticas de empresa y país: reciben el DTO del reporte y devuelven datos.
Si otra empresa (Ecuador, Panamá, Santa Reyes) habilita el módulo, no hay que tocarlas — el alcance
lo resuelve el backend por `company_menus` y por las granjas asignadas al usuario.
