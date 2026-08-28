# El plazo de la doble validación se juzga en DÍA OPERATIVO (UTC−5), no en UTC crudo

> **Origen:** ticket de operación de Panamá (granja **DAYLAND**, galeras 6, 5, 4 y 3) — «no se puede
> ingresar más registro ya que se ingresaron tarde los datos». Auditado el 27/28-ago-2026 contra la
> copia de producción.
>
> Continúa [`plazo_validacion_desde_creacion_plan.md`](plazo_validacion_desde_creacion_plan.md). Aquel
> movió el **origen** del plazo (`fecha` → `created_at`); éste corrige la **zona horaria** con la que
> se juzga ese plazo. Son dos defectos independientes del mismo cálculo.

---

## 1. Qué está mal

`ValidacionSeguimientoService.Hoy` es `DateOnly.FromDateTime(DateTime.UtcNow)` — **UTC crudo**. Las
tres operaciones (Colombia, Ecuador, Panamá) corren en **UTC−5 sin horario de verano**, así que entre
las **19:00 y la medianoche local el backend ya está contando el día siguiente**.

Lo mismo pasa con `created_at`, que es un **instante** y se convertía a día con `DateOnly.FromDateTime`
sin descontar el offset: un registro guardado a las 20:00 hora local nace con fecha de creación del
día siguiente.

Consecuencia neta: **el plazo no vence a la medianoche local sino a las 19:00**, y no dura lo mismo
según la hora en que se cargó el registro.

### La evidencia que lo motiva (DAYLAND, medido el 27-ago-2026)

Los 9 registros que tenían el lote trabado se cargaron el **26-ago entre las 11:47 y las 13:57** y
vencieron el **27-ago a las 19:00** — no a la medianoche del 27.

| Galera | Lote | Sin confirmar | Cargados (local) | Vencieron (local) |
|---|---|---:|---|---|
| 4 | 225 | 1 | 26-ago 12:44 | 27-ago **19:00** |
| 5 | 224 | 3 | 26-ago 11:47–12:00 | 27-ago **19:00** |
| 6 | 216 | 5 | 26-ago 13:52–13:57 | 27-ago **19:00** |

### Es el mismo defecto que ya se corrigió dos veces en este repo

- `VentanaFechaRegistroCalculos.DiaOperativo` nació justamente para esto en la ventana de fecha de
  inventario (comentario textual: *«entre las 19:00 y la medianoche local el servidor ya estaría en el
  día siguiente»*).
- El commit `6fb1edd` («la revocación juzgaba las fechas 5 horas antes de tiempo») lo corrigió para
  las sesiones.

La validación de seguimientos quedó como el último consumidor que seguía en UTC crudo.

---

## 2. Enfoque arquitectónico

**Un instante se convierte a día operativo; una fecha pura NO se toca.** Es la distinción que decide
todo el cambio:

| Campo | Qué es | Cómo se lee |
|---|---|---|
| `created_at` | **instante** real (cuándo se apretó Guardar) | → día operativo (UTC−5) |
| `now()` | **instante** | → día operativo (UTC−5) |
| `fecha` | **fecha pura** guardada como `timestamptz` | ← se deja en UTC, **sin desplazar** |

`fecha` no se toca a propósito: el formulario la guarda a **mediodía UTC** y el trigger del cruce de
reproductora a **medianoche UTC**. Leerla con offset −5 movería las filas del cruce un día hacia
atrás y reescribiría el estado del histórico. Verificado en la copia de producción: las 27 filas
manuales del lote de prueba están a `12:00:00Z` y las 45 del cruce a `00:00:00Z`.

La conversión vive en `Application/Calculos/` (regla de CLAUDE.md: la lógica pura no va a
Infrastructure) y **delega en el helper canónico** `VentanaFechaRegistroCalculos.DiaOperativo` — una
sola fórmula por número, no una segunda copia del `-5`.

---

## 3. Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Application/Calculos/ValidacionSeguimientoCalculos.cs` | **+** `DiaOperativo(DateTime instante)`: instante → `DateOnly` del día operativo, delegando en `VentanaFechaRegistroCalculos`. |
| `Infrastructure/Services/ValidacionSeguimiento/ValidacionSeguimientoService.cs` | `Hoy` pasa a día operativo. Los 4 casos de `LeerPendientesDelLoteAsync` convierten `CreatedAt` con el helper; `Fecha` queda igual. |
| `tests/ZooSanMarino.Application.Tests/ValidacionSeguimientoCalculosTests.cs` | Tests nuevos del helper y del invariante de dirección. |

**No se modifica:**
- **Frontend.** `shared/funciones/estado-validacion-seguimiento.funcion.ts` ya usa el día calendario
  **local del navegador**, que para un usuario en Panamá *es* el día operativo. El espejo ya era
  correcto; el backend era el desalineado. Este cambio los alinea, no los separa.
