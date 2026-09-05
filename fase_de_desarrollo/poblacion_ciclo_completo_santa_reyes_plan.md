# Población de un ciclo completo Santa Reyes por carga masiva — plan

> **Entregable:** los archivos `.xlsx` de carga masiva (Levante + Producción) de un lote nuevo de
> Santa Reyes que cubren el ciclo entero — encasetamiento → levante → cierre → producción → venta
> final —, con alimento que entra al silo y sale por el seguimiento diario **cuadrando en cero**, y
> huevos que se producen por ítem y salen completos hacia planta.
>
> **No es una migración EF.** El pedido original decía «en migración para montar en producción» y
> terminaba con «mejor generame los archivos para cargar masivamente»: manda lo segundo. Los datos
> operativos de un lote **no van por migración** — para eso existe el módulo Migraciones Masivas, que
> valida contra las reglas de negocio vivas (stock, disponibilidad de huevo, saldo de aves) que un
> `INSERT` de migración se saltearía. Ver `CLAUDE.md §🗄️` (SQL crudo solo para funciones/vistas/
> backfills) y [[carga-masiva-plantilla-por-empresa]].

## Contexto medido (no asumido)

| Qué | Valor | Cómo se verificó |
|---|---|---|
| Empresa | **Santa Reyes** (`companies.id = 6`) | `select * from companies where id=6` |
| Flags | `maneja_inventario_por_silo=t`, `clasificacion_huevo_por_items=t`, `oculta_machos_en_postura=t`, `consumo_alimento_solo_hembras=t`, `captura_huevos_en_levante=f`, `semana_inicio_produccion_guia=18`, `huevo_primera_postura_hasta_semana=22` | idem |
| Granja | **La Esperanza** (`farms.id = 109`), 3 núcleos, 38 galpones | `farms`, `nucleos`, `galpones` |
| Silos | 39 activos (`Silo 1`…`Silo 38` + `Insumos`) | `farm_silos where granja_id=109` |
| Alimentos | 45 ítems `item_inventario` `tipo_item='alimento'`, **sin nombres duplicados** | `item_inventario where company_id=6` |
| Huevo | 28 ítems `catalogo_items` `item_type='huevo'` | `catalogo_items where company_id=6` |
| Guía genética | `guia_genetica_santa_reyes`, razas Azur / Babcock Brown / **Hy Line Brown** / Criolla / Lohmann LSL, año 2026, semanas **18→140** | `guia_genetica_santa_reyes` |
| Módulo habilitado | migración `20260904160000_HabilitarMigracionesMasivasSantaReyes` | `Migrations/` |

## Reglas del importador que condicionan el archivo (leídas del código, no del manual)

1. **El lote no viaja en el archivo**: se elige en pantalla. Lo que sí queda atado al lote son las
   fechas (≥ encasetamiento), los códigos de alimento (empresa), los silos (`lote_silos`) y los ítems
   de huevo (`lote_huevo_items`).
2. 🔴 **Una fila por fecha en la hoja `Datos`.** `MigracionService.Historicos.cs:460` (levante) y
   `:614` (producción): `"Fecha repetida en el archivo."` es **Error**, descarta la fila. **No se
   pueden cargar varios seguimientos diarios del mismo día por carga masiva.** El flag
   `companies.permite_multiples_seguimientos_diarios` existe y está en `true` para Santa Reyes, pero
   lo introdujo la rama `feature/seguimiento-multiples-registros-dia` (commit `ead0692`, migraciones
   `20260905015025` / `20260905015934` / `20260905021548`) que **no está en `main` ni desplegada**, y
   **no tocó el importador** (verificado por `git show --stat`). Lo que sí admite varios por día son
   los movimientos: `Alimento`, `Movimientos Aves` y `Movimientos Huevos`.
3. **Columnas emitidas para Santa Reyes: 22 en cada línea** (`PlantillaPosturaCalculos.ColumnasOcultas`).
   Levante: sin nada de machos, sin huevos, con `Silo Alimento 1/2 H`. Producción: además sin
   `Huevo Total` / `Huevo Incubable` / `Peso Huevo (g)` / las 11 categorías (van por la hoja `Huevos`).
4. **Silo obligatorio y validado dos veces**: existe y está activo en la granja del lote, **y** está
   en `lote_silos` (`ResolverSiloSlotPostura`, `Historicos.cs:818-868`).
5. **Balance de alimento**: `MigracionAlimentoCalculos.Simular` compara **total entradas vs total
   salidas por (silo, ítem)**, no día a día → el archivo se rechaza entero si falta stock.
6. **Disponibilidad de huevo por ítem**: `ValidarDisponibilidadHuevosPorItemAsync` exige
   `disponible + producido_por_el_archivo − movido ≥ 0` por ítem. La hoja `Movimientos Huevos` **no**
   aplica la lista blanca del lote; la hoja `Huevos` **sí**.
