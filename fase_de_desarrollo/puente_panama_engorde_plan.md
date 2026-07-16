# Plan — Puente de consulta: migración/sincronización Pollo Engorde desde ZooPanamaPollo

> **2026-07-16 (noche) — Historial enriquecido de corridas + confirmación inequívoca.**
>
> Pedido del usuario tras su sincronización real: ver en la pantalla las corridas hechas (reales y
> validaciones) con el MISMO detalle que la previsualización, y una confirmación clara de que la corrida
> sí pasó.
>
> **Backend:**
> 1. **Columna jsonb `detalle_json` en `migracion_masiva`** — migración idempotente
>    `20260716120000_AddDetalleJsonMigracionMasiva` (`ADD COLUMN IF NOT EXISTS`; Designer copiado del
>    último por sed + propiedad insertada a mano en Designer y ModelSnapshot). NO se reutiliza
>    `errores_json` (el historial genérico deserializa ahí `List<MigracionErrorDto>`); los demás tipos de
>    migración dejan `detalle_json = null` → contrato del módulo Migraciones intacto. Se aplica sola al
>    reiniciar el backend (RunMigrations default true en Development).
> 2. **`RegistrarAuditoriaAsync`** guarda en `detalle_json` el `ResultadoSincronizacionDto` **podado**
>    (`PuentePanamaCalculos.PodarDetalleParaHistorial`: conserva TODOS los contadores y mensajes; de la
>    lista de lotes persiste solo los con novedad — excluye "YaExiste", que en re-corridas serían cientos
>    de filas — con tope 500 y mensaje aclaratorio si podó) y devuelve el id de auditoría en el campo
>    nuevo `ResultadoSincronizacionDto.AuditoriaId`.
> 3. **Endpoints nuevos** en `api/sincronizacion-panama`: `GET /historial` (paginado, empresa activa,
>    `incluirValidaciones`, proyección SIN el jsonb pesado, pendientes derivados =
>    totales−nuevos−omitidos−error) y `GET /historial/{id}` (metadatos + ResultadoSincronizacionDto
>    deserializado; `resultado=null` en corridas previas a la mejora). Implementados en el partial
>    `Funciones/PuentePanamaService.Historial.cs` consultando el contexto directo (no se tocó
>    `IMigracionRepository`).
>
> **Front (`/migraciones/sincronizacion-panama`):**
> 4. **Sección "Historial de sincronizaciones"** (debajo del resultado): tabla con fecha, badge
>    Real/Validación, estado, duración, lotes totales/nuevos/pendientes/con error, paginado, check
>    "incluir validaciones", botón Refrescar, y "Ver detalle" que reusa `app-resultado-sincronizacion`
>    (mismo look del preview). Se refresca sola tras cada corrida. 404 del endpoint (backend viejo sin
>    reiniciar) se degrada sin toast rojo.
> 5. **Banner de confirmación persistente** sobre el resultado (`construir-confirmacion.funcion.ts`,
>    función pura): éxito real → verde "✔ Sincronización completada en X s — los datos SÍ se migraron:
>    N lotes · M seguimientos · … · auditada como corrida #id"; ConAdvertencias → ámbar con
>    pendientes/errores; dry-run → "Validación completada — esto es lo que se migrará (no se modificó
>    nada)"; Fallido → rojo. Verde SOLO para éxito (regla de marca). Toasts complementarios intactos.
>
> Tests: +7 (poda del detalle y pendientes derivados) → suite 412/412. Build API 0 err/0 warn ·
> front `ng build` OK. **El usuario debe reiniciar su backend** (aplica la migración al arrancar) y
> recargar el front.

