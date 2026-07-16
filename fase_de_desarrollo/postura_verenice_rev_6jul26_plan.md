# Plan — Matriz Verenice rev 6-jul-26 · Postura Colombia (validación + corrección)

> **Fuente:** `Downloads/Plantilla de lider Funcional AppZootenico Verenice 6jul26.xlsx` (12 hojas: matriz + hojas por REQ con screenshots + hoja FÓRMULAS con ~140 parámetros zootécnicos).
> **Naturaleza:** re-validación de la líder funcional sobre la versión ya corregida en julio (iniciativa `postura_colombia_alineacion_guia_plan.md`). Incluye 2 REQ nuevos (011, 012), fallas que persisten (REQ-008 "siguen pendientes las correcciones") y bugs nuevos detectados en Indicadores.
> **Método de validación:** investigación de código en main (workflow 8 agentes, evidencia file:line) + verificación de datos en BD local `sanmarinoapplocal` (copia de prod). `main-produccion` está solo 3 commits detrás de main ⇒ los fixes de julio YA están desplegados; varias capturas del Excel son del 12-jun (pre-fix).

---

## 1. Resumen ejecutivo de la validación

| Estado | Ítems |
|---|---|
| ✅ **RESUELTO en main** (verificar en QA/prod) | REQ-007e (Día calendario), REQ-007f/h (alimento por sexo), REQ-007g (Uniformidad/CV), REQ-010a/c/d (comparativo guía, 157g eliminado, guía por semana), REQ-N5 (etapas — con borde a alinear) |
| 🟡 **PARCIAL** | REQ-001a, REQ-002f/jkl, REQ-002g/i, REQ-002-B36, REQ-007d, REQ-009a/b, REQ-010b/f, REQ-000a/c, REQ-003, REQ-004, REQ-005, REQ-006, REQ-N1/N2/N3 |
| 🔴 **FALLA** (bug vigente en main) | REQ-002a/b/e/m/n, REQ-008a/b/c, REQ-011a/b/c/d, REQ-012a/b/c/d/e, REQ-000b |
| 🆕 **NUEVO** (alcance nuevo) | REQ-001c (nombres ERP), REQ-007i (bodegas), REQ-009c (guardas creación lote), REQ-N4 (embriodiagnosis) |
| ⛔ **BLOQUEADO por definición de la líder/negocio** | REQ-006 (flujo aprobación), REQ-007i (qué es "bodega"), REQ-001b (doc "Informe RA Pesadas 2025"), IP (fórmula), tabla nutricional alimentos, % clasificación huevo en guía, formato embriodiagnosis |

### Descubrimientos raíz (explican la mayoría de los síntomas)

1. **Columnas corridas en Indicadores Levante (REQ-002m/n):** en `tabla-lista-indicadores.component.html` el `<tbody>` pinta el bloque Mortalidad&Selección ANTES que Peso y Uniformidad, mientras el `<thead>` los tiene después → 9 columnas desplazadas. "Peso Real (g) = 1.01%" es la mortalidad semanal; "Uniformidad Tabla = 159/320/473" son pesos de guía. **El backend calcula todo bien** (`fn_indicadores_levante_postura` verificada en BD: peso 155/312/493 g, unif 71.5–76.75 vs tabla 70).
2. **Lote duplicado con encaset futuro (REQ-011, REQ-002-B36):** existen DOS lotes "A374A" — id 114 (encaset 2025-10-16, real) e id 116 (encaset **2026-10-14, un año en el futuro**, aves iniciales NULL; creado a mano vía `LoteService.CreateAsync` que NO valida fecha futura ni nombre duplicado). Con encaset futuro, todos los cálculos clampean: Semana=1/Edad=1 congeladas, saldo=0, %pérdidas=100%, indicadores colapsados en 1 fila absurda. Ídem A374B (117).
3. **Filas de traslado fechadas "hoy" (REQ-008c, REQ-009):** el traslado escribió filas SALIDA/INGRESO en `seguimiento_diario_levante` con fecha 2026-06-08/11 (día en que se ejecutó, no el movimiento físico) → semanas fantasma 34/35 en lote 114 e ingreso "tardío" en 116 (144 filas previas con saldo 0). El modal HOY ya tiene input de fecha (`f18b559`), pero el **default sigue siendo hoy** (y usa `toISOString()` = UTC, que después de ~19:00 en Colombia da mañana).
4. **Año de guía desalineado (REQ-002g/i):** K345A/B (lotes 13/14) siguen con `ano_tabla_genetica=2023` en BD (el script de alineación de julio no se corrió en prod y la BD local es copia de prod) → el join con `guia_genetica_sanmarino_colombia` matchea la guía vieja o nada.
5. **Producción sin fecha base (REQ-012c/d):** el endpoint `filter-data` de producción arma `LoteFilterItemDto` SIN `fechaEncaset` (el DTO correcto `LotePosturaProduccionFilterItemDto` existe pero nadie lo construye) → la tabla calcula edad con base null: EDAD(DÍAS)=0, EDAD(SEMANAS)=clamp(26) fija, mientras la tarjeta (otro endpoint) dice 74. La etapa se guarda congelada desde un modal que recibe `fechaEncaset=null` y además usa rango 25-33 (debe ser 26-33).

