# Conciliación lote K345 (NIZA III) — Aplicación vs ERP · Análisis del área de desarrollo

**Fecha:** 07-ago-2026
**Origen:** correo de conciliación del área de costos (levante mensual, producción bimestral).
**Fuente de datos:** snapshot de la BD de producción (`sanmarinoapplocal`), tablas
`seguimiento_diario_levante` y `seguimiento_diario_produccion`.

---

## 0. Identificación del lote

«K345» no es un lote: son **dos**, ambos en NIZA III (empresa Agroavícola Sanmarino).

| Lote | `lote_id` | Encasetamiento | Hembras | Machos | Total |
|---|---|---|---|---|---|
| K345A | 13 | 29-ene-2025 | 7.999 | 1.132 | 9.131 |
| K345B | 14 | 01-feb-2025 | 10.991 | 1.596 | 12.587 |
| | | | **18.990** | **2.728** | **21.718** |

Ambos corren en paralelo. Todas las cifras de este documento son **K345A + K345B**, agrupadas por
mes calendario en UTC (que es el corte con el que la aplicación reproduce el correo: enero = 486,00 kg
exacto).

Rango: levante 29-ene-2025 → 25-jul-2025 (semanas 1-25) · producción 16-jul-2025 → 15-may-2026.

---

## 1. ¿Las cifras del correo salen de la plataforma? — Sí, 19 de 26 celdas reproducen exacto

Antes de explicar diferencias hay que verificar que la columna **APLICACIÓN** del correo es realmente
lo que tiene la plataforma. Se reconstruyó día a día desde la BD:

### 1.1 Levante — alimento (kg)

| Mes | APLICACIÓN (correo) | BD plataforma | ¿Reproduce? |
|---|---:|---:|---|
| Enero | 486,00 | **486,00** | ✅ exacto |
| Febrero | 18.967,90 | **18.967,90** | ✅ exacto |
| Marzo | 27.720,30 | 28.573,60 | ❌ −853,30 |
| Abril | 33.300,90 | **33.300,90** | ✅ exacto |
| Mayo | 44.962,60 | 44.692,60 | ❌ +270,00 |
| Junio | 59.265,20 | **59.265,20** | ✅ exacto |
| Julio | 39.008,02 | 56.133,30 (39.181,30 sin traslape) | ❌ ver §3 |

### 1.2 Producción — alimento (kg)

| Bimestre | APLICACIÓN (correo) | BD plataforma | Δ |
|---|---:|---:|---:|
| jul-ago | 133.556,40 | **133.556,40** | ✅ 0,00 |
| sep-oct | 199.756,20 | **199.756,20** | ✅ 0,00 |
| nov-dic | 183.138,00 | 183.138,50 | ✅ 0,50 |
| ene-feb | 172.169,00 | 172.169,50 | ✅ 0,50 |
| mar-abr | 171.185,00 | 171.185,60 | ✅ 0,60 |
| may-jun | 31.809,00 | 31.809,90 | ✅ 0,90 |

Los 6 bimestres reproducen (los ≤0,90 kg son redondeo de presentación).

### 1.3 Producción — mortalidad

| Bimestre | APLICACIÓN | BD | Δ |
|---|---:|---:|---:|
| jul-ago | 328 | 346 | ❌ −18 |
| sep-oct | 320 | **320** | ✅ |
| nov-dic | 218 | **218** | ✅ |
| ene-feb | 208 | **208** | ✅ |
| mar-abr | 314 | **314** | ✅ |
| may-jun | 625 | **625** | ✅ |

**Conclusión:** la columna APLICACIÓN **sí** es la plataforma. El reporte no pierde ni inventa datos:
devuelve exactamente lo que se registró. Las diferencias con el ERP hay que buscarlas en el **dato
registrado** y en el **criterio de comparación**, no en el cálculo.

---

## 2. HALLAZGO PRINCIPAL — la columna de mortalidad del correo cambia de criterio a mitad de tabla

La aplicación guarda **mortalidad** y **selección** en campos separados. La columna «mortalidad» del
correo mezcla los dos criterios:

| Mes | APLICACIÓN (correo) | Mortalidad BD | Selección BD | Mort.+Sel. | Criterio usado | ERP |
|---|---:|---:|---:|---:|---|---:|
| Enero | 37 | **37** | 4 | 41 | mortalidad sola | 41 |
| Febrero | 285 | **285** | 38 | 323 | mortalidad sola | 324 |
| Marzo | 307 | 123 | 204 | 327 | *ninguno* | 382 |
| Abril | 326 | 71 | 255 | **326** | mort. + selección | 271 |
| Mayo | 359 | 43 | 316 | **359** | mort. + selección | 369 |
| Junio | 116 | 32 | 84 | **116** | mort. + selección | 176 |
| Julio | 257 | 69 | 188 | **257** | mort. + selección | 216 |

**Enero y febrero se compararon con mortalidad sola; abril a julio con mortalidad + selección.**
Corrigiendo el criterio, dos de las siete diferencias reportadas **desaparecen**:

