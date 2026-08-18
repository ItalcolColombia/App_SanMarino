# §2.4 — La sección BULTO del Reporte Contable es de la GRANJA, y el reporte no lo dice

**Origen:** hallazgo **§2.4** (🟡 confirmado) del bloque *«Auditoría de cierre — alimento previo al
encaset»*: *«Cada lote padre muestra el kardex de la GRANJA entera (granja 20 tiene 4 padres ⇒ los 4
reportes muestran los mismos 2.907 bultos; sumarlos da 11.628 vs 2.907 reales). Preexistente, no
arreglable en la query (la tabla no tiene columna de lote). Peor: `AcumularSaldos` resta consumos POR
LOTE de entradas POR GRANJA ⇒ el saldo no es ni de la granja ni del lote»*.
**Fecha:** 2026-08-17.

---

## 1. Confirmado en el código y revalidado con datos

- El reporte se genera **por lote padre** (`GenerarReporteAsync` exige `request.LotePadreId`).
- Los movimientos de alimento se traen filtrando **solo por granja**
  (`ReporteContableService`: `.Where(m => m.FarmId == granjaId …)`). No hay filtro de lote porque **no
  hay dato con qué filtrar**.
- `AcumularSaldos` integra `entradas − traslados − retiros − consumoH − consumoM` sobre las filas del
  reporte: las **entradas son de la granja** y los **consumos son de los sublotes de ESE padre**.

**Cuántos casos hay (17ago26):**

| empresa | granjas con 1 lote padre | **con más de uno** | máx. padres | padres afectados |
|---|---|---|---|---|
| Agroavicola Sanmarino | 1 | **3** (MANGOS 4 · LA ESMERALDA 4 · MIRALINDO 2) | 4 | **10 de 11** |
| Demo | 5 | 0 | 1 | 0 |

**Por qué no se puede atribuir el alimento al lote padre:** en Sanmarino los movimientos de alimento
son de **nivel granja** —1.077 de 1.078 filas sin núcleo ni galpón— y los padres de cada granja
**comparten el mismo núcleo** (MANGOS 4 padres/1 núcleo, LA ESMERALDA 4/1, MIRALINDO 2/1). No hay
ubicación ni referencia que separe. La auditoría tenía razón: no es arreglable en la query.

Escala del dato compartido: LA ESMERALDA tiene **4.356 bultos de entradas y 3.830 de consumo** en toda
su historia, y **4 reportes** los muestran como propios.

---

## 2. Qué entra ahora y qué necesita decisión

### ✅ FASE 1 — que el reporte DIGA de quién es el kardex (entra)

El daño concreto que documentó la auditoría es **sumar los reportes**: 4 × 2.907 = 11.628 bultos que no
existen. Eso se corta diciéndolo donde se lee.

| archivo | qué |
|---|---|
| `Application/Calculos/ReporteContableBultosCalculos.cs` | + `AdvertenciaAlcance(lotesPadreEnGranja, granjaNombre)`: **puro**, devuelve `null` cuando el padre es el único de la granja (sin ruido) y el aviso cuando comparte |
| `Application/DTOs/ReporteContableDto.cs` | + `LotesPadreEnGranja` y `AdvertenciaBultos` en el DTO completo |
| `Infrastructure/Services/ReporteContableService.cs` | cuenta los lotes padres vivos de la granja y llena los dos campos |
| front `tabla-bultos-contable` | pinta el aviso bajo el título **BULTO** |

**No se toca ningún número.** El saldo, las entradas, los retiros y los consumos salen exactamente
igual que hoy.

### ⏸️ FASE 2 — el saldo coherente con el nivel del dato (NO entra: es decisión del usuario)

Hoy el saldo de bultos es `entradas de la GRANJA − consumos de ESTE padre`, así que **sobreestima**
tanto como consuman los otros padres de la granja. Las dos salidas posibles:

- **(a) Saldo de granja:** restar el consumo de **todos** los lotes de la granja. El número pasa a
  significar algo verificable contra el inventario, pero **cambia una columna que Costos ya lee**.
- **(b) Dejarlo:** el saldo sigue sin ser de nadie, con el aviso de la Fase 1 al lado.

Se recomienda **(a)**, pero se deja explícitamente fuera de esta entrega: mover una columna de un
reporte contable en uso es una decisión de producto, no un refactor. Medida disponible para decidir:
en las 3 granjas afectadas el consumo no restado es el de 1 a 3 lotes padres adicionales.

---

## 3. Casos de prueba (xUnit, puros)

| # | caso | espera |
|---|---|---|
| T1 | 1 lote padre en la granja | `null` — sin aviso, sin ruido |
| T2 | 4 lotes padres, granja «LA ESMERALDA» | aviso que nombra la granja **y** el 4 |
| T3 | 2 lotes padres | aviso en singular/plural correcto |
| T4 | 0 o negativo (dato ausente) | `null` — nunca inventa un aviso |
| T5 | nombre de granja vacío | aviso genérico, sin comillas huérfanas |

---

## 4. Verificación

1. `dotnet build` + `dotnet test` con los T1-T5.
2. `yarn build`.
3. Smoke: el reporte de un padre de **LA ESMERALDA** (4 padres) trae el aviso; el de **NIZA III**
   (único padre) **no** lo trae.
4. **Ningún número del reporte se mueve**: la comparación es contra el mismo reporte antes del cambio.

---

## 5. Fuera de alcance, dicho

- No se toca `AcumularSaldos` ni ninguna cifra del reporte (ver Fase 2).
- No se intenta atribuir el alimento al lote padre: **no hay dato** (§1).
- No se toca la sección de aves ni la de huevos.
