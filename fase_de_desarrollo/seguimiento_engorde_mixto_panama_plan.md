# Plan — Seguimiento pollo engorde MIXTO (Panamá): Excel mixto + descuento de aves mixtas

**Fecha:** 2026-07-27
**Origen:** en Panamá, una vez que el lote sale de reproductora **no se maneja por sexo**. El Excel de carga
masiva y la plantilla descargable siguen pidiendo consumo/mortalidad "H" y "M", y ninguna de las dos vías
(pantalla o carga masiva) descuenta las aves por mortalidad/selección.

**Decisiones del usuario (27-jul-2026):**
- El descuento impacta **las dos cosas**: movimiento de retiro MIXTO auditado + descuento en el maestro del lote.
- Aplica a **los dos caminos**: formulario diario en pantalla y carga masiva (la lógica vive en el servicio).

---

## 1. Enfoque arquitectónico

Dos ejes independientes, deliberadamente desacoplados:

| Eje | Qué decide | Cómo se decide | Por qué |
|---|---|---|---|
| **Presentación del Excel** | Si la plantilla/archivo habla de "Mixto" o de "H/M" | **Flag por empresa** `companies.seguimiento_engorde_mixto` | Es una preferencia de operación de la empresa (Panamá), no un hecho del dato. Patrón obligatorio de CLAUDE.md (`venta_engorde_peso_diferido` es el precedente). |
| **A qué bucket se descuenta** | Si las bajas salen de `mixtas` o de `hembras_l`/`machos_l` | **Datos del lote** (`mixtas > 0 && hembras_l == 0 && machos_l == 0`) | Un lote mixto es mixto en cualquier país. Fail-safe: si el lote tiene sexos, se comporta como hoy. Nada de `if (pais == X)`. |

Consecuencia: un lote mixto de Ecuador también descuenta bien, y Panamá puede tener un lote con sexos sin romperse.

### 1.1 Compatibilidad del Excel (no se rompe nada)

Los títulos "Consumo H (kg)", "Mort H", etc. **siguen siendo válidos**. Lo mixto entra como **alias** de la
misma columna, así que:

- Archivos viejos → cargan igual (byte a byte).
- Archivos nuevos con encabezados mixtos → cargan en el mismo campo.
- La **plantilla descargable** emite un juego u otro de títulos según el flag de la empresa.

---

## 2. Archivos a crear / modificar

### Backend — Fase A (Excel mixto)

| Archivo | Cambio |
|---|---|
| `Domain/Entities/Company.cs` | + `bool SeguimientoEngordeMixto` |
| `Infrastructure/Persistence/Configurations/CompanyConfiguration.cs` | mapeo `seguimiento_engorde_mixto` |
| `Migrations/<ts>_AddSeguimientoEngordeMixtoCompany.cs` | **NUEVA** — `ADD COLUMN IF NOT EXISTS ... boolean NOT NULL DEFAULT false` |
| `Migrations/<ts>_SeedSeguimientoEngordeMixtoPanama.cs` | **NUEVA** data-only — `UPDATE companies SET ... WHERE name='ItalcolPanama'` idempotente |
| `Application/Calculos/MigracionEsquemas.cs` | alias mixtos en las 6 columnas de género + esquema `SeguimientoPolloEngordeMixto` para la plantilla |
| `Application/DTOs/CompanyDto` + `CompanyService.ToDto` / `.Crud` / `CompanyResolver` / `CompanyPaisService` | el flag viaja al front (todas las proyecciones) |
| `Services/Migracion/Funciones/MigracionService.SeguimientoEngorde.cs` | la plantilla usa el esquema mixto y las instrucciones mixtas cuando el flag está ON |

**Alias mixtos** (todos apuntan a la columna "H", que es donde el sistema guarda el total):

| Columna real | Alias mixtos que se aceptan |
|---|---|
| `Mort H` | `mort mixta`, `mortalidad mixta`, `mortalidad mixtas` |
| `Sel H` | `sel mixta`, `seleccion mixta` |
| `Error Sexaje H` | `error sexaje mixta` |
| `Consumo H (kg)` | `consumo mixto (kg)`, `consumo mixto`, `consumo mixtas (kg)` |
| `Peso H (g)` | `peso mixto (g)`, `peso mixto` |
| `Uniformidad H` | `uniformidad mixta` |

### Backend — Fase B (descuento de aves mixtas)

| Archivo | Cambio |
|---|---|
| `Application/Calculos/RetiroAvesEngordeCalculos.cs` | **NUEVO** — lógica PURA: reparto del retiro y nuevo maestro |
| `Services/SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.Crud.cs` | Create/Update/Delete descuentan, compensan y revierten |
| `Services/CorreccionAvesDisponiblesEngordeService.cs` | la invariante suma las bajas de seguimiento |
| `tests/.../RetiroAvesEngordeCalculosTests.cs` | **NUEVO** — xUnit |