---

## 2. Matriz de validación detallada (estado real en código, con evidencia)

### REQ-000 — Generalidades (Crítica)
| Ítem | Estado | Causa raíz | Solución |
|---|---|---|---|
| a) Carga masiva datos previos | 🟡 PARCIAL | Migraciones Masivas cubre Granjas/Núcleos/Galpones (F1) + SeguimientoLevante/Producción (F2). `TipoMigracion.cs:42-49`: Ventas/MovimientoAves/MovimientoHuevos `Disponible=false` (F3 sin implementar) y NO existe alta masiva de LOTES postura (sí de engorde) | Agregar `TipoMigracion.LotesPostura` (espejo de LotesPolloEngorde: lote + lote_postura_levante con encaset/raza/año/aves) + implementar F3 |
| b) Pérdida de datos con señal inestable | 🔴 FALLA | 0 usos de localStorage/draft en lote-levante y lote-produccion; sin service worker/PWA | `DraftStorageService` compartido en `shared/`: autosave del form (valueChanges + debounce ~1s) con clave `seg-draft:{modulo}:{loteId}:{fecha}`, restaurar al abrir con banner, limpiar al guardar OK. Aplicar a modal-create-edit (levante) y modal-seguimiento-diario (producción) |
| c) Gráficos en celular | 🟡 PARCIAL | NO es responsive (breakpoints y chart.js responsive existen): el empty-state "No hay datos suficientes… desde la semana 26" es el mismo bug de datos (lote 116 encaset futuro / K345 guía 2023) visto en móvil | Se resuelve con el data-fix (Fase 0); mejora UX: empty-state que muestre la CAUSA (encaset inválido / sin guía) |

### REQ-001 — Ingreso Diario (glosario)
| Ítem | Estado | Detalle |
|---|---|---|
| a) "Bajas" → "Mortalidad Total" | 🟡 PARCIAL | Tabla diaria ya dice "TOTAL MORT+SEL/DÍA" (f18b559). Falta el tab Reporte semana: `tabs-principal.component.html:378` "Bajas total", `:386` "% bajas/ini" + export Excel (ts:675,683) + hints (:196,:360). Ojo semántico: bajasTotal = mort+sel+err.sexaje |
| b) Nombres según "Informe RA Pesadas Parámetros - Gráficos 2025" | ⛔ BLOQUEADO | Necesitamos el documento de la líder para mapear nombre a nombre |
| c) Alimentos = ERP | 🆕 DATO/PROCESO | `item_inventario` se llena manual/Excel sin validación contra ERP (quedan ítems de prueba tipo "moises"). Operativo: cargar maestro ERP vía `POST /ItemInventario/CargaMasivaExcel` (upsert por Código) + desactivar ítems de prueba. Opcional: sync periódica |

