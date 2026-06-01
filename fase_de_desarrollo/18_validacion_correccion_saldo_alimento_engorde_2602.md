# 18 — Validación y corrección masiva del saldo de alimento (pollo engorde, lotes "2602")

**Fecha:** 2026-05-31
**Disparador:** El usuario reporta que en el lote 75 (`/api/LoteAveEngorde/75`, nombre `2602`,
granja 40 / núcleo 723809 / galpón G0042) **el saldo de alimento del 29 al 30 de mayo no cuadra**.
**Alcance acordado:** los **34 lotes** cuyo `lote_nombre` termina en `2602`.
**Entorno:** **solo BD local** (`sanmarinoapplocal`) primero. No tocar prod sin confirmación explícita.
**Fuente de verdad del cálculo:** `fn_seguimiento_diario_engorde(p_lote_id)`
(`backend/sql/fn_seguimiento_diario_engorde.sql`, v5).

---

## 1. Diagnóstico

### 1.1 La función NO tiene bug de cálculo

Se comparó, por (lote, fecha), el `saldo_alimento_kg` **persistido** en
`seguimiento_diario_aves_engorde` contra el **calculado dinámicamente** por la función:
**divergencia = 0 en los 34 lotes**. El persistido y el calculado coinciden. El problema está en
**los datos de entrada** (ingresos de inventario y/o consumos del seguimiento), no en la fórmula.

### 1.2 Fórmula actual del saldo (v5, líneas 462-468 de la fn)

```
saldo_alimento_kg(fecha) = GREATEST(0,
      apertura_kg
    + Σ(ingreso + traslado_entrada − traslado_salida)   [hist_alimento, fecha' ≤ fecha]
    − Σ(consumo_dia_kg)                                  [acumulado ordenado hasta la fecha]
)
```

El `GREATEST(0, …)` enmascara cualquier punto donde el **acumulado** se vuelve negativo,
mostrando `0`. Como el acumulado se recalcula desde apertura (no desde el valor mostrado),
el déficit se **arrastra**: en cuanto entra el siguiente ingreso, el saldo "rebota". Ese rebote
es justamente lo que el usuario ve como "no cuadra del 29 al 30".

### 1.3 El caso lote 75 (G0042)

| fecha | edad | ingreso | consumo | saldo (fn = persistido) | saldo SIN clamp |
|---|---|---|---|---|---|
| 27-05 | 28 | 0 | 3200 | 1435 | 1435 |
| **28-05** | 29 | 0 | 3200 | **0** | **−1765** ← clamp |
| 29-05 | 30 | 10320 | 3400 | 5155 | 5155 |
| 30-05 | 31 | 10120 | **360** | 14915 | 14915 |

- **La aritmética 29→30 sí cuadra entre sí:** `5155 + 10120 − 360 = 14915` ✓.
- Lo que distorsiona la lectura:
  1. El **28-05 el ledger real es −1765 kg** (consumió 3200 con 1435 disponibles) → se muestra 0.
  2. El **consumo del 30-05 = 360 kg es anómalo** (días previos ~3200-3400). Probable dígito
     faltante. Infla el saldo del día 30.
- **Total del lote: disponible 62 455 kg vs consumido 47 540 kg → el negativo es TRANSITORIO**
  (timing: el alimento llegó tarde respecto al consumo, pero al final hay sobrante).

### 1.4 Validación masiva de los 34 lotes "2602"

15 lotes presentan ledger negativo en algún punto. Se separan en dos clases según el **saldo final**
(`disponible − consumido` de todo el ciclo):

#### A) Negativo TRANSITORIO — 9 lotes (alimento existió, registro tardío del ingreso)
El "0" es un artefacto de fechas. **Corregible por cálculo, sin inventar datos.**

| lote | galpón | disponible | consumido | saldo final |
|---|---|---|---|---|
| 5  | G0050 | 279 325 | 117 625 | +161 700 |
| 75 | G0042 | 62 455 | 47 540 | +14 915 |
| 65 | G0036 | 201 920 | 194 420 | +7 500 |
| 10 | G0048 | 84 735 | 78 750 | +5 985 |
| 7  | G0051 | 122 820 | 117 440 | +5 380 |
| 15 | G0052 | 132 690 | 127 690 | +5 000 |
| 66 | G0035 | 189 380 | 184 740 | +4 640 |
| 43 | G0038 | 112 400 | 110 140 | +2 260 |
| 69 | G0063 | 45 920 | 44 320 | +1 600 |