---

## 3. Reglas de negocio

### 3.1 Reparto del retiro (función pura)

Entrada: maestro del lote (`hembrasL`, `machosL`, `mixtas`) + bajas del día (`bajasH`, `bajasM`).
`bajas = mortalidad + selección + error de sexaje`, por sexo.

```
esMixto = mixtas > 0 && hembrasL == 0 && machosL == 0
si esMixto  → retiroX = bajasH + bajasM ; retiroH = retiroM = 0
si no       → retiroH = bajasH ; retiroM = bajasM ; retiroX = 0
```

**Clamp obligatorio:** el nuevo maestro nunca baja de 0 (`Math.Max(0, …)`), y se reporta cuánto se pudo
descontar realmente. Un día que mate más aves de las disponibles **no bloquea** la carga (dato histórico) pero
deja el maestro en 0 y una observación en el movimiento.

### 3.2 Efectos del Create / Update / Delete

| Operación | Maestro `lote_ave_engorde` | Histórico unificado |
|---|---|---|
| Create | resta el retiro del día | fila `tipo_evento = 'BAJA_SEGUIMIENTO'` con H/M/X |
| Update | resta (nuevo − viejo); si es negativo, devuelve aves | actualiza la fila del día |
| Delete | devuelve el retiro | marca la fila `anulado = true` |

### 3.3 Riesgos identificados y cómo se cubren

1. **Doble descuento en `saldo_aves`.** `fn_seguimiento_diario_engorde` calcula
   `aves_iniciales` desde `aves_encasetadas`, **salvo** en la rama `suma_hm > 0 AND aves_encasetadas = 0`
   ([apply_fn_seguimiento_diario_engorde.sql:99-112](../backend/sql/apply_fn_seguimiento_diario_engorde.sql)),
   donde usa el maestro. En esa rama, descontar el maestro por mortalidad haría que el saldo baje **dos veces**.
   → **Mitigación:** en esa rama la inicial pasa a `suma_hm + bajas_seguimiento` (reconstruye el encaset), que
   es equivalente al comportamiento actual cuando no hay bajas. Migración SQL idempotente de la función.
2. **Conservación de `CorreccionAvesDisponiblesEngordeService`.** Hoy exige
   `maestro = iniciales − ventas − ajustes`; con el descuento pasaría a drift negativo y reportaría
   "posible sobre-descuento" en todos los lotes. → se suma el término `bajasSeguimiento`.
3. **Retrocompatibilidad de lotes históricos.** No se hace backfill: los lotes ya cargados conservan su maestro.
   El descuento aplica solo a los seguimientos creados a partir del deploy. Se documenta en el tracker.

---

## 4. Casos de prueba

### 4.1 xUnit — `RetiroAvesEngordeCalculosTests`

| # | Caso | Esperado |
|---|---|---|
| 1 | Lote con sexos (500 H / 300 M), bajas 5 H y 3 M | H−5, M−3, X igual (comportamiento actual) |
| 2 | Lote mixto (0/0/800), bajas 30 en H | X−30, H y M en 0 |
| 3 | Lote mixto, bajas repartidas H=20 M=10 | X−30 |
| 4 | Lote mixto con bajas > disponibles (X=10, bajas 30) | X=0, retiro efectivo 10, marca `Insuficiente` |
| 5 | Bajas en 0 | maestro intacto, sin movimiento |
| 6 | Lote con sexos y mixtas > 0 (híbrido) | NO es mixto → camino por sexo (fail-safe) |
| 7 | Update que baja la mortalidad | devuelve aves al maestro |
| 8 | Delete | revierte exactamente el retiro registrado |

### 4.2 Esquemas de migración

- Alias mixtos resuelven a la misma columna que los títulos "H".
- Archivo con encabezados viejos → 0 desconocidos (regresión).
- Archivo con encabezados mixtos → 0 desconocidos.
- Archivo que mezcla `Consumo H (kg)` y `Consumo Mixto (kg)` → encabezado duplicado (advertencia, gana el primero).

### 4.3 Smoke

- Empresa con flag **OFF** (Sanmarino/Demo): plantilla descargada idéntica a la de hoy.
- Empresa con flag **ON** (ItalcolPanama): plantilla con columnas mixtas y sin columnas por sexo.
- Carga masiva de un día en lote mixto → `consumo_kg_hembras` con el total, `qq_mixtas` informativo,
  maestro `mixtas` descontado por mortalidad + selección, fila en el histórico unificado.

---

## 5. Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas)
- `cd backend && dotnet test` (todo verde, incluidos los tests nuevos)
- `cd frontend && yarn build` si se toca el front (solo si el flag debe viajar a la UI)
- Sin procesos huérfanos al terminar.
