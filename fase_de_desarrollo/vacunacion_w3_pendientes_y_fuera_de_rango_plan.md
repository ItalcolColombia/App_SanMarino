# Vacunación W3 — Bandeja de pendientes + la novedad se despliega sola fuera de rango

**Fecha:** 2026-08-17
**Continúa:** W1.1–W1.4 (`a19807b`, `bd935cb`) y W2 (`f2794c6`).
**Plan madre:** [vacunacion_cronograma_vivo_plantillas_plan.md](vacunacion_cronograma_vivo_plantillas_plan.md) §3.3 y §3.5.

## 1. Qué está roto hoy (verificado en el código)

| Síntoma | Dónde | Evidencia |
|---|---|---|
| Nadie sabe qué vacuna toca hoy sin abrir lote por lote | no existe endpoint de pendientes | `VacunacionRegistroController` sólo tiene `aplicar` / `no-aplicar` |
| El usuario se entera de que necesita motivo **por un 400** | `modal-registro-aplicacion` | el textarea dice *"obligatorio solo si quedó fuera de la franja"* y no valida nada; el back lanza `InvalidOperationException` en `Registrar.cs:64` |
| El rótulo de una aplicación adelantada no dice cuánto | `calcular-estado-visual.funcion.ts:29` | `AplicadoAdelantado` → `"Aplicado adelantado"`, **sin los días**, mientras el tardío sí los muestra |

Riesgo declarado: **bajo**. W3 no escribe una sola fila nueva — agrega una lectura, una validación de UI y un rótulo.

## 2. La trampa de la fecha (por qué el 400 no se arregla con un `if` en el front)

El backend fija la fecha con `DateTime.UtcNow.Date` (`Registrar.cs:60`). El navegador de Ecuador/Colombia
(UTC−5) está en **otro día** entre las 19:00 y la medianoche local. Un pre-chequeo con la fecha local
diría *"está dentro de franja"* mientras el servidor calcula `+1 d`, exige motivo y devuelve el mismo
400 que W3 viene a eliminar — sólo que ahora con la UI jurando que todo estaba bien.

⇒ **El front evalúa con la fecha UTC**, la misma base que el servidor, y la función pura lo declara en
su doc. El backend sigue siendo la autoridad: la UI adelanta el aviso, no reemplaza la validación.

## 3. Enfoque arquitectónico

### 3.1 Una sola fórmula para "¿esto queda fuera de franja?"

Hoy la regla vive dentro de `VacunacionCalculos.CalcularEstadoAplicacion` (estado + desviación +
incumplido + requiereMotivo, todo junto) y necesita el umbral por empresa, que un pre-chequeo no tiene.
Se **extrae** la parte que no depende del umbral:

```
ProyectarAplicacion(franja, fecha) → (Estado, DiasDesviacion, RequiereMotivo)
CalcularEstadoAplicacion(franja, fecha, umbral) → delega en ProyectarAplicacion + Incumplido
```

No cambia ni un resultado: `CalcularEstadoAplicacion` queda como envoltorio. Los tests existentes son el
gate de que la extracción fue neutra.

### 3.2 La bandeja se resuelve en la BD (regla: el backend orquesta, la BD filtra)

`fn_vacunacion_pendientes(p_user_guid, p_company_id, p_pais_id, p_dias_horizonte)`, hermana de
`fn_vacunacion_cronograma_lote`:

- Ítems `activo = true`, `deleted_at IS NULL`, **sin registro** o con registro `Pendiente` (mismo
  criterio que el guard de `VacunacionRegistroService.CargarItemAsync` y que `CronogramaAsync`).
- Lote **no cerrado**: `estado_cierre` / `estado_operativo_lote` `IS DISTINCT FROM 'Cerrado'`
  (⚠️ nunca `= 'Abierto'`: el dato dice `'Abierto'` *y* `'Abierta'` — lección de W2.3.3).
- Franja calculada con la **misma expresión** que `fn_vacunacion_cronograma_lote` (Semana → encaset +
  (valor−1)×7 · Dia → encaset + valor · Fecha → fecha_objetivo, ± rangos).
- Clasificación contra `p_hoy`:
  - `fin < hoy` ⇒ **`Vencido`**, `dias = hoy − fin` (positivo)
  - `hoy ∈ [inicio, fin]` ⇒ **`EnFranja`**, `dias = 0`
  - `inicio > hoy` y `inicio ≤ hoy + horizonte` ⇒ **`Proximo`**, `dias = −(inicio − hoy)`
  - fuera del horizonte ⇒ no sale.
- Scoping: **igual que `fn_vacunacion_filter_data` hoy** (`user_farms` + empresa + país). W4 sube las
  dos funciones a `restrict_locations` / `user_farm_scopes` de una sola pasada — queda anotado en el
  encabezado del `.sql` para que W4 no se olvide de esta.
- Orden: vencidos (más atrasado primero) → en franja → próximos.

