# C2 — Indicadores de PRODUCCIÓN postura → función SQL

> Objetivo: mover el cómputo de indicadores semanales de **producción** postura del backend C#
> (`IndicadoresProduccionService.ObtenerIndicadoresSemanalesAsync`, 727 líneas, todo en memoria)
> a una **función PL/pgSQL**, para que el servicio delegue en la BD (`SqlQueryRaw`) en vez de
> calcular en C#. Patrón canónico: C1 (`fn_indicadores_levante_postura`).
>
> Política del usuario: **REPLICAR EXACTO** el algoritmo actual + **CORREGIR los bugs conocidos de
> comparación vs guía** (documentando cada corrección). En producción las correcciones de guía
> (REQ-004) **ya están aplicadas en el C# actual** (fuente de verdad) → replicarlas tal cual; no se
> introducen correcciones nuevas. Toda desviación que se preserve (no un bug de guía) se documenta.

---

## 1. Fuente de verdad (algoritmo actual)
`backend/src/ZooSanMarino.Infrastructure/Services/IndicadoresProduccionService.cs`
→ `ObtenerIndicadoresSemanalesAsync` + `CalcularIndicadoresAsync`.
Endpoint: `POST Produccion/indicadores-semanales` (`ProduccionController`).
Contrato de respuesta a preservar: `IndicadoresProduccionResponse` con
`Indicadores: List<IndicadorProduccionSemanalDto>`, `TieneDatosGuiaGenetica`, `MensajeGuiaGenetica`.

### 1.1 Resolución de lote (se queda en C#)
- **Flujo LPP** (`LotePosturaProduccionId > 0`): busca `lote_postura_produccion` por LPP + company + no borrado.
  - `fechaEncaset` = `fecha_encaset` del **levante** ligado (`lote_postura_levante_id`) si existe;
    si no, `lpp.fecha_encaset`; si no, `lpp.fecha_inicio_produccion`.
  - `avesHIniciales` = `aves_h_inicial ?? hembras_iniciales_prod ?? 0`; `avesMIniciales` = `aves_m_inicial ?? machos_iniciales_prod ?? 0`.
  - `raza`, `anoTablaGenetica` = del LPP.
  - Datos: `seguimiento_diario_levante_reproductoras` (tipo `produccion`, `lote_postura_produccion_id=lpp`)
    **UNION** legacy `seguimiento_diario_produccion_reproductoras` (`lote_postura_produccion_id=lpp`),
    **merge por Fecha.Date** (agrupa por día, toma el primero por fecha), ordenado por fecha.
- **Flujo legacy** (`LoteId > 0`): busca `lotes` en fase `Produccion` con `lote_padre_id=loteId` (o `lote_id=loteId`),
  company + no borrado. `fechaEncaset`= `fecha_inicio_produccion` (o `fecha_encaset` del padre).
  Aves = `hembras_iniciales_prod`/`machos_iniciales_prod`. Datos: unificado (`lote_id = str(lote_prod.lote_id)`)
  UNION legacy (`lote_id = str`), mismo merge por día. Raza/año del lote o su padre.

### 1.2 Guía genética (una sola tabla: `guia_genetica_sanmarino_colombia`)
El C# usa DOS lecturas de la MISMA tabla (`ProduccionAvicolaRaw` mapea a `guia_genetica_sanmarino_colombia`):
- `guias` (vía `ObtenerGuiaGeneticaProduccionAsync`): edad≥26, parsea `gr_ave_dia_h/m`, `peso_h/m`,
  `mort_sem_h/m`, `uniformidad`. Filtro: `company_id` + `raza` LIKE (trim+lower, sin comodines = igualdad
  case-insensitive) + `anio_guia` (trim) = año.
- `guiaRawBySemana` (`ProduccionAvicolaRaw` crudo): mismo filtro; parsea `h_total_aa`, `h_inc_aa`,
  `prod_porcentaje`, `peso_huevo`. Indexado por edad numérica (`TryParseEdadNumerica`).
- **Edad de la guía = semana de VIDA** (26, 27, …), no la semana de producción.
- Parseo de strings: `ParseDecimal` (trim, coma→punto, InvariantCulture).

### 1.3 Agrupación por semana
- `dias = (Fecha.Date − fechaEncaset.Date).Days`; `semanaVida = (dias / 7) + 1` (división entera).
- Filtro `semanaVida >= 26` (solo producción). Luego filtro por `SemanaDesde/SemanaHasta` (sobre semanaVida).
- `fechaInicioSemana = fechaEncaset + (semanaVida−1)*7 días`; `fechaFinSemana = +6`.

