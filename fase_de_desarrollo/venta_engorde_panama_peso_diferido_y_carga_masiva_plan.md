# Plan — Venta Pollo Engorde: peso diferido en Panamá + carga masiva completa

**Fecha:** 2026-07-26 · **Módulos:** `movimientos-pollo-engorde` (ventas) · `migraciones-masivas` (carga masiva)

## Objetivo (pedido del usuario)

**(A) Carga masiva de ventas de pollo engorde con TODOS los campos del formulario real**, incluida la aplicación del peso.
Hoy la plantilla tiene 11 columnas contra ~16 campos del formulario, y la función SQL no persiste ninguno de los campos de despacho.

**(B) En Panamá, el peso báscula (bruto/tara) deja de ser obligatorio al registrar la venta** — ese dato llega al día siguiente.
La venta se registra sin peso y queda `Pendiente`; **al confirmarla se abre un modal de registro de peso**, se hace el UPDATE de esos campos y se ejecuta el cálculo correspondiente (neto, promedio por ave y prorrateo entre los lotes del despacho).

### Decisiones tomadas por el usuario (2026-07-26)

1. **Carga masiva multi-lote**: un archivo puede traer varios lotes; se agrupa por `N° Despacho` en una factura y el peso global se prorratea entre lotes, igual que una venta hecha por pantalla.
2. **Corregir la idempotencia** de `fn_migracion_venta_engorde` (comparar fecha por rango de día + incluir `numero_despacho` en la clave).
3. **Peso diferido en ambos sentidos**: el modal carga peso + confirma en una transacción, **y** permite corregir el peso de una venta ya `Completada` cuando la báscula llega tarde.

---

## 1. Enfoque arquitectónico

### 1.1 Principio rector: el peso entra ANTES de la transición de estado

`Pendiente` pasa a significar, en las empresas con el flag, **"venta registrada, esperando báscula"**. El modal de peso se dispara en la confirmación y escribe el peso **dentro de la misma transacción** que hace `Pendiente → Completado`.

Esto es lo que hace el cambio quirúrgico en vez de invasivo:

- **No se toca ningún gate de estado**: ni `"Solo se pueden editar movimientos en estado Pendiente."` ([Crud.cs:395](../backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs:395)) ni `"Solo se pueden completar movimientos en estado Pendiente."` ([Crud.cs:713](../backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs:713)).
- **Nunca existe un `Completado` sin peso** ⇒ el detector `MOV_SIN_PESO` (severidad crítico) de `fn_auditoria_liquidacion_engorde.sql:170-186` filtra por `estado='Completado'` y sigue intacto.
- Los reportes de liquidación e indicadores filtran por estado ⇒ **cero exposición** mientras la venta está `Pendiente`.

### 1.2 El camino "sin peso" del backend YA existe

[`MovimientoPolloEngordePanamaService.cs:70-77`](../backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordePanama/MovimientoPolloEngordePanamaService.cs:70) ya contempla `tienePeso == false` y arma un array de prorrateo con nulls. **Lo único que lo bloquea es la validación de la línea 65.** No hay NOT NULL ni CHECK en BD: las 9 columnas de peso son `double precision` NULL ([MovimientoPolloEngordeConfiguration.cs:62-70](../backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoPolloEngordeConfiguration.cs:62)) ⇒ **(B) no requiere DDL sobre `movimiento_pollo_engorde`**.

### 1.3 El re-prorrateo también existe y es reusable tal cual

[`ReprorratearPesoTrasEdicionAsync` (Crud.cs:445-504)](../backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs:445) ya:

- carga todas las líneas vivas de la factura (`FacturaId`, `DeletedAt == null`, `Estado != "Cancelado"`);
- llama `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea` (3 decimales + residuo a la línea con más aves);
- escribe los **9 campos** de peso en cada línea;
- lo hace **a propósito sobre líneas hermanas ya Completadas** — comentario textual en `Crud.cs:441`: *"el peso no afecta saldos de aves, solo liquidación"*.

Es exactamente el núcleo que necesita (B). Lo único que falta es un camino que lo invoque **sin pasar por `UpdateAsync`** (que corta antes con el gate de estado).