7. **`Salida` de aves exige un lote contraparte existente en la misma fase**; `Ingreso` y `Venta` no.
8. **Fila repetida en `Movimientos Aves`** (misma fecha + tipo + cantidades) = Error.
9. **Reimportar no corrige**: la idempotencia omite la fecha ya cargada en silencio.

## Escenario diseñado

**Lote a crear en producción antes de importar** (ficha exacta en el README del entregable):

- Nombre `SR-2025-01`, granja **La Esperanza**, núcleo **Núcleo 1**, galpón a elegir.
- Encasetamiento **2025-02-24**, **20.000 hembras**, 0 machos.
- Raza **Hy Line Brown**, año tabla genética **2026**.
- Silos asignados: **Silo 1** (levante) y **Silo 2** (producción).
- Ítems de huevo declarados: `528` SIN CLASIFICAR ROJO, `2756` SIN CLAS ROJO PRIMERAS POSTURAS,
  `538` MANCHADO ROJO, `537` PICADO ROJO.

**Línea de tiempo** (todas las fechas pasadas respecto de hoy 2026-09-05, para no disparar la
advertencia de fecha futura):

| Fase | Semanas de vida | Días | Fechas |
|---|---|---|---|
| Levante | 0 → 17 | 0–125 (126 filas) | 2025-02-24 → 2025-06-29 |
| Producción | 18 → 78 | 126–552 (427 filas) | 2025-06-30 → 2026-08-29 |

**Alimento** (código ERP, nunca nombre):

| Fase | Semanas | Ítem | Silo |
|---|---|---|---|
| Levante | 0–5 | `1254` POLLITA PREINICIADOR SR Q SIN COCC | Silo 1 |
| Levante | 6–9 | `1260` POLLITA INICIACION SR Q SIN COCC | Silo 1 |
| Levante | 10–14 | `1268` POLLA CRECIMIENTO SR H SIN COCC | Silo 1 |
| Levante | 15–17 | `1270` POLLA LEVANTE SR H SIN COCC | Silo 1 |
| Producción | 18–20 | `1281` PREPICO INICIAL SR H | Silo 2 |
| Producción | 21–24 | `1284` PREPICO ARRANQUE SR H | Silo 2 |
| Producción | 25–44 | `1294` PREPICO 100 SR H | Silo 2 |
| Producción | 45–78 | `1313` HUEVO FASE II SR H | Silo 2 |

El **día de cambio de alimento usa los dos slots** (`Alimento 1 H` con el saldo del viejo y
`Alimento 2 H` con el nuevo), que es como ocurre en la granja y ejercita el descuento por ítem+silo.

**Aves** — el saldo cierra en 0:
`20.000 encasetadas + 500 ingreso por traslado − mortalidad − selección − venta de descarte final = 0`.

- Mortalidad de levante: curva propia (~4 % acumulado a la semana 17).
- Mortalidad de producción: **derivada de `retiro_ac_h` de la guía genética** (diferencia semanal).
- `Movimientos Aves` levante: 1 `Ingreso` (traslado recibido, sin contraparte) + 1 `Venta` (descarte).
- `Movimientos Aves` producción: `Venta` final por el saldo exacto ⇒ aves = 0.

**Huevos** — la disponibilidad cierra en 0:

- Producción diaria por ítem = `aves_vivas × prod_porcentaje/100` de la guía, repartido:
  hasta la semana 22 el grueso va a `2756` PRIMERAS POSTURAS (regla
  `huevo_primera_postura_hasta_semana=22`), desde la 23 a `528` SIN CLASIFICAR ROJO; `538` MANCHADO y
  `537` PICADO se llevan un porcentaje pequeño.
- `Movimientos Huevos`: **traslado diario a planta** (`Tipo Destino = Planta`,
  `Destino = PLANTA CLASIFICADORA BUGA`) por lo producido ese día + **venta mensual** del picado a un
  cliente. Total movido por ítem == total producido ⇒ disponibilidad final 0.

**Alimento en cero:** por cada (silo, ítem) la hoja `Alimento` emite ingresos desde planta en lotes
redondos de 10.000 kg y un último ingreso por el remanente exacto, de modo que
`Σ ingresos == Σ consumos de la hoja Datos`. El delta de stock del archivo es **exactamente 0**.

## Archivos a generar

```
Desktop/Poblacion_Ciclo_Santa_Reyes/
├── LEEME.md                                    # ficha del lote + orden de importación + cuadres
├── 1_Carga_Masiva_LEVANTE_SR-2025-01.xlsx      # Datos · Alimento · Movimientos Aves · Instrucciones
└── 2_Carga_Masiva_PRODUCCION_SR-2025-01.xlsx   # Datos · Alimento · Huevos · Movimientos Huevos · Movimientos Aves · Instrucciones
```

