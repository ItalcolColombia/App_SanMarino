# Santa Reyes — Requerimientos de Italapp (plan de implementación)

> **Origen.** Dos archivos entregados por el cliente el 18-ago-2026:
> `Requerimientos de Italapp.docx` (7 módulos, 10 pantallas anotadas) y `Guías Genéticas.xlsx`
> (5 líneas × 108 semanas).
>
> **Entregables comerciales generados** (para presentar al cliente, fuera del repo):
> `~/Desktop/Plan_de_Trabajo_Santa_Reyes.xlsx` y `~/Desktop/Plan_de_Trabajo_Santa_Reyes.docx`.
>
> ⚠️ **Los dos documentos comerciales presentan TODO el alcance como trabajo por ejecutar**, por
> decisión explícita del usuario. Este archivo, en cambio, dice la verdad técnica: qué base ya
> existe en el repo y qué falta de verdad. Los tiempos del cronograma comercial son cortos
> **porque** esa base existe; no se declara así hacia afuera.

**Cronograma comercial (v2.0, vigente):** **100 horas en 10 jornadas de 10 h** ·
mié 19-ago-2026 → **mar 1-sep-2026** · 1 dev full-stack · 4 hitos · 12 paquetes · 29 actividades.
Cada jornada resuelve **varias actividades** y suma exactamente 10 h.

> La v1 (34 días hábiles a 1 actividad por día, entrega 5-oct) quedó descartada: el usuario confirmó
> que sostiene jornadas de 10 h. El acompañamiento post-entrega (semana del 2 al 8-sep) quedó
> **fuera** de las 10 jornadas, declarado bajo demanda.
>
> ⚠️ **El riesgo Alto #1 del plan comprimido** es que la estructura física de granjas y los códigos
> ERP no lleguen antes del 19-ago: la actividad F1.2 corre el **día 1** y no hay holgura.

---

## 1. Enfoque arquitectónico

Todo el alcance es **comportamiento distinto para UNA empresa** ⇒ aplica el patrón obligatorio de
`CLAUDE.md` §🏢:

1. La señal vive como **columna tipada en `companies`**, nombrada por el comportamiento
   (`consumo_alimento_solo_hembras`), **nunca** por el tenant.
2. La decisión es **lógica pura** en `Application/Calculos/<Feature>Calculos.cs` + tests xUnit
   obligatorios: con flag OFF el comportamiento previo debe quedar **byte a byte idéntico**.
3. Empresa efectiva **siempre por datos, fail-closed** (`farms.company_id` de la granja del lote).
4. El flag viaja en `CompanyDto` — agregarlo en **TODAS** las proyecciones: `CompanyService.ToDto`,
   `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService` — y el front lo lee con
   `core/services/company-config/active-company-config.service.ts` (fail-closed → `false`).
5. Seeds de la empresa = migración EF **data-only**, idempotente.

---

## 2. Estado real del código (auditoría 18-ago-2026)

Esto es lo que hace que las estimaciones sean cortas. **No exponer al cliente.**

| Requerimiento | Qué ya existe | Qué falta de verdad |
|---|---|---|
| **Silo como estructura física** | `SiloCatalogo`, `FarmSilo`, `GalponSilo`, `LoteSilo` + controllers + `SiloCalculos`/`ConsumoSiloCalculos`/`InventarioUbicacionSiloCalculos`; flag `ManejaInventarioPorSilo`; migraciones `2026081222…`→`20260813210000` (fases B/C/D) | Exponer el silo en el **form de ingreso a granja** (hoy vive en inventario). Carga de la estructura real |
| **Códigos ERP por nivel** | Flag `ManejaCodigosErpAvicola`; `CodigoErp` en `LotePosturaBase`; ERP engorde por granja (Panamá) | Homologación explícita CO/bodega/ubicación/centro de costo en granja, núcleo y silo |
| **Guías genéticas** | `ProduccionAvicolaRaw` (tabla `guia_genetica`) con `Raza`, `Edad`, `ProdPorcentaje`, `GrAveDiaH`, `RetiroAcH`; `GuiaGeneticaEcuadorHeader/Detalle`; `GuiaGeneticaRequisitoCalculos`; seed Panamá Ross 308 como molde | Seed de las **5 líneas de postura** (540 filas). Mapear `% Mort Acum.` → `RetiroAcH`. Asociar línea ↔ lote en postura |
| **Semanas por raza** | Etapa autocalculada en `modal-seguimiento-diario.component.ts:1463` — **hardcodeada 26-33 / 34-50 / >50** | Parametrizar por raza: 8+16 levante; 4+74 (rojas/criollas) o 4+84 (blancas/Azur) |
| **Consumo solo hembras** | Ítems por género en `SeguimientoItemDto`; alimento macho ya separado | Flag nuevo + ocultar bloque «Machos» en producción y levante |
| **Error de sexaje** | `ErrorSexaje` en ~20 DTOs/cálculos (`SaldoAvesLevanteCalculos`, `DescuentoAvesSeguimientoCalculos`, …) | **Ocultar en UI, NO borrar del modelo** — lo consumen saldos e históricos de otras empresas |
| **Clasificación de huevo por ítems** | Flag `ClasificacionHuevoPorItems` + `HuevoItemsCalculos` (metadata `huevoItems`) + `fn_clasificacion_huevo_items_produccion`; conserva `huevo_tot` legacy | Los 7 ítems «sin clasificar», primera postura por raza, y la **regla de vigencia ≤ sem 22** |
| **Renombrar incubables → sin clasificar** | — | Etiquetas en formularios, tablas, panel de eficiencia y reportes |
| **PNC renombrados** | 11 columnas fijas (Sucio/Deforme/Blanco/Doble Yema/Piso/Pequeño/Roto/Desecho/Otro) | Mapear a Manchado/Decolorado/Enyemado/Picado/Fárfara **por catálogo**, sin tocar las columnas físicas |
| **Traslado de aves — transporte** | `MovimientoAves` **ya tiene** `Placa`, `Conductor`, `Sellos`, `GuiaAgrocalidad`, `HoraSalida` | Exponerlos en el modal de postura (hoy solo engorde). «Precinto» ⇒ reusar `Sellos` |
| **Traslado de huevos — bodega salida** | `traslado-huevos-form` con `PlantaDestino` **digitado** (`string?`) | Lista desplegable acotada a los destinos de la granja |
| **Tipos de inventario** | `CatalogItem` con tipos abiertos | Acotar a `alimento` / `aves` para la empresa |