### 1.4 Regla de empresa, no de país

Flag nuevo **`venta_engorde_peso_diferido`** (`companies`, `boolean NOT NULL DEFAULT false`), front `ventaEngordePesoDiferido`. Nombra el **comportamiento**, no el tenant.

- ⛔ Prohibido `es_panama` / `if (paisActivo == "PANAMA")` (anti-patrón vivo en `FarmService.cs:986`) y el patrón `AutoNombrePorCorrida` (el front decide y el back obedece).
- Resolución backend **por datos y fail-closed**: `farms.company_id` de la granja de cabecera del despacho (`dto.GranjaOrigenId`, ya presente en `MovimientoPolloEngordePanamaService.cs:95`). Si no resuelve → `false` ⇒ peso obligatorio (comportamiento actual).
- Front: `ActiveCompanyConfigService` (caché 5 min, fail-closed). **No** `CountryFilterService.isPanama()`, que es fail-**open** y decide por país.
- Default `false` ⇒ Colombia / Ecuador / Demo / Sanmarino sin un solo cambio observable.

---

## 2. Trampas verificadas (leídas en el código, no supuestas)

| # | Trampa | Evidencia | Mitigación |
|---|---|---|---|
| 1 | 🔴 **El trigger del espejo NO escucha `peso_bruto`/`peso_tara`.** La lista es `UPDATE OF cantidad_*, peso_neto, peso_tara_real, promedio_peso_ave, fecha_movimiento, tipo_movimiento, numero_despacho, ...origen` | `create_lote_registro_historico_unificado.sql:266-269` | El UPDATE de confirmación **debe** escribir `peso_neto` (+ `peso_tara_real`, `promedio_peso_ave`) en la misma sentencia, o el espejo queda en 0 kg para siempre. `ReprorratearPesoTrasEdicionAsync` ya los escribe los tres. |
| 2 | 🔴 **Idempotencia rota en la carga masiva**: la fn compara `m.fecha_movimiento = f.fecha::timestamptz` (medianoche) y la UI graba **mediodía UTC** (`ymdToIsoUtcNoon`) | `fn_migracion_venta_engorde.sql:60` vs `mapear-venta-panama-dto.funcion.ts:75` | Comparar por **rango de día** + sumar `numero_despacho` a la clave (decisión 2 del usuario). |
| 3 | 🔴 **La carga masiva descuenta el sexo equivocado en Panamá**: resta de `hembras_l`/`machos_l`/`mixtas` y nunca marca `es_venta_mixta`, mientras `CompleteAsync` descuenta H/M y **fuerza `Mixtas = 0`** | `fn_migracion_venta_engorde.sql:103-108` vs [Crud.cs:718-727](../backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs:718) | Columna nueva `Venta sobre mixtas` + descuento espejado en plpgsql. |
| 4 | El seguimiento diario / informe semanal Panamá leen el **espejo** filtrando `tipo_evento='VENTA_AVES' AND NOT anulado`, **sin mirar `estado`**, y el trigger inserta ya en el INSERT | `fn_seguimiento_diario_engorde.sql:324-327`, `fn_informe_semanal_pollo_engorde.sql:129,150` | Exposición real y acotada: el día muestra aves despachadas con 0 kg hasta que llegue la báscula. **Se auto-corrige** por el `ON CONFLICT ... DO UPDATE` del trigger al escribir `peso_neto`. Se mitiga con marca visual "peso pendiente" en el listado. |
| 5 | **Editar una venta sin peso sería imposible**: el modal genérico repone `Validators.required` porque `tipoMovimiento === 'Venta'` | `modal-movimiento-pollo-engorde.component.ts:328-341` (`syncPesoValidators`) | Gatear ese `required` con el mismo flag. |
| 6 | El mensaje de error del modal Panamá asume que la única causa de invalidez es el peso (*"Complete la fecha del despacho."*) | `modal-venta-panama.component.ts:214-219` | Reescribir esa rama al quitar el `required`. |
| 7 | **Bug preexistente que el flag hereda**: el form de Config→Empresas no manda los flags y `CompanyService.Crud.cs:110-114` los pisa con el default `false` del record ⇒ **editar ItalcolPanama apagaría el peso diferido en silencio** | `company-management.component.ts:419-430` | Agregar el flag al form de empresas (junto con los 5 existentes, que hoy sufren lo mismo). |
| 8 | `OrganizarPeso` **no sirve** para (B): su universo excluye justo las ventas sin peso (`&& (m.PesoBruto != null \|\| m.PesoTara != null)`), es masivo por granja y el front lo llama con `reprocesarTodo:true` | [OrganizarPeso.cs:57](../backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.OrganizarPeso.cs:57) | Endpoint nuevo por factura. Se documenta la limitación de `OrganizarPeso`. |
| 9 | El `ConfirmationModalComponent` del listado ya está multiplexado con un ternario anidado sobre 5 banderas | `movimientos-pollo-engorde-list.component.html:615` | El modal de peso es **componente propio**, no una 6ª bandera. |
| 10 | ⛔ **Nunca cargar peso por movimiento individual**: `PesoBruto`/`PesoTara` son el peso GLOBAL del camión clonado en cada fila | `MovimientoPolloEngordeConfiguration.cs:58-61` | El modal opera sobre la **fila agrupada de despacho** (key `facturaId`, `agrupar-despachos.funcion.ts:45-48`). Hacerlo línea a línea reproduce el daño que `OrganizarPeso` vino a reparar. |
| 11 | Angular 22: omitir `changeDetection` = OnPush ⇒ modal colgado en "Cargando…" con HTTP 200 | CLAUDE.md | El modal nuevo lleva `ChangeDetectionStrategy.Eager` **explícito**. Probar abriendo y cerrando dos veces. |
| 12 | La venta Panamá **no pasa por `CreateAsync`** (arma la entidad a mano) | `MovimientoPolloEngordePanamaService.cs:90-131` | Todo campo nuevo hay que agregarlo en **ambos** caminos. |
| 13 | `dotnet-ef` global es 9.0.9 y el proyecto es EF Core 10 | memoria del repo | Usar `~/.dotnet/tools-ef10/dotnet-ef.exe`. |