### 1.4 Cálculo por semana (orden idéntico al C#)
Acumuladores: `avesHActuales`=iniciales, `avesMActuales`=iniciales, `cumHuevosTotales=0`, `cumHuevosIncubables=0`.
Por semana (en orden):
- Sumas: `mortH, mortM, selH` (solo H), `consumoKgH, consumoKgM`, `huevosTotales, huevosIncubables`, clasificadora.
- `promedioHuevosPorDia = count>0 ? huevosTotales/count : 0`.
- `eficiencia = %Producción(hen-day) = hembrasVivas>0 ? promedioHuevosPorDia/hembrasVivas*100 : 0`
  (denominador = `avesHActuales` = hembras al inicio de la semana; **solo hembras** — REQ-004).
- `cumHuevosTotales += huevosTotales`; `cumHuevosIncubables += huevosIncubables`.
- `htaaReal = hembrasIniciales>0 ? cumHuevosTotales/hembrasIniciales : 0`; `hiaaReal` análogo.
- Peso aves H/M: promedio de registros con peso no nulo → **NormalizarPesoKg** (`>100 ? /1000 : val`) (REQ-004).
- Uniformidad, CV, pesoHuevo: promedios de registros con valor no nulo (pesoHuevo: `>0`).
- `porcMortH = avesHActuales>0 ? mortH/avesHActuales*100 : 0`; `porcMortM` análogo con machos;
  `porcSelH = avesHActuales>0 ? selH/avesHActuales*100 : 0`.
- `avesHInicioSemana = avesHActuales + mortH + selH`; `avesMInicioSemana = avesMActuales + mortM`.
  **(⚠ preservado tal cual — ver §3, no es un bug de guía)**.
- Guía por `edadGuia = semanaVida`: consumoH/M, mortH/M, pesoH/M (÷1000), uniformidad; huevosTot/Inc,
  %prod, pesoHuevo (del raw).
- `consumoRealH = count>0 && avesHInicioSemana>0 ? consumoKgH*1000/(count*avesHInicioSemana) : null`; `M` análogo.
- Diferencias % (`(real−guia)/guia*100`, null si falta real/guía o guía=0):
  consumoH/M, mortH/M (real=porcMort), pesoH/M (real=pesoPromedio kg), uniformidad,
  huevosTot (real=**htaaReal**), huevosInc (real=**hiaaReal**), %prod (real=**eficiencia**), pesoHuevo.
- Decremento al final: `avesHActuales = max(0, avesHActuales − mortH − selH)`; `avesMActuales = max(0, avesMActuales − mortM)`.

### 1.5 Respuesta
- `semanaInicial/Final` = min/max de semanas filtradas.
- `tieneDatosGuia = tieneGuia && guias.Any()`.
- `mensajeGuia` cuando hay raza+año pero 0 filas de guía.
- Si legacy sin datos → response vacío `(lista, 0,0,0,false)`.

---

## 2. Enfoque arquitectónico (C1 espejo)
1. `fn_indicadores_produccion_postura(p_company_id, p_lote_postura_produccion_id, p_lote_id,
   p_semana_desde, p_semana_hasta, p_fecha_desde, p_fecha_hasta)` → `RETURNS TABLE(...)` con **todas** las
   props de `IndicadorProduccionSemanalDto` en snake_case. PL/pgSQL con `FOR` loop de semanas (espejo del `foreach`).
   - Tipos: enteros/`double precision` para réplica bit-exacta del `decimal` C# en las divisiones;
     conteos de huevos como `integer`. Se usa `double precision` en las operaciones para evitar overflow y
     casar con el orden C#. (El DTO usa `decimal` pero los valores no se redondean → float8 es equivalente.)
