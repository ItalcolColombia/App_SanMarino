# Seguimiento diario de Producción: «Total del día» y resumen de lo guardado (huevos repetidos 2-3 veces)

Fecha: 18-sep-2026 · Solo frontend · Sin migración, sin flag, sin backend.
Continúa `seguimiento_huevos_primer_guardado_plan.md` (commit `4f71cbe`), a partir de la BD real de producción.

## Qué mostró la copia de producción (cargada por el usuario el 18-sep, ~12:20)

Registros de Santa Reyes del 14/09 cargados el 18-sep entre 11:48 y 12:20 por un mismo operario (Auxiliar de operación,
Chrome en Windows, sesión activa sin cortes: `sesiones_activas` 11:45-12:17), poniendo al día los 6 galpones de La
Esperanza (LPP 23-28, P-LOTE 217A). El de la captura del usuario es el **Galpón 4 = LPP 26**.

| Galpón | Registros del 14/09 (hora de creación) |
|---|---|
| 1 (LPP 23) | #685 mortalidad 13, sin huevos (11:48) · #686 huevos 8.340 **con fecha 18/09** (11:51) |
| 2 (LPP 24) | #687 mortalidad 15 + consumo 1.232 + huevos 7.606 (11:52, guardó todo junto) · #688 vacío (11:54) |
| 3 (LPP 25) | #690 huevos 7.530 (12:02) · #691 **idéntico** (12:04) — #689, con 1.138 kg de consumo, se borró a las 12:02 |
| 4 (LPP 26) | #692 mortalidad 15 + consumo 1.215, **huevos 0** (12:05) · #693 huevos 7.010 (12:06) · #694 **idéntico** (12:07) · #695 **idéntico** (12:09) |
| 5 (LPP 27) | #696 consumo 1.180, huevos 0 (12:18) |
| 6 (LPP 28) | #697 mortalidad 5 + consumo 966, huevos 0 (12:20) — #682 y #683 se borraron a las 12:19 |

Descartado con datos: cola sin red (`sync_operaciones` vacía), sesión vencida, doble clic (entre un registro y el
repetido pasan 61-117 s), silos y tipos de huevo (los 6 lotes declaran los mismos 4 tipos —Primera 2520, Pnc
2521/2522/2523, ninguno de «primera postura»— y tienen 1 silo asignado).

Daño medido en la fn canónica y en el espejo de huevos:

| Galpón | Huevos del 14/09 que cuenta el sistema | % postura | Real aproximado |
|---|---|---|---|
| 4 | 21.030 | **183 %** | 7.010 (61 %) |
| 3 | 15.060 | **135 %** | 7.530 (67 %) |
| 1 | 0 (los 8.340 están en el 18/09) | 0 % | 8.340 |
| 5 y 6 | 0 | 0 % | sin cargar |

«Traslado de huevos» ve disponibles 21.030 (G4) y 15.060 (G3); lo real es ~7.010 y ~7.530.

## Lectura

1. **El vaciado del formulario ocurrió en producción.** #686 quedó con la fecha de hoy cuando todos los demás van con
   14/09: al reiniciarse el modal (`resetForm`) la fecha vuelve a «hoy» y el operario siguió tecleando sin notarlo. En 5 de
   6 galpones el primer registro tras elegir el lote salió sin huevos. Eso ya está corregido en `4f71cbe` (sin desplegar).
2. **Los duplicados no son doble clic ni fallas de red: el operario repitió la carga porque la pantalla no le confirmaba
   que los huevos estaban.** En la grilla, el primer renglón de un día con varios registros muestra SOLO su registro:
   «14/09/2026 · 4 registros … huevos **0**», y los 7.010 quedan tres renglones más abajo. Medido en la copia real con el
   front ya arreglado: la primera línea del día sigue diciendo 0. Ese es el «queda en 0» de la queja. Tampoco había
   confirmación visible del guardado (solo el diálogo viejo que ya se retiró).

## Cambio

1. **«Total del día»** en la primera línea de un día con 2+ registros: `filasGrillaProduccion` le cuelga a esa fila el
   renglón agrupado del día (`totalDia`, la misma fn canónica que ya calcula el total), y la celda de la fecha agrega
   «Total del día: N huevos» (mortalidad, selección y consumo del día en el tooltip). Las cifras y los botones de cada renglón
   quedan intactos (cada registro sigue siendo editable/borrable por su fila).
2. **El aviso de éxito dice qué se guardó:** `resumirGuardadoSeguimiento(request)` (pura) → «Seguimiento creado: 7.010
   huevos · mortalidad 15 · consumo 1.215 kg» (solo lo que traiga valores). Así el operario ve en el momento que los
   huevos SÍ entraron.

Archivos: `lote-produccion/funciones/filas-grilla-produccion.funcion.ts` (+ spec),
`lote-produccion/funciones/resumen-guardado-seguimiento.funcion.ts` (+ spec, nuevo),
`tabs-principal.component.html/.scss`, `lote-produccion-list.component.ts`.

## Casos de prueba

- Un día con 1 registro: sin `totalDia` (idéntico a hoy). Con 2+: solo la primera fila lo lleva.
- Resumen del guardado: solo huevos; huevos por ítems (suma) vs 11 categorías (`huevosTotales`); consumo por ítems (kg)
  vs escalar (kg/g); todo en cero → texto sin cifras; miles con punto (es-CO).
- Navegador (copia real, LPP 26): la primera línea del 14/09 muestra el total del día; un guardado con huevos muestra el
  resumen; un día de un solo registro no cambia; Sanmarino (flag apagado) sin cambios.

## Decisiones que NO tomé (son del usuario)

- **Aviso o confirmación cuando el día ya tiene huevos** (evitaría 691, 694 y 695 aunque la grilla no se lea bien):
  Santa Reyes decidió que los registros del día SUMAN, así que una confirmación estorbaría en los días legítimos con dos
  recogidas; un aviso informativo dentro del modal no. Falta que el usuario elija.
- **Limpieza de los datos ya cargados** (694, 695, 691 repetidos; 686 con fecha equivocada; 688 vacío; huevos de los
  Galpones 5 y 6): es corrección en producción, la hace un usuario autorizado desde la app (eliminar devuelve el
  inventario); no se toca la BD.
- **Levante** tiene la misma grilla (`registros-por-dia`); aquí solo Producción, donde está la evidencia.
