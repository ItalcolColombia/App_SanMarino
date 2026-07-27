# Plan — Hora de encasetamiento: define el primer día con registro (engorde y reproductora)

**Fecha:** 2026-07-27

**Requerimiento:** al crear el lote se captura la **hora de llegada de las aves**. Esa hora decide si el
primer consumo va el mismo día del encasetamiento o el siguiente:

- Llegan el 27/07 **antes de las 13:00** → fecha de encasetamiento 27/07 y **primer consumo 27/07**.
- Llegan el 27/07 **desde las 13:00** → fecha de encasetamiento 27/07 (igual) pero **primer consumo 28/07**.

**Decisiones del usuario (27-jul-2026):**
1. **Hora de corte = 13:00.** `hora < 13:00` → mismo día; `hora >= 13:00` → día siguiente. No queda
   ninguna hora sin regla (la franja 12:00–12:59 va al mismo día).
2. **La edad NO se recorre.** Se sigue contando desde `fecha_encaset`: si el lote llega tarde el 27, el
   28 es **edad 1 (Día 2)**, no el Día 1. Lo único que cambia es **cuál es el primer día con registro
   válido**.

Consecuencia de (2), que es lo que hace este cambio barato y seguro: **no se toca ninguna función SQL**
(`fn_seguimiento_diario_engorde`, `fn_cruce_reproductora_a_engorde`, `fn_indicadores_pollo_engorde`,
`fn_informe_semanal_pollo_engorde` siguen derivando `edad_dia` de `fecha_encaset`), ni la guía genética,
ni los indicadores, ni el informe semanal.

---

## 1. Enfoque

Una sola pregunta pura: **¿cuál es el primer día con registro de este lote?**

```
esTardio       = hora.HasValue && hora >= 13:00
primerDia      = fechaEncaset + (esTardio ? 1 : 0)
edadMinima     = esTardio ? 1 : 0
```

`hora` es **opcional**. Sin hora (todos los lotes existentes y cualquier alta que no la informe) →
`esTardio = false` → comportamiento **idéntico al actual, byte a byte**. Retrocompatible por construcción,
sin backfill.

El cruce reproductora→engorde **no necesita cambios**: genera una edad solo cuando TODAS las
reproductoras tienen registro CONFIRMADO de esa edad. Si el lote llegó tarde nadie registra la edad 0,
así que la edad 0 simplemente no se genera. Se ajusta solo la validación de captura.

---

## 2. Archivos

### Backend

| Archivo | Cambio |
|---|---|
| `Domain/Entities/LoteAveEngorde.cs` | + `TimeOnly? HoraEncasetamiento` |
| `Domain/Entities/LoteReproductoraAveEngorde.cs` | + `TimeOnly? HoraEncasetamiento` |
| `Persistence/Configurations/…` (las 2) | mapeo `hora_encasetamiento` (`time`) |
| `Migrations/<ts>_AddHoraEncasetamiento…` | **NUEVA** — `ADD COLUMN IF NOT EXISTS … time NULL` en las 2 tablas |
| `Application/Calculos/EncasetamientoCalculos.cs` | **NUEVO** — puro: corte, primer día, edad mínima |
| `Application/Calculos/ReproductoraEngordeCalculos.cs` | `EsEdadSeguimientoValida(edad, dias, edadMinima = 0)` (parámetro opcional ⇒ llamadas actuales intactas) |
| `Services/SeguimientoDiarioLoteReproductoraService.cs` | Create + Update pasan la edad mínima del lote |
| `Services/Migracion/…SeguimientoReproductora.cs` | idem en la carga masiva |
| `Services/Migracion/…SeguimientoEngorde.cs` | fecha mínima = primer día (mensaje de error explica la hora) |
| DTOs de lote engorde y reproductora (create/update/detail/list) | la hora viaja al front |
| `tests/…/EncasetamientoCalculosTests.cs` | **NUEVO** |

### Frontend

| Archivo | Cambio |
|---|---|
| Form de creación de lote engorde | input `time` "Hora de encasetamiento" (opcional) |
| Form de creación de lote reproductora | idem |
| Servicios/tipos de esos dos módulos | `horaEncasetamiento` en el payload y en el modelo |
| `modal-seguimiento-reproductora` | `minFechaYmd` = primer día (hoy = encaset) |
| Componente padre que precalcula `defaultFecha` | arranca en el primer día |

---

## 3. Reglas de negocio

1. **Corte 13:00 inclusive**: `13:00:00` ya es tardío.
2. **Hora opcional**: sin hora se comporta como hoy. No se hace obligatoria para no romper la carga
   masiva de lotes ni las altas existentes.
3. **La fecha de encasetamiento NO cambia** en ningún caso: sigue siendo el día real de llegada.
4. **La edad sigue siendo `fecha − fecha_encaset`**. Un lote tardío arranca en edad 1.
5. **Reproductora**: la ventana válida sigue siendo `[edadMinima, 7]`. Con lote tardío, `[1, 7]` — que
   son exactamente los 7 días de recogida. Con lote temprano, `[0, 7]` como hoy.
6. **Engorde**: el primer registro manual no puede ser anterior al primer día; el mensaje de error dice
   por qué (hora de llegada) para que el usuario no crea que es un bug.

---

## 4. Casos de prueba (xUnit)

| # | Hora | Esperado |
|---|---|---|
| 1 | sin hora (null) | primer día = encaset, edad mínima 0 (comportamiento actual) |
| 2 | 06:00 | mismo día |
| 3 | 11:59 | mismo día |
| 4 | 12:00 | mismo día (la franja del mediodía NO es tardía) |
| 5 | 12:59 | mismo día |
| 6 | 13:00 | día siguiente (corte inclusive) |
| 7 | 18:30 | día siguiente |
| 8 | 23:59 | día siguiente |
| 9 | 00:00 | mismo día |
| 10 | tardío + fin de mes (31/01 18:00) | primer día = 01/02 |
| 11 | tardío + año bisiesto (28/02/2028 14:00) | primer día = 29/02/2028 |

Más: `EsEdadSeguimientoValida` con `edadMinima = 1` rechaza edad 0 y sigue aceptando 1..7; con el
default sigue aceptando 0..7 (regresión).

---

## 5. Validación

- `dotnet build` + `dotnet test` (todo verde, incluidos los nuevos)
- `yarn build` en el front
- Smoke: lote temprano (sin hora y con hora 09:00) → puede registrar el día del encaset;
  lote tardío (15:00) → el día del encaset se rechaza y el siguiente entra como edad 1.