| Mes | Diferencia reportada | Diferencia con criterio homogéneo |
|---|---:|---:|
| Enero | −4 | **0** (41 vs 41) |
| Febrero | −39 | **−1** (323 vs 324) |

Esto no es un defecto de la plataforma ni del ERP: es el criterio de la conciliación. **Antes de
volver a comparar hay que fijar por escrito si «mortalidad» incluye selección y error de sexaje.**

En todo el levante: mortalidad 660 · selección 1.089 · error de sexaje 76.

---

## 3. HALLAZGO — traslape levante ↔ producción en julio 2025 (16.952 kg contados dos veces)

El seguimiento de levante llega hasta la semana 25 (22-jul K345A, 25-jul K345B) pero el seguimiento de
producción **arranca antes**, con el primer huevo (16-jul K345A, 19-jul K345B). Esos días quedan
registrados en **las dos etapas, con el mismo consumo**:

| Lote | Días traslapados | kg duplicados | Mortalidad duplicada |
|---|---:|---:|---:|
| K345A | 16 a 22-jul-2025 (7) | 7.170,80 | 4 |
| K345B | 19 a 25-jul-2025 (7) | 9.781,20 | 6 |
| **Total** | **14** | **16.952,00** | **10** |

Consecuencia directa: **julio no es comparable** contra el ERP tal como está, y cualquier reporte que
sume «levante + producción» del ciclo infla el consumo en ~17 toneladas. Explica el grueso del
descuadre de julio en las dos filas del correo (levante −17.125 kg respecto de la BD; producción
jul-ago −10.440,60 vs ERP).

**Acción:** el corte de etapa debe ser único y excluyente. Propuesta: el día en que abre producción
cierra levante; ningún día puede existir en las dos tablas.

---

## 4. HALLAZGO — el histórico de producción se cargó de forma retroactiva, no día a día

Trazabilidad de los registros de K345A + K345B:

| Etapa | Registros | Cómo se crearon |
|---|---:|---|
| Levante | 340 de 351 | el **mismo día** de la fecha registrada (captura diaria real) |
| Levante | 11 | creados el **9 y 10-abr-2026**, con 278 a 384 días de rezago (días faltantes completados después) |
| **Producción** | **602 (100 %)** | **todos con el mismo `created_at`: 11-jul-2026 06:15:00.955181, `created_by_user_id = 0`** |

Los 602 días de producción (jul-2025 → may-2026) entraron **en una sola transacción, sin usuario y sin
registro en el módulo de Migraciones Masivas**. Es decir: para este lote, la conciliación no está
midiendo la captura diaria de la plataforma sino **la calidad de una carga histórica hecha después de
cerrado el ciclo**.

Los 11 días de levante completados en abril-2026 explican además por qué una misma consulta da
resultados distintos según cuándo se extrajo (marzo y mayo del correo, §1.1).

> ⚠️ Confirmar contra producción antes de comunicarlo formalmente. En el snapshot local la evidencia
> es inequívoca (los registros de levante conservan su `created_at` real día a día, así que el
> restore no reescribió la columna).

---

## 5. HALLAZGO — las dos diferencias grandes de alimento se compensan entre sí

| Bimestre | APLICACIÓN | ERP | Diferencia |
|---|---:|---:|---:|
| jul-ago | 133.556,40 | 143.997,00 | **−10.440,60** |
| sep-oct | 199.756,20 | 189.636,00 | **+10.120,20** |
| **Suma** | **333.312,60** | **333.633,00** | **−320,40** (−0,10 %) |

No es pérdida de información: es **desfase de corte de periodo** (~10,3 t que la aplicación registró
en un bimestre y el ERP en el otro). El correo lo atribuye a «un error en el registro»; los datos
dicen que el dato está completo, solo asignado a otro mes. Se cierra solo en el acumulado.

Lo mismo pasa con el traslape de julio (§3): no falta alimento, está en la otra etapa.

---

## 6. Cierre del ciclo — el +524 de mortalidad de mayo es un descarte de machos

Un único registro, **K345B el 14-may-2026, con 539 machos en mortalidad** (el día siguiente el lote
cierra). Es el descarte final del plantel de machos, cargado como mortalidad.

| Concepto | Aplicación | ERP | Δ |
|---|---:|---:|---:|
| Mortalidad producción, total | 2.031 | 1.508 | +523 |
| Mortalidad producción, sin el descarte final | **1.492** | 1.508 | **−16 (−1,1 %)** |

Criterio de registro al cierre, no defecto de cálculo. Hay que definir si el descarte final va como
mortalidad, venta o selección — hoy queda como mortalidad e infla el indicador del último bimestre.

---

## 7. Foto global del ciclo (lo que importa para costos)

### Alimento