---

## 3. (A) Carga masiva de ventas — diseño

### 3.1 Plantilla: columnas nuevas

Todas **`Requerida: false`** ⇒ un archivo con las 11 columnas viejas sigue siendo válido (retro-compatibilidad, con test de gate).

**Bloque de ubicación (nuevo, al principio — espejo de `SeguimientoPolloEngorde`):**

| Columna | Alias | Parseo | Destino |
|---|---|---|---|
| `Granja` | `nombre granja` | `TextoLimpio` | resolución de lote (no se persiste) |
| `Núcleo` | `nombre nucleo` | `TextoLimpio` | ídem |
| `Galpón` | `nombre galpon` | `TextoLimpio` | ídem |
| `Lote` | `nombre lote` | `TextoLimpio` | `lote_ave_engorde_origen_id` — vacío ⇒ `ctx.LoteId` |

**Bloque existente (11 columnas) — sin cambios de nombre.** Único ajuste: `Peso Bruto (kg)` / `Peso Tara (kg)` pasan de `DobleOpc` (hoy **acepta negativos**, `VentaEngorde.cs:41-42`) a `DobleNoNeg`, espejo del `Validators.min(0)` del front. Cambio de comportamiento acotado y declarado: rechaza lo que hoy entra mal.

**Bloque de despacho (nuevo, al final):**

