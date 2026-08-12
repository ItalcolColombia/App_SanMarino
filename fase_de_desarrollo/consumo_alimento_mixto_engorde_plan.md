# Plan — Columna «Consumo mixto (kg)» en el seguimiento diario pollo engorde

**Fecha:** 2026-08-11
**Módulo:** `frontend/src/app/features/aves-engorde` (tab «Registros diarios» + export Excel)
**Tipo:** cambio de PRESENTACIÓN (tabla en pantalla + Excel). Cero cambios de datos, de cálculo o de backend.

---

## 1. Problema (reportado con evidencia)

Excel adjunto por el usuario: `Seguimiento_engorde_94 - 2_20260811.xlsx` (lote `94 - 2`, granja DOÑA MARIA,
empresa **ItalcolPanama**, encaset 26/06/2026 → `lote_ave_engorde_id = 163`).

| Día | Consumo kg hembras | Consumo kg machos |
|---|---|---|
| 1–7 | 272,158 · 408,237 · 498,957 … | 272,158 · 362,878 · 498,957 … |
| 8 en adelante | 1.905,108 · 2.508,272 · 2.358,705 … | **0** |

A partir del día 8 el alimento del lote es **mixto** (una sola ración para todo el galpón), pero la UI y el
Excel lo depositan íntegro en la columna **«Consumo hembras»** con machos en 0 → se lee como si solo
comieran las hembras. Es un problema de rotulado, no de números: el total del día ya es correcto.

## 2. Por qué pasa — la señal ya existe en el dato

Verificado en la BD local (`sanmarinoapplocal:5433`):

```
 origen_cruce | created_by_user_id | filas | con_machos
--------------+--------------------+-------+-----------
 t            | SYSTEM_CRUCE       |   203 |        195
 f            | <uuid del usuario> | 5.567 |          1
```

- **Días 1–7** → la fila la genera `fn_cruce_reproductora_a_engorde` desde los lotes reproductora, que sí
  traen desglose real por sexo. Esas filas nacen con `origen_cruce = true` **y**
  `created_by_user_id = 'SYSTEM_CRUCE'` (correspondencia 1:1, 203/203).
- **Día 8 en adelante** → la fila la crea el usuario desde este módulo. En Panamá el modal opera en modo
  Mixto y `mapearPanamaMixtoAHM()` vuelca el consumo a `consumoKgHembras` con machos = 0.
  En Ecuador pasa lo mismo de hecho: 5.046 filas, 1 sola con machos > 0.

⇒ **La regla no depende del país ni del número de día**: depende del origen de la fila, que ya viaja al
front en `createdByUserId` (el template ya lo usa en la línea 332 para el badge «🔄 Auto»).

`origen_cruce` **no** está en el `RETURNS TABLE` de `fn_seguimiento_diario_engorde`; exponerlo obligaría a
`DROP FUNCTION` + migración + DTO + tests. No hace falta: `created_by_user_id` ya se devuelve y basta.

## 3. Regla de decisión (función pura, sin `this` ni DI)

```
modoConsumoAlimentoFila(f):
  createdByUserId === 'SYSTEM_CRUCE' → 'genero'   (cruce reproductora: desglose real H/M)
  consumoKgMachos > 0                → 'genero'   (red de seguridad: hay consumo de machos registrado)
  en cualquier otro caso             → 'mixto'
```

La segunda condición cubre la fila congelada de un lote liquidado (`liquidacion_lote_engorde_congelada_fila`
conserva `created_by_user_id`, pero hoy 0 de sus 4.315 filas son de cruce) y la única fila de Ecuador con
machos > 0, que debe seguir mostrándose desglosada.

## 4. Archivos

| Archivo | Cambio |
|---|---|
| `aves-engorde/funciones/modo-consumo-alimento-fila.funcion.ts` | **NUEVO** — función pura + constante `USUARIO_CRUCE_REPRODUCTORA` |
| `aves-engorde/funciones/modo-consumo-alimento-fila.funcion.spec.ts` | **NUEVO** — casos de la tabla de pruebas |
| `tabs-principal-engorde.component.ts` | métodos `esConsumoPorGenero`/`esConsumoMixto` que delegan · header + celda nueva en el Excel · `colspanRegistroDiario` |
| `tabs-principal-engorde.component.html` | `<th>` «Consumo mixto (kg)» + 3 celdas condicionadas |

Sin backend, sin SQL, sin migración.

## 5. Comportamiento resultante

**Tabla (pantalla).** Panamá es el único país que hoy muestra las columnas de consumo por sexo; ahí se
agrega la tercera, en el mismo bloque: `Consumo día (kg) · Consumo hembras (kg) · Consumo machos (kg) ·
Consumo mixto (kg) · Consumo acumulado (kg)`. Ecuador no muestra ninguna de las tres → **cero cambios
visibles en pantalla** (su tabla queda byte a byte igual).

| Fila | Hembras | Machos | Mixto |
|---|---|---|---|
| Cruce reproductora (días 1–7) | valor | valor | — |
| Registro del módulo (día 8+) | — | — | `consumoDiaKg` |

