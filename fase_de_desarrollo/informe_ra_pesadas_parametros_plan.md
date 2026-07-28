# Plan — Informe RA Pesadas (Parámetros + Gráficos)

**Fuente:** `Requerimiento sanmarino 2026/Informe RA Pesadas Parámetros - Gráficos 2025 v1.xlsb`
**Fecha:** 2026-07-28
**Empresa objetivo:** Agroavícola Sanmarino (company_id = 1) — postura reproductora pesada
**Estado:** VALIDACIÓN CERRADA · implementación pendiente de decisiones D1–D5 (§8)

---

## 1. Qué es el archivo (auditoría de las 10 hojas)

| # | Hoja | Filas × cols | Qué es | ¿Existe en la app? |
|---|---|---|---|---|
| 1 | **RESUMEN SEMANAL** | 80 × 23 | Tablero ejecutivo: **una fila por lote** de TODA la operación en **una semana calendario**. Dos bloques: Levante (edad 1-25) y Producción AP (edad 26-70). Filtros `Años(Fecha)`, `SemAño`, `TRASLADO` (lev) / `CICLO` (prod). | ❌ **NO EXISTE** — es la pieza nueva de verdad |
| 2 | **Datos semanal LEV** | 1.825 × 135 | Base cruda: 1 fila por (lote, semana de edad). 73 lotes. Real + guía + derivadas + bonificación. | ✅ ~85 % (`Reporte Técnico Semanal → Levante`) |
| 3 | **Gráf LEV Hembras** | 89 × 27 | 6 gráficas + **encabezado consolidado multi-lote** (`Resumen Hembras a semana 25`: HEMBRA INI 1.129.682, Peso 3.256, ConsAcum 11.505, Unif 82,05, RetiroAc 5,36). | ⚠️ Gráficas sí (por lote); el consolidado multi-lote NO |
| 4 | **Gráf LEV Machos** | 70 × 32 | Idem machos + `%RelM/H` vs guía. | ⚠️ igual |
| 5 | **ALIMLev** | 117 × 18 | Energía y proteína **agregadas por FASE de alimento** (INI/LEV/PP/F1 · H, INI/LEV/M · M) real vs guía + series por edad. | ❌ NO EXISTE |
| 6 | **Datos semanal PROD** | 2.960 × 146 | Base cruda producción: 1 fila por (lote, semana). 70 lotes. | ✅ ~80 % (`Reporte Técnico Semanal → Producción`) |
| 7 | **Gráf Producción** | 91 × 44 | 6 gráficas + consolidado multi-lote a semana 64. | ⚠️ igual que 3/4 |
| 8 | **CLAS Huevo** | 115 × 17 | % de clasificación por semana: Sucio, Deforme Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, %Limpio, %Tratado, %AprovSem. | ❌ NO EXISTE como reporte (los datos SÍ se capturan) |
| 9 | **Guías RAP** | 876 × 45 | La guía genética (tabla maestra, NO un reporte). | ✅ `guia_genetica_sanmarino_colombia` |
| 10 | **AUX** | 11 × 7 | Catálogo de alimento 2026 (ALIMENTO, TIPO, ENERGÍA, PROTEÍNA, rangos de semana). | ❌ NO EXISTE (ver §5) |

**Granularidad verificada:** en LEV y PROD la clave `(lote, edad)` es **única** (0 duplicados en 1.825 y 2.960 filas) ⇒ el Excel trabaja a nivel **lote**, ya consolidado, no por galpón. La columna `GRANJA` del Excel es en realidad **granja + núcleo** (`Niza 3 mod 1`, `mod 4`, `mod 5` = granja `NIZA III` + núcleos `Modulo I…IV` en la app).

---

## 2. Decisión: **UN módulo con tabs**, extendiendo el existente

**No** son varios reportes sueltos, y **tampoco** un módulo nuevo desde cero.

Las hojas 2/3/4/6/7 ya están implementadas al 80-85 % en `reporte-tecnico-semanal` (front `frontend/src/app/features/reporte-tecnico-semanal/`, back `Infrastructure/Services/ReporteTecnicoSemanal/`). Crear un módulo paralelo duplicaría la fn SQL de extras, la spec de columnas, el parseo de guía y las gráficas. La ruta correcta es **extender ese módulo** y renombrar la etiqueta de menú a **«Informe RA Pesadas»**.

Estructura de tabs propuesta (2 niveles, porque conviven **dos granularidades distintas**):