### REQ-002 — Indicadores Levante
| Ítem | Estado | Causa raíz | Solución |
|---|---|---|---|
| a) Región/granja como encabezado | 🔴 FALLA | 4 columnas (REGIONAL/GRANJA/MÓDULO/SUB LOTE) repetidas por fila con valores del lote (html:66-67, 82-85, 118-121) | Quitar columnas; franja/chips encima de la tabla (patrón chips del list + chip Regional). Corregir colspan empty-state (105: dice 28, la tabla tiene 27) |
| b) Regional muestra "-" | 🔴 FALLA | Front pinta `lote_postura_levante.regional` (NULL). El dato real: `farms.regional_id` → `master_list_options.value` ("Oriente"/"Occidente" verificado en BD) | En `LotePosturaLevanteService` devolver `Regional = l.Regional ?? lookup MasterListOptions` (patrón `FarmService.cs:497`) |
| e) Consumo sin H/M | 🔴 FALLA | `fn_indicadores_levante_postura.sql:133` suma H+M; `:183` compara vs promedio simple de guía (sesgado: machos ~12% del lote) | Extender fn con `consumo_diario_h/m` + `consumo_tabla_h/m` (guía ya tiene `gr_ave_dia_h/m`); DTO + subcolumnas H/M. Requiere saldo por género dentro de la fn |
| f/jkl) Cálculos/resumen no coinciden | 🟡 PARCIAL | (1) acumulados = SUMA de % semanales sobre base decreciente (sql:206-208,238-240; real 2.171% vs fn 2.19%); (2) semana 25 fantasma por filas traslado clampeadas con `LEAST(25)` (sql:127) — y el Resumen Acumulado del front toma LA ÚLTIMA fila (html:192-200); (3) semana sin pesaje con peso_inicial 0 → dif -100% | (1) acumulados = bajas_acum/aves_iniciales; (2) excluir filas solo-traslado del loop; (3) resumen desde totales, no última fila; decidir si err.sexaje entra al acumulado (hoy descuenta saldo pero no suma) |
| g/i) Guía no es 2026 | 🟡 DATO | Código resuelve bien por `lotes.ano_tabla_genetica`; K345A/B siguen 2023 (el script de julio nunca corrió en PROD) | Data-fix: UPDATE lotes 13/14 → 2026 + auditar company 1 + sync `lote_postura_levante`. Decisión de negocio: ¿año del encaset o año vigente? |
| m) Peso con % | 🔴 FALLA | **Columnas corridas**: tbody pinta mortalidad (html:151-164) antes que peso (165-167) y uniformidad (168-169); thead al revés (70-79). Backend OK | Reordenar los `<td>` del tbody para calzar con el thead. Solo template |
| n) Uniformidad real 0 / tabla=pesos | 🔴 FALLA | Misma corrida: bajo "Unif Real" cae errorSexajeSem; bajo "Unif Tabla" cae pesoCierre. fn SÍ calcula unif (71.5–76.75 real, 70 tabla) | El mismo reorden de (m). Nota: semanas sin pesaje → unif 0 (la fn no arrastra unif como sí arrastra peso; decidir arrastre) |
| B36) Indicadores vacíos lote Oriente | 🟡 PARCIAL | Lote 116: encaset futuro → fn colapsa 144 días en "semana 1" con base 0 (GREATEST(1,…) sql:127; COALESCE(aves,0) sql:112). Bug latente extra: llamar la fn 2× en la misma transacción falla (`CREATE TEMP TABLE _seg_sem` sin DROP) | Data-fix 116/117 + defensas fn: fallback base = COALESCE(aves_encasetadas, hembras+machos, primer traslado_ingreso); no clampear semanas negativas; `DROP TABLE IF EXISTS _seg_sem` |

### REQ-003 — Lote General A+B+C (Crítica)
🟡 PARCIAL — El consolidado existe COMPLETO en reportes-tecnicos (back: `ReporteTecnicoService.cs:511-546` Sum flujos + Average peso/unif/cv — exactamente la regla pedida; producción: `ReporteTecnicoProduccionService`). GAP: los tabs operativos de lote-levante/lote-produccion no tienen opción "Lote General". **Solución:** opción "Lote General (todos los sublotes)" en el filtro de lote de tabs-principal que consuma los endpoints consolidados existentes.

### REQ-004 — Parámetros técnicos producción
🟡 PARCIAL — %Prod (÷hembras), HTAA, HIAA, GrAveDia H/M, ConsAc H/M implementados en `fn_indicadores_produccion_postura` + UI + Excel (verificado). GAP: **%Retiro sem/acum real no existe en la fn ni en la UI** (`ProduccionCalculos.PorcentajeRetiro*` está testeado pero sin consumidores; solo viaja la guía `retiroAcumulado*Guia`). **Solución:** agregar retiro_sem/ac H/M a la fn + DTO + columnas Real vs Guía + Excel.