> **2026-07-16 (tarde) — Post-mortem de la 1ª corrida real + mejoras.**
>
> **Qué pasó:** la corrida real del usuario (migracion_masiva ids 4-6, empresa 5) terminó "Ok" con 287 lotes
> totales y 0 procesados: se crearon las 9 granjas, 9 núcleos y 38 galpones pero NINGÚN lote. **Causa raíz
> (evidencia en BD local + código):** (1) el checkbox del front `importarGuiaGenetica` arrancaba en `false`
> y `construirRequest` lo enviaba explícito, pisando el default `true` del backend → `EnsureGuiaAsync`
> devolvía false de una para los 287 lotes (empresa 5 sin guía: `guia_genetica_ecuador_header` = 0 filas) →
> todos "Pendiente"; (2) los "Pendiente" eran invisibles: no iban a `r.Mensajes`, `LotesPendientes` no se
> persistía y el Estado quedaba "Ok" → auditoría 287/0/0/0 sin errores_json. Se verificó contra el API real
> que el origen respondía bien (Listas tipo 1 = ROSS 308 AP / COBB 500; /api/GuiaGenetica = 49 filas).
>
> **Fixes/mejoras aplicados:**
> 1. **Front:** `importarGuiaGenetica` arranca en `true` (causa raíz); raza global pasó a OPCIONAL con
>    confirmación (el backend usa la línea del origen por lote); toast de advertencia cuando quedan pendientes.
> 2. **Observabilidad:** `LotesPendientes > 0` ⇒ `Estado = "ConAdvertencias"` + mensaje-resumen al frente de
>    `Mensajes` con las causas agrupadas (p.ej. "falta guía genética para raza 'X' año Y (280); sin línea…(7)")
>    que queda persistido en la auditoría (errores_json). Contadores de auditoría sin cambios de contrato.
> 3. **Guía de PRUEBA (FAKE)** — `CrearGuiaFakeSiFalta` (default true, check visible en el front): si no hay
>    guía (raza, año) y la del origen no está disponible/viene vacía (o la importación está apagada), se crea
>    una guía marcada como PRUEBA (datos del origen si existen; si no, curva placeholder lineal 14→264 g/día ×
>    49 días = misma forma que la real). Rastro fuerte: flag `GuiaGeneticaFakeCreada`, sufijo "(PRUEBA)" en
>    `GuiaGeneticaRazaAnio`, mensaje fuerte en `Mensajes` y banner de alerta en el front. El upsert de guía ya
>    no aborta la corrida si falla (lote queda Pendiente con motivo).
> 4. **Nomenclatura reproductoras:** `NombreLote = "<lote>-<n>"` (lote "94" → "94-1", "94-2"…, n por orden de
>    id origen) para distinguirlas del lote; `ReproductoraId = "PA-<id>"` sigue siendo la clave idempotente y
>    el nombre origen ("27", "CHONG"…) se preserva en `CodigoReproductora` junto a la incubadora
>    ("SAN MARINO · 27", varchar(100)). Las reproductoras ya creadas en corridas previas NO se renombran.
> 5. **Lesiones por reproductora:** nuevo GET origen `api/Lesion/GetLesionByIdLoteReproductora/{id}` (trae
>    id, fechaRegistro, edadDia, tipoLesionLista + lesiontipotext, aveHembra/Macho/Mixto, observación) →
>    tabla `lesiones` (módulo existente, tab Lesiones de reproductora): `ModuloOrigen='REPRODUCTORA'`,
>    `LoteReproductoraId` = PK destino como string (contrato del tab del front), `LoteId` = lote engorde,
>    FarmId/GalponId de la ubicación, tipo = lesiontipotext (fallback Listas tipo 13). **Idempotencia por
>    marcador `[PA-LES-<id>]` al inicio de Observaciones** (la tabla no tiene clave de origen). Se escribe
>    directo al DbContext (el `LesionService.CreateAsync` fija FechaRegistro=now y acá se preserva la fecha
>    del origen; no se tocó el servicio compartido). Contadores `LesionesNuevas/Omitidas` + columna en la
>    tabla/preview/Excel del front.
> 6. **Homologación con lo preexistente (verificado en BD local):** geografía de Panamá cargada (10
>    departamentos; dep 21 "PANAMA" con 12 municipios) y el preflight la resuelve por nombre del cliente
>    origen; `company_pais (5,3)` existe; el match de granjas por nombre normalizado reutiliza las manuales
>    homónimas y les asegura user_farms. **Limitación documentada:** si una granja manual tiene un nombre
>    DISTINTO al del origen, el puente no puede saber que son la misma → crea una segunda granja con el
>    nombre del origen (revisar/merger a mano si pasa en prod).
>
> Build API 0 err/0 warn · tests 405/405 (13 nuevos del puente) · front `ng build` OK.

