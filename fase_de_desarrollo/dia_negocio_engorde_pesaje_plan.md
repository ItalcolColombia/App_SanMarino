# Plan — Pollo engorde: numeración de día 1-based y pesaje al cierre de semana

**Fecha:** 2026-07-27
**Antecede:** [hora_encasetamiento_primer_registro_plan.md](hora_encasetamiento_primer_registro_plan.md)
(commits `f5765c7`, `7639b79`, `56edf3a`)

## Problema reportado

Tras una carga masiva en Panamá (granja DAYLAND, lote `13 - 1`), la tabla de seguimiento de pollo
engorde muestra el día del encasetamiento como **Edad 0** y a partir de ahí 1, 2, 3… El usuario
espera que el **primer día con registro sea el Día 1** — que es exactamente como ya se ve en
reproductora desde `56edf3a` — y que la exigencia de peso caiga **al final de cada semana**.

### Causa raíz (verificada)

1. **Numeración**: la tabla pinta crudo `f.edadDia`, que sale de
   [`fn_seguimiento_diario_engorde.sql:436`](../backend/sql/fn_seguimiento_diario_engorde.sql) como
   `fecha − fecha_encaset` ⇒ 0-based. La regla de la hora de llegada **nunca se cableó** en el módulo
   de engorde: `EncasetamientoCalculos` solo se usa en reproductora y en la validación de fecha de la
   carga masiva.
2. **Pesaje corrido un día**: la regla está escrita sobre la edad 0-based en dos sitios espejo —
   [`modal-seguimiento-engorde.component.ts:647`](../frontend/src/app/features/engorde-comun/pages/modal-seguimiento-engorde/modal-seguimiento-engorde.component.ts)
   y [`MigracionService.SeguimientoEngorde.cs:355`](../backend/src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoEngorde.cs) —
   como `edad ∈ [1,7] ∨ edad % 7 == 0`. Como la semana de la propia fn es `ceil((edad+1)/7)`, la
   semana 1 son las **edades 0..6**: pedir peso en la edad 7 lo pide en el **primer día de la semana
   2**, no en el último de la 1. El mensaje de error delata el bug: dice `"Día {edad}"` usando la edad.

## Decisiones del usuario (27-jul-2026)

| # | Decisión |
|---|---|
| D1 | **Alcance: pantalla + validaciones.** La edad técnica (fn SQL, guía genética, indicadores, informe semanal, liquidación, gráficas) **no se toca**. Nada de datos históricos se reescribe. |
| D2 | La columna **conserva el encabezado «Edad (días vida)»** pero muestra el número 1-based. |
| D3 | El **corrimiento del día de pesaje aplica SOLO** a empresas con `primer_registro_segun_hora_llegada = true` (hoy solo ItalcolPanama). Ecuador y el resto quedan **byte a byte** como hoy. |
| D4 | La **numeración 1-based de la columna sí se ve en todas las empresas** (es cosmética y alinea con reproductora). |

**Consecuencia aceptada de D3+D4:** en una empresa sin el flag la columna dirá «Día 8» el día en que
se exige el peso (edad 7). Es lo que se eligió: preferir cero cambio de comportamiento sobre
cosmética perfecta en un país que no lo pidió.

---

## 1. Enfoque

Una sola noción nueva, pura: **el día de negocio**.

```
desplazamiento = reglaActiva && hora >= 13:00   ? 1 : 0     (ya existe: EncasetamientoCalculos)
diaNegocio     = (fecha − fechaEncaset) − desplazamiento + 1
semanaNegocio  = ceil(diaNegocio / 7)
esDiaDePesaje(d) = (d ∈ [1,7]) ∨ (d > 7 ∧ d % 7 == 0)
```

`esDiaDePesaje` se evalúa sobre `diaNegocio` **si el flag está activo** y sobre la **edad cruda si no**
(literalmente la expresión de hoy, sin tocar) ⇒ D3 sale por construcción, sin `if (pais == X)`.

Con `desplazamiento = 0` (todo lote sin hora, toda empresa sin flag) `diaNegocio = edad + 1`: la
columna simplemente deja de mostrar el 0, que es D2/D4.

## 2. Archivos

### Backend