#### B) DÉFICIT REAL — 6 lotes (se consumió más de lo que jamás entró)
**No se puede cuadrar solo con cálculo.** Falta un ingreso real o el consumo está sobrecargado.
Requiere fuente de verdad (Excel / inventario físico) o decisión de negocio.

| lote | galpón | disponible | consumido | saldo final |
|---|---|---|---|---|
| 16 | G0055 | 115 850 | 122 770 | **−6 920** |
| 8  | G0043 | 0 | 5 650 | **−5 650** (sin ningún ingreso registrado) |
| 62 | G0058 | 50 240 | 53 120 | **−2 880** |
| 61 | G0057 | 47 240 | 50 120 | **−2 880** |
| 74 | G0041 | 55 635 | 57 665 | **−2 030** |
| 71 | G0026 | 39 480 | 39 600 | **−120** |

> Nota: el detector de "anomalías de consumo" (caída >60 % vs día previo) arrojó 71 filas, pero la
> mayoría son **falsos positivos**: caídas legítimas de fin de ciclo (edad 50-66) cuando el lote ya
> está casi todo despachado. No se usa como criterio de error. La señal limpia es el **ledger negativo**.

---

## 2. Modelos de corrección evaluados (con ejemplo numérico lote 75)

| modelo | qué hace | lote 75 día 29 / 30 | pro | contra |
|---|---|---|---|---|
| **M0 — actual (cumulativo + clamp)** | recalcula desde apertura, piso 0 | 5155 / 14915 | no pierde kg consumidos; correcto al final | muestra 0 + rebote confuso en el transitorio |
| **M1 — piso 0 con reseteo de base** | `saldo = max(0, saldo_mostrado_ayer + ingreso − consumo)` | 6920 / 16680 | elimina los "0" espurios y el rebote | **"perdona" el déficit transitorio (−1765 kg consumidos desaparecen)** → menos exacto |
| **M2 — corregir fecha del ingreso tardío** | adelantar el ingreso que llegó tarde a su fecha física real | depende del dato | cuadra sin perder kg | cambia datos de inventario; necesita saber la fecha física real |
| **M3 — déficit real: insertar ingreso de cuadre o recortar consumo** | solo aplica a los 6 lotes clase B | — | cuadra el ciclo | inventa/ajusta datos; necesita fuente de verdad |

**Recomendación a confirmar:** ver §3.

---

## 3. Plan propuesto (a confirmar antes de mutar datos)

### Clase A (9 transitorios) — corrección de cálculo
- El modelo **M0 actual ya da el saldo final correcto**; el único defecto es cosmético (0 + rebote).
- Opción preferida: **M1 (piso 0 con reseteo de base)** SOLO si el negocio acepta que el saldo
  "olvide" el déficit transitorio de timing. Es lo que mejor encaja con la instrucción
  *"valida si tiene saldo anterior y el consumo es menos de lo que se tiene, corrige ese 0"*.
- Alternativa más exacta: **M2** (corregir la fecha del ingreso registrado tarde) — requiere
  confirmar la fecha física real de cada ingreso.

### Clase B (6 déficit real) — necesita decisión / fuente de verdad
- No hay forma puramente algebraica de cuadrar: o se **inserta un ingreso de cuadre** por el déficit
  (precedente existente: `"Cuadre saldos Excel — Insertar traslado entrada…"`), o se **recorta el
  consumo** sobrecargado. Ambas cambian datos y deben validarse contra el Excel/inventario físico.
- Lote 8 (0 ingresos, 5650 consumidos, solo 7 seguimientos) parece un lote mal cargado — revisar aparte.

### Reglas de ejecución (CLAUDE.md)
- Solo BD local primero. Respaldo previo de las tablas tocadas.
- Si se toca la función → migración EF **idempotente** (`CREATE OR REPLACE`), no script suelto.
- Si se tocan datos → script con respaldo (`_backup_correccion_saldo_2602_2026_05_31`) y reversible.
- Validar con `fn_seguimiento_diario_engorde` por lote tras cada cambio. `make down` al terminar.

---

## 4. Decisión pendiente del usuario
1. Clase A: ¿**M1** (resetear a 0, olvida déficit de timing) o **M2** (corregir fecha de ingreso)?
2. Clase B: ¿insertar ingreso de cuadre, recortar consumo, o esperar Excel/inventario físico?
3. ¿La corrección de cálculo se aplica en la **función** (afecta a TODO engorde) o solo en un
   recálculo de la columna persistida para estos lotes?