### REQ-005 — Comparativo Power BI
🟡 PARCIAL — `backend/sql/vw_guia_genetica_por_lote_postura.sql` (+`f_safe_numeric`) existe y es idempotente, pero **ninguna migración EF la aplica** ⇒ nada garantiza que exista en RDS prod. **Solución:** migración EF manual (patrón `AddFnIndicadoresProduccionPostura`) con el contenido del .sql.

### REQ-006 — Edición de registros
🟡 PARCIAL — Levante: bloqueo por lote Cerrado SOLO en front (`seguimiento-lote-levante-list.component.ts:163-166,888`); backend Update/Delete NO valida EstadoCierre. Producción: sin guard ni en front ni back. Sin modelo de aprobación. **Solución corto plazo:** replicar guard en producción + enforcement backend (patrón `LotePosturaLevanteService.cs:335`). **Mediano plazo (⛔ decisión):** ventana de edición + estado_registro/aprobado_por + endpoint aprobación.

### REQ-007 — Form/tabla seguimiento levante
| Ítem | Estado | Detalle |
|---|---|---|
| c) Consumo acumulado H/M | 🔴 FALLA | Un solo acumulador `acumCons += ch+cm` (ts:199,219-222). Dato diario por sexo ya viaja. Fix front-only: 2 acumuladores + 2 columnas + export + colspan |
| d) %pérdidas → %Retiro semanal | 🟡 PARCIAL | Header renombrado (f18b559) pero la celda sigue con `pctPerdidasDia` (html:282); `ProduccionCalculos.PorcentajeRetiroSemanal` existe SIN consumidores; fallback `saldo=0 → 100%` (ts:227-232) es el 100% del lote 116 | Calcular retiro semanal agrupando por semana en buildDiarioFilas (saldo<=0 → null, jamás 100 sintético) + export |
| e) Día calendario | ✅ RESUELTO | f18b559 (2026-07-01); screenshots eran pre-fix. Limpieza opcional: `diaCorto`/`formatDiaSemanaCorto` quedaron muertos |
| f/h) Alimento por sexo | ✅ RESUELTO | 58099b4 + 8e9bbc1 (2026-07-10): FormArrays `itemsHembras/itemsMachos` independientes con stock. Verificar en QA que company 1 tenga ítems con stock |
| g) Uniformidad/CV | ✅ RESUELTO | f18b559 quitó `hidden`; payload y indicadores OK. Si Verenice no los ve → guía del lote (K345 año 2023) |
| i) Bodega única por granja | 🆕/⛔ | Colombia modelo B descuenta stock a nivel granja (`ColombiaInventarioConsumoService.cs:114-132`, nucleo/galpon NULL a propósito); el esquema YA soporta ubicación (índice farm+item+nucleo+galpon). Opciones: (a) migrar Colombia al camino EC/PA (consumo por núcleo+galpón, con migración de stock); (b) dimensión bodega_id; (c) por sexo ya se logra con ítems distintos H/M. **Definir con Verenice qué es "bodega"** |

### REQ-008 — Reporte Semanal Levante ("siguen pendientes")
| Ítem | Estado | Detalle |
|---|---|---|
| a) Consumo agrupado → parámetros por sexo | 🔴 FALLA | Tab emite Consumo H/M/Total absolutos (html:379-381; ts:461-469); nunca se implementó gr/ave/día por sexo. Requiere saldo por sexo en buildDiarioFilas (DTOs ya traen todo por género). Nota: convertir getter `reporteSemanaFilas` en campo memoizado (patrón NG0103) |
| b) % consumo H/M | 🔴 FALLA | Columnas presentes HOY en main (html:382-383, ts:66-67,470-471,488-489 + export 679-680,702-703). Eliminarlas (memoria vieja verificó otro tab) |
| c) Semana fantasma 34/35 | 🔴 FALLA | Filas traslado-only con fecha=hoy + agrupación por semana sin filtrar (`esTraslado` ya existe en el DTO front). Fix: excluir filas traslado-only del reporte + data-fix de las filas existentes |

