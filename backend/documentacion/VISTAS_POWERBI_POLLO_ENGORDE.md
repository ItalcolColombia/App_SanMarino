# Vistas Power BI — Pollo de Engorde Ecuador

Guía de las 3 vistas de reporting para el encargado de Power BI: qué representa cada una,
por qué columnas filtrar, qué espera recibir y el significado de cada campo.

> **Regla general:** las vistas NO están parametrizadas — devuelven TODOS los lotes/empresas.
> En Power BI **siempre** filtrá por `company_id` (multiempresa). En `vw_indicadores_diarios_engorde`
> filtrá además por `pais_id`. El resto de filtros (granja, núcleo, galpón, lote, fechas) son opcionales.
>
> **Llave para relacionar las 3 vistas entre sí y con dimensiones:** `lote_ave_engorde_id`.
> Dimensiones de contexto comunes: `company_id`, `granja_id`, `nucleo_id`, `galpon_id`.

---

## 1) `vw_seguimiento_pollo_engorde` — Seguimiento diario del lote

**Qué es:** el detalle día a día del lote (mortalidad, selección, consumo de alimento, pesos,
saldos de alimento y de aves, despachos). Equivale a la pantalla "Seguimiento diario" del sistema.

**Grano (1 fila = ):** un lote por día. **Incluye días que solo tienen movimiento de inventario/venta
sin registro de seguimiento** (ver `tipo_fila`).

**Filtros recomendados en Power BI:**
`company_id` (obligatorio) · `granja_id` · `nucleo_id` · `galpon_id` · `lote_ave_engorde_id` ·
`fecha_registro` (rango) · `semana` (1–8) · `tipo_alimento` · `tipo_fila`.

**Importante:**
- `seguimiento_id` **puede ser NULL** en filas de movimiento (no uses esa columna como clave única;
  usá `tipo_fila` para distinguir, o `lote_ave_engorde_id` + `fecha_registro`).
- `saldo_alimento_kg_bd` = saldo guardado en BD; `saldo_alimento_kg_calculado` = recalculado con la
  misma lógica de la app (piso 0, modelo M1). Normalmente coinciden.
- `saldo_aves_vivas` ya descuenta mortalidad + selección + error de sexaje **+ ventas/despachos**.