**Anti-patrón a evitar:** `AutoNombrePorCorrida` (el front decide y el back obedece). Acá la
decisión la toma el backend desde el flag.

---

## 3. Archivos a crear o modificar

**Backend**
- `Domain/Entities/Company.cs` — flags nuevos (`ConsumoAlimentoSoloHembras`,
  `OcultaMachosEnPostura`, `HuevoPrimeraPosturaHastaSemana`, `SiloEnEstructuraGranja`, …).
- `Application/Calculos/` — `SemanasCicloPosturaCalculos.cs`, `HuevoPrimeraPosturaCalculos.cs`,
  `CatalogoHuevoSantaReyesCalculos.cs` (puros, sin EF).
- `Infrastructure/Migrations/` — flags (idempotente), seed de los 5 guías genéticas (data-only),
  seed del catálogo de ítems de huevo.
- Proyecciones de `CompanyDto` (**las 4**, ver §1.4).

**Frontend**
- `features/lote-produccion/pages/modal-seguimiento-diario/` — etapa parametrizada, ocultar machos,
  ítems de huevo, retirar tratado/peso/tipo de alimento.
- `features/lote-levante/` — ocultar machos.
- `features/traslados-aves/` — ocultar machos + campos de transporte.
- `features/traslados-huevos/pages/traslado-huevos-form/` — bodega destino desplegable + tipos.
- `core/services/company-config/active-company-config.service.ts` — leer los flags nuevos.

⚠️ **Todo componente/modal nuevo lleva `changeDetection: ChangeDetectionStrategy.Eager` explícito**
(CLAUDE.md §Change detection — en v22 omitirlo = OnPush = modal colgado en «Cargando…»).

---

## 4. Casos de prueba (xUnit — gate de CI)

1. **Flag OFF ⇒ idéntico.** Para cada cálculo nuevo, con el flag apagado la salida es byte a byte
   la de hoy, **mensajes de error incluidos**.
2. **Semanas por raza.** Roja sem 22 → primera postura habilitada; sem 23 → deshabilitada. Blanca
   sem 88 → dentro del ciclo (84+4); sem 89 → fuera.
3. **Suma de huevos = total de granja.** La suma de los ítems debe igualar `huevo_tot`; con desglose
   `[]` los totales vuelven a los campos sueltos (contrato ya cubierto por `HuevoItemsCalculos`).
4. **Consumo solo hembras.** Con el flag ON, un payload que traiga consumo de machos se rechaza o se
   ignora de forma explícita — no se persiste en silencio.
5. **Fail-closed de empresa.** Granja sin `company_id` resoluble ⇒ vacío/error, nunca datos de otra
   empresa.
6. **No regresión multipaís.** Sanmarino, Panamá y Ecuador sin cambios. Si se toca cualquier
   `*SaldoAlimento*` o `fn_seguimiento_diario_*`, corre el **gate multipaís**
   (`backend/sql/verificar_paridad_saldo_engorde.sql`, antes y después: 0 en toda empresa ajena).

---

## 5. Riesgos técnicos

- **`ErrorSexaje` está entretejido en saldos.** Ocultarlo en UI es seguro; quitarlo del modelo
  rompería `SaldoAvesLevanteCalculos` y los históricos de otras empresas. **No borrar.**
- **Las 11 columnas fijas de PNC son físicas** en `seguimiento_diario_produccion`. El renombre va
  por catálogo/etiqueta; tocar las columnas rompe espejos, triggers e indicadores.
- **`huevo_tot` legacy debe conservarse** aunque el desglose viva en `metadata->huevoItems`
  (CLAUDE.md §🏢.6).
- **Orden de migraciones:** cualquier `UPDATE companies … WHERE name='Santa Reyes'` debe quedar
  **después** de `20260725190000_SeedEmpresaSantaReyes`.
