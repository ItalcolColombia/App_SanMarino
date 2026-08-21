# Ajuste de encasetamiento en lote (engorde + postura) — corregir las aves con que arrancó un lote que ya tiene seguimiento

**Ticket:** crearon el lote de pollo engorde con la cantidad de aves equivocada y siguieron
cargando el seguimiento diario. Ahora necesitan **editar el lote y sumarle** (o restarle) aves, y que
esa corrección se refleje en cascada: seguimiento diario, reportes, consumo, disponibilidad y ventas.
El mismo caso puede darse en **postura**.

**Decisiones del usuario (21-ago-2026):**
- Restar aves por debajo de lo ya consumido ⇒ **bloquear con detalle** (día y faltante), nada se guarda a medias.
- Postura ⇒ la corrección llega a **levante Y producción**.

---

## 1 · Diagnóstico (verificado contra el código y la BD local, no asumido)

### 1.1 En engorde el formulario edita el SALDO, no el encasetamiento

`lote_ave_engorde` tiene **dos números distintos** que el form trata como uno solo:

| Columna | Qué es | Quién la mueve |
|---|---|---|
| `aves_encasetadas` | **Inicial** (histórico, nunca baja) | solo el alta del lote |
| `hembras_l` / `machos_l` / `mixtas` | **Saldo vivo** | `RetiroAvesEngordeAplicador` (bajas del seguimiento) + ventas/traslados |

Medido en la BD local (lote id 5): `aves_encasetadas = 25.542`, `hembras_l+machos_l+mixtas = 1.840`.

El form (`lote-engorde-list.component.ts`) carga `hembrasL`/`machosL` **del maestro vivo** y tiene
`actualizarEncasetadas()` (línea 1031), que hace `avesEncasetadas = hembrasL + machosL` — es decir,
**pisa el inicial con el saldo**. Está enganchado por `valueChanges` (líneas 273-274) **y** por
`(input)` en el HTML (líneas 524, 528).

⇒ **Hoy no existe un camino correcto para sumar ni restar aves.** En cuanto el operario toca
`# Hembras`, `aves_encasetadas` pasa de 25.542 a ~1.840 y `fn_seguimiento_diario_engorde` vuelve a
restar sobre esa base las mismas bajas y ventas que ya estaban descontadas: toda la serie diaria,
la conversión, el % de mortalidad, los informes y la liquidación quedan mal.

`LoteAveEngordeService.UpdateAsync` (línea 408) escribe los cuatro campos a ciegas desde el DTO
(`ent.HembrasL = dto.HembrasL`, `ent.AvesEncasetadas = dto.AvesEncasetadas`) y **no toca** el
historial.

### 1.2 El invariante que hay que preservar

`fn_cuadre_aves_engorde` vigila la identidad:

```
aves_encasetadas == historial(Inicio).total
maestro          == Inicio − ventas Completado − BAJA_SEGUIMIENTO − ajustes fantasma
```

**Línea base medida hoy en la BD local: 191 lotes, `cuadra=false` → 0, `referencia_confiable=false` → 0.**
Los 191 lotes tienen su fila `Inicio` en `historial_lote_pollo_engorde`. Cualquier ajuste debe mover
las **tres** copias a la vez o el detector se enciende.

### 1.3 En postura la semántica es la OPUESTA — y media cadena ya está resuelta

| Tabla / columna | Qué es | ¿La edición la corrige hoy? |
|---|---|---|
| `lotes.hembras_l` / `machos_l` | **inicial** | ✅ `LoteService.UpdateAsync:610` |
| `lote_postura_levante.aves_h_inicial` | inicial espejo | ✅ trigger `trg_lotes_sync_lote_postura_levante` |
| `lote_postura_levante.aves_h_actual` | **saldo vivo** | ✅ el trigger corre el **delta** (migración `20260806074742`) |
| `lote_etapa_levante.aves_inicio_hembras` | inicial, **gana** sobre `lotes.hembras_l` en `GetMortalidadResumenAsync` | ❌ solo se escribe en el alta |
| `lote_postura_produccion.aves_h_inicial` / `hembras_iniciales_prod` | base de `fn_seguimiento_diario_produccion` | ❌ nunca |
| `lote_postura_produccion.aves_h_actual` | caché que la fn reescribe | ❌ (deriva de la anterior) |