| Campo | Tipo | Descripción |
|---|---|---|
| `seguimiento_id` | bigint | ID del registro de seguimiento. **NULL** si la fila es solo movimiento (`tipo_fila='movimiento'`). |
| `lote_ave_engorde_id` | int | ID del lote (llave de relación). |
| `lote_nombre` | texto | Nombre del lote. |
| `company_id` / `company_nombre` | int / texto | Empresa (de la granja). **Filtro obligatorio.** |
| `granja_id` / `granja_nombre` | int / texto | Granja. |
| `galpon_id` / `galpon_nombre` | texto | Galpón. |
| `nucleo_id` / `nucleo_nombre` | texto | Núcleo. |
| `fecha_dmy` | texto | Fecha formato DD/MM/YYYY (presentación). |
| `fecha_registro` | date | Fecha del día (usar para ejes/relaciones de tiempo). |
| `semana` | smallint | Semana de vida (1–8). |
| `edad_dias_vida` | int | Días desde encasetamiento. |
| `dia_calendario_corto` | texto | Día calendario corto (ej. "Lun, 05 May"). |
| `mortalidad_hembras` / `mortalidad_machos` | int | Mortalidad del día por sexo. |
| `seleccion_hembras` / `seleccion_machos` | int | Selección (descarte) del día por sexo. |
| `total_mort_mas_sel_dia` | int | Mortalidad + selección del día (sin error de sexaje). |
| `error_sexaje_hembras` / `error_sexaje_machos` | int | Reclasificación por error de sexaje. |
| `despacho_hembras_hist` / `despacho_machos_hist` / `despacho_mixtas_hist` | bigint | Aves despachadas/vendidas del día (del histórico). |
| `saldo_alimento_kg_bd` | numeric | Saldo de alimento (kg) persistido. |
| `saldo_alimento_kg_calculado` | numeric | Saldo de alimento (kg) recalculado (lógica app). |
| `saldo_aves_vivas` | int | Aves vivas al cierre del día (global). |
| `saldo_aves_vivas_hembras` / `saldo_aves_vivas_machos` | bigint | Aves vivas por sexo (desglose). |
| `tipo_alimento` / `tipo_alimento_corto` | texto | Tipo de alimento del día (corto: PRE/INI/ENG/FIN-D). |
| `ingreso_alimento_texto_hist` | texto | Ingreso de alimento del día (texto, ej. "1.250 kg"). |
| `traslado_texto_hist` | texto | Traslados de alimento del día (texto). |
| `documento_hist` | texto | Documentos asociados (ingresos/ventas) del día. |
| `metadata_ingreso_alimento` / `metadata_traslado` / `metadata_documento` | texto | Datos de la captura (metadata del seguimiento). |
| `consumo_kg_hembras` / `consumo_kg_machos` | numeric | Consumo de alimento del día por sexo (kg). |
| `consumo_real_dia_kg` | numeric | Consumo real del día (kg) = hembras + machos. |
| `consumo_acumulado_kg` | numeric | Consumo acumulado del lote (kg) hasta el día. |
| `consumo_bodega_kg` | numeric | Consumo registrado en bodega (INV_CONSUMO) del día. |
| `consumo_agua_diario` | numeric | Consumo de agua del día. |
| `pct_perdidas_dia` | numeric | % de pérdidas del día sobre aves vivas al inicio. |
| `peso_prom_hembras` / `peso_prom_machos` | numeric | Peso promedio del día por sexo (según captura). |
| `observaciones` | texto | Observaciones del registro. |
| `metadata` / `items_adicionales` | jsonb | Datos crudos de la captura (JSON; uso técnico). |
| `tipo_fila` | texto | **`'seguimiento'`** (hay registro) o **`'movimiento'`** (solo venta/ingreso ese día). |
| `uniformidad_hembras` / `uniformidad_machos` | numeric | Uniformidad por sexo. |
| `cv_hembras` / `cv_machos` | numeric | Coeficiente de variación por sexo. |
| `consumo_agua_ph` / `consumo_agua_orp` / `consumo_agua_temperatura` | numeric | Mediciones de calidad de agua. |
| `ciclo` | texto | Ciclo del lote. |
| `historico_consumo_alimento` | jsonb | Detalle de consumo por ítem de alimento (JSON). |
| `despacho_peso_neto` / `despacho_peso_tara` | numeric | Peso neto/tara del despacho del día. |
| `despacho_promedio_peso_ave` | numeric | Peso promedio por ave despachada del día. |
| `created_by_user_id` | texto | Usuario que creó el registro. |

---

## 2) `vw_liquidacion_ecuador_pollo_engorde` — Liquidación técnica del lote

**Qué es:** los indicadores de cierre/liquidación por lote (mortalidad, conversión, eficiencias,
kg de carne, merma, ajuste, etc.) más el estado en tiempo real (aves actuales, si está cerrado).
Equivale a la matriz "Indicador Ecuador / Liquidación Pollo".

**Grano (1 fila = ):** un lote (resumen, no por día). Incluye lotes abiertos y cerrados.

**Filtros recomendados en Power BI:**
`company_id` (obligatorio) · `granja_id` · `nucleo_id` · `galpon_id` · `lote_ave_engorde_id` ·
`fecha_encaset` (rango) · `estado_operativo_lote` · `lote_cerrado_logico` · `tiene_aves`.

**Importante:**
- Los campos de **merma/ajuste** salen **NULL** cuando Costos no registró merma para el lote
  (`merma_unidades` y `merma_kilos` ambos vacíos). Con merma registrada traen el cálculo.
- `lote_cerrado_logico` = el lote ya no tiene aves (o sus reproductoras se vendieron), útil para
  separar lotes activos vs liquidados sin depender del flag manual.
- Constantes de conversión ajustada: `peso_ajuste_variable=2.7`, `divisor_ajuste_variable=4.5`.