> **2026-07-16 — Alineación 100% validada (workflow 39 agentes: 4 validadores por módulo + verificación adversarial).**
> 32 hallazgos confirmados y corregidos. Los clave: (1) granjas exigen departamento/municipio (NOT NULL + FarmService) → se resuelven por nombre desde el cliente del origen (`departamentoText`/`municipioText`) con fallback config/primer dep. de Panamá + preflight que corre también en dry-run; (2) `farms.regional_id` seguía NOT NULL en BD por incidente de historial → migración idempotente `20260716090000_FixFarmsRegionalIdNullable`; (3) regla de línea genética: el lote SIN línea se crea igual (sin asignar) usando la raza global si existe; raza por lote = override global ?? línea del origen (Listas tipo 1: ROSS 308 AP / COBB 500), visible en `Linea`; "Pendiente" solo por falta de guía; (4) consumo: quintales→kg con factor oficial 45.36 (mixto pliega a Hembras) porque si no el consumo quedaba en 0; además los servicios de seguimiento (engorde y reproductora) NO persistían los Qq* → corregidos (Create/Update/Map); (5) seguimiento del lote: día 8+ solo si hay reproductoras; sin reproductoras se importan también los días 1-7; seguimiento repro limitado a edades 1..7 (el destino corta por cantidad) y se reporta cobertura incompleta del cruce; (6) user_farms asegurado para granjas reutilizadas (sin bypass admin en la validación de lotes); (7) galpón usa el id devuelto por el servicio (autogenera otro si colisiona) y detecta galpón bajo otra granja; (8) guard de empresa destino (`PuentePanama:EmpresaDestino`), advertencia si falta empresa-país; granjas/lotes soft-deleted homónimos detectados; textos truncados a los varchar destino y negativos clampeados (checks BD). Clientes NO se migran (solo se usan para resolver geografía). Sin ventas/traslados (el origen no tiene). Build API 0/0 + suite completa 392/392.

**Objetivo:** traer al sistema (empresa **ItalcolPanama**, `company_id = 5`, país 3) los **lotes, granjas, seguimiento diario y seguimiento de reproductora** del sistema externo **ZooPanamaPollo** (Swagger `https://italapp.italcol.com/ZooPanamaPollo`), filtrando por **año de inicio del lote**, y sincronizándolos con el módulo de **pollo engorde**. Solo **LECTURA** contra el Swagger externo (jamás `PUT`/`DELETE`/`POST` de escritura hacia Panamá).

---

## 1. Enfoque arquitectónico

Servicio **backend .NET** (Clean Architecture) que:
1. Se autentica en el API externo (`POST /api/Login`, JWT en `result.token`) y cachea el token.
2. **Recorre solo con GET** la jerarquía origen `Cliente → Granja → Galpón → Lote → InfoProductiva / LoteReproductora → InfoProductivaLoteReproductora`.
3. Filtra lotes por `fechaRegistroInicio.Year == año` (el filtro server-side del origen NO soporta fecha; el año se aplica en el puente).
4. Mapea a nuestras entidades y **reutiliza los servicios existentes** (`IFarmService`, `INucleoService`, `IGalponService`, `ILoteAveEngordeService`, `ISeguimientoAvesEngordeService`, `ILoteReproductoraAveEngordeService`, `ISeguimientoDiarioLoteReproductoraService`) — mismas reglas de negocio, sin duplicar lógica.
5. Es **idempotente** (re-ejecutable sin duplicar) y **auditado** (reusa `migracion_masiva`).
6. Soporta **dry-run** (previsualizar qué traería y validar) antes de insertar.

Se ejecuta bajo el **contexto de empresa activa = ItalcolPanama** (header `X-Active-Company`), igual que el módulo de Migraciones Masivas por Excel. Así se respetan tenant, país y validaciones existentes.

> Es un **módulo hermano** del de Migraciones Masivas por Excel (`api/Migracion`), pero con **fuente = API externo** en vez de archivo. No lo mezclamos: nuevo módulo `PuentePanama`.

---

## 2. Endpoints ORIGEN a consumir (todos GET, solo lectura)