⇒ En postura el bug no es que se corrompa: es que **la corrección se queda a mitad de camino**.

---

## 2 · Enfoque arquitectónico

Introducir el concepto explícito de **ajuste de encasetamiento**: una corrección *retroactiva* de la
base del lote que (a) mueve el inicial, (b) corre el saldo vivo por el **mismo delta** sin borrar las
bajas ya aplicadas, (c) queda auditada, y (d) se rechaza entera si dejaría algún día en negativo.

Tres reglas rectoras, todas del CLAUDE.md:

1. **Una sola fórmula por número.** El delta y el diagnóstico viven en `Application/Calculos/`
   (puros, con tests xUnit); los services solo resuelven datos y delegan.
2. **El delta, nunca la sobreescritura.** Es el mismo criterio que ya aplicó el trigger de levante
   en ago-2026 y que `RetiroAvesEngordeAplicador` usa para las bajas.
3. **Refactor ≠ cambio de comportamiento.** Un lote que no cambia sus aves tiene que salir byte a
   byte igual que hoy; el testigo es `fn_cuadre_aves_engorde` en 0/0.

---

## 3 · Archivos a crear / modificar

### 3.1 Backend — cálculo puro (nuevo)

**`backend/src/ZooSanMarino.Application/Calculos/AjusteEncasetamientoCalculos.cs`** *(nuevo)*

- `record Delta(int Hembras, int Machos, int Mixtas)` — `nuevoInicial − inicialActual` por sexo.
- `Delta Calcular(BaseAves inicialActual, BaseAves inicialNuevo)`.
- `MaestroAves AplicarDelta(MaestroAves maestro, Delta delta)` — suma con clamp a 0, respetando la
  convención mixta de `RetiroAvesEngordeCalculos` (lote mixto ⇒ el delta va al bucket `mixtas`).
- `Diagnostico Diagnosticar(int inicialNuevo, IReadOnlyList<MovimientoDia> serie)` — simula el saldo
  acumulado día a día (`inicial − Σ(bajas + ventas)`) y devuelve el **primer día que quedaría
  negativo** y por cuántas aves. Espejo ejecutable de `fn_seguimiento_diario_engorde` §13.
- `string MensajeIncompatible(Diagnostico d)` — mismo estilo que `EncasetamientoRetroactivoCalculos`:
  qué pasa, en qué día, cuántas aves y qué tiene que hacer el usuario.
- `bool SinCambio(Delta d)` — idempotencia: delta cero ⇒ el service no escribe nada.

**Tests** → `backend/tests/ZooSanMarino.Application.Tests/AjusteEncasetamientoCalculosTests.cs`
(ver §5).

### 3.2 Backend — engorde

**`LoteAveEngordeService.UpdateAsync`** (`Services/LoteAveEngordeService.cs:408`)

Reemplazar las 4 asignaciones ciegas por el flujo de ajuste:

1. Leer la fila `Inicio` del historial → `inicialActual` (por sexo). Sin fila ⇒ caer a
   `aves_encasetadas` (retrocompatible; hoy los 191 lotes la tienen).
2. `delta = AjusteEncasetamientoCalculos.Calcular(inicialActual, dto)`.
3. `SinCambio(delta)` ⇒ **no se toca nada de aves** (el resto del Update sigue igual → un PUT que
   solo cambia el técnico no mueve un solo número).
4. Delta ≠ 0 ⇒ cargar la serie (`seguimiento_diario_aves_engorde` + `VENTA_AVES` del histórico
   unificado), `Diagnosticar`, y si `!Compatible` → `InvalidOperationException` con el mensaje
   detallado (400, nada escrito).
5. Compatible ⇒ escribir, **dentro de la misma transacción**:
   - `ent.AvesEncasetadas = inicialNuevo.Total`
   - `ent.HembrasL/MachosL/Mixtas = AplicarDelta(maestro, delta)`
   - fila `Inicio` del historial ⇐ `inicialNuevo` (mantiene `aves_encasetadas == Inicio.total`)
   - fila de auditoría nueva en `historial_lote_pollo_engorde` con
     `TipoRegistro = "AjusteEncaset"` guardando el delta, fecha y usuario.