- **Base de datos.** La regla es de cálculo, no de esquema. **Sin migración.**

---

## 4. Reglas de negocio

Después del cambio, el plazo se enuncia sin ambigüedad:

> **Un registro guardado el día D (hora de la granja) se puede confirmar hasta el final del día D+1
> (hora de la granja).**

Y se conserva todo lo que ya estaba decidido:

- El **bloqueo no se toca**: cualquier vencido sin validar sigue bloqueando el alta de días nuevos
  (decisión del usuario, ratificada dos veces).
- Confirmar un registro vencido sigue permitido, y confirmarlo **destraba** el lote.
- Con el flag de empresa apagado, nada cambia: `AsegurarPuedeRegistrarDiaAsync` corta antes por
  `RequiereValidacionAsync`.
- Sin `created_at` (filas viejas sin auditoría) se cae al comportamiento previo, byte a byte.

---

## 5. La medición que eligió el diseño

El cambio tiene dos mitades que empujan en **direcciones opuestas**, así que no se podía asumir que
«corregir la zona horaria» fuera seguro por definición:

- `Hoy` en día operativo ⇒ `Hoy` es menor o igual ⇒ **afloja** siempre.
- `Creacion` en día operativo ⇒ el límite es menor o igual ⇒ **aprieta** para los cargados después de
  las 19:00.

Medido sobre la copia de producción, **ItalcolPanama, 60 días, 1.097 capturas manuales de engorde**:

| | Solo `Hoy` | `Hoy` + `Creacion` (elegida) |
|---|---:|---:|
| Registros que **aflojan** (ganan las 5 h robadas) | 1.097 | 1.097 |
| Registros que **aprietan** (pierden el día de regalo) | 0 | 309 |
| **Confirmaciones que se habrían perdido** | 0 | **0** |

**Los 309 que aprietan no le costaron nada a nadie:** en 60 días, *ninguna* confirmación cayó dentro
de la ventana de 19 h que el cambio elimina. El «día extra» que hoy reciben los registros cargados de
noche nunca se usó — es un efecto colateral del bug, no una tolerancia que la operación aproveche.

En la otra dirección, **5 registros se confirmaron dentro de las 5 horas que la regla actual les
robaba**: cinco veces el operario confirmó a tiempo según su reloj y el sistema igual dejó el lote
bloqueado. Ese es el daño que el cambio elimina.

Por eso se implementan **las dos mitades**: la versión completa es la semánticamente correcta, y la
medición muestra que su única desventaja teórica tiene costo real cero.

---

## 6. Casos de prueba

**Del helper (`DiaOperativo`):**

| # | Instante UTC | Día operativo esperado | Por qué |
|---|---|---|---|
| 1 | `2026-08-27 04:59Z` | `2026-08-26` | 23:59 local: todavía es ayer para el operario. |
| 2 | `2026-08-27 05:00Z` | `2026-08-27` | 00:00 local: recién ahí cambia el día. |
| 3 | `2026-08-27 23:59Z` | `2026-08-27` | 18:59 local: sigue siendo hoy. |
| 4 | `2026-08-28 00:00Z` | `2026-08-27` | **19:00 local** — el instante exacto del defecto. |

**De la regla completa (caso DAYLAND reproducido):**

| # | Escenario | Esperado |
|---|---|---|
| 5 | Creado 26-ago 12:44 local, se evalúa el 27-ago a las 19:00 local | `PENDIENTE` (hoy fallaba: `EN_RETRASO`) |
| 6 | Creado 26-ago 12:44 local, se evalúa el 27-ago a las 23:59 local | `PENDIENTE` |
| 7 | Creado 26-ago 12:44 local, se evalúa el 28-ago a las 00:00 local | `EN_RETRASO` — el plazo **no** es indefinido |
| 8 | Creado 26-ago 20:00 local (= 27-ago 01:00Z) | La creación es el **26**, no el 27 |

**Invariantes que no se pueden romper:**

| # | Invariante |
|---|---|
| 9 | Día operativo ≤ día UTC, **siempre** (el offset es negativo). |
| 10 | Con el flag apagado, todo idéntico: el helper ni se llama. |
| 11 | Sin `created_at`, comportamiento previo byte a byte. |
| 12 | Un registro cargado por anticipado no arranca con menos plazo (el `max` sigue mandando). |

---

## 7. Validación

1. `cd backend && dotnet build` — 0 errores, sin advertencias nuevas.
2. `dotnet test` — la suite completa en verde (gate de CI).
3. Recontar los 9 pendientes de DAYLAND con la fórmula nueva y confirmar que dentro de la ventana
   19:00–24:00 quedan `PENDIENTE` en vez de `EN_RETRASO`.
4. Verificar en la copia de producción que **ninguna empresa sin el flag** cambia de estado (el
   cálculo ni se ejecuta, pero se confirma con datos).