| Paso | GET | Devuelve (result) |
|---|---|---|
| Clientes | `/api/Cliente` | id, nombre, idPais, textos… |
| Granjas × cliente | `/api/Granja/GetGranjaByIdCliente/{idCliente}` | id, nombre, latitud, longitud, certificadoGab, idCliente |
| Galpones × granja | `/api/Galpon/GetGalponByIdGranja/{idGranja}` | id, nombre, largo, ancho, tipogalpon, idGranja |
| Lotes × galpón | `/api/Lote/GetLoteByIdGalpon/{idGalpon}` | id, codLote, nombre, fechaRegistroInicio, numAvesEncasetadas, avesHembra/Macho/Mixta, pesoPromLleg\*, idLineaGeneticaLista, idGalpon |
| Seg. diario engorde × lote | `/api/InfoProductiva/GetInfoProductivaByIdLote/{idLote}` | id, fechaRegistro, edadDias, mortalidad/seleccion/peso/qq × sexo, marcaAlimento, faseAlimentacion |
| Reproductora × lote | `/api/LoteReproductora/GetLoteReproductoraByIdLote/{idLote}` | id, incubadora, nombreReproductora, avesHembra/Macho/Mixta, pesoLlegada\*, idLote |
| Seg. diario reproductora | `/api/InfoProductivaLoteReproductora/GetInfoProductivaLoteRepByIdLoteRTeproductora/{idLoteRepro}` | id, fechaRegistro, edadDia, mortalidad/seleccion/peso/qq × sexo |
| (opcional) Liquidación × lote | `/api/Liquidacion/GetLiquidacionByIdLote/{idLote}` | métricas finales |

Todas las respuestas vienen envueltas en `ObjectGenericResult { error, codError, message, result }`.

---

## 3. Mapeo ORIGEN → DESTINO

Empresa fija `CompanyId = 5 (ItalcolPanama)`. Se mantiene en memoria un mapa `idOrigen → idDestino` por cada nivel (granja, galpón, lote, reproductora) durante la corrida.

### 3.1 Granja → `Farm` (tabla `farms`, vía `IFarmService`/`CreateFarmDto`)
| Destino | Origen |
|---|---|
| Name | `nombre` |
| CompanyId | 5 |
| Status | `'A'` |
| Latitud / Longitud | `latitud` / `longitud` |
| CertificadoGab | `certificadoGab` |
| DepartamentoId / CiudadId / RegionalId / ClienteId | `null` (no hay equivalencia directa) |
**Idempotencia:** por índice único `ux_farms_company_name (CompanyId, Name)` → si ya existe, se reutiliza su `Id`.

### 3.2 Núcleo (sintético) → `Nucleo` (tabla `nucleos`, vía `INucleoService`/`CreateNucleoDto`)
El origen **no tiene núcleo**, pero nuestro modelo exige `Farm → Núcleo → Galpón`.
→ Se crea **un núcleo por granja**: `NucleoId = "1"`, `NucleoNombre = "PRINCIPAL"`, `GranjaId = <farmId>`.
**Idempotencia:** por PK compuesta `(NucleoId, GranjaId)`.

### 3.3 Galpón → `Galpon` (tabla `galpones`, vía `IGalponService`/`CreateGalponDto`)
| Destino | Origen |
|---|---|
| GalponId | `"PA-" + id` (determinístico) |
| GalponNombre | `nombre` |
| NucleoId | `"1"` |
| GranjaId | `<farmId>` |
| Ancho / Largo | `ancho` / `largo` (a string) |
| TipoGalpon | `tipogalpon` (texto) |
**Idempotencia:** por `(CompanyId, GalponId)`.