**Excel.** Se inserta `Consumo kg mixto` entre `Consumo kg machos` y `Consumo real día (kg)` — misma
lógica de llenado. Aplica a los dos países (el export es un único método). Para Ecuador el efecto es que
su consumo deja de aparecer bajo «hembras» y pasa a «mixto», que es justamente lo pedido.
`Consumo real día (kg)` y `Consumo acumulado (kg)` **no cambian de valor** en ningún caso.

## 6. Casos de prueba

1. Fila de cruce con H y M > 0 → `genero`; celdas H/M con valor, Mixto `—`.
2. Fila de cruce con M = 0 (existen 8 de 203) → `genero` por `SYSTEM_CRUCE`; **no** se rotula mixto.
3. Fila del módulo Panamá (H = 1.905,108 · M = 0) → `mixto`; H/M `—`, Mixto 1.905,108.
4. Fila de Ecuador con H > 0 y M = 0 → `mixto`.
5. Fila de Ecuador con M > 0 (la anómala) → `genero`.
6. Fila de movimiento sin seguimiento (`segId = null`, todo en 0, `createdByUserId = null`) → `mixto` con
   0,00, igual que el 0,00 que hoy muestra en hembras.
7. `createdByUserId` con espacios / distinto casing → se compara `trim()` exacto (la fn siempre escribe el
   literal en mayúsculas).
8. Suma de la columna Mixto + H + M del Excel = suma de `Consumo real día (kg)` (invariante: no se
   duplica ni se pierde ningún kg).

## 7. Validación

- `cd frontend && yarn build` → 0 errores (único warning aceptado: bundle budget preexistente).
- Smoke doble: lote de **Panamá** con cruce (`94 - 2`, id 163) → 7 filas desglosadas + resto en Mixto;
  lote de **Ecuador** → tabla en pantalla sin cambios y Excel con el consumo en Mixto.
- Excel descargado: las 3 columnas de consumo suman exactamente `Consumo real día (kg)`.

---

## 8. Extensión aprobada por el usuario: la misma distinción en Power BI (2026-08-11)

La auditoría dejó un único punto sin alinear: la vista que consume Power BI publicaba
`consumo_kg_hembras` crudo, documentado como «consumo por sexo», así que la ración mixta se
graficaba como alimento de hembras — el mismo malentendido que se corrigió en la pantalla.

**Cambio (aditivo, migración `20260812021716_AddConsumoMixtoVistaPowerbiEngorde`):**
dos columnas nuevas AL FINAL de `vw_seguimiento_pollo_engorde`:

| Columna | Qué trae |
|---|---|
| `consumo_kg_mixto` | numeric — el consumo del día cuando fue una sola ración para el galpón; NULL si la fila tiene desglose real |
| `consumo_es_mixto` | boolean — `true` ⇒ usar `consumo_kg_mixto`; `false` ⇒ usar `consumo_kg_hembras` / `consumo_kg_machos` |

`consumo_kg_hembras`, `consumo_kg_machos` y `consumo_real_dia_kg` quedan **intactos y en su misma
posición** ⇒ ningún reporte de Power BI ya armado cambia.

**Por qué la migración ENVUELVE la vista en vez de recrearla.** El espejo del repo
(`backend/sql/seguimiento_pollo_engorde_tabla_unificada_vista.sql`) está desactualizado: nombra la
vista `vw_seguimiento_pollo_engorde_unificado` y le faltan **14 columnas** que la vista desplegada sí
tiene (`tipo_fila`, `uniformidad_*`, `cv_*`, `consumo_agua_ph/orp/temperatura`, `ciclo`,
`historico_consumo_alimento`, `despacho_peso_*`, `created_by_user_id`). Recrear desde ese archivo
habría BORRADO esas columnas en producción. La migración lee `pg_get_viewdef` y agrega las dos
columnas al final, así que conserva exactamente lo que cada ambiente tenga. Se dejó el aviso dentro
del `.sql` para que nadie lo aplique creyendo que es la fuente de verdad.

**Regla usada:** la misma del front — `created_by_user_id = 'SYSTEM_CRUCE'` (filas del cruce
reproductora) **o** `consumo_kg_machos > 0` ⇒ desglose por sexo; en cualquier otro caso, mixto.
`origen_cruce` no está en la vista, y `SYSTEM_CRUCE` es 1:1 con esa marca.

**Validación ejecutada (BD local):**
- `dotnet build` 0 errores · `dotnet test` **2.222 tests verdes** (2.221 Application + 1 Domain).
- Migración aplicada por el arranque del backend (`Applying migration '20260812021716_…'`).
- Columnas nuevas en posición **66 y 67**; `created_by_user_id` sigue en la 65 ⇒ nada se corrió.
- Lote 94-2 (id 163): días 1–7 `consumo_es_mixto = false` con H/M reales y mixto NULL; del día 8,
  `true` con el total en `consumo_kg_mixto`.
- **Invariante en las dos empresas**: Σ(por régimen) = Σ(`consumo_real_dia_kg`) exacto —
  Ecuador 8.050.971,000 kg (5.057 filas) · Panamá 1.839.861,020 kg (735 filas, 203 por sexo).
  0 filas mixtas descuadradas.
- **Idempotencia**: segunda corrida del `Up()` no duplica columnas (67 → 67).
- **Round-trip**: `Down()` deja 65 columnas exactas y `Up()` vuelve a 67 con el invariante intacto.