2. Resolución de lote/company/encaset/aves/raza/año/**merge de seguimientos** se queda en C#; el servicio
   arma el request y delega el CÁLCULO por semana a la fn pasando el lote resuelto. Para no duplicar la
   resolución en SQL, la fn recibe `p_lote_postura_produccion_id` o `p_lote_id` y **resuelve internamente**
   igual que el C# (misma prioridad), replicando la unión unificado+legacy y el merge por día.
3. `IndicadoresProduccionService` refactorizado: mantiene la resolución de company y el armado de
   `IndicadoresProduccionResponse` (tieneDatosGuia, mensaje); el cálculo de filas viene de `SqlQueryRaw`.
4. Cálculo puro extraíble → `Application/Calculos/IndicadoresProduccionCalculos.cs` (ya existe `ProduccionCalculos`
   con %Producción/HTAA/HIAA/GrAveDia; se añaden helpers si aportan) + tests xUnit.
5. **Equivalencia PRIMERO**: golden del C# actual para P-K345A (LPP 7) y P-K345B (LPP 6) → comparar fn campo a campo.

## 3. Desviaciones preservadas (NO son bugs de guía → se replican)
- `avesHInicioSemana = avesHActuales + mortH + selH` (sobrecuenta el censo de inicio respecto al saldo real
  de arranque de la semana). Es un campo informativo, no afecta la comparación vs guía. **Se replica exacto.**
- `consumoRealH/M` divide por `avesHInicioSemana` (el valor sobrecontado de arriba). **Se replica exacto.**
- Timezone: con `Npgsql.EnableLegacyTimestampBehavior=true` el back lee `timestamptz` como hora **local del
  proceso**; en dev/local el TZ es UTC-5 (= America/Bogota sin DST) → `.Date` = fecha Bogotá. La fn usa
  `AT TIME ZONE 'America/Bogota'` (igual que C1) para el corte de semanas: casa con dev y con el calendario
  del negocio (Colombia). **Normalización documentada.**

## 4. Bugs de comparación vs guía (ya corregidos en el C# actual → replicados, no re-corregidos)
- **REQ-004a** %Producción hen-day usa **solo hembras** en el denominador (no hembras+machos).
- **REQ-004b** Peso de aves normalizado a **kg** (`>100 ? /1000`) para casar con la guía (peso_h/1000).
- **REQ-004c** HTAA/HIAA reales (acumulados por ave alojada) se comparan contra `h_total_aa`/`h_inc_aa`
  (que son acumulados), no contra el promedio de huevos/día.
- **REQ-004d** Mortalidad de guía es **%** (decimal), no entero (no se trunca a 0).
- Guía = tabla real `guia_genetica_sanmarino_colombia` filtrada por company+raza+año (no Ecuador/hardcode).

## 5. Casos de prueba (equivalencia)
- P-K345A (LPP 7, company 1, AP/2026, encaset del levante 1, datos en tabla legacy 301 regs).
- P-K345B (LPP 6, company 1, AP/2026) si tiene datos.
- Comparar TODAS las columnas del DTO con tolerancia epsilon 1e-6 (float). Diferencias esperadas: 0.

## 6. Resultado de equivalencia
**PASA — 0 diferencias.** Metodología:
1. Golden C# actual capturado con un harness (scratchpad) que instancia el `IndicadoresProduccionService`
   ORIGINAL contra la BD local (mismo runtime: `Npgsql.EnableLegacyTimestampBehavior=true`,
   `UseSnakeCaseNamingConvention`), stubs de `ICurrentUser`/`ICompanyResolver` (company 1).
   → P-K345A (LPP 7): 43 semanas (26–68); P-K345B (LPP 6): 43 semanas.
2. Salida de la fn (`SELECT * FROM fn_indicadores_produccion_postura(1, <lpp>)`) exportada a JSON.
3. Comparación campo a campo (script `compare.py`, epsilon relativo **1e-6**) de **los 62 campos** del
   DTO en **las 43 semanas** de ambos lotes: **0 mismatches**.
4. Tras refactorizar el servicio para delegar en la fn (`SqlQueryRaw`), se re-corrió el harness
   (ahora usa la fn) y se comparó contra la salida directa de la fn: **0 mismatches** → el mapeo
   `SqlQueryRaw` snake_case→PascalCase + la conversión `double→decimal` son fieles.

Cadena de equivalencia: **C# original ≡ fn SQL ≡ servicio refactorizado (SqlQueryRaw)**.
Las únicas diferencias observadas son de representación IEEE-754 en el ~13.º dígito significativo
(`decimal` C# vs `double precision` SQL), muy por debajo del epsilon; los valores no se redondean en
ninguna capa (el redondeo lo hace la presentación).

## 7. Archivos y validación
- SQL: `backend/sql/fn_indicadores_produccion_postura.sql` (+ helpers `fn_dif_pct`, `fn_parse_edad_numerica`).
- Migración a mano: `backend/src/ZooSanMarino.Infrastructure/Migrations/20260703120000_AddFnIndicadoresProduccionPostura.cs`
  (+ `.Designer.cs` con `[Migration]` y `BuildTargetModel` = snapshot actual; ModelSnapshot NO tocado).
- DTO fila cruda: `backend/src/ZooSanMarino.Application/DTOs/Produccion/IndicadorProduccionSemanalBdRow.cs`.
- Cálculo puro + mapeo: `backend/src/ZooSanMarino.Application/Calculos/IndicadoresProduccionCalculos.cs`.
- Servicio refactorizado (delega en fn): `backend/src/ZooSanMarino.Infrastructure/Services/IndicadoresProduccionService.cs`
  (727 → ~250 líneas; conserva resolución company/lote y armado de respuesta; cálculo → BD).
- Tests: `backend/tests/ZooSanMarino.Application.Tests/IndicadoresProduccionCalculosTests.cs` (8) +
  `ProduccionCalculosTests.cs` (7).
- Build: **0 errores / 0 warnings**. Tests: **17 pasan / 0 fallan** (1 Domain + 16 Application; 15 nuevos).
- Migración aplicada en BD local (`dotnet ef database update`) OK. Contrato de respuesta
  `IndicadoresProduccionResponse` intacto → front sin cambios.
