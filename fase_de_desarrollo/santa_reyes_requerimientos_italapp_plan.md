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

---

## 6. F3 — Semanas de producción por raza (diseño técnico, 20-ago-2026)

**Texto literal del requerimiento** (`~/Downloads/Requerimientos de Italapp.docx`, extraído con
python-docx porque `pandoc`/`soffice` no están en PATH local — sección **"Consumo de alimento"**,
justo antes del requerimiento de F4):

> Se tienen que configurar las semanas de producción de la siguiente manera:
> **Aves Rojas y criollas:** Levante: Desde la creación del Item, son 8 sem en alistamiento, y 16
> semanas en etapa de levante. Producción: 4 semanas en etapa levante pero en granjas de producción
> y 74 semanas en etapa de postura.
> **Aves Blancas y Azur:** Levante: ídem (8+16). Producción: 4 semanas en etapa levante pero en
> granjas de producción y **84** semanas en etapa de postura.

**Por qué se auditó el .docx en vez de confiar en la fila de la tabla §2:** la fila comprimía el
requisito a una línea y el caso de prueba #2 (§4) quedaba ambiguo (¿"semana 88" es edad global del
ave o semana relativa a producción?). El documento fuente confirma **"desde la creación del Item"**
→ **edad global del ave desde encasetamiento**, el mismo contador que ya usa TODO el sistema hoy
(`FaseLoteCalculos`, la guía genética por `Edad`, y el propio `calcularEtapa` del modal). El caso de
prueba #2 se reescribe con límites propios más abajo, verificados contra el texto fuente.

### 6.1 Alcance real (auditado en código, no solo grep)

Existen **dos conceptos distintos** que un grep por "hardcodeada 26-33" puede confundir:

1. **`FaseLoteCalculos.SemanasParaProduccion = 26`** (backend) — umbral que sólo se usa para
   clasificar la `Fase` (`Levante`/`Produccion`) de un lote **al crearlo/editarlo** (útil para carga
   masiva con encaset viejo) y para filtrar reportes de levante. El paso REAL levante→producción es
   una acción manual del operador (crea fila en `lote_postura_produccion`), no depende de este
   cálculo en el día a día. **No se toca**: no está en el alcance literal del requerimiento (que
   habla de "etapas" para consumo de alimento, no de cuándo migrar de tabla) y tocarlo arriesga un
   cálculo compartido con Sanmarino/Panamá/Ecuador para un beneficio que el cliente no pidió.
2. **El campo `Etapa` (1/2/3) del modal de producción** (`calcularEtapa`/`getEtapaLabel`,
   `modal-seguimiento-diario.component.ts:1454-1487`, espejado en backend por
   `MovimientoAvesCalculos.EtapaProduccion` sólo para migración de histórico) — **esto sí es el
   mismo tipo de dato que pide el cliente**: una etapa por semana de vida del ave, hoy con cortes
   26-33/34-50/>50 sin relación con raza. Se persiste tal cual en
   `seguimiento_diario_produccion.etapa` como dato informativo/exportable («Fase / Etapa» en
   `ExportacionExcelService`); ningún cálculo de saldo lo consume aritméticamente → cambiar su
   semántica **por empresa** es seguro.
3. **El módulo de levante NO tiene ningún campo "etapa" hoy** (grep sin resultados en
   `features/lote-levante`). F3.1 (alistamiento/levante) es aditivo ahí, no un refactor.

**Confirmado en código, no supuesto:**
- `LotePosturaLevante.Raza` y `LotePosturaProduccion.Raza` existen como campo propio (denormalizado,
  no requiere join a `LotePosturaBase`) — ya lo llenan con literal "BABCOK BROWN"/"LOHMANN LSL", etc.
- `LoteDto.raza` (frontend, `lote.service.ts`) ya viaja al componente padre
  (`lote-produccion-list.component.ts: selectedLote`) — el modal no lo recibe todavía (no está en la
  lista de `@Input()`), pero el dato ya está un nivel arriba, sólo falta pasarlo.
- Las 5 líneas sembradas en F2.1 (`guia_genetica_santa_reyes`) son exactamente las que el
  requerimiento agrupa: **Rojas y criollas** = Babcock Brown, Hy Line Brown, Criolla · **Blancas y
  Azur** = Lohmann LSL, Azur.
- `Company.HuevoPrimeraPosturaHastaSemana` (F0.1) ya documenta el mismo contador ("última semana de
  **vida del lote**") → refuerza que "semana" en todo Santa Reyes es edad global, consistente con
  este diseño.

### 6.2 Cálculo puro — límites (edad en semanas desde encasetamiento, iguales para ambos grupos
salvo el final de postura)