### 3.4 Lote → `LoteAveEngorde` (tabla `lote_ave_engorde`, vía `ILoteAveEngordeService`/`CreateLoteAveEngordeDto`)
| Destino | Origen |
|---|---|
| LoteNombre | `nombre` (fallback `codLote`) |
| GranjaId | `<farmId>` |
| NucleoId / GalponId | `"1"` / `"PA-"+idGalpon` |
| FechaEncaset | `fechaRegistroInicio` (UTC) |
| HembrasL / MachosL / Mixtas | `avesHembra` / `avesMacho` / `avesMixta` |
| AvesEncasetadas | `numAvesEncasetadas` |
| PesoInicialH / PesoInicialM / PesoMixto | `pesoPromLlegHembra` / `pesoPromLlegMacho` / `pesoPromLlegMixt` |
| **Raza** | **⚠️ a resolver (ver §5)** |
| **AnoTablaGenetica** | **⚠️ a resolver (ver §5)** |
| LineaGenetica | texto de la lista `idLineaGeneticaLista` |
| LoteErp | `"PA-" + id` **(clave de idempotencia)** |
**Filtro año:** solo si `fechaRegistroInicio.Year == año`.
**Idempotencia:** se omite si existe `lote_ave_engorde` con `(CompanyId, LoteErp)`.
**Efecto colateral esperado:** `CreateAsync` crea también `historial_lote_pollo_engorde` (TipoRegistro="Inicio") con las aves iniciales — correcto, lo queremos.

### 3.5 InfoProductiva → `SeguimientoDiarioAvesEngorde` (vía `ISeguimientoAvesEngordeService`, reusa `CreateSeguimientoLoteLevanteRequest`)
| Destino | Origen |
|---|---|
| LoteId (=LoteAveEngordeId) | `<loteId destino>` |
| FechaRegistro | `fechaRegistro` (UTC) |
| MortalidadHembras / MortalidadMachos | `mortalidadHembra` / `mortalidadMacho` |
| SelH / SelM | `seleccionHembra` / `seleccionMacho` |
| PesoPromHembras / PesoPromMachos | `pesoHembra` / `pesoMacho` |
| QqHembras / QqMachos / QqMixtas | `qqhembra` / `qqmacho` / `qqmixta` (Panamá usa quintales) |
| TipoAlimento | `marcaAlimento` + `faseAlimentacion` |
| Observaciones | `observacion` |
**Idempotencia:** constraint único `uq_seg_diario_aves_engorde_lote_fecha` (1 registro por lote/día) — se omiten fechas ya cargadas.
**Riesgo a validar:** `CreateAsync` aplica retiro de `InventarioAves` y recálculo de saldo de alimento. Para históricos de Panamá sin inventario configurado hay que confirmar que no rompa (probar con 1 lote; si aplica, usar variante que no dependa de inventario).

### 3.6 LoteReproductora → `LoteReproductoraAveEngorde` (`lote_reproductora_ave_engorde`)
| Destino | Origen |
|---|---|
| LoteAveEngordeId | `<loteId destino>` |
| NombreLote | `nombreReproductora` |
| CodigoReproductora | `incubadora` |
| H / M | `avesHembra` / `avesMacho` |
| PesoInicialH / PesoInicialM | `pesoLlegadaHembra` / `pesoLlegadaMacho` |
**Idempotencia:** por `(LoteAveEngordeId, NombreLote)`.

### 3.7 InfoProductivaLoteReproductora → `SeguimientoDiarioLoteReproductoraAvesEngorde`
Análogo a §3.5 pero FK `LoteReproductoraAveEngordeId`. Idempotencia por `(loteReproId, fecha)`.

### 3.8 (Opcional, fuera del alcance inicial) Liquidación → `LiquidacionLoteEngordePanama`
El usuario indicó que **no hay ventas ni traslados**; la liquidación no fue pedida. Se documenta el mapeo pero **no se implementa** salvo confirmación.

---

## 4. Estructura de archivos (patrón CLAUDE.md: partial class + cálculo puro)