```
Informe RA Pesadas
├── Modo RESUMEN            (todos los lotes × 1 semana calendario)   ← NUEVO
│   ├── Levante             (hoja 1, bloque superior)
│   └── Producción          (hoja 1, bloque inferior)
└── Modo DETALLE DE LOTE    (1 lote base × todas las semanas)         ← EXISTE
    ├── Levante             tabla/gráficas por galpón + consolidado   (hojas 2,3,4)
    ├── Producción          idem                                       (hojas 6,7)
    ├── Alimento por fase   energía/proteína INI/LEV/PP/F1 vs guía     (hoja 5)  ← NUEVO
    └── Clasificación huevo % por semana + gráfica                     (hoja 8)  ← NUEVO
```

**Por qué dos modos y no 6 tabs planos:** el Resumen filtra por *año + semana del año* y devuelve N lotes; el Detalle filtra por *lote base* y devuelve N semanas. Una sola barra de filtros para ambos confunde y obliga a recargar de más. Los dos modos comparten el mismo menú, el mismo servicio HTTP y el mismo exportador Excel multi-hoja.

**Hojas 9 y 10 NO son tabs.** `Guías RAP` es la tabla maestra que la app ya tiene (`Configuración → Guía Genética`); `AUX` es catálogo de alimento (ver §5). Replicarlas como "reporte" sería congelar datos maestros dentro de un reporte.

---

## 3. La guía genética: qué pasa y qué se hace

**Confirmado por consulta a BD (`guia_genetica_sanmarino_colombia`, company_id = 1):**

| Guías EN LA APP | Guías EN EL EXCEL |
|---|---|
| 2021 (AP, C500, R308), 2022 (AP, C500), 2023 (AP, C500), **2026 (AP 77 filas edad 1-76, APN 75, C500 72)**, G21 (AP, C500) | 2023, **2024**, **2025**, **2025EC**, G21 (AP/C500/APN) + guías por lote reciclado: `A289R`, `K291R`, `A299R`, `K307R`, `K309R` (edades 65-97) |

Y `backend/sql/alinear_ano_genetico_postura_colombia_2023_2026.sql` ya movió los lotes de Sanmarino AP de `ano_tabla_genetica` 2023 → **2026**.

**Regla del reporte (no negociable):** la columna «Guía» se resuelve SIEMPRE por `lote.raza + lote.ano_tabla_genetica + company_id` contra `guia_genetica_sanmarino_colombia`, exactamente como ya lo hace `ReporteTecnicoSemanalService.CargarGuiaPorSemanaAsync`. **Nada de la hoja `Guías RAP` se importa.**

⚠️ **Consecuencia esperada y correcta:** las columnas `*GUIA` del reporte nuevo **no van a coincidir** con el Excel v1, porque el Excel compara contra 2024/2025 y la app contra 2026. Eso no es un bug: es el objetivo («que estén alineados»). La validación de aceptación se hace contra la guía 2026, no contra los números del xlsb.

**Completitud de la guía 2026 AP verificada (77 filas):** mort_sem_h 77/77 · apareo 77 · alim_h 77 · kcal_sem_h 77 · uniformidad 25 (solo levante ✔) · masa_huevo 52 y nacim_% / pollito_aa 51 (solo producción ✔). Cubre todo lo que piden las hojas 1-8, **salvo tres huecos** (§4).

---

## 4. Huecos reales detectados (lo que NO se puede replicar tal cual)