| Archivo | Cambio |
|---|---|
| `Application/Calculos/EncasetamientoCalculos.cs` | + `DiaDeNegocio(fecha, fechaEncaset, hora)` y `SemanaDeNegocio(dia)` (puros) |
| `Application/Calculos/PesajeEngordeCalculos.cs` | **NUEVO** — `EsDiaDePesajeObligatorio(int dia)` |
| `Services/Migracion/Funciones/MigracionService.SeguimientoEngorde.cs` | la advertencia de pesaje usa el día de negocio cuando `reglaHoraActiva`, y la edad cruda cuando no; el mensaje dice el número que el usuario ve |
| `tests/…/EncasetamientoCalculosTests.cs` | + casos de `DiaDeNegocio` / `SemanaDeNegocio` |
| `tests/…/PesajeEngordeCalculosTests.cs` | **NUEVO** — incluye la regresión con flag OFF |

Sin migraciones, sin SQL, sin DTOs nuevos (`horaEncasetamiento` ya viaja en `LoteAveEngordeDto`).

### Frontend

| Archivo | Cambio |
|---|---|
| `features/engorde-comun/funciones/dia-negocio-engorde.funcion.ts` | **NUEVO** — funciones puras espejo del backend (`desplazamientoPrimerDia`, `diaDeNegocio`, `semanaDeNegocio`, `esDiaDePesajeObligatorio`) |
| `features/aves-engorde/pages/seguimiento-aves-engorde-list/…` | lee el flag de empresa; conserva la `horaEncasetamiento` del `LoteAveEngordeDto` y la pasa al tab y al modal |
| `features/aves-engorde/pages/tabs-principal-engorde/…` (ts + html) | `@Input()` flag + hora; la columna Edad, la columna Semana, el filtro de semana y el Excel usan el día de negocio |
| `features/engorde-comun/pages/modal-seguimiento-engorde/…` | `@Input()` flag + hora; `esPrimeraSemana` y `esDiaPesoObligatorio` pasan a evaluarse sobre el día de negocio cuando el flag está activo |

## 3. Reglas de negocio

1. **Día 1 = primer día CON REGISTRO.** Sin hora o con hora < 13:00 es el día del encaset; con hora
   ≥ 13:00 y flag activo, el día siguiente.
2. **La edad técnica no cambia.** `edadDia` sigue llegando 0-based del backend y sigue alimentando
   guía genética, indicadores, informe semanal y liquidación.
3. **Semana = `ceil(diaNegocio / 7)`.** Con desplazamiento 0 es idéntica a la que devuelve la fn hoy
   (`ceil((edad+1)/7)`) ⇒ no-op para todos los lotes actuales; solo corrige los lotes tardíos.
4. **Pesaje obligatorio**: días 1–7 todos los días, y después cada múltiplo de 7 (14, 21, 28…).
   **Solo con el flag activo.** Sin flag, la expresión sigue siendo la de hoy sobre la edad.
5. **Fail-closed**: si no se resuelve el flag (error de red, empresa sin config) ⇒ `false` ⇒
   comportamiento actual.

## 4. Casos de prueba

### xUnit (backend)

| # | Escenario | Esperado |
|---|---|---|
| 1 | encaset 08/06, fecha 08/06, sin hora | día 1 |
| 2 | encaset 08/06, fecha 09/06, sin hora | día 2 |
| 3 | encaset 08/06, fecha 08/06, hora 15:00 | día 0 (anterior al primer día válido) |
| 4 | encaset 08/06, fecha 09/06, hora 15:00 | día 1 |
| 5 | encaset 08/06, fecha 14/06, sin hora | día 7, semana 1 |
| 6 | encaset 08/06, fecha 15/06, sin hora | día 8, semana 2 |
| 7 | `EsDiaDePesajeObligatorio` 1..7 | true |
| 8 | `EsDiaDePesajeObligatorio` 8..13 | false |
| 9 | `EsDiaDePesajeObligatorio` 14, 21, 28 | true |
| 10 | `EsDiaDePesajeObligatorio` 0 y negativos | false |
| 11 | Regresión flag OFF: la expresión sobre la edad da exactamente el mismo set que hoy | idéntico |

### Smoke manual

- **Panamá (flag ON)**, lote DAYLAND `13 - 1`: la primera fila (08/06) pasa a mostrar **Edad 1** y la
  semana 1 cubre 08/06–14/06. El modal exige peso el 14/06 (día 7), no el 15/06.
- **Panamá, lote con hora ≥ 13:00**: el primer día con registro (día siguiente al encaset) muestra
  Edad 1 y la semana 1 son sus 7 días.
- **Ecuador (flag OFF)**: la columna muestra 1-based, pero el día en que el modal exige el peso es
  **el mismo de siempre** (edad 7 ⇒ hoy visible como Día 8). Cero cambio funcional.

## 5. Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test` (todo verde)
- `cd frontend && yarn build` (0 errores; solo el warning de bundle budget preexistente)