**⚠️ `AjusteEncaset` NO participa en la conservación.** `CorreccionAvesDisponiblesEngordeService`
solo suma `TipoRegistro == "Ajuste"` (constante `TipoRegistroAjusteFantasma`, línea 167) y
`fn_cuadre_aves_engorde` hace lo mismo ⇒ el tipo nuevo es invisible para los dos por construcción,
igual que `AjusteResync`. Se verifica con el testigo de §5.

**`LiquidacionCongeladaGateCalculos.ValidarEscritura`** ya bloquea la edición de un lote liquidado
(línea 419). **No se toca**: reabrir sigue siendo el camino.

### 3.3 Backend — postura

**`LoteService.UpdateAsync`** (`Services/LoteService.cs:610`), después de `ent.HembrasL = dto.HembrasL`:

1. Calcular el delta contra los valores **previos** de la entidad (EF los tiene en el change tracker).
2. `lote_etapa_levante.aves_inicio_hembras/machos` ⇐ nuevo inicial (cierra el hueco que hace ganar
   un valor viejo en `GetMortalidadResumenAsync`).
3. Si el lote tiene `lote_postura_produccion` viva: correr el delta sobre `aves_h_inicial`/
   `aves_m_inicial` y `hembras_iniciales_prod`/`machos_iniciales_prod` con el **mismo** helper puro.
   `aves_h_actual` es caché de la fn (ver memoria `aves-h-actual-produccion-es-cache-de-la-fn`):
   se corre por el delta igual, y la próxima consulta la reescribe desde la fn ya corregida.
4. El mismo gate de negativos que engorde, contra `fn_seguimiento_diario_produccion` /
   `seguimiento_diario` de levante.

`lote_postura_levante` **no se toca desde C#**: lo hace el trigger, ya correcto. Duplicarlo sería
justamente la doble verdad que este repo ya pagó cara.

### 3.4 Frontend — engorde (el cambio que hace entendible la pantalla)

`features/lote-engorde/components/lote-engorde-list/`:

- El form pasa a editar el **inicial**, no el saldo. `LoteAveEngordeDetailDto` expone
  `inicialHembras`/`inicialMachos`/`inicialMixtas` (de la fila `Inicio`), y el form los carga ahí.
- `# Hembras` / `# Machos` se re-etiquetan **"Hembras encasetadas"** / **"Machos encasetados"**.
- Bloque nuevo readonly **"Saldo actual"** (hembras/machos/mixtas vivas) para que el operario vea la
  diferencia y no confunda los dos números.
- Al editar un lote con seguimiento, aviso inline: *"Corregir estas cantidades recalcula toda la serie
  diaria del lote"*.
- `actualizarEncasetadas()` sigue existiendo (el template lo llama) pero suma los **iniciales**;
  se mueve a `funciones/calcular-aves-encasetadas.funcion.ts` como función pura, según §CLEAN CODE.
- El 400 del gate se muestra con `ToastService.error` (nunca `alert()`).

### 3.5 Frontend — postura

`features/lote/components/lote-list/` (el form vivo, resuelto por `/config/lote-management`):
mismo bloque readonly de saldo actual y el mismo aviso. `hembrasL`/`machosL` **ya son** el inicial
acá, así que el form no cambia de semántica: solo gana contexto y el manejo del 400.

---

## 4 · Reglas de negocio

1. **El ajuste es retroactivo**: cambia la base del día 1, no es un ingreso fechado. Toda la serie
   diaria se recalcula (saldo, % mortalidad, conversión, ave-día).
2. **El saldo vivo se corre por el delta, nunca se pisa.** Sumar 500 sube el saldo en 500 y conserva
   todas las bajas ya descontadas.
3. **Restar se rechaza entero** si con la nueva base algún día de la serie cerraría en negativo. El
   mensaje dice el día y las aves faltantes.
4. **Delta cero ⇒ no-op.** Editar el técnico, la regional o el ERP no mueve un solo número de aves.
5. **Lote liquidado/cerrado**: se conserva el gate actual (hay que reabrir).
6. **Auditoría obligatoria**: quién, cuándo y cuánto, en `historial_lote_pollo_engorde`
   (`AjusteEncaset`) para engorde; `updated_by_user_id` + fila de historial para postura.