### REQ-009 — Traslado de aves
| Ítem | Estado | Detalle |
|---|---|---|
| a) Fecha queda en actual | 🟡 PARCIAL | Input de fecha ya existe con max=hoy (f18b559) y el back respeta `dto.FechaSeguimiento`. PERO ambos callers pasan `new Date().toISOString()` como default (UTC ⇒ +1 día después de 19:00 COL) y el usuario no la cambia | Default = fecha del último registro del lote origen (o vacía y required); calcular hoy en local; opcional: persistir fecha_evento en historial_traslado_lote |
| b) Genera semana actual | 🟡 PARCIAL | Consecuencia de (a). Se cierra con (a) + filtro REQ-008c + data-fix filas históricas |
| c) NUEVO — guardas creación lote | 🆕 | `LoteService.CreateAsync` no valida fecha futura ni nombre duplicado (así nació el 116); el modal no tiene `[max]`. Fix back (patrón `ProduccionService.cs:147`) + front `[max]=hoy` + chequeo duplicado |

### REQ-010 — Gráficas levante
| Ítem | Estado | Detalle |
|---|---|---|
| a) Comparar con guía | ✅ RESUELTO | Tarjeta Real vs Guía + TODAS las gráficas leen del endpoint BD (e6e008d/d5b8766, 2-jul). La guía viaja embebida en el DTO por semana |
| b) Selector H/M | 🔴 FALLA | Ningún selector de sexo; la fn devuelve series mixtas (suma/promedio). Fix: parámetro sexo en fn/endpoint (o columnas paralelas *_h/*_m) + selector en gráficas e indicadores |
| c) 157 g / sumatoria | ✅ RESUELTO | 0 ocurrencias de "157"; consumo = g/ave/día desde BD. Mejora: renombrar etiquetas a "Consumo (g/ave/día)" |
| d) Guía plana | ✅ RESUELTO | Guía por semana desde BD. Riesgo residual = datos (K345 año 2023 → serie Guía desaparece) |
| f) Conversión alimenticia | 🟡 PARCIAL | Solo se quitó de `metricasDisponibles` (ts:91). Sigue viva en: checkbox+canvas+2 tarjetas de gráficas (html:144-151,439-455,578-602,667-672 + ts), grupo "Conversión (CA)" en tabla-indicadores-diarios, ficha FCR en tabla-lista-indicadores, y **liquidación técnica** (aporta al % cumplimiento — quitarla cambia el promedio de 4→3 parámetros: cambio de comportamiento INTENCIONAL, documentar) |

### REQ-011 — Levante: semana congelada (NUEVO)
| Ítem | Estado | Detalle |
|---|---|---|
| a) Encaset no corresponde | 🔴 FALLA+DATO | Lote 116/117 con año tecleado mal (2026); sin validación anti-futuro en form ni back. Data-fix + guardas (ver Fase 0 y REQ-009c) |
| b) Consumo de hembras sin hembras | 🔴 FALLA | Ninguna capa valida consumo/mortalidad de un sexo con saldo 0 a esa fecha. Además `CalcularHembrasVivasAsync` NO suma traslados (auto-consumo por gramaje da 0 en lotes poblados por traslado) | Validación back (saldo por sexo a la fecha, incluyendo traslados) + aviso front + fix CalcularHembrasVivasAsync |
| c) Saldo 0 / %pérdidas 100% | 🔴 FALLA+DATO | inicial = hembrasL+machosL (NULL→0); la fila de ingreso por traslado quedó fechada 2026-06-11 ⇒ 140 filas previas sin saldo. El cálculo por fila es correcto; el dato de fecha está corrido | Data-fix fechas de filas traslado + aviso "lote poblado por traslado" en vez de 100% |
| d) Semana no avanza | 🔴 FALLA | Todas las variantes clampean silencioso: front `max(0/1,…)` (ts:726-737, 234-238), back `SeguimientoEngordeCalculos.CalcularSemana` max(1,…); `LiquidacionTecnicaService.cs:357-364` SIN clamp (semanas negativas). | Data-fix resuelve síntoma; código: fecha<encaset ⇒ edad/semana null + celda "—" + banner; unificar cálculo en función pura compartida |

### REQ-012 — Producción (NUEVO)
| Ítem | Estado | Detalle |
|---|---|---|
| a) Fecha inicio ≠ primer dato | 🔴 FALLA | Default del modal de cierre de levante = HOY (`seguimiento-lote-levante-list.component.ts:899`); se congela en LPP; nadie valida vs seguimientos (existen registros anteriores a la fecha) | Fecha efectiva = MIN(fecha) de seguimientos si < almacenada (en `ObtenerInformacionLoteAsync`) + backfill SQL + warning en el modal |
| b) Semana 25 sin huevos | 🔴 FALLA | Restricción distribuida: fn borra `sem_vida<26` (sql:397) + loop desde 26 (sql:410) + front clamp `max(26,…)` + legacy 182 días (off-by-one → aparece en 27) | Cambiar todo a 25 (guía sem 25 no existe → columnas guía NULL ya soportado); legacy 175 días. Migración EF CREATE OR REPLACE de la fn |
| c) Etapa 3 con edad 26 | 🔴 FALLA | Etapa ALMACENADA al crear, calculada con `fechaEncaset` que llega null en flujo LPP (etapa manual del usuario); rangos front 25-33 vs hoja 26-33; K345A real tiene 74 semanas (etapa 3 correcta — lo roto es la EDAD mostrada) | Etapa calculada en vivo desde semana real (26-33/34-50/>50), select solo-lectura, alinear las 3 fórmulas (modal/detalle/indicadores + `MovimientoAvesCalculos.EtapaProduccion` borde <26) |
| d) EDAD 0/26 fija vs tarjeta 74 | 🔴 FALLA | `filter-data` devuelve DTO genérico SIN fechaEncaset (el DTO correcto existe, nadie lo construye) → base null → 0 días/clamp 26. Tarjeta usa otro endpoint (correcto, 74) | Fix mínimo: anteponer `informacionLote.fechaEncaset` en `getFechaBaseEdad()` (ya viaja). Fondo: filter-data propio de producción con FechaEncaset/aves/estadoCierre; eliminar fallback que usa fechaInicio PRODUCCIÓN como encaset |
| e) TIPO ITEM sobra | 🔴 FALLA | Con el modelo de items siempre infiere "alimento" (ts:255-268). Quitar columnas tabla+Excel; ocultar select en modal si `isColombia()` (patrón levante) |

### Hoja FÓRMULAS — gap de parámetros nuevos
| Grupo | Estado | Detalle |
|---|---|---|
| N1) Kcal/Proteína sem+acum H/M vs guía | 🟡 PARCIAL/⛔ datos | Provider + 4 columnas (solo H, solo diario, solo levante) existen; **0/125 ítems con datos nutricionales en catalogo_items.metadata** ⇒ todo NULL. La guía SÍ trae kcal/prot pobladas. Falta: UI para kcal/kg + %prot en catálogo, machos, agregación semanal en las 2 fn, comparación guía. **Bloqueante: tabla nutricional de la líder** |
| N2) % clasificación huevo vs guía | 🟡 PARCIAL/⛔ datos | Lado REAL completo (11 tipos capturados + % en reporte Clasificación). GAP: la guía no tiene columnas de % por tipo (los *Guia van null a propósito). Falta migración de columnas en guía + plantilla import + lookup. **Bloqueante: confirmar si la guía trae esos % por semana** |
| N3) IP, MasaHuevo, PesoM/H, GrHuevoT/Inc(+MES) | 🟡 PARCIAL | Derivados de datos existentes; guía ya trae gr_huevo_t/inc y peso_mh (solo se importan, no se usan). MasaHuevo ya aproximada en un reporte. **IP: fórmula no definida — pedirla** |
| N4) Infertilidad (embriodiagnosis) | 🆕 L | 0 matches "embrio" en el repo: módulo nuevo completo (entidad+migración+CRUD+pantalla+join en fn). **Bloqueante: levantamiento del formato con la líder** |
| N5) Etapas 26-33/34-50/>50 | ✅ RESUELTO | `MovimientoAvesCalculos.EtapaProduccion` idéntico a la hoja; alinear borde front 25→26 y "resto" del back (<26 devuelve 3) — se cubre con REQ-012c |

---

## 3. Fases de implementación

### Fase 0 — Data-fix (desbloquea la mayoría de síntomas) 🔥
Script idempotente `backend/sql/fix_datos_postura_verenice_jul26.sql` (aplicar LOCAL ya; **PROD solo con OK explícito**):
1. `lotes` 116/117: `fecha_encaset` ← la del lote origen (114: 2025-10-16 / 115: 2025-10-21); sync espejo en `lote_postura_levante` (ids 8/9).
2. Poblar `hembras_l/machos_l/aves_encasetadas` de 116/117 con lo trasladado (7.617 H / 1.010 M según acumulados) **o** re-fechar la fila de ingreso del traslado antes del primer registro — ⛔ confirmar con operación la fecha real del movimiento físico (~2025-11-04).
3. Re-fechar/eliminar filas traslado-only mal fechadas (lote 114: 2026-06-08 y 2026-06-11 "Traslado SALIDA"; espejo INGRESO en 116) → elimina semanas fantasma 34/35.
4. `lotes` 13/14 (K345A/B): `ano_tabla_genetica` 2023→2026 + auditoría de todos los lotes activos company 1 con año sin guía cargada + sync `lote_postura_levante/produccion`.
5. Backfill `lote_postura_produccion.fecha_inicio_produccion` = MIN(fecha) de seguimientos cuando existan registros anteriores.

### Fase 1 — Hotfixes front (S, alto impacto visual)
- REQ-002m/n: reordenar `<td>` del tbody de tabla-lista-indicadores (peso+uniformidad antes de mortalidad) — arregla 9 columnas.
- REQ-002a: quitar 4 columnas repetidas → chips/franja encima (incluye chip Regional); corregir colspan.
- REQ-008b: eliminar % consumo H/M (tabla + export).
- REQ-012e: quitar TIPO ITEM H/M (tabla + export) y ocultar select si Colombia.
- REQ-001a: "Bajas total"/"% bajas/ini" → "Mortalidad Total"/"% Mort. Total/ini" (tabla + export + hints).
- Validar `cd frontend && yarn build`.

### Fase 2 — Indicadores levante (fn + DTO + UI)
- REQ-002b: Regional resuelto en `LotePosturaLevanteService` vía master_list_options.
- REQ-002e: consumo H/M real y guía H/M en `fn_indicadores_levante_postura` (+ saldo por género interno) + DTO + subcolumnas.
- REQ-002f/jkl: acumulados = bajas_acum/aves_iniciales; excluir filas solo-traslado del loop (mata semana 25 fantasma); Resumen Acumulado desde totales.
- REQ-002-B36: defensas fn (fallback base aves, no clamp negativo, `DROP TABLE IF EXISTS _seg_sem`).
- Migración EF idempotente (CREATE OR REPLACE) + actualizar spec en `backend/sql/`. Tests de la lógica pura si se extrae.

### Fase 3 — Semana/edad/etapa + validaciones anti-corrupción
- REQ-011a/REQ-009c: back `LoteService.Create/UpdateAsync` rechaza encaset futuro + warning duplicado; front `[max]=hoy`.
- REQ-011d: fecha<encaset ⇒ edad/semana null + "—" + banner (tabs-principal y tabla-lista-registro); unificar cálculo semana (alinear `LiquidacionTecnicaService` que da negativos).
- REQ-011b: validación consumo/mortalidad vs saldo por sexo a la fecha (back, warning front); fix `CalcularHembrasVivasAsync` (sumar traslados).
- REQ-007d: cablear %Retiro semanal (null si saldo 0, nunca 100%).
- REQ-012c/d: `getFechaBaseEdad()` ← `informacionLote.fechaEncaset`; filter-data de producción con DTO real (FechaEncaset/aves/estado); etapa calculada 26-33/34-50/>50 en vivo (select readonly); alinear `MovimientoAvesCalculos`.
- REQ-012a: fecha inicio producción efectiva = MIN(fecha) + warning en modal de cierre.
- REQ-012b: semana 25 habilitada (fn DELETE <25 + loop 25.. + clamps front 26→25 + legacy 175 días).

### Fase 4 — Traslado
- REQ-009a: default = fecha último registro del lote origen (o vacío+required); hoy en fecha LOCAL (no toISOString).
- REQ-008c: excluir filas traslado-only (esTraslado && todo 0) del Reporte semana.
- Opcional: persistir fecha_evento en historial_traslado_lote.

### Fase 5 — Reporte semana + gráficas
- REQ-008a: quitar Consumo total; agregar gr/ave/día H y M (saldo por sexo en buildDiarioFilas); memoizar reporteSemanaFilas (NG0103).
- REQ-010f/REQ-002h: barrido conversión alimenticia en lote-levante (gráficas + tabla indicadores diarios + ficha FCR + liquidación técnica — recalcular % cumplimiento con 3 parámetros, cambio intencional documentado).
- REQ-010b: sexo H/M/Ambos en fn/endpoint + selector en gráficas e indicadores.
- Renombrar etiquetas "Consumo Real (g)" → "Consumo (g/ave/día)".

### Fase 6 — Transversales
- REQ-005: migración EF de `vw_guia_genetica_por_lote_postura` + `f_safe_numeric`.
- REQ-006 (parte no bloqueada): guard producción + enforcement backend lote cerrado.
- REQ-003: opción "Lote General (A+B+C)" en tabs (consume consolidados existentes).
- REQ-004: %Retiro real en fn producción + UI + Excel.
- REQ-000b: DraftStorageService + autosave en los 2 modales.
- REQ-000a: TipoMigracion.LotesPostura + Fase 3 (Ventas/MovAves/MovHuevos).
- REQ-000c: empty-state con causa.

### Fase 7 — Fórmulas nuevas (⛔ dependen de insumos de la líder)
- N1 Kcal/Prot (tras recibir tabla nutricional): UI catálogo + machos + semanal en fns + guía.
- N2 % huevo vs guía (tras confirmar guía): columnas + import + lookup.
- N3 IP/Masa/PesoM/H/GrHuevo: fórmulas puras + fn + UI (IP tras definición).
- N4 Embriodiagnosis: módulo nuevo (tras levantamiento).

## 4. Preguntas/insumos pendientes para Verenice (bloqueantes)
1. Fecha real del movimiento físico de aves A374A/B galpón→galpón (para re-fechar filas de traslado).
2. Confirmar corrección de año de guía K345A/B 2023→2026 y política general (¿año del encaset o vigente?).
3. REQ-006: flujo de corrección (¿supervisor directo con ventana de N días? ¿aprobación contable?).
4. REQ-007i: qué es "bodega" (¿galpón? ¿bodega física por granja?).
5. Documento "Informe RA Pesadas Parámetros - Gráficos 2025" (REQ-001b) + maestro de alimentos ERP (REQ-001c).
6. Tabla nutricional por alimento (kcal/kg, %proteína) — N1.
7. ¿La guía genética trae % de clasificación de huevo por semana? — N2.
8. Fórmula exacta del IP — N3.
9. Formato del embriodiagnosis en planta — N4.

## 5. Casos de prueba (mínimos por fase)
- **F0:** fn_indicadores_levante_postura(116) devuelve N semanas desde nov-2025 (no 1 fila); Reporte semana lote 114 sin semanas 34/35; K345A muestra serie Guía 2026.
- **F1:** en Indicadores levante, Peso Real ≈ 155/312/493 g, Unif Real ≈ 70-77%, Unif Tabla = 70; sin columnas Regional/Granja por fila; chip Regional = "Oriente"/"Occidente".
- **F2:** mortalidad acumulada lote 114 = 2.17% (bajas/iniciales); consumo H y M por separado con guía gr_ave_dia_h/m.
- **F3:** crear lote con encaset mañana → rechazado; registro con fecha < encaset → celda "—" + banner; producción: EDAD(SEMANAS) avanza con la fecha, etapa 1 para semana 26; captura semana 25 permitida (guía en blanco).
- **F4:** traslado sin tocar fecha → cae en la fecha del último registro origen; Reporte semana no gana semanas nuevas por traslado.
- **F5:** Reporte semana sin % consumo ni total agrupado, con gr/ave/día H/M; ninguna vista de levante muestra conversión; % cumplimiento de liquidación promedia 3 parámetros.
- **Backend:** xUnit para toda fórmula pura nueva (retiro, gr/ave/día por sexo, masa huevo, etc.) en `tests/ZooSanMarino.Application.Tests/`.

## 6. Riesgos
- **Data-fix en prod:** DDL/DML sobre lotes reales — requiere OK explícito + backup (db-studio ya permite copia completa).
- **fn en migración:** cambiar `fn_indicadores_*` exige migración EF idempotente nueva (no editar migraciones aplicadas).
- **Liquidación técnica sin conversión:** cambia % cumplimiento (4→3 parámetros) — cambio de comportamiento pedido por REQ, comunicar.
- **BD compartida entre branches** (memoria): coordinar migraciones locales.
- **Validación consumo-vs-saldo (REQ-011b):** puede bloquear operaciones legítimas (ajustes retroactivos) — implementar como warning primero, error después de validar con campo.