| Columna | Alias | Parseo | Destino |
|---|---|---|---|
| `N° Despacho` | `numero despacho`, `despacho` | `TextoLimpio` (50) | `numero_despacho` **+ clave de agrupación de factura** |
| `Total Pollos Galpón` | `total pollos` | `EnteroNoNegNull` | `total_pollos_galpon` |
| `Hora Salida` | `hora` | **`TryHora` (nuevo)** | `hora_salida` (`time` / `TimeOnly?`) |
| `Guía Agrocalidad` | `guia` | `TextoLimpio` (100) | `guia_agrocalidad` |
| `Sellos` | — | `TextoLimpio` (500) | `sellos` |
| `Ayuno` | — | `TextoLimpio` (50) | `ayuno` |
| `Cliente / Conductor` | `cliente`, `conductor` | `TextoLimpio` (200) | `conductor` (mismo campo, dos rótulos en la UI) |
| `Planta Destino` | `planta` | `TextoLimpio` | `planta_destino` |
| `Descripción` | — | `TextoLimpio` | `descripcion` |
| `Estado` | — | `Opciones: ["Completado","Pendiente"]`, default `Completado` | `estado` — habilita cargar ventas Panamá **en espera de báscula** |
| `Venta sobre mixtas` | `es venta mixta`, `panama` | booleano `Sí/No`, default `No` | `es_venta_mixta` — corrige el descuento Panamá |

**Derivados — NO son columnas** (los calcula la fn): `peso_neto`, `promedio_peso_ave`, `peso_*_global`, `peso_bruto_real`, `peso_tara_real`, `factura_id`, `numero_movimiento`. `aves_sobrante` no se migra (es resultado de la validación de disponibilidad, que la fn no hace).

### 3.2 Agrupación multi-lote (decisión 1)

Dentro de `p_rows`, las filas se agrupan por **`(numero_despacho, fecha, granja)`**. Por cada grupo:

- un `gen_random_uuid()` → `factura_id` compartido;
- `peso_bruto_global` / `peso_tara_global` / `peso_neto_global` = el peso del grupo;
- `peso_bruto_real` / `peso_tara_real` / `peso_neto` / `promedio_peso_ave` **prorrateados por aves de cada línea**, espejando `ProrratearPesoPorLinea`: **redondeo a 3 decimales** (no 2) y residuo a la línea con más aves.
- Filas **sin** `N° Despacho` ⇒ cada una es su propio grupo (comportamiento actual: venta suelta, sin `factura_id`).

### 3.3 Idempotencia (decisión 2)

Clave nueva: `company + lote + tipo 'Venta' + cantidades + COALESCE(numero_despacho,'') + fecha POR RANGO DE DÍA`.

Rango de día en vez de igualdad exacta: reconoce las ventas que la UI graba a **mediodía UTC**. Efecto colateral aceptado por el usuario: dos despachos legítimos del mismo lote/fecha/cantidades **dejan de colapsar** en uno (hoy el segundo se perdía en silencio).

### 3.4 Descuento del lote

- `Estado = 'Pendiente'` ⇒ **no se descuenta** (lo hará `CompleteAsync`).
- `Estado = 'Completado'` ⇒ se descuenta una vez, y con `es_venta_mixta = true` se espeja `CompleteAsync`: `mixtas = GREATEST(0, mixtas - (h+m))` y `mixtas = 0` forzado, **sin** tocar `hembras_l`/`machos_l`.

---

## 4. (B) Peso diferido — diseño

### 4.1 Endpoint nuevo

```
POST /api/MovimientoPolloEngorde/factura/{facturaId:guid}/registrar-peso
body { pesoBruto: double, pesoTara: double, confirmar: bool }
```

Semántica, en una transacción:

