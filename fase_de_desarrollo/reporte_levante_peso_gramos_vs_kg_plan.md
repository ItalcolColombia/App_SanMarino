# Reporte técnico de levante: el peso real en gramos contra la guía en kilos

**Pedido (14ago26):** corregir el `%Dif Peso` del reporte técnico de levante, que rinde valores
absurdos (S369A semana 1 → `104.037,93 %`; K345A semana 1 → `109.555,17 %`).

---

## 1. Diagnóstico

En [`ReporteTecnicoService.cs`](../backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs)
la guía se convierte a kilos y el peso real no:

```csharp
PesoHGUIA    = ParseGuiaRaw(guiaRaw.PesoH) / 1000.0,   // guía peso_h (g) → kg  ✔
PesoH        = pesoH > 0 ? pesoH : null,               // seguimiento peso_prom_hembras: GRAMOS ✘
PorcDifPesoH = (pesoH - guia/1000.0) / (guia/1000.0) * 100   // gramos ÷ kilos ⇒ ×1000
```

`seguimiento_diario_levante.peso_prom_hembras` guarda **gramos** (151 en la semana 1, 3.029 en la
24) y `produccion_avicola_raw.peso_h` también (145, 2.915). La liquidación de cierre lo hace bien:
[`LiquidacionCierreLoteLevanteService.cs:231`](../backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionCierreLoteLevanteService.cs)
compara gramos contra gramos y su `%DifPeso` sale correcto. El reporte es el único que mezcla.

### La unidad que la pantalla espera es KILOS

- Cabeceras del reporte vivo: `Peso H — kg Real` · `Peso H — Guía kg`, formateadas `1.3-3`
  (tres decimales: `0.145`, `3.029`), no `3029.167`.
- Excel «Real vs Guía»: apila `PesoH` (fila Real) sobre `PesoHGUIA` (fila Guía) en la misma
  columna ⇒ obligadas a compartir unidad. Hoy salen `3029,17` y `2,92`.
- `tabla-levante-semanal-hembras/machos` (sección **PESO CORPORAL Grs.**) ya trae el parche
  `dato.pesoH * 1000` — el front asumía kilos y multiplicaba para mostrar gramos; con el backend
  mandando gramos, esa celda muestra `151.000`. La celda de la guía, al lado, muestra `0,1`.

⇒ El contrato correcto es **peso real y guía en kilos**, y quien quiera gramos multiplica.

---

## 2. Enfoque

Una sola fórmula por número: la conversión y el % pasan a
`Application/Calculos/PesoLevanteCalculos.cs` (puro, con tests) y los dos armados de fila semanal
del service delegan. El % se calcula **en gramos contra gramos** (es invariante a la unidad y evita
arrastrar el redondeo de la división).

| Archivo | Cambio |
|---|---|
| `Application/Calculos/PesoLevanteCalculos.cs` | **nuevo**: `AKilos(gramos)` + `PorcDiferencia(realG, guiaG)` |
| `ReporteTecnicoService.cs` (1796, 1808, 1857, 1895 y 3102, 3116, 3171, 3184) | `PesoH`/`PesoM` en kg; `%Dif` por el cálculo puro |
| `tabla-levante-semanal-hembras/machos.component.html` | la celda de la guía pasa a gramos (`× 1000`), como ya hacía la del real |
| `reporte-tecnico-main.component.html` | rotular la tabla DIARIA como gramos, para que no parezca contradecir a la semanal |
| `tests/…/PesoLevanteCalculosTests.cs` | cobertura |

**Fuera de alcance (no se toca):** el peso **diario** (`PesoPromH`/`PesoPromM`) sigue en gramos —
no se compara contra guía, así que no hay número roto que arreglar; sólo se le pone la unidad al
encabezado.

---

## 3. Casos de prueba

- `AKilos(3029.17)` ⇒ `3.02917`; `AKilos(0)` y `AKilos(-1)` ⇒ `null`.
- `PorcDiferencia(3029.17, 2915)` ⇒ `+3.92 %` (antes `103.816,52`).
- `PorcDiferencia(151, 145)` ⇒ `+4.14 %` (antes `104.037,93`).
- `PorcDiferencia(x, 0)` / `PorcDiferencia(0, x)` ⇒ `null` (mismo guard que hoy).
- Invariancia de unidad: `PorcDiferencia(g1, g2) == PorcDiferencia(g1/1000, g2/1000)`.
- Peso real por DEBAJO de la guía ⇒ negativo (el semáforo del front pinta rojo).

**Smoke:** S369 (base 30) y K345 (base 1) — `%DifPeso` en el rango ±20 %, `pesoH` en kilos y el
Excel de levante con Real y Guía en la misma unidad.

---

## 4. Resultado (backend local :5002, 14ago26)

`dotnet build` limpio · `dotnet test` **2.500 pasados** (14 nuevos) · `yarn build` OK (sólo el
warning de bundle budget preexistente).

### `%Dif Peso` — antes / ahora

| Lote · semana | peso real | guía | %Dif antes | %Dif ahora |
|---|---|---|---|---|
| S369A · 1 | 0,151 kg | 0,145 kg | `104.037,93` | **`+4,14`** |
| S369A · 24 | 3,029 kg | 2,915 kg | `103.816,52` | **`+3,92`** |
| K345A · 1 | 0,159 kg | 0,145 kg | `109.555,17` | **`+9,66`** |
| K345A · 2 | 0,200 kg | 0,260 kg | `76.707,69` | **`−23,19`** (rojo, el lote pesa menos) |

Ninguna de las 24 semanas de S369 ni de las 25 de K345 queda fuera de ±100 %. Verificado en los dos
armados de fila semanal: `POST /levante/obtener` y `GET /levante/completo/{id}`.

### Excel

- Hoja **«Semanal Hembras»**: `PESO H` `0,153` junto a `PESO H GUIA` `0,145` (antes `153` vs `0,145`).
- Hoja **«Real vs Guía»** (apila Real sobre Guía en la misma columna): sem 1 → `0,15` / `0,14` /
  `4,14`; sem 24 → `3,03` / `2,92` / `3,92`.

**Backend apagado; `:5002` libre.**