```
backend/src/
├── ZooSanMarino.Application/
│   ├── Interfaces/IPuentePanamaService.cs
│   ├── DTOs/PuentePanama/
│   │   ├── PanamaModels.cs         # ObjectGenericResult<T>, PanamaGranja/Galpon/Lote/InfoProductiva/LoteRepro/InfoProdRepro
│   │   ├── SincronizarPanamaRequest.cs   # { Anio, DryRun, ClienteIdOrigen? }
│   │   └── ResultadoSincronizacionDto.cs # contadores + errores por nivel
│   └── Calculos/PuentePanamaCalculos.cs  # PUROS: filtro año, claves determinísticas, mapeos origen→CreateXDto
├── ZooSanMarino.Infrastructure/Services/PuentePanama/
│   ├── PuentePanamaApiClient.cs    # HttpClient: login + GET (solo lectura), token cache
│   ├── PuentePanamaService.cs      # ANCLA: ctor, campos, interfaz IPuentePanamaService, helpers
│   └── Funciones/
│       ├── PuentePanamaService.Recorrido.cs   # walk cliente→granja→galpon→lote (dry-run/real)
│       ├── PuentePanamaService.Estructura.cs   # upsert granja/nucleo/galpon
│       ├── PuentePanamaService.Lotes.cs        # upsert lotes engorde + preflight genética
│       ├── PuentePanamaService.Seguimiento.cs  # upsert seguimiento diario engorde
│       └── PuentePanamaService.Reproductora.cs # upsert reproductora + su seguimiento
└── ZooSanMarino.API/Controllers/PuentePanamaController.cs  # api/sincronizacion-panama
```

**Endpoints destino (nuevos):**
- `GET  /api/sincronizacion-panama/previsualizar?anio=YYYY` — dry-run: cuenta y valida (sin insertar).
- `POST /api/sincronizacion-panama/sincronizar?anio=YYYY&dryRun=false` — ejecuta.
- `GET  /api/sincronizacion-panama/historial` / `/historial/{id}` — auditoría (reusa `migracion_masiva`).

**Config** (appsettings, sección `PuentePanama`, **no commitear credenciales reales**):
```json
"PuentePanama": {
  "BaseUrl": "https://italapp.italcol.com/ZooPanamaPollo",
  "Email": "<en appsettings.Development.json / secreto>",
  "Password": "<en appsettings.Development.json / secreto>",
  "GeneticaPorDefecto": { "Raza": "<confirmar>", "Anio": null }
}
```
Registro de `HttpClient` tipado en `Program.cs`.

**Tests:** `backend/tests/ZooSanMarino.Application.Tests/PuentePanamaCalculosTests.cs` (xUnit) sobre `PuentePanamaCalculos` (filtro año, claves determinísticas, mapeos). El gate de CI exige tests verdes.

---

## 5. Puntos a confirmar con el usuario (bloqueantes de negocio)

1. **Año a migrar** (los datos de muestra son 2025). ¿2025? ¿otro?
2. **Genética (Raza + Año)**: crear lotes de engorde **exige** una `Raza` + `AnoTablaGenetica` que exista en la guía genética de **ItalcolPanama** (company 5). Hoy esa empresa **no tiene guía cargada** (local). Opciones:
   - (a) Usuario indica **Raza + Año** ya cargados en la guía de ItalcolPanama → se asignan a todos los lotes.
   - (b) Cargar primero la guía genética de ItalcolPanama (fuera de este puente) y luego correr.
   - (c) Año derivado de `fechaRegistroInicio.Year` + Raza fija (requiere que la guía tenga esa raza para ese año).
3. **Empresa destino = ItalcolPanama (id 5)**: confirmar (país 3 coincide con el origen).
4. **Reproductora**: confirmado que sí se trae (§3.6/§3.7).
5. **Liquidación**: fuera del alcance inicial salvo que se pida.

---

## 6. Orden de ejecución (una corrida por año)

1. Login origen (token). 2. Recorrer clientes→granjas→galpones→lotes; filtrar por año. 3. Por cada lote filtrado: traer seguimiento diario, reproductora y su seguimiento. 4. **Preflight**: validar que exista guía (raza, año) para company 5; si falta, abortar con mensaje claro (dry-run lo reporta). 5. Upsert en orden de FKs: Granja → Núcleo → Galpón → Lote → Seg. diario → Reproductora → Seg. reproductora. 6. Auditar en `migracion_masiva` (Tipo="SincronizacionPanamaEngorde", contadores, errores, dry-run flag).

## 7. Casos de prueba
- Cálculo puro: filtro por año (incluye/excluye por `fechaRegistroInicio`), claves determinísticas (`PA-<id>`), mapeos de cada modelo.
- Idempotencia: correr dos veces no duplica (lote por `LoteErp`, seguimiento por lote/fecha).
- Dry-run no inserta.
- Un lote end-to-end (smoke) contra el API real antes de correr todo el año.
- Falla controlada si falta guía genética (mensaje accionable, sin dejar estado a medias).