| # | Hueco | Evidencia | Impacto | Salida propuesta |
|---|---|---|---|---|
| H1 | **Sin guía para lotes reciclados / 2.º ciclo más allá de la semana 76** | El Excel trae 5 guías `*R` (edades 65-97); la 2026 AP llega a 76. En PROD el Excel tiene `CICLO` = 1 / 2 / D | Lotes de 2.º ciclo mostrarían guía vacía > sem 76 | **D1**: cargar guías de reciclaje por lote, o extender 2026 AP, o aceptar guía vacía |
| H2 | **`%Grasa` de la guía vacía** (0/77 filas) | `grasa_porcentaje` NULL en 2026 | Columna vacía | No usarla (tampoco hay dato real — ver H3) |
| H3 | **Pechuga / Grasa / Fertilidad no se capturan** | No existe ninguna columna `pechuga*`/`grasa*`/`fertil*` en `seguimiento_diario_*`. **Y en el propio Excel: GrasaH 0 %, PechugaM 0 %, Fertilidad 0 %, Otro 0 %, InfertilidadBon 0 % de llenado** | Ninguno si se excluyen | **Excluir del alcance** — son columnas muertas también en el Excel |
| H4 | **Bonificación sin origen** | 12 columnas `*Bon` llenas al 100 % (`PesoBonH`, `RetiroBonH`, `UnifHBon`…) y bandas `DifPesoHMinB/MaxB` que varían por semana. Ni las bandas ni los objetivos `PechugaHBon/GrasaHBon/PechugaMBon` existen en `guia_genetica_sanmarino_colombia` | Bloque completo no replicable | **D2**: fuera de alcance en fase 1, o modelar `guia_bonificacion` (semana → banda mín/máx por indicador) |
| H5 | **Nutrición de MACHOS sin dato real** | `seguimiento_diario_levante` tiene `kcal_al_h`/`prot_al_h` pero **no** el equivalente macho (ya documentado en `fn_reporte_semanal_levante_extras.sql`) | Media hoja ALIMLev sin lado «real» | Ver §5 — derivar del catálogo de alimento |
| H6 | **Regional `ECUADOR` no existe en la app** | `master_lists.region_option_key` de Sanmarino = Oriente / Occidente / Centro / Abuelas / División Pollita. Las granjas `PIMAN` (regional_id 5 → huérfano) y `PARAISO` (regional_id 59 = *Occidente*) están mal clasificadas; el Excel las agrupa en `ECUADOR` (300 filas LEV + 451 PROD) | El filtro Regional del Resumen agruparía mal ~15 % de los lotes | **D3**: crear la opción `Ecuador` y reasignar esas granjas (dato, no schema) |
| H7 | **Nacimientos / pollitos reales** | Ya auditado y documentado en `reporte_tecnico_semanal_postura_plan.md` §9: no hay tabla de retorno de incubadora | El bloque Pollitos queda con guía + «HI Cargado» real | Sin cambio — se mantiene como está |
| H8 | **`VentaH`/`VentaM` de producción** | Columnas del Excel llenas solo al 3 % · en la app las salidas van por `traslado_salida_*` / movimientos de aves | Bajo | **D4**: mapear a movimientos de aves o excluir |

---

## 5. Hoja AUX (catálogo de alimento) — lo que habilita

`AUX` define, para **2026**: INICIO 2900 kcal / 19 % (sem 1-6), LEVANTE 2750 / 13 (H sem 7-19, M sem 7-24), PREPOSTURA 2870 / 13 (sem 20-24), FASE I 2930 / 13,5, FASE II 2930 / 13, FASE III 2930 / 12,5, MACHO 2900 / 10 (sem 25-final).

En la app `catalogo_items.metadata` guarda `{raza, genero, type_item}` — **sin energía ni proteína**. Agregar `energia_kcal` y `proteina_pct` al metadata del ítem de alimento (aditivo, sin migración de schema) resuelve H5 y permite calcular la nutrición real de machos por `tipo_alimento × consumo`, cerrando la hoja ALIMLev completa.

---

## 6. Arquitectura

### 6.1 Backend — BD

**Nueva fn SQL `fn_resumen_semanal_ra_pesadas`** (`backend/sql/`), aplicada por migración EF idempotente.

```
fn_resumen_semanal_ra_pesadas(
  p_company_id  integer,
  p_anio        integer,
  p_sem_anio    integer,          -- semana ISO del año (America/Bogota)
  p_etapa       text,             -- 'levante' | 'produccion'
  p_regional_ids integer[] DEFAULT NULL,
  p_granja_ids   integer[] DEFAULT NULL,
  p_ciclo        text     DEFAULT NULL,   -- solo producción
  p_traslado     integer  DEFAULT NULL    -- solo levante
) RETURNS TABLE (una fila por lote)
```

⚠️ **Regla de CLAUDE.md — la BD filtra, el backend orquesta.** Está **prohibido** iterar los ~70 lotes llamando `fn_indicadores_*_postura(lote)` desde C#: son fns por-lote y el Resumen es multi-lote. Eso cuelga el endpoint (mismo patrón que ya rompió los endpoints multipaís). La fn nueva resuelve el join lote × semana × guía **en una sola consulta**, reusando las mismas fórmulas de semana (`floor((fecha_bogota - encaset)/7)+1`), guards (encaset NULL/futuro ⇒ sin filas) y saldo por sexo que las fns existentes.

Columnas de salida = las 21 del bloque Levante + las 23 del bloque Producción de la hoja 1, incluida `part` (participación del lote sobre el total de aves de la selección — el Excel la usa para los ponderados de las hojas 3/4/7).