Generador reproducible: `scripts/generar_poblacion_santa_reyes.py` (openpyxl), con la guía genética
leída de la BD y volcada al propio script para que no dependa de la conexión.

## Orden de importación obligatorio (va en el LEEME)

1. Crear el lote con la ficha exacta, asignarle **Silo 1** y **Silo 2** y declararle los 4 ítems de huevo.
2. Importar `1_..._LEVANTE.xlsx` (Migraciones Masivas → Seguimiento Levante → ese lote).
3. **Cerrar y liquidar el levante** (`resumen-cierre` → `guardar` → `cerrar`) — sin esto el lote no es
   elegible para producción (`Historicos.cs:60-71`).
4. Importar `2_..._PRODUCCION.xlsx`.

⚠️ El lote **no cambia de granja** en este escenario, así que no aplica el paso de traslado del manual
([[manual-carga-masiva-postura-y-traslado]]): el consumo descuenta siempre de la ubicación actual.

## Casos de prueba / verificación

- **V1** Dry-run (`Validar`) de cada archivo: 0 errores de severidad `Error`.
- **V2** Stock por (silo, ítem) después de importar los dos archivos == stock previo (delta 0).
- **V3** `huevo_tot` de cada día == suma de los ítems de la hoja `Huevos` de esa fecha.
- **V4** Disponibilidad de huevo del LPP al final == 0 por ítem.
- **V5** `aves_h_actual` del LPP al final == 0.
- **V6** Reimportar cualquiera de los dos archivos: 0 procesadas, todas omitidas, sin cambios.
- **V7** Todas las fechas del archivo son pasadas ⇒ ninguna advertencia de fecha futura.

---

## Anexo · Renombrar `Peso H/M (g)` → `(kg)` en los esquemas de POSTURA (05-sep-2026)

**Por qué.** El peso corporal de postura se captura y se muestra en **kg** en todo el sistema —
`modal-create-edit.component.html:426-427` (`pesoPromH`) y
`modal-seguimiento-diario.component.html:283-284` (`pesoH`), ambos «Peso promedio (kg)» con
`step="0.01"`, payload **sin conversión**, y la grilla y el modal de detalle igual—. El **único**
lugar que decía gramos era la columna del Excel de carga masiva, y es por donde entraron los
históricos de Sanmarino en la escala equivocada.

**Alcance: sólo POSTURA.** Engorde queda intacto: ahí el peso **sí** es en gramos de punta a punta
(`Peso de llegada (g)`, el campo se llama literalmente `pesoLlegadaG`), y sus históricos de
Panamá/Ecuador están en esa escala.

**Compatibilidad hacia atrás.** `MigracionCalculos.NormalizarClave` sólo pasa a minúsculas, saca
acentos y colapsa espacios: **conserva los paréntesis**, así que `peso h (g)` y `peso h (kg)` son
claves distintas. Agregando `"peso h (g)"` como ALIAS, cualquier archivo ya armado con el título
viejo se sigue leyendo sin tocarlo. El valor no se convierte: se renombra el rótulo, no el dato.

**Archivos:**
- `MigracionEsquemas.cs:116-117` (SeguimientoLevante) y `:176-177` (SeguimientoProduccion) —
  título a `(kg)` + alias `peso h (g)` / `peso m (g)`. **NO tocar `:365-366` ni `:488-489`, que son
  de engorde.**
- `MigracionService.Historicos.cs:481-482` y `:640-641` — el título es la clave de lookup
  (`ClavesDeColumna` busca la columna normalizando el título): si no se actualiza, deja de encontrar
  la columna.
- `PlantillaPosturaCalculos.cs:47,57` — `MachosLevante` / `MachosProduccion` listan `"Peso M (g)"`;
  sin actualizarlo, Santa Reyes dejaría de ocultar esa columna y la plantilla saldría con 23.
- `MigracionEjemploPosturaCalculos.cs:168-169` — títulos **y valores** del ejemplo (1450 → 1,45).
- Tests: `MigracionEsquemasTests.cs:135,257`, `PlantillaPosturaCalculosTests.cs:74,113,152`.

**Casos de prueba:**
- R1 Un archivo con el encabezado VIEJO `Peso H (g)` se sigue leyendo (alias).
- R2 Un archivo con el encabezado NUEVO `Peso H (kg)` se lee.
- R3 La plantilla de Santa Reyes sigue emitiendo **22** columnas por línea (el ocultamiento de
  `Peso M (kg)` sigue funcionando).
- R4 Engorde intacto: sus esquemas siguen diciendo `(g)`.
- R5 `dotnet build` 0/0 + `dotnet test` verdes.