1. Cargar todas las líneas vivas de la factura (`FacturaId`, `CompanyId`, `DeletedAt == null`, `Estado != "Cancelado"`).
2. Validar `bruto > 0`, `tara >= 0`, `bruto >= tara` (cálculo puro).
3. Re-prorratear y escribir los 9 campos en cada línea, reutilizando el núcleo de `ReprorratearPesoTrasEdicionAsync` — **incluye `peso_neto`, `peso_tara_real` y `promedio_peso_ave`, que son los que disparan el trigger del espejo** (trampa #1).
4. Si `confirmar == true` → `CompletarBatchAsync` de las líneas que sigan `Pendiente`.

**Cubre los dos casos de la decisión 3:** `confirmar:true` sobre una factura `Pendiente` (flujo normal) y `confirmar:false` sobre una factura ya `Completada` (báscula tardía). El segundo tiene precedente explícito en el código (`Crud.cs:441`).

**Permisos:** el camino confirmar reusa `confirmar_despacho` (la acción *es* la confirmación, y evita un alta manual de `role_menus` en prod). La corrección sobre una venta ya `Completada` exige el permiso de edición del módulo. Ningún endpoint del módulo tiene hoy políticas más allá de `[Authorize]`; esto no relaja nada.

### 4.2 Cálculo puro

Extender la firma existente en vez de crear una función paralela:

```csharp
public static void ValidarPesoObligatorioEnVenta(
    string? tipoMovimiento, double? pesoBruto, double? pesoTara,
    bool pesoDiferidoPermitido = false)
```

- `pesoDiferidoPermitido: true` **y ambos null** → no lanza.
- **Un solo** peso presente, o valores inválidos → sigue lanzando **los mismos mensajes literales**.
- El default `false` deja los 6 tests existentes verdes sin tocarlos y no cambia `Crud.cs:27` ni `VentaGranja.cs:59`.

Único call-site a modificar: `MovimientoPolloEngordePanamaService.cs:65` (conservando el literal `"Venta"`).

### 4.3 Frontend

- **Modal nuevo** `modal-registro-peso-venta` (componente propio, `ChangeDetectionStrategy.Eager` explícito): bruto, tara, neto y promedio por ave calculados en vivo, y **preview del prorrateo por lote** antes de confirmar.
- Se dispara desde el botón de confirmar del listado **cuando la fila agrupada no tiene peso y el flag está ON**; si ya tiene peso, la confirmación sigue como hoy.
- Acción adicional "Registrar/corregir peso" sobre despachos ya `Completado` sin peso.
- Marca visual **"peso pendiente"** en la fila del listado (trampa #4).
- `modal-venta-panama`: quitar `Validators.required` de bruto/tara cuando el flag está ON y reescribir el mensaje de error (trampa #6).
- `modal-movimiento-pollo-engorde`: gatear `syncPesoValidators` con el mismo flag (trampa #5).
- Flag leído con `ActiveCompanyConfigService` (fail-closed).

---

## 5. Archivos a crear / modificar

### Backend — Application
- `Calculos/MovimientoPolloEngordeCalculos.cs` — parámetro `pesoDiferidoPermitido` (M)
- `Calculos/MigracionCalculos.cs` — `TryHora` (M)
- `Calculos/MigracionEsquemas.cs:174-187` — 15 columnas nuevas (M)
- `DTOs/RegistrarPesoFacturaDto.cs` (C) · `DTOs/CompanyDto.cs`, `CreateCompanyDto.cs`, `UpdateCompanyDto.cs` (M)
- `Interfaces/IMovimientoPolloEngordeService.cs` (M)

### Backend — Domain / Infrastructure
- `Entities/Company.cs` + `Configurations/CompanyConfiguration.cs` — flag (M)
- `Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.RegistrarPeso.cs` (C, `partial`)
- `Services/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs` — extraer el núcleo reusable del re-prorrateo (M, sin cambio de comportamiento)
- `Services/MovimientoPolloEngordePanama/MovimientoPolloEngordePanamaService.cs:65` — resolver flag y delegar (M)
- `Services/Migracion/Funciones/MigracionService.VentaEngorde.cs` — parser multi-lote + campos nuevos (M)
- `Services/CompanyService/CompanyService.cs`, `.../Funciones/CompanyService.Crud.cs`, `CompanyResolver.cs` (2 sitios), `CompanyPaisService.cs` — propagación del flag (M)

### Backend — API
- `Controllers/MovimientoPolloEngordeController.cs` — endpoint nuevo (M)

### Backend — SQL / migraciones
- `backend/sql/fn_migracion_venta_engorde.sql` — fuente canónica actualizada (M)
- Migración `CREATE OR REPLACE FUNCTION fn_migracion_venta_engorde` (C) — ⛔ **jamás** editar `20260712190000_AddFnMigracionVentaEngorde.cs`, ya aplicada en prod
- Migración `AddVentaEngordePesoDiferidoCompany` — `ADD COLUMN IF NOT EXISTS`, idempotente (C)
- Migración data-only `SeedVentaEngordePesoDiferido` — ItalcolPanama, `IS DISTINCT FROM`, Designer clonado y **ModelSnapshot intacto** (C)

### Frontend
- `features/movimientos-pollo-engorde/components/modal-registro-peso-venta/` (C, 3 archivos)
- `funciones/prorateo-peso.funcion.ts` — reuso para el preview (M si hace falta)
- `pages/movimientos-pollo-engorde-list/` — disparo del modal, marca "peso pendiente" (M)
- `components/modal-venta-panama/` — required condicional + mensaje (M)
- `components/modal-movimiento-pollo-engorde/` — `syncPesoValidators` gateado (M)
- `services/movimiento-pollo-engorde.service.ts` — método nuevo (M)
- `core/services/company-config/active-company-config.service.ts` — flag (M)
- `features/config/.../company-management.component.ts` — flags en el payload (M, arregla trampa #7)

---

## 6. Casos de prueba

### xUnit — cálculo puro (gate CI)
1. `ValidarPesoObligatorioEnVenta` flag **OFF** + sin peso → lanza, **mensaje byte a byte idéntico** (`..._ComportamientoActualIntacto`).
2. Flag **ON** + ambos null → no lanza.
3. Flag ON + **solo bruto** / **solo tara** → lanza (peso parcial es error en ambos modos).
4. Flag ON + `bruto <= 0` / `tara < 0` / `bruto < tara` → mismos mensajes.
5. `TryHora`: serial fraccionario de Excel (0–1), `DateTime`, `"HH:mm"`, `"H:mm:ss"`, `"hh:mm tt"`, basura → `false`.
6. Prorrateo tras peso diferido == prorrateo del alta, para el mismo bruto/tara/aves (equivalencia exacta, 3 decimales y residuo).
7. `MigracionEsquemas`: archivo con las **11 columnas viejas** sigue siendo válido.

### Smoke API local (JWT minteado)
8. Panamá flag ON: crear venta **sin peso** → 200, N filas `Pendiente`, peso NULL, `factura_id` compartido.
9. `registrar-peso` con `confirmar:true` → todas `Completado`, 9 campos poblados, suma de netos == neto global, aves descontadas **de mixtas** una sola vez.
10. **Espejo**: `lote_registro_historico_unificado` pasa de 0 kg al neto correcto tras el UPDATE (verifica la trampa #1 de punta a punta).
11. `registrar-peso` con `confirmar:false` sobre factura ya `Completada` → peso actualizado, estado y saldos de aves **sin cambios**.
12. Flag **OFF** (Demo/Sanmarino): crear venta sin peso → **400 con el mensaje actual, idéntico**.
13. Carga masiva: archivo con las 11 columnas viejas → mismo resultado que hoy.
14. Carga masiva multi-lote: 3 lotes, mismo `N° Despacho` → 1 `factura_id`, peso prorrateado, suma == global.
15. Carga masiva `Estado='Pendiente'` → no descuenta; confirmar después descuenta una sola vez.
16. Carga masiva `Venta sobre mixtas=Sí` → descuenta de `mixtas`, `mixtas=0`, no toca `hembras_l`/`machos_l`.
17. **Idempotencia**: re-cargar el mismo archivo → 0 insertados; cargar por Excel un día ya vendido por pantalla (mediodía UTC) → **0 insertados, sin doble descuento**.
18. Dry-run no persiste nada.

### Smoke UI (dev server, sesión inyectada)
19. Panamá: registrar venta sin peso; el modal de peso aparece al confirmar; preview del prorrateo; confirmar apaga el spinner **en pantalla** (abrir y cerrar el modal **dos veces**).
20. Editar una venta sin peso desde el modal genérico → posible (trampa #5).
21. Marca "peso pendiente" visible en el listado.
22. Empresa con flag **OFF** → cero cambios visibles, peso sigue obligatorio.
23. Consola sin errores, sin NG0103.

### Validación de build
24. `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test` (suite completa verde).
25. `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
26. Servidores detenidos y BD local restaurada al terminar.