| Etapa | Semanas (edad) | Duración |
|---|---|---|
| Alistamiento | 1–8 | 8 |
| Levante | 9–24 | 16 |
| Levante en granja de producción | 25–28 | 4 |
| Postura (rojas/criollas) | 29–102 | 74 |
| Postura (blancas/Azur) | 29–112 | 84 |
| Fuera de ciclo | >102 (rojas/criollas) / >112 (blancas/Azur) | — |

`Application/Calculos/SemanasCicloPosturaCalculos.cs` (estático, puro, sin EF):
- `EsGrupoBlancaAzur(string? raza)`: match case-insensitive contra los 5 literales conocidos de la
  guía genética (`LOHMANN`, `AZUR` ⇒ true; `BABCOCK`/`BABCOK`, `HY LINE`, `CRIOLLA` ⇒ false).
  Raza `null`/no reconocida ⇒ `null` (etapa indeterminada, **no** adivinar grupo — el caller muestra
  «—» en vez de una etapa potencialmente incorrecta).
- `ObtenerEtapa(string? raza, int semanasDesdeEncaset) → string?` (constantes tipo
  `FaseLoteCalculos`: `Alistamiento`/`Levante`/`LevanteEnProduccion`/`Postura`/`FueraDeCiclo`), `null`
  si raza no reconocida o semanas < 1.
- Espejo TypeScript puro en `features/lote-produccion/funciones/semanas-ciclo-postura.funcion.ts`
  (mismos cortes, literal) — igual patrón que el resto de `funciones/`: el modal ya calcula `etapa`
  100% en el cliente antes de guardar (no hay round-trip al backend para esto), así que necesita su
  propia copia igual que hoy tiene su propio `calcularEtapa`.

**Gate flag OFF:** con `Company.SemanasCicloPosturaPorRaza = false` (default en TODAS las empresas
incluida Santa Reyes hasta este commit), `calcularEtapa`/`getEtapaLabel`/`EtapaProduccion` quedan
byte a byte iguales — el nuevo cálculo ni se llama.

### 6.3 Flag nuevo

`Company.SemanasCicloPosturaPorRaza` (bool, default `false`) — mismo patrón de 8 capas que
`ConsumoAlimentoSoloHembras` (F0.1): entidad, `CompanyDto`/`CreateCompanyDto`/`UpdateCompanyDto`,
`CompanyConfiguration`, `CompanyService.ToDto` + `.Crud`, `CompanyResolver` (x2), `CompanyPaisService`,
+ registrar en `flags-empresa.funcion.ts` (catálogo admin) y en
`active-company-config.service.ts` (front, caché 5 min, fail-closed). Migración idempotente
data-only para encenderlo en Santa Reyes, con timestamp posterior a
`20260820093323_SeedGuiaGeneticaSantaReyes`.

### 6.4 Dónde se usa

- **Modal producción** (`modal-seguimiento-diario.component.ts`): nuevo `@Input() raza`, el padre
  (`lote-produccion-list.component.html`) lo pasa como `[raza]="selectedLote?.raza || null"` (mismo
  patrón que `fechaEncaset`). Con el flag ON y raza reconocida, `getEtapaLabel` muestra
  "Levante en producción" / "Postura" / "Fuera de ciclo" en vez de "Etapa 1/2/3"; el valor numérico
  persistido sigue 1/2/3 (Validators.max(3) sin tocar) mapeado 1=LevanteEnProduccion, 2=Postura,
  3=FueraDeCiclo — dato de exportación informativo, no rompe nada al reinterpretarlo por empresa
  (mismo criterio que `metadata jsonb` en CLAUDE.md §🏢.6).
- **Form de levante** (`seguimiento-lote-levante-form.component.ts`): campo nuevo, sólo lectura,
  visible con el flag ON — "Alistamiento" / "Levante", mismo cálculo. Aditivo: nada que migrar para
  las demás empresas.

### 6.5 Casos de prueba (xUnit, reemplazan al caso #2 ambiguo de §4)

1. Flag OFF (o raza no reconocida) ⇒ `ObtenerEtapa` no se usa / devuelve `null`; `calcularEtapa`
   sigue devolviendo exactamente 1/2/3 con los cortes 26-33/34-50/>50 de siempre.
2. Roja/criolla: semana 8→Alistamiento, 9→Levante, 24→Levante, 25→LevanteEnProduccion,
   28→LevanteEnProduccion, 29→Postura, **102→Postura, 103→FueraDeCiclo**.