`VacunacionPendientesCalculos.Clasificar(inicio, fin, hoy, horizonte)` es la **especificación
ejecutable** de esa clasificación (misma relación que `SeguimientoAvesEngordeCalculos` con su fn): la
SQL es la dueña del número, el cálculo puro es el test, y el smoke compara fila a fila las dos.

### 3.3 Panel en Home, gemelo del de Implementación

`panel-pendientes-vacunacion` espeja `panel-pendientes-firma` (I4): desplegable, arranca abierto, **no
se dibuja si no hay nada**, y falla en silencio (el inicio no es lugar para pelear con la red). Las
clases `.pendientes-panel*` se **mueven** de `implementacion/styles/implementacion-shared.scss` a
`shared/styles/pendientes-panel.scss` sin tocar una declaración, y los dos componentes la referencian.

Cada fila lleva a `/vacunacion/registro` con `?linea=&loteId=`, y la página preselecciona granja y lote.

## 4. Archivos

### Backend
| Archivo | Cambio |
|---|---|
| `Application/Calculos/VacunacionCalculos.cs` | + `ProyectarAplicacion` (extracción neutra) |
| `Application/Calculos/VacunacionPendientesCalculos.cs` | **nuevo** — `Clasificar` + `Situacion` |
| `Application/DTOs/Vacunacion/VacunacionPendienteDto.cs` + `...PendienteRow.cs` | **nuevos** |
| `Application/Interfaces/IVacunacionRegistroService.cs` | + `GetPendientesAsync` |
| `Infrastructure/.../VacunacionRegistroService.Pendientes.cs` | **nuevo** partial (SqlQueryRaw) |
| `backend/sql/fn_vacunacion_pendientes.sql` | **nuevo** (espejo) |
| `Migrations/…_AddFnVacunacionPendientes.cs` | **nueva**, data-only, Designer clonado, `CREATE OR REPLACE` |
| `API/Controllers/VacunacionRegistroController.cs` | + `GET pendientes` (gate `vacunacion.registro.aplicar`) |
| `tests/…/VacunacionPendientesCalculosTests.cs` | **nuevo** |

### Frontend
| Archivo | Cambio |
|---|---|
| `models/vacunacion.model.ts` | + `VacunacionPendienteDto`, `SituacionPendiente` |
| `services/vacunacion.service.ts` | + `getPendientes(diasHorizonte)` |
| `funciones/evaluar-aplicacion-hoy.funcion.ts` (+ `.spec.ts`) | **nuevo** — espejo de `ProyectarAplicacion` en base UTC |
| `funciones/describir-pendiente.funcion.ts` | **nuevo** — rótulo y color por situación |
| `components/modal-registro-aplicacion` | aviso automático + motivo obligatorio + botón deshabilitado |
| `funciones/calcular-estado-visual.funcion.ts` | «Fuera de rango» con días en adelantado/tardío |
| `components/panel-pendientes-vacunacion/` | **nuevo**, montado en `home.component` |
| `shared/styles/pendientes-panel.scss` | **nuevo** (mudanza literal desde Implementación) |
| `pages/registro-aplicacion` | lee `?linea=&loteId=` y preselecciona |

**BD:** ninguna tabla, columna ni índice. Sólo una función nueva.

## 5. Reglas de negocio (contrato de los tests)

1. Un ítem con registro en estado distinto de `Pendiente` **no** es pendiente (mismo criterio que el guard de escritura).
2. Lote cerrado ⇒ fuera de la bandeja. Marcador único `'Cerrado'`, comparado por desigualdad.
3. `Vencido` = fin de franja **anterior** a hoy. Hoy == fin ⇒ `EnFranja` (el último día todavía cumple).
4. `Proximo` sólo dentro del horizonte (default 7 días); fuera, no aparece.
5. Franja imposible (Semana/Dia sin encaset) ⇒ la fila **no** entra en la bandeja (no se inventa una fecha).
6. `RequiereMotivo` ⇔ `DiasDesviacion != 0`. Sin cambio respecto de hoy.
7. La UI **nunca bloquea aplicar** por fecha: sólo exige el motivo que el backend ya exige.
8. Empresa y alcance: los mismos que ya ve el módulo. Fail-closed ⇒ bandeja vacía, nunca lotes ajenos.

## 6. Casos de prueba

- **`VacunacionPendientesCalculos`** (xUnit): los 3 bordes exactos (hoy = inicio, hoy = fin, hoy = fin+1),
  horizonte inclusivo/exclusivo, franja invertida, sin encaset.
- **`VacunacionCalculos`**: los 53 tests existentes siguen verdes ⇒ la extracción de `ProyectarAplicacion` fue neutra.
- **`evaluar-aplicacion-hoy.spec.ts`** (Karma): mismos bordes que el xUnit + el caso 19:00 local (que en
  UTC ya es el día siguiente) para probar que la base es la del servidor.
- **Smoke SQL**: la fn contra `VacunacionPendientesCalculos` fila a fila sobre la BD local; y la fn
  comparada con `fn_vacunacion_cronograma_lote` para el mismo lote (mismas franjas).
- **Regresión**: empresa sin cronograma ⇒ bandeja `[]` y Home sin panel (byte a byte como hoy).
