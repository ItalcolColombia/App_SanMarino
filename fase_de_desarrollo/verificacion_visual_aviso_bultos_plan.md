# Verificación visual del aviso de alcance del kardex de bultos (V19.3.4)

**Pendiente que cierra:** `tracker_estado.md` → bloque **V19**, checkbox **V19.3.4**:
> ⚠️ Lo que NO pude smokear: el aviso PINTADO en pantalla. El panel de bultos vive dentro de la
> cascada de filtros del reporte (granja → lote → sublote → semana) y no logré conducirla desde el
> harness; el DTO sí llega con el campo al componente. Queda como verificación visual pendiente de
> la próxima sesión que abra esa pantalla.

**Fecha:** 2026-08-17 · Bloque propio — no tocar desde otras sesiones.

---

## 1. Qué se verifica (y qué NO)

V19 entregó el aviso de §2.4: los movimientos de alimento son de la **granja**, así que cuando la
granja tiene varios lotes padres los reportes de todos muestran los mismos kilos. La cadena ya está
verificada **hasta el DTO** (smoke de API en V19.3.3: lote 114 ⇒ `lotesPadreEnGranja: 4` + aviso;
lote 13 ⇒ `null`). Lo único sin verificar es el **último tramo**: que el texto se pinte, legible,
dentro del panel BULTO.

- ✅ **En alcance:** abrir la pantalla real, conducir la cascada completa y ver el aviso renderizado
  en el caso positivo y **ausente** en el de control.
- ⛔ **Fuera de alcance:** no se cambia ningún número del reporte, ni la Fase 2 de V19 (V19.2.1, el
  saldo coherente, que es decisión de producto). Si el aviso pinta bien, esta sesión **no toca
  código de producción**; si no pinta, el arreglo se limita al render.

## 2. Enfoque arquitectónico

Nada nuevo: la cadena ya existe y es de una sola dirección.

| Capa | Pieza | Estado |
|---|---|---|
| Application | `ReporteContableBultosCalculos.AdvertenciaAlcance(lotesPadreEnGranja, granjaNombre)` | entregado V19.1.1 + tests T1-T5 |
| Application | `ReporteContableCompletoDto.LotesPadreEnGranja` / `.AdvertenciaBultos` | entregado V19.1.2 |
| Infrastructure | `ReporteContableService` cuenta padres vivos de la granja y llama al cálculo | entregado V19.1.2 |
| Front (service) | `ReporteContableCompletoDto.advertenciaBultos?: string \| null` | entregado V19.1.3 |
| Front (página) | `[advertenciaBultos]="reporte()?.advertenciaBultos ?? null"` sobre `<app-tabla-bultos-contable>` | entregado V19.1.3 |
| Front (componente) | `@Input() advertenciaBultos` + `@if (advertenciaBultos) { <p class="alcance-aviso" role="note"> }` · `.alcance-aviso` en el `.scss` · `changeDetection: Eager` | entregado V19.1.3 |

## 3. Casos de prueba (medidos hoy contra `sanmarinoapplocal`)

Padres vivos por granja (`lotes` con `lote_padre_id IS NULL`, `deleted_at IS NULL`):

| Granja | Padres | Lotes |
|---|---|---|
| 12 MANGOS | 4 | 142 S369A · 143 S369B · 144 S369A · 145 S369B |
| **20 LA ESMERALDA** | **4** | **114 A374A** · 115 A374B · 116 A374A · 117 A374B |
| 23 MIRALINDO | 2 | 146 A402A · 147 A402B |
| **5 NIZA III** | **1** | **13 K345A** |
| 5 granjas de Demo | 1 c/u | — |

- **T-VIS-1 (positivo).** Granja **LA ESMERALDA** → lote base **A374A (114)** → fase **Levante**
  (tiene 45 filas en `seguimiento_diario_levante`) → *Generar Reporte* → en el panel **BULTO** debe
  verse el aviso: *«Estos movimientos de alimento son de la GRANJA «LA ESMERALDA», que hoy tiene 4
  lotes padres: el reporte de los otros 3 muestra los mismos kilos. NO sumar los reportes entre sí.»*