3. Blanca/Azur: igual hasta semana 28; 29→Postura, **112→Postura, 113→FueraDeCiclo**.
4. `EsGrupoBlancaAzur`: las 5 razas sembradas en F2.1 devuelven el grupo correcto; raza desconocida
   (`null`/string arbitrario) ⇒ `ObtenerEtapa` devuelve `null`, no asume un grupo.
5. Equivalencia frontend↔backend: mismos 5 puntos de corte en `semanas-ciclo-postura.funcion.ts`
   (test Karma o comparación manual documentada si el módulo no tiene harness de test unitario hoy).

---

## 7. F6 — Tipos de inventario (diseño técnico, 20-ago-2026)

**Texto literal** (`Requerimientos de Italapp.docx`, sección «LEVANTES»): *"En el tipo de Items, se
deben actualizar los tipos de inventarios que manejan la compañía así: Alimento. Aves."*

**Módulo real:** `CatalogItem.ItemType` (`api/catalogo-alimentos`) — el plan §2 ya lo señalaba. Hoy
el tipo es un string abierto con 6 valores usables desde la UI: `alimento`, `medicamento`,
`accesorio`, `biologico`, `consumible`, `otro` (`aves` no existe todavía, hay que crearlo). La
pantalla viva es **`CatalogoAlimentosListComponent`** (ruta lazy `config/catalogo-alimentos` →
`CatalogoAlimentosModule` → `CatalogoAlimentosRoutingModule`, path `''`) — trae su propio modal de
alta/edición embebido. **`CatalogoAlimentosFormComponent`** (rutas `nuevo`/`editar/:id` del mismo
módulo) está **huérfano**: nada navega ahí (verificado, sin `routerLink` ni `.navigate` hacia esas
rutas en todo el repo) — no se toca.

**Alcance:** sólo UI (mismo criterio que F4/F5 — `OcultaMachosEnPostura` es "solo UI", no borra
nada del modelo). El backend no valida `ItemType` contra una lista cerrada hoy; no se le agrega
validación nueva — sería ampliar el alcance más allá de "acotar las opciones que ve Santa Reyes".

- Flag nuevo `Company.LimitaTiposInventarioAlimentoYAves` (bool, default `false`), mismas 8 capas +
  `flags-empresa.funcion.ts` + `active-company-config.service.ts` que los flags anteriores.
  Migración idempotente + `UPDATE … WHERE name = 'Santa Reyes'` (mismo patrón que §6.3), esta vez sí
  la enciende porque esta misma entrega construye lo que la consume.
- `catalogo-alimentos.service.ts`: agrega `'aves'` a `CatalogItemType` (tipo compartido, usado
  también por engorde/levante/producción para tipar `CatalogItemDto.itemType` — agregar un literal
  al union no cambia el comportamiento de nadie que no lo use).
- `CatalogoAlimentosListComponent`: `tiposItem` pasa de array fijo a getter que devuelve
  `['alimento', 'aves']` con el flag ON, o los 6 de siempre con el flag OFF — alimenta los DOS
  `<select>` que ya iteran sobre él (filtro de la lista y el combo `itemType` del modal alta/edición,
  líneas 65 y 225 del template). `camposPorTipo['aves'] = []` (sin campos estructurados propios,
  igual que `'otro'` hoy) y su propio type-union local (duplicado histórico del de servicio, no se
  toca esa duplicación — no es parte de este alcance).

**Riesgo:** ninguno — aditivo, y ninguna empresa hoy tiene items con `itemType = 'aves'`, así que no
hay dato existente que reclasificar.

**Validación:** `dotnet build`/`dotnet test` (flag nuevo, sin lógica de cálculo — no hace falta test
xUnit dedicado) + `yarn build` + cruce manual de los dos `@for` del template contra `tiposItem`.

---

## 8. F7 — Huevo sin clasificar y primera postura (diseño técnico, 20-ago-2026)

**Texto literal** (`~/Downloads/Requerimientos de Italapp.docx`, sección «Producción de huevos»,
extraído con python-docx — párrafos 48-58):

> Lo que se llama huevos incubables, cambiarlo por huevos sin clasificar. Y se deben tener 4 campos
> o lista desplegable para: Huevo sin clasificar rojo / Blanco / criollo / gallina feliz / Azur /
> Boneg / Libre de Jaula. Huevo de primera postura (este sí debe tener lista desplegable por raza).
> Se necesita que cuando se cree un lote poder especificar los huevos que va a producir en la etapa
> de producción "mostrar primera postura hasta el último día de la semana 22, desde el primer día
> de la semana 23 no usa más el ítem de primera postura". No se usa el campo de huevo tratado, y
> tampoco es necesita la parte de peso promedio ni de tipo de alimento.