| Campo | Tipo | Descripción |
|---|---|---|
| `company_id` / `empresa_nombre` | int / texto | Empresa. **Filtro obligatorio.** |
| `granja_id` / `granja_nombre` | int / texto | Granja. |
| `nucleo_id` / `nucleo_nombre` | texto | Núcleo. |
| `galpon_id` / `galpon_nombre` | texto | Galpón. |
| `lote_ave_engorde_id` / `lote_nombre` | int / texto | Lote (llave de relación). |
| `fecha_encaset` | date | Fecha de encasetamiento. |
| `estado_operativo_lote` | texto | Estado operativo (manual). |
| `liquidado_at` | timestamptz | Fecha/hora de liquidación (si aplica). |
| `cantidad_lotes_reproductores` | int | Cantidad de lotes reproductores asociados. |
| `aves_encasetadas` | bigint | Aves encasetadas (inicial). |
| `aves_sacrificadas` | bigint | Aves vendidas/despachadas/retiradas. |
| `mortalidad` | bigint | Mortalidad + selección acumulada. |
| `mortalidad_porcentaje` / `supervivencia_porcentaje` | numeric | % mortalidad y % supervivencia. |
| `consumo_total_alimento_kg` | numeric | Consumo total de alimento (kg). |
| `consumo_ave_gramos` | numeric | Consumo por ave (g). |
| `kg_carne_pollos` | numeric | Kg de carne producidos (peso neto individual; fix R3.1). |
| `peso_promedio_kilos` | numeric | Peso promedio por ave (kg). |
| `conversion` / `conversion_ajustada2700` | numeric | Conversión alimenticia y ajustada a 2.7 kg. |
| `peso_ajuste_variable` / `divisor_ajuste_variable` | numeric | Constantes de la conversión ajustada (2.7 / 4.5). |
| `edad_promedio` | numeric | Edad promedio de las aves al despacho (días). |
| `metros_cuadrados` | numeric | Área del galpón (o suma de la granja). |
| `aves_por_metro_cuadrado` / `kg_por_metro_cuadrado` | numeric | Densidad por m². |
| `eficiencia_americana` / `eficiencia_europea` / `indice_productividad` | numeric | Índices de eficiencia. |
| `ganancia_dia` | numeric | Ganancia diaria (g/día) = peso prom ÷ edad × 1000. |
| `aves_trasladadas_rep` | bigint | Aves trasladadas a reproductora. |
| `aves_actuales` | bigint | Aves vivas en galpón en tiempo real. |
| `tiene_aves` | boolean | `aves_actuales > 0`. |
| `lote_cerrado_logico` | boolean | Lote cerrado por aves en 0 o reproductoras vendidas. |
| `cerrado_por_aves_cero` / `cerrado_por_reproductores_vendidos` | boolean | Motivo del cierre lógico. |
| `fecha_cierre_ultimo_despacho` | timestamptz | Fecha del último despacho. |
| `fecha_cierre_efectiva` | timestamptz | Fecha efectiva de cierre (despacho o último movimiento/seg). |
| `merma_unidades` | int | Merma en unidades (NULL si no registrada). |
| `merma_kilos` | numeric | Merma en kg (NULL si no registrada). |
| `merma_porcentaje` | numeric | % merma sobre aves sacrificadas (NULL si no registrada). |
| `ajuste_aves` | int | Ajuste de aves = encasetadas − sacrificadas − mortalidad − merma (NULL si no registrada). |
| `porcentaje_ajuste` | numeric | % de ajuste de aves (NULL si no registrada). |
| `produccion_kilo_en_pie` | numeric | Producción en kilo en pie (= kg de carne). |
| `total_kilos_despachados_cliente` | numeric | Kg despachados al cliente = kg carne − merma kg (NULL si no registrada). |
| `aves_sobrante` | int | Aves sobrantes. |
| `dias_engorde` | int | Días de engorde (encaset → cierre). |
| `ratio_sacrificadas` | numeric | Sacrificadas ÷ encasetadas. |
| `fecha_inicio_lote` | date | = fecha de encasetamiento. |
| `fecha_cierre_lote` | timestamptz | Fecha de cierre del lote. |
| `fecha_liquidacion` | timestamptz | Fecha de liquidación. |
| `fecha_alistamiento` | date | Fecha de alistamiento. |

---

## 3) `vw_indicadores_diarios_engorde` — Indicador diario vs guía genética

**Qué es:** comparación día a día del lote contra la **guía genética Ecuador (sexo mixto)**:
peso real vs tabla, ganancia, consumo, alimento acumulado, conversión (CA) y mortalidad.
Equivale a la pantalla "Indicadores diarios" del lote de engorde.

**Grano (1 fila = ):** un lote por día (días con registro de seguimiento).

**Filtros recomendados en Power BI:**
`company_id` **y `pais_id`** (ambos obligatorios — así se resuelve la guía) · `granja_id` ·
`nucleo_id` · `galpon_id` · `lote_ave_engorde_id` · `raza` · `ano_tabla_genetica` ·
`fecha_registro` (rango) · `dia_vida`.

**Importante / qué espera recibir:**
- El lote debe tener **`raza`** y **`ano_tabla_genetica`** configurados, y debe existir una **guía
  cargada** para ese `company_id` + `pais_id` + raza + año. Si no, las columnas `*_tabla_g` y
  `guia_genetica_ecuador_header_id` salen vacías (no hay con qué comparar).