### 6.2 Backend — C#

```
Application/DTOs/ReporteTecnicoSemanal/          + ResumenSemanalRaPesadasDto, AlimentoPorFaseDto, ClasificacionHuevoSemanalDto
Application/Calculos/ReporteTecnicoSemanalCalculos.cs      (existente — agregar derivadas del resumen)
Application/Calculos/AlimentoPorFaseCalculos.cs            (NUEVO, puro: agrupa semanas → fase leyendo alim_h/alim_m de la guía)
Infrastructure/Services/ReporteTecnicoSemanal/
  ├── ReporteTecnicoSemanalService.cs                       (ancla — sin cambios de ctor)
  └── Funciones/
      ├── ReporteTecnicoSemanalService.Levante.cs           (existente)
      ├── ReporteTecnicoSemanalService.Produccion.cs        (existente)
      ├── ReporteTecnicoSemanalService.Resumen.cs           (NUEVO partial)
      ├── ReporteTecnicoSemanalService.AlimentoFase.cs      (NUEVO partial)
      └── ReporteTecnicoSemanalService.ClasificacionHuevo.cs(NUEVO partial)
API/Controllers/ReporteTecnicoSemanalController.cs
  + POST api/ReporteTecnicoSemanal/resumen
  + POST api/ReporteTecnicoSemanal/alimento-fase
  + POST api/ReporteTecnicoSemanal/clasificacion-huevo
```

Namespace **plano** `ZooSanMarino.Infrastructure.Services`; interfaz `IReporteTecnicoSemanalService` solo en el ancla. Empresa efectiva por `ResolveCompanyIdAsync()` ya existente (fail-closed) + `ILocationScopeResolver` para el alcance granular usuario-granja.

**Clasificación de huevo:** reusar `fn_clasificacion_huevo_items_produccion` cuando la empresa tenga `clasificacion_huevo_por_items` = true; con el flag OFF, agregar las columnas fijas (`huevo_limpio…huevo_otro`) de `seguimiento_diario_produccion`. Nota de mapeo: el Excel tiene **una** columna «Deforme Blanco» y la BD tiene **dos** (`huevo_deforme`, `huevo_blanco`) → el reporte suma ambas y lo documenta en el encabezado.

### 6.3 Frontend

```
features/reporte-tecnico-semanal/
├── models/reporte-tecnico-semanal.model.ts          (+ tipos del resumen / fase / clasificación)
├── funciones/
│   ├── columnas-reporte-semanal.funcion.ts          (existente — 2 specs nuevas)
│   ├── columnas-resumen-ra-pesadas.funcion.ts       (NUEVO — specs Levante y Producción de la hoja 1)
│   ├── construir-graficas-reporte-semanal.funcion.ts(existente — + gráficas de fase y clasificación)
│   └── construir-aoa-reporte-semanal.funcion.ts     (existente — + hojas nuevas al Excel)
├── pages/reporte-tecnico-semanal-main/              (orquestador: agrega selector de modo)
└── services/reporte-tecnico-semanal.service.ts      (+ 3 métodos)
```

- Funciones **puras** en `funciones/` (sin `this`, sin DI) — patrón `movimientos-pollo-engorde`.
- `changeDetection: ChangeDetectionStrategy.Eager` **explícito** en todo componente nuevo (Angular 22: omitirlo = OnPush ⇒ spinner colgado).
- Export a `.xlsx` con `exportarAoaMultiHojaExcel` de `shared/utils/excel/` — **una hoja por tab**, replicando el orden del archivo original.
- Filtros con el diseño unificado «Selección de contexto» (`styles/filter-context.scss`).
- Toasts vía `ToastService`; confirmaciones vía `ConfirmDialogService`. Nada de `alert()`/`confirm()`.

### 6.4 Menú

El ítem `/reporte-tecnico-semanal` ya está sembrado solo para Sanmarino (migración `20260726160100`, `company_menus` + `role_menus`). Solo cambia la **etiqueta** a «Informe RA Pesadas» por migración idempotente (`UPDATE ... WHERE route='/reporte-tecnico-semanal'`). **No** crear ítems nuevos ni ids fijos.

---

## 7. Reglas de negocio