### 8.1 Hallazgo central: la mecánica de F7 YA EXISTE — el gap real es más chico de lo que dice §2

Auditando `modal-seguimiento-diario.component.html`/`.ts` (no solo el grep de "incubable" del plan
comercial): con `clasificacionHuevoPorItems = true` (Santa Reyes, encendido desde antes de este
commit) el bloque entero de "Huevos Incubables"/"Clasificadora de Huevos" — incluida la tarjeta
"Eficiencia de Producción" con el stat "Incubables" — está envuelto en `@if (!clasificacionHuevoPorItems)`
y **NUNCA se renderiza para Santa Reyes**. En su lugar se muestra el selector por ítems del catálogo
(`gruposHuevoItems`, agrupado por `metadata.tipoHuevo` = `Primera`/`Pnc`) construido en F0.2. La
palabra "Incubables" no aparece en ningún punto del flujo de Santa Reyes hoy. Verificado en BD
local: los 7 ítems "Primera" (Rojo/Blanco/Criollo/Gallina Feliz/Bonegg/Libre de Jaula
Certificado/Azur) YA existen en `catalogo_items` para `company_id` de Santa Reyes.

**Lo que faltaba de verdad, y es lo que agrega este commit:**

1. **Nombre literal de los 6 ítems.** Solo `HUEVO SIN CLASIFICAR AZUR` ya seguía la nomenclatura
   pedida; los otros 6 (Rojo/Blanco/Criollo/Gallina Feliz/Bonegg/Libre de Jaula) se llamaban
   `HUEVO ROJO`, etc. — sin el prefijo. El propio catálogo ya probaba el patrón correcto (Azur):
   renombrado por migración data-only a `HUEVO SIN CLASIFICAR <RAZA>` (Libre de Jaula conserva
   "CERTIFICADO", es un dato del ítem, no parte del rename).
2. **Vigencia del ítem "Huevo de primera postura" (semana 22).** `Company.HuevoPrimeraPosturaHastaSemana`
   existe desde F0.1 pero **nada lo consumía** (grep confirma cero usos fuera de las 8 capas de
   exposición) y estaba `NULL` incluso en Santa Reyes. Cierre real de este commit — ver §8.2.
3. **F8.2 "no se usa peso promedio ni tipo de alimento"** — auditado junto con F7 porque el .docx lo
   trae en el mismo párrafo, sin separador. `pesoHuevo`/`tipoAlimento` **no estaban gateados por
   ningún flag** (a diferencia de `huevoTratado`, que sí queda oculto dentro del bloque
   `!clasificacionHuevoPorItems`) — gap real, cerrado acá reusando `clasificacionHuevoPorItems`
   (mismo párrafo del requerimiento, no amerita un flag nuevo). Los controles conservan su valor por
   defecto (`0` / `'Standard'`) — siguen siendo válidos para `Validators.required` y se guardan
   igual que siempre; es un cambio de UI, no de contrato.

### 8.2 Vigencia de "Huevo de primera postura" — diseño

- **Marcador de catálogo:** los ítems que representan "primera postura" (hoy 3: Rojo/Blanco/Criollo
  — únicos con esa variante ya cargada, ver §8.4) se tagean `metadata.primeraPostura = true` (migración
  data-only, `metadata || '{"primeraPostura": true}'`). El resto de "Primera"/"Pnc" no lleva la clave
  (`leerMetaBool` → `false`).
- **Cálculo puro** `HuevoPrimeraPosturaCalculos.EsVigente(hastaSemana, semanaVida)` (backend) +
  espejo `esVigentePrimeraPostura` (`items-huevo-catalogo.funcion.ts`): `semanaVida <= hastaSemana`;
  fail-open (vigente) si falta el límite (toda empresa salvo Santa Reyes) o la semana de vida
  (sin fecha de encaset todavía) — mismo criterio "no ocultar por falta de dato" que el resto de la
  familia F0-F6.
- **Semana de vida:** se reusa `semanaVidaLevante(fechaEncaset, fechaRegistro)` (ya existe, la usa
  F3) — no se duplica el cálculo de fecha una cuarta vez en el repo.