- **T-VIS-2 (control negativo).** Granja **NIZA III** → lote base **K345A (13)** → fase **Levante**
  → *Generar Reporte* → el panel BULTO **no** muestra ningún aviso (`AdvertenciaAlcance` devuelve
  `null` con 1 padre; el `@if` no pinta nada).
- **T-VIS-3 (legibilidad).** El aviso queda **dentro** del panel, bajo el título `BULTO` y **encima**
  de la tabla, sin tapar ni desplazar columnas.

## 4. Cambios de BD / SQL

**Ninguno.** Es verificación de solo lectura sobre la BD compartida. Cero escrituras: el reporte es
un `GET`. Se comprueba con conteos antes/después.

## 5. Reglas de negocio en juego

1. El aviso es **informativo**: no cambia ni un número del reporte (V19.1.5 lo probó por el diff).
2. `null` cuando el padre es el único de su granja — el kardex sí es suyo y un aviso sería ruido.
3. El conteo de padres respeta empresa (`CompanyId`) y `deleted_at IS NULL`.

## 6. Procedimiento

1. Puertos limpios antes de arrancar (regla dura de CLAUDE.md: ningún backend viejo vivo).
2. `dotnet build` + `yarn build` para confirmar que el HEAD compila.
3. Backend `:5002` (Development ⇒ `sanmarinoapplocal:5433`) + front `:4200` con el toolchain portable.
4. Login con un usuario de **Agroavicola Sanmarino**, navegar a Reporte Contable y correr T-VIS-1,
   T-VIS-2 y T-VIS-3, con captura de pantalla de cada uno.
5. Apagar back y front; confirmar `:5002` y `:4200` libres.
6. Actualizar el tracker (marcar V19.3.4) y commitear.

---

## 7. Resultado (17-ago-2026)

**El aviso estaba bien; la pantalla que lo contiene, no.**

- **T-VIS-1 ✔** LA ESMERALDA / A374A (114) / Levante, semana 44: el aviso se pinta bajo el título
  **BULTO** y encima de la tabla (1116×32 px, borde ámbar, `role="note"`), con el texto de V19.
- **T-VIS-2 ✔** NIZA III / K345A (13) / Levante, semana 81: el panel BULTO se pinta y no hay ningún
  `.alcance-aviso` en la página (`lotesPadreEnGranja: 1`).
- **T-VIS-3 ✔** Verificado por geometría: el aviso queda entre el título y la tabla.

### Lo que apareció en el camino (y se arregló)

V19.3.4 decía «no logré conducirla desde el harness». **No era el harness.** El tab de la semana se
destruía y se volvía a crear en **cada ciclo de change detection**, así que:

- no se podía hacer clic (el nodo moría entre leerlo y clickearlo), y
- salía **sin rótulo**: 40 px en blanco donde debía decir *«Sem 44 (13/8-19/8)»*.

Angular lo gritaba en consola: `NG0956` («track by identity caused re-creation of the entire
collection») y `NG0100`. Causa: `get semanasParaSubloteActual()` proyectaba las semanas en cada
lectura ⇒ array nuevo de objetos nuevos por ciclo, recorrido con `track` por identidad.

**Arreglo (solo render, cero cambios de cálculo):**

1. El cuerpo del getter pasó **tal cual** a `calcularSemanasParaSublote(r, sublote)` y el getter
   memoriza el resultado contra sus dos únicas entradas (`reporte()` y `selectedSublote`).
2. `track reporteSemanal` → `track reporteSemanal.semanaContable` en los tres `@for`.

**Validación:** `yarn build` 0 errores · `yarn test` **325 SUCCESS** (6 casos nuevos en
`frontend/src/tests/reporte-contable-semanas-memo.spec.ts`) · recorrido completo con **0 mensajes
`NG0xxx`** · BD compartida sin una sola escritura · puertos 5002/4200/9333 libres.