1. **Semana del año** = ISO week en `America/Bogota`, coherente con `date_trunc`/`FechasPuras.RangoDiaUtc`. Nunca `.Date ==` sobre `timestamptz`.
2. **Guía** = `lote.raza + lote.ano_tabla_genetica + company_id`. Guía ausente ⇒ celda vacía, **nunca** fallback a otro año.
3. **Empresa efectiva** por dato (`farms.company_id` / `X-Active-Company`), fail-closed. Sin empresa ⇒ vacío, jamás datos de otra empresa.
4. **Alcance de granjas** por `ILocationScopeResolver` (restrict_locations + user_farm_scopes).
5. **`PART`** = aves iniciales del lote ÷ total de aves de la selección; los consolidados de las hojas 3/4/7 son **promedio ponderado por aves iniciales**, no promedio simple. (El consolidado por galpón del Detalle sigue siendo suma de conteos + promedio simple de pesos, como hoy — no se toca.)
6. **Ubicación** en el Resumen = `granja + núcleo` (para reproducir `Niza 3 mod 1`).
7. Refactor ≠ cambio de comportamiento: los tabs Levante/Producción existentes conservan columnas, redondeos y consolidación actuales.

---

## 8. Decisiones pendientes del usuario

| ID | Decisión | Opciones |
|---|---|---|
| **D1** | Guía para lotes reciclados / 2.º ciclo > semana 76 | (a) cargar las 5 guías `*R` del Excel · (b) extender 2026 AP · (c) dejar guía vacía |
| **D2** | Bloque de **bonificación** (12 columnas) | (a) fuera de alcance fase 1 *(recomendado)* · (b) modelar `guia_bonificacion` con bandas por semana |
| **D3** | Regional **ECUADOR** | (a) crear opción + reasignar `PIMAN`, `PARAISO`, `SACACHUM`, `AZUCAR` *(recomendado)* · (b) dejarlas como están |
| **D4** | `VentaH`/`VentaM` en producción | (a) excluir *(3 % de llenado)* · (b) mapear a movimientos de aves |
| **D5** | Energía/proteína por alimento (hoja AUX) | (a) agregar `energia_kcal`/`proteina_pct` al metadata de `catalogo_items` y cerrar ALIMLev completo *(recomendado)* · (b) ALIMLev solo hembras |

---

## 9. Casos de prueba

**Puros (xUnit, `tests/ZooSanMarino.Application.Tests/`) — obligatorios antes de mergear:**
- `ResumenSemanalRaPesadasCalculosTests`: `PART` suma 1,000 ± tolerancia · ponderado por aves iniciales vs promedio simple · lote sin guía ⇒ celdas null (no 0) · lote con encaset futuro ⇒ excluido · semana sin registros ⇒ fila ausente, no fila en cero.
- `AlimentoPorFaseCalculosTests`: agrupación semana→fase leyendo `alim_h`/`alim_m` de la guía · cambio de fase a mitad de semana · fase sin consumo ⇒ 0 y no división por cero · `%DIF` con guía 0 ⇒ null.
- `ClasificacionHuevoCalculosTests`: `Deforme Blanco` = `huevo_deforme + huevo_blanco` · semana sin huevos ⇒ null, no 0/0 · `%AprovSem` = incubables/total.
- **Regresión**: los tabs Levante y Producción existentes deben dar resultado **byte a byte idéntico** al de hoy (los 30 tests actuales de `ReporteTecnicoSemanalCalculos` siguen verdes).

**Integración / smoke:**
- `POST api/ReporteTecnicoSemanal/resumen` con Sanmarino en una semana con datos: nº de filas = nº de lotes vivos esa semana; comparar 3 lotes contra el cálculo manual sobre `seguimiento_diario_*`.
- Mismo endpoint con empresa **Demo** ⇒ solo lotes de Demo (no fuga cross-empresa).
- Usuario con `restrict_locations` ⇒ solo sus granjas.
- Semana futura / empresa sin postura ⇒ 200 con lista vacía, sin error.
- Front: doble apertura del modo Resumen (verifica `changeDetection`), export Excel multi-hoja abre en Excel con las hojas en el orden del original.

**Validación:** `dotnet build` 0 errores · `dotnet test` verde · `yarn build` (solo el warning preexistente de bundle budget).

---

## 10. Fuera de alcance (explícito)

- Importar la hoja `Guías RAP` como datos (la app manda con 2026).
- Pechuga, Grasa, Fertilidad, `Otro`, `InfertilidadBon` — sin captura y vacías en el propio Excel.
- Nacimientos y pollitos reales (H7) — requiere capturar el retorno de incubadora; decisión de negocio ya documentada.
- Bonificación, salvo que D2 = (b).
- Cualquier reporte cross-empresa: el Resumen es **por empresa activa**.