| Etapa | Aplicación | ERP | Diferencia | % |
|---|---:|---:|---:|---:|
| Levante | 223.710,92 | 222.465,40 | +1.245,52 | +0,56 % |
| Producción | 891.613,60 | 896.514,05 | −4.900,45 | −0,55 % |
| **Ciclo** | **1.115.324,52** | **1.118.979,45** | **−3.654,93** | **−0,33 %** |

De los −4.900 kg de producción, **−4.534 están en el último bimestre** (may-jun), donde el lote se
liquida el 15-may: consumo que el ERP despachó y que la aplicación ya no registró como consumido.

### Mortalidad, con criterio homogéneo

| Etapa | Aplicación | ERP | Δ | % |
|---|---:|---:|---:|---:|
| Levante (mortalidad + selección) | 1.749 | 1.779 | −30 | −1,7 % |
| Producción (sin descarte final de machos) | 1.492 | 1.508 | −16 | −1,1 % |
| **Ciclo** | **3.241** | **3.287** | **−46** | **−1,4 %** |

Con criterio homogéneo el ciclo cierra en **−0,33 % en alimento y −1,4 % en aves**. Las diferencias
mensuales grandes son de **asignación de periodo y de criterio**, no de dato perdido.

### Producción de huevo registrada (no está en el correo)

Total 3.632.634 · fértil/incubable 3.484.872 (95,9 %) · desecho 18.083.

---

## 8. Los dos defectos de reporte que reporta costos: CONFIRMADOS

### 8.1 Hoja RESUMEN sin selección — CONFIRMADO

`backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableExcelService.cs:143-156`

La hoja RESUMEN consolida `Semana · Período · Mortalidad · Traslados · Ventas · Alimento · Agua ·
Medicamento · Vacuna · Otros · Total General`. **No tiene columna de Selección** (ni de error de
sexaje), aunque el dato ya viaja en el DTO (`TotalSeleccionSemanal`,
`ReporteContableDto.cs:117-119`) y **sí** aparece en las hojas semanales
(`ReporteContableExcelService.cs:384`).

Matiz: el resumen no consolida «únicamente mortalidad» — también traslados y ventas. Lo que falta es
**selección**, que en este lote pesa mucho: **1.089 aves en levante y 11.919 en producción**.

Corrección: agregar la columna al arreglo de encabezados y al acumulado. Cambio acotado, sin
migración ni impacto en otros módulos.

### 8.2 Excel contable sin movimiento de huevo — CONFIRMADO (con matiz)

La información **sí existe** en el módulo:
- Endpoint `GET /api/ReporteContable/movimientos-huevos`
  (`ReporteContableController.cs:172`), con `HvtoFertil` (incubable), `HvoComercial`
  (limpio + tratado) y `HuevoDesecho` — `ReporteMovimientosHuevosDto.cs:18-21`.
- Pestaña **«Movimientos de Huevos»** en pantalla
  (`frontend/src/app/features/reporte-contable/components/tabla-movimientos-huevos/`).

Lo que falta es **exportarla**: `ReporteContableExcelService.cs` no escribe ni un campo de huevo (0
coincidencias), y `ReporteContableDto.cs` tampoco los lleva. El botón «Exportar Excel» genera RESUMEN
+ una hoja por semana, sin huevo.

Corrección: agregar una hoja «MOVIMIENTOS DE HUEVOS» al libro, alimentada del mismo servicio que ya
usa la pantalla. Cambio acotado.

---

## 9. Acciones propuestas

**Plataforma (desarrollo)**
1. Hoja RESUMEN del informe contable: agregar Selección (y error de sexaje). — §8.1
2. Excel del informe contable: agregar la hoja de movimiento de huevo. — §8.2
3. Corte único levante/producción: impedir que un mismo día exista en las dos etapas; y limpiar los
   14 días traslapados de K345. — §3
4. Eliminar el registro huérfano de levante de K345A con fecha **07-abr-2026** (todo en ceros, creado
   9 meses después de cerrado el levante) y bloquear el alta de seguimientos en etapas ya cerradas.
5. Que toda carga histórica pase por Migraciones Masivas (usuario + archivo + trazabilidad). Los 602
   registros de §4 entraron sin rastro auditable.

**Proceso (costos + técnica)**
6. Fijar por escrito el criterio de «mortalidad» para conciliar (¿incluye selección? ¿error de
   sexaje? ¿descarte final?) y volver a correr la comparación con ese criterio.
7. Definir si se concilia **consumo** (aplicación) contra **despacho** (ERP), y con qué corte de
   periodo. Buena parte de las diferencias mensuales son desfase de corte.
8. Repetir la validación sobre un lote **capturado día a día**, no sobre uno cargado retroactivamente,
   si lo que se quiere medir es la plataforma.

**Pendiente de insumo**
9. Pedir a costos el archivo exacto con el que armaron levante marzo (27.720,30), mayo (44.962,60) y
   julio (39.008,02): son las 3 celdas que no reproducen contra la BD.
10. Pedir a Verenice sus registros técnicos para conciliar campo por campo.