- **Alcance: solo UI (oferta del selector), no rechazo al guardar.** El ítem fuera de vigencia
  aparece `disabled` en el `<option>` con el sufijo "(fuera de vigencia)", mismo patrón que
  `itemHuevoUsadoEnOtraFila`. **Decisión explícita de no bloquear en el backend**: el requerimiento
  describe un comportamiento de formulario ("no usa más el ítem"), no una regla de integridad de
  datos: es la misma familia que `OcultaMachosEnPostura` (solo UI, el dato sigue existiendo en el
  modelo). Si el cliente confirma que además debe RECHAZARSE en el guardado (defensa ante un POST
  directo o una pestaña vieja), es un cambio pequeño y aislado: extender
  `HuevoItemsCalculos.Validar` con el mismo `EsVigente` — pendiente hasta que se pida.
- **Flag de valor** `Company.HuevoPrimeraPosturaHastaSemana = 22` para Santa Reyes (migración
  data-only, ya existía la columna desde F0.1 — solo faltaba poblarla).

### 8.3 Lo que se deja SIN construir en este commit — ambigüedad real, no se adivina

- **"4 campos o lista desplegable" vs. 7 ítems listados.** El texto dice "4 campos" pero enumera 7
  razas — se interpreta como imprecisión de redacción del cliente (7 líneas genéticas/colores ya
  sembradas coinciden 1:1 con la lista), no como una reducción real a 4. Si el cliente insiste en 4,
  hay que preguntar cuáles 4.
- **Contradicción textual en "eficiencia" (párrafo 68, sección PNC):** *"En la eficiencia, deben
  cambiarse por incubables la palabra huevo sin clasificar"* — literalmente pide el rename EN
  SENTIDO CONTRARIO al de §8.1 (huevo sin clasificar → incubables), pero solo en un "panel de
  eficiencia" que **no existe como pantalla propia** (no hay componente ni ruta con ese nombre en el
  repo). La lectura más consistente con el resto del documento es que se refiere a los reportes
  YA EXISTENTES y multi-empresa (`IndicadoresProduccionCalculos`, Reporte Técnico) que siguen
  diciendo "incubables" desde antes de Santa Reyes — es decir, "no toques esos reportes", que es
  exactamente lo que este commit hace (cero cambios ahí). **No se construye un "Panel de eficiencia"
  nuevo** (F8.3, "cuadre suma huevos = total granja") porque no está claro si es una pantalla nueva o
  un ajuste de nomenclatura sobre una existente — a confirmar con el cliente antes de tocar reportes
  financieros.
- **4 primera-postura faltantes.** Solo Rojo/Blanco/Criollo tienen variante "primera postura" en el
  catálogo; Gallina Feliz/Bonegg/Libre de Jaula/Azur no. No se inventan los 4 ítems que faltan
  (cantidad/nombre exacto no está especificado) — mismo criterio que el "Enyemado" faltante de F8.1
  (hallazgo de F0.2, aún sin cerrar). Con el flag de vigencia ya wireado, agregarlos después es solo
  una migración de datos más.
- **F8 completo** (PNC → Manchado/Decolorado/Enyemado/Picado/Fárfara, panel de eficiencia) y **F7.3**
  ("especificar los huevos que va a producir" al CREAR el lote — ¿selección de qué ítems ofrece ese
  lote, o algo distinto de la clasificación por ítems que ya existe?) quedan fuera de este commit:
  necesitan la misma conversación con el cliente que gatilla el punto anterior.

### 8.4 Validación

`dotnet build` (solución completa, SDK 10 portable) → **0 errores**, 21 warnings preexistentes
(ninguno en archivos tocados) · `dotnet test` → **2968/2968** (incluye los 9 casos nuevos de
`HuevoPrimeraPosturaCalculosTests`, sin regresión) · `yarn build` (Node portable) → **0 errores**,
solo el warning preexistente de `package.json` sin `license` · migración
`20260821020826_SantaReyesF7HuevoSinClasificarYVigenciaPrimeraPostura` aplicada en local y
re-verificada por consulta directa (los 6 nombres, `metadata.primeraPostura` en los 3 ítems,
`huevo_primera_postura_hasta_semana = 22`). **Sin smoke visual en navegador**: mismo bloqueo que
F0.1/F3 (minteo de sesión rechazado por el clasificador de seguridad de Auto Mode) — el entorno local
SÍ es nativo (no Docker: Postgres 17 en :5433, back :5002, front :4200 con toolchain portable),
confirmado en esta sesión, pero entrar con credenciales reales para el smoke sigue fuera de lo que
puedo hacer solo. Pendiente que alguien lo abra una vez en pantalla.
