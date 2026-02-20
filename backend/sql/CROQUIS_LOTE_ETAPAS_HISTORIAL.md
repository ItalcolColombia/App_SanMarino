# Croquis: historial del lote por etapas (Levante, Producción, Reproductora)

## Objetivo

Mantener el historial de **con cuántas aves se abrió** cada etapa del lote y **con cuántas se cerró** (o cuántas hay en tiempo real), sin modificar los datos originales del lote en `lotes`.

- **Lote** → Se crea con X hembras e Y machos; ese dato **no se descuenta** en la tabla `lotes`.
- **Levante** → Se registra con cuántas aves inicia (igual al lote) y los descuentos van en seguimientos; al pasar a Producción se guarda con cuántas aves terminó Levante.
- **Producción** → Se registra “con cuántas aves inicia producción”; los descuentos van en seguimientos diarios; opcionalmente se puede registrar cierre (fecha_fin, aves_fin).
- **Reproductora** → Se registra con cuántas aves se abrió el lote reproductora y las actuales (H/M).

---

## Tablas y flujo

### 1. `lotes` (sin cambios de lógica)

- **hembras_l**, **machos_l**, **aves_encasetadas**: datos con que se **creó** el lote (historial fijo, no se descuentan aquí).

### 2. `lote_etapa_levante` (nueva)

- Una fila por lote.
- **aves_inicio_hembras**, **aves_inicio_machos**: con cuántas aves inicia Levante (mismo que al crear el lote).
- **fecha_inicio**: inicio de Levante (p. ej. fecha encaset).
- **fecha_fin**, **aves_fin_hembras**, **aves_fin_machos**: se rellenan cuando el lote **pasa a Producción** (saldos vivos al cierre de Levante).

**Cálculo de aves actuales en Levante:**  
`aves_inicio - MortCaja - Σ(mortalidad, sel, error sexaje)` de `seguimiento_lote_levante` (o tabla unificada de seguimiento diario tipo `levante`).

### 3. `seguimiento_lote_levante` / seguimiento diario tipo `levante`

- Aquí se registran los **descuentos** (mortalidad, sel, error sexaje, etc.) del lote en Levante.
- No se modifica el número “inicial” del lote ni de `lote_etapa_levante`; solo se acumulan movimientos para calcular el saldo actual.

### 4. `produccion_lotes` (campos añadidos)

- **hembras_iniciales**, **machos_iniciales**: con cuántas aves **inicia la etapa de Producción** (las que “entran” desde Levante).
- **fecha_fin**, **aves_fin_hembras**, **aves_fin_machos** (opcionales): cierre de la etapa de producción (depopulación/fin de ciclo).

**Cálculo de aves actuales en Producción:**  
`aves_iniciales - Σ(mortalidad, sel, etc.)` de los seguimientos de producción.

### 5. `lote_reproductoras` (columnas añadidas)

- **h**, **m**: cantidades **actuales** (hembras/machos).
- **aves_inicio_hembras**, **aves_inicio_machos**: con cuántas aves se **abrió** el lote reproductora (historial).

---

## Resumen por etapa

| Etapa         | Dónde está “inicio”              | Dónde están los descuentos / actuales      |
|---------------|-----------------------------------|--------------------------------------------|
| Creación      | `lotes.hembras_l`, `machos_l`     | —                                          |
| Levante       | `lote_etapa_levante.aves_inicio_*`| `seguimiento_lote_levante` → saldo actual   |
| Producción    | `produccion_lotes.hembras_iniciales`, `machos_iniciales` | Seguimientos producción → saldo actual |
| Reproductora  | `lote_reproductoras.aves_inicio_*` | `lote_reproductoras.h`, `m` (actuales)    |

---

## Flujo en la aplicación

1. **Al crear un lote**  
   Se inserta una fila en `lote_etapa_levante` con `aves_inicio_hembras`, `aves_inicio_machos` (y `fecha_inicio`). No se tocan los datos de `lotes`.

2. **Durante Levante**  
   Solo se registran seguimientos (mortalidad, sel, etc.). El “inicio” sigue en `lote_etapa_levante`; el saldo actual se calcula.

3. **Al pasar a Producción (p. ej. semana 26)**  
   - Se actualiza `lote_etapa_levante`: `fecha_fin`, `aves_fin_hembras`, `aves_fin_machos` (saldos al cierre de Levante).
   - Se crea `produccion_lotes` con `hembras_iniciales`, `machos_iniciales` (aves con que inicia producción).

4. **Durante Producción**  
   Seguimientos diarios; saldo = iniciales − descuentos. Opcionalmente, al cerrar la etapa se rellenan `fecha_fin`, `aves_fin_*` en `produccion_lotes`.

5. **Lote reproductora**  
   Al crear/actualizar se mantiene **aves_inicio_*** con el valor al abrir; **h** y **m** son las cantidades actuales.

Con esto se puede siempre comparar: con cuántas aves se creó el lote, con cuántas inició Levante, con cuántas terminó Levante, con cuántas inició Producción y con cuántas está hoy en cada etapa.