7. **Multi-tenant**: el ajuste resuelve la empresa por los datos del lote (patrón fail-closed del
   CLAUDE.md §🏢); nada de flags nuevos — esto es comportamiento base, no una feature por empresa.

---

## 5 · Casos de prueba

### Cálculo puro (xUnit, `AjusteEncasetamientoCalculosTests`)

| # | Caso | Esperado |
|---|---|---|
| 1 | Delta cero | `SinCambio` ⇒ true; el service no escribe |
| 2 | Sumar 500 H a lote con 3.000 bajas aplicadas | inicial +500, maestro +500, bajas intactas |
| 3 | Restar 200 con saldo final de 1.840 | compatible |
| 4 | Restar 2.000 con saldo final de 1.840 | **incompatible**, primer día negativo identificado |
| 5 | Restar donde el negativo aparece a mitad de serie y se recupera | **incompatible** (regla 3) |
| 6 | Lote mixto (Panamá) | el delta va al bucket `mixtas`, igual que `RetiroAvesEngordeCalculos` |
| 7 | Lote sin fila `Inicio` | cae a `aves_encasetadas`, sin excepción |
| 8 | Aplicar delta positivo y luego el negativo opuesto | vuelve al maestro original (reversibilidad sin clamp) |
| 9 | Delta negativo mayor que el maestro | clamp a 0 y diagnóstico incompatible (no queda negativo silencioso) |

### Integración / datos

| # | Verificación | Comando |
|---|---|---|
| 10 | El invariante no se mueve | `SELECT count(*) FILTER (WHERE NOT cuadra), count(*) FILTER (WHERE NOT referencia_confiable) FROM fn_cuadre_aves_engorde(NULL);` ⇒ **0, 0** (línea base: 191/0/0) |
| 11 | Paridad multipaís del alimento | `backend/sql/verificar_paridad_saldo_engorde.sql` antes y después ⇒ 0 en toda empresa |
| 12 | Cuadre de alimento | `backend/sql/verificar_cuadre_alimento_engorde.sql` ⇒ sin nuevos descuadrados |
| 13 | Smoke engorde: sumar 500 a un lote con seguimiento | serie diaria +500 en todos los días, ventas siguen despachables, widget de disponibilidad coherente |
| 14 | Smoke engorde: restar por debajo de lo consumido | 400 con día y faltante; **nada** escrito en BD |
| 15 | Smoke postura levante | `lotes`, `lote_etapa_levante`, `lote_postura_levante` los tres corregidos y el saldo vivo conserva las bajas |
| 16 | Smoke postura producción | `lote_postura_produccion.aves_h_inicial` corregido; la fn recalcula `aves_h_actual` coherente |
| 17 | No regresión | editar un lote SIN tocar aves ⇒ 0 cambios en las 3 copias (`updated_at` del historial intacto) |

### Build / suite

`cd backend && dotnet build` (0 errores, 21 warnings preexistentes) + `dotnet test` (2975 verdes hoy)
· `cd frontend && yarn build` (0 errores).

---

## 6 · Cambios de BD

**Ninguno de schema.** El ajuste usa columnas y tablas que ya existen:
`historial_lote_pollo_engorde.tipo_registro` es texto libre (hoy con 3 valores: `Inicio`, `Ajuste`,
`AjusteResync`) ⇒ `AjusteEncaset` entra sin DDL.

`lote_etapa_levante` y `lote_postura_produccion` ya tienen las columnas necesarias.

Si al implementar apareciera una columna faltante, va por **migración EF idempotente**, nunca por
`.sql` suelto (CLAUDE.md §🔴 *el .sql es el espejo, la migración el vehículo*).

---

## 7 · Orden de ejecución

1. Cálculo puro + tests (sin tocar services) → suite verde.
2. Engorde backend (service + auditoría) → `dotnet test` + testigo `fn_cuadre_aves_engorde`.
3. Engorde frontend (inicial vs saldo) → `yarn build` + smoke.
4. Postura backend (etapa levante + producción) → tests + testigo.
5. Postura frontend → `yarn build` + smoke.
6. Gate multipaís completo (§5 #10-#12) antes de mergear.