- La comparación es **mixta** (no hay desglose por sexo en esta vista).
- Unidades: pesos y consumos en **gramos (g)** o **g/ave**; porcentajes en %.
- `ganancia_diaria_real_g` se calcula contra el **último día con pesaje** (no el día anterior):
  si el lote se pesa cada varios días, ese valor representa la ganancia acumulada del período entre
  pesajes (es esperado; dividir por los días entre pesajes para ver g/día real).

| Campo | Tipo | Descripción |
|---|---|---|
| `company_id` / `empresa_nombre` | int / texto | Empresa. **Filtro obligatorio.** |
| `pais_id` | int | País del lote. **Filtro obligatorio** (define qué guía aplica). |
| `lote_ave_engorde_id` / `lote_nombre` | int / texto | Lote (llave de relación). |
| `granja_id` / `granja_nombre` | int / texto | Granja. |
| `galpon_id` / `galpon_nombre` | texto | Galpón. |
| `nucleo_id` / `nucleo_nombre` | texto | Núcleo. |
| `raza` | texto | Raza del lote (debe coincidir con la guía). |
| `ano_tabla_genetica` | int | Año de la tabla genética (debe coincidir con la guía). |
| `guia_genetica_ecuador_header_id` | int | ID de la guía emparejada (NULL si no hay guía). |
| `fecha_ymd` | texto | Fecha YYYY-MM-DD (presentación). |
| `fecha_registro` | date | Fecha del día. |
| `dia_vida` | int | Día de vida (desde encaset). |
| `aves_iniciales` | bigint | Aves iniciales del lote. |
| `aves_inicio_dia` / `aves_fin_dia` | bigint | Aves vivas al inicio / fin del día (descuenta mort+sel+errSexaje+despachos). |
| `peso_inicial_mixto_g` | numeric | Peso inicial mixto (g). |
| `peso_real_g` | numeric | Peso promedio real del día (g, mixto). |
| `peso_tabla_g` | numeric | Peso objetivo de la guía para ese día (g). |
| `ganancia_diaria_real_g` | numeric | Ganancia real (g) vs último pesaje. |
| `ganancia_diaria_tabla_g` | numeric | Ganancia diaria objetivo de la guía (g). |
| `consumo_diario_real_g` | numeric | Consumo real por ave del día (g/ave). |
| `consumo_diario_tabla_g` | numeric | Consumo diario objetivo de la guía (g/ave). |
| `alimento_acum_real_g` / `alimento_acum_tabla_g` | numeric | Alimento acumulado por ave real / guía (g/ave). |
| `ca_real` / `ca_tabla` | numeric | Conversión alimenticia real / guía. |
| `mort_sel_real_pct` / `mort_sel_tabla_pct` | numeric | % mortalidad+selección del día real / guía. |
| `dif_peso_vs_tabla_pct` | numeric | % de diferencia del peso real vs guía. |
| `mort_acum_pct` | numeric | % acumulado de pérdidas (mort+sel+errSexaje+despachos) sobre aves iniciales. |

> **Nota de calidad de dato:** valores extremos de `ganancia_diaria_real_g` (p.ej. > 500 g) suelen
> venir de errores de digitación del peso (`peso_prom_*`) en el seguimiento, no de la vista. Una
> captura de peso mal cargada genera una ganancia muy alta ese día y una muy negativa al día
> siguiente. Conviene validar esos pesos en el seguimiento (no se corrigen desde Power BI).

---

## Resumen de filtros (qué debe enviar Power BI)

| Vista | Filtros obligatorios | Filtros opcionales útiles | Llave de relación |
|---|---|---|---|
| `vw_seguimiento_pollo_engorde` | `company_id` | granja, núcleo, galpón, lote, `fecha_registro`, `semana`, `tipo_alimento`, `tipo_fila` | `lote_ave_engorde_id` |
| `vw_liquidacion_ecuador_pollo_engorde` | `company_id` | granja, núcleo, galpón, lote, `fecha_encaset`, `estado_operativo_lote`, `lote_cerrado_logico`, `tiene_aves` | `lote_ave_engorde_id` |
| `vw_indicadores_diarios_engorde` | `company_id`, `pais_id` | granja, núcleo, galpón, lote, `raza`, `ano_tabla_genetica`, `fecha_registro`, `dia_vida` | `lote_ave_engorde_id` |
