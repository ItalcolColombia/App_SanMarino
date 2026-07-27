# Plan — Tab «Huevos» en Seguimiento Diario Levante (semana 14+) y arrastre automático a Producción al liquidar

> **Requerimiento del usuario (verbatim, resumido):** el seguimiento diario de **levante** debe tener, **a partir de la semana 14**, un tab «Huevos» igual al de **producción** (clasificación de huevos por día). Al **liquidar el levante**, todos los huevos registrados en levante pasan **automáticamente al primer registro de producción** (la fecha de liquidación). Si ese mismo día el usuario hace su seguimiento diario de producción, lo que registre se **SUMA** a lo que ya venía de levante — hoy la lógica no deja crear un segundo registro el mismo día, y **en este caso particular tiene que cambiar**.
>
> Ejemplo del usuario: liquido el levante el 24 de febrero → ese día nace producción → el primer registro de producción (24-feb) trae todos los huevos de levante; si además hacen seguimiento ese 24-feb, lo que digiten se suma por tipo de huevo sobre lo que ya estaba.

**Decisiones confirmadas con el usuario (26-jul-2026):**
1. **Alcance = flag por empresa** → `companies.captura_huevos_en_levante` (default `false`). OFF ⇒ comportamiento byte a byte idéntico al actual.
2. **Reportes/indicadores de LEVANTE no muestran huevos en esta fase** (fase 2 opcional; las fns SQL de levante hoy no mencionan huevos y no hay guía genética de huevos antes de la semana 26).
3. El campo **«Huevos iniciales (al cerrar)»** del modal de cierre pasa a **readonly** mostrando el **total real calculado** desde los seguimientos de levante, y ese total es el que se arrastra.

---

## 1. Hallazgo que define la arquitectura

**Las 13 columnas de huevos YA EXISTEN en la tabla de levante.** Levante y producción comparten la tabla unificada `seguimiento_diario_levante` (entidad `SeguimientoDiario`, discriminador `tipo_seguimiento`), y su configuración ya mapea todo:

```csharp
// backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoDiarioConfiguration.cs
b.ToTable("seguimiento_diario_levante", "public");                     // línea 12
b.Property(x => x.HuevoTot).HasColumnName("huevo_tot");                // 60  ── "// Solo producción"
b.Property(x => x.HuevoInc).HasColumnName("huevo_inc");                // 61
b.Property(x => x.HuevoLimpio).HasColumnName("huevo_limpio");          // 62 … 72 (11 categorías)
b.Property(x => x.PesoHuevo).HasColumnName("peso_huevo").HasColumnType("double precision");  // 73
```

**El corte está en el mapper del módulo levante**, que manda 15 `null` explícitos:

```csharp
// SeguimientoLoteLevante/Funciones/SeguimientoLoteLevanteService.Mapeos.cs:104-118 y 174-188
HuevoTot: null, HuevoInc: null, HuevoLimpio: null, HuevoTratado: null, HuevoSucio: null,
HuevoDeforme: null, HuevoBlanco: null, HuevoDobleYema: null, HuevoPiso: null, HuevoPequeno: null,
HuevoRoto: null, HuevoDesecho: null, HuevoOtro: null, PesoHuevo: null, Etapa: null,
```

⇒ **NO hace falta migración de columnas de huevos.** El trabajo es: destapar el mapper, poner el gate de semana 14, replicar la UI, y construir el arrastre.

⚠️ `SeguimientoLoteLevante.cs` (entidad) y `seguimiento_lote_levante_deprecated` son **letra muerta** (sin DbSet). No tocarlas.

---

## 2. Fórmula canónica de semana de vida (el gate)

```csharp
// backend/src/ZooSanMarino.Application/Calculos/MovimientoAvesCalculos.cs:20-24 — CANÓNICA
public static int SemanaDesdeEncaset(DateTime fecha, DateTime fechaEncaset)
{
    var diasDesdeEncaset = (fecha.Date - fechaEncaset.Date).Days;
    return (diasDesdeEncaset / 7) + 1;
}
```
Equivalente SQL usado por todas las fns de indicadores (corte de día en **America/Bogota**):
```sql
(floor((( (sl.fecha AT TIME ZONE 'America/Bogota')::date - v_enc_date ) / 7.0))::int) + 1
```

**El día del encaset es la SEMANA 1** (contrato fijado por `MovimientoAvesCalculosTests`: `(0,1) (6,1) (7,2) (13,2) (14,3)`).
⇒ **SEMANA 14 ⟺ `dias >= 91`** (13 × 7).

**Fuente de la fecha base:** `lotes.fecha_encaset` (ya cargada en `CreateAsync`/`UpdateAsync` como `lote.FechaEncaset`). **Fail-closed**: `null` ⇒ no se permiten huevos.

⛔ **Prohibido** usar para el gate: `lote_postura_levante.edad` / `lote_postura_produccion.edad` (`floor(dias/7)` escrito **sólo al crear**, estancado), `LotePosturaLevanteService.EdadMaximaSeguimiento` (`Math.Floor(dias/7.0)`, 1 semana menos) ni los `Math.Ceiling(diff/7)` del front de producción.

---

## 3. Diseño

### 3.1 Persistencia de los huevos de levante
**Las 13 columnas existentes de `seguimiento_diario_levante`** (nullable). Cero DDL. Los consumidores de huevos de producción filtran `tipo_seguimiento = 'produccion'` o leen la otra tabla ⇒ **cero doble conteo y cero regresión** en `fn_indicadores_levante_postura` / `fn_reporte_semanal_levante_extras` (no mencionan `huevo_*`).

### 3.2 Aritmética (idéntica a producción, sin reinventar)
```
HuevoInc = huevoLimpio + huevoTratado
HuevoTot = HuevoInc + sucio + deforme + blanco + dobleYema + piso + pequeno + roto + desecho + otro
```
Se extrae a cálculo puro y la usan **el front, el gate y el arrastre** (una sola fuente).

**Fuera de alcance:** el modo «clasificación por ítems del catálogo» (`companies.clasificacion_huevo_por_items`, Santa Reyes). Se implementa el clasificador **clásico de 11 columnas** (el del modal que mandó el usuario). Guarda explícita: si una empresa tuviera **ambos** flags en ON, el tab de levante no se muestra y el backend rechaza los huevos con mensaje claro (fail-closed, evita datos mal formados en silencio).

### 3.3 Gate «semana >= 14» — backend autoritativo, front cosmético
- **Backend** (`SeguimientoLoteLevanteService.Crud.cs`, Create **y** Update, después de `EnsureLoteLevanteAbiertoAsync`): si llega algún huevo `> 0` y no procede ⇒ `InvalidOperationException`. Si el flag de empresa está OFF ⇒ los huevos se **neutralizan a null** (comportamiento actual intacto).
- **Empresa efectiva por datos, fail-closed**: `farms.company_id` de la granja del lote (patrón del CLAUDE.md), **no** `_current.CompanyId` a secas.
- **Front**: el tab «Huevos» se muestra sólo si `capturaHuevosEnLevante && semana(fechaRegistro) >= 14`, **reactivo al control `fechaRegistro`** (no contra «hoy»).

### 3.4 Arrastre al liquidar
**Destino: UNA fila en `seguimiento_diario_produccion` con `fecha_registro` = fecha de inicio de producción.** Es la única tabla que alimenta `EspejoHuevoProduccionSyncService` (⇒ traslados de huevos y saldos correctos) y la que leen los indicadores/reportes de producción.

Servicio nuevo `IArrastreHuevosLevanteService`:
1. Suma los huevos de levante (`tipo_seguimiento='levante'` del lote) — **en la BD**, no en memoria.
2. Total 0 ⇒ **no crea nada**.
3. Busca la fila del día:
   - **no existe** → INSERT (huevos + `LotePosturaProduccionId` + `CompanyId` + auditoría, mortalidad/consumo en 0, `Observaciones = "Huevos arrastrados del levante"`).
   - **ya existe** (traslado de aves, o el usuario ya registró) → **SUMA** los huevos sin tocar `traslado_*`, mortalidad ni consumo.
4. Escribe la **marca de idempotencia** en `metadata` preservando el resto de claves:
   ```json
   "arrastreHuevosLevante": { "lotePosturaLevanteId": 12, "fecha": "2026-02-24", "aplicado": { … }, "version": 1 }
   ```
5. `RecalcularEspejoHuevoProduccionAsync`.

**Enganche** en `LotePosturaLevanteService.CerrarLoteYCrearProduccionAsync` (:379-424): envolver en `BeginTransactionAsync` → `Add(prod)` + `EstadoCierre="Cerrado"` → `SaveChanges` (para tener el Id del LPP) → `ArrastrarAsync` → `SaveChanges` → `Commit`.

### 3.5 La SUMA el mismo día (el cambio de la regla)
En `ProduccionService.CrearSeguimientoAsync` (:275-294):
```
si existe fila del día:
    si trae metadata.arrastreHuevosLevante → MERGE: suma los huevos del request sobre los de la fila,
        recalcula tot/inc, toma mortalidad/consumo/peso/alimento/etapa/observaciones del request,
        CONSERVA la marca, recalcula el espejo → devuelve el Id (200)
    si NO trae la marca → throw InvalidOperationException("Ya existe un seguimiento para esta fecha y lote.")  ← IDÉNTICO A HOY
```
Restringir el merge a la fila marcada mantiene el 400 en **todos** los casos actuales ⇒ cero cambio de comportamiento no pedido («en este caso particular» del usuario).

⛔ **Prohibido** crear una 2ª fila el mismo día (el índice único lo impide y `DISTINCT ON (fecha)` de las fns descartaría una **en silencio**). ⛔ **Prohibido** mutar `tipo_seguimiento` de 'levante' a 'produccion' (la rama UPDATE del trigger del espejo haría `historico - OLD + NEW` sobre un OLD que nunca se sumó ⇒ espejo descuadrado).

### 3.6 Reversibilidad e idempotencia
- **Reversibilidad: gratis.** `AbrirLoteAsync` → `EliminarDependientesLoteProduccionAsync` ya hace `ExecuteDelete` de `SeguimientoProduccion` + `EspejoHuevoProduccion` del LPP y borra el LPP ⇒ la fila de arrastre desaparece sola. Los huevos originales siguen intactos en levante (fuente de verdad).
- **Idempotencia: doble red.** (a) el cierre ya rechaza liquidar dos veces (`"Ya existe un lote de producción asociado a este lote de levante."`); (b) **delta por marca** (`nuevo − aplicado`) ⇒ cerrar → reabrir → cerrar da los mismos totales, no el doble, y corrige si se editaron huevos de levante en el medio.

### 3.7 Blindaje contra pérdida silenciosa (4 focos reales)
1. `Mapeos.cs` manda 15 `null` + `SeguimientoDiarioService.UpdateAsync` asigna sin condición ⇒ **editar borraría los huevos**. Fix: mapear de verdad y, en el facade de levante, tratar `null` como «conservar».
2. `SeguimientoDiarioService.teneManualExist` (~252) no evalúa huevos ⇒ una fila de **sólo huevos** se considera vacía y se **elimina**. Fix: incluir huevos.
3. `FilaSinContenido` (~778) ídem.
4. `MergearManualSobreTrasladoAsync` (~816) no copia huevos. Fix: copiarlos.

---

## 4. Archivos a crear / modificar

### Backend — Application
| Archivo | Cambio |
|---|---|
| `Calculos/HuevosLevanteCalculos.cs` | **NUEVO** (puro): `SemanaMinimaHuevosLevante = 14`, `SemanaVida`, `PermiteHuevos` (fail-closed), `record HuevosClasificacion` (11 categorías + `Inc`/`Tot` calculados), `Sumar`, `Delta`, `TieneHuevos`, `PesoHuevoPonderado`, `MetadataKeyArrastre`, `LeerArrastreDeMetadata`, `EscribirArrastreEnMetadata` (preserva el resto de claves) |
| `DTOs/CreateSeguimientoLoteLevanteRequest.cs` | 13 props **opcionales** (`int?`) + `double? PesoHuevo`, con `[JsonPropertyName]` camelCase, propagadas en `ToDto()` |
| `DTOs/SeguimientoLoteLevanteDto.cs` | mismos campos **al FINAL** del record posicional con `= null` (compartido con engorde/reproductora/Puente Panamá ⇒ opcional obligatorio) |
| `Interfaces/IArrastreHuevosLevanteService.cs` | **NUEVO** |
| `DTOs/CompanyDto.cs` (+ proyecciones) | `CapturaHuevosEnLevante` |
| `CierreLoteLevanteResumenDto` | + `HuevosLevanteTotales`, `HuevosLevanteIncubables` |

### Backend — Domain / Infrastructure
| Archivo | Cambio |
|---|---|
| `Domain/Entities/Company.cs` | `bool CapturaHuevosEnLevante` |
| `Configurations/CompanyConfiguration.cs` | `captura_huevos_en_levante`, default `false`, junto a `clasificacion_huevo_por_items` (~línea 39) |
| `Services/SeguimientoLoteLevante/Funciones/…Mapeos.cs` | reemplazar los 15 `null` (Create y Update) + devolver huevos en `MapToLevanteDto` |
| `Services/SeguimientoLoteLevante/Funciones/…Crud.cs` | gate semana 14 + flag de empresa en Create/Update; `null` = conservar en Update |
| `Services/SeguimientoLoteLevanteService.cs` (ancla) | helper de resolución del flag por granja (fail-closed) |
| `Services/SeguimientoDiarioService.cs` | los 4 blindajes de §3.7 |
| `Services/ArrastreHuevosLevanteService.cs` | **NUEVO** (§3.4) |
| `Services/LotePosturaLevanteService.cs` | tx + arrastre en `CerrarLoteYCrearProduccionAsync`; total de huevos en `GetResumenCierreAsync` |
| `Services/ProduccionService.cs` | rama de merge (§3.5) |
| `CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService` | propagar el flag en **todas** las proyecciones |
| `API/Program.cs` | DI del servicio de arrastre |

### Migraciones EF (idempotentes)
| Archivo | Cambio |
|---|---|
| `<ts>_AddCapturaHuevosEnLevanteCompany.cs` | `ALTER TABLE companies ADD COLUMN IF NOT EXISTS captura_huevos_en_levante boolean NOT NULL DEFAULT false;` |
| `<ts+1>_SeedCapturaHuevosEnLevante.cs` | data-only, `UPDATE companies SET … = true WHERE name = 'Agroavicola Sanmarino' AND … IS DISTINCT FROM true;` (Designer clonado, sin tocar ModelSnapshot) |

### Frontend
| Archivo | Cambio |
|---|---|
| `core/services/company-config/active-company-config.service.ts` | `capturaHuevosEnLevante` en `CompanyFlags` + `FLAGS_APAGADOS` (fail-closed) |
| `features/lote-levante/models/huevo-levante.model.ts` | **NUEVO** |
| `features/lote-levante/funciones/totales-huevos-levante.funcion.ts` | **NUEVO**, pura |
| `features/lote-levante/funciones/semana-vida-levante.funcion.ts` | **NUEVO**, pura (patrón mediodía local) |
| `features/lote-levante/funciones/README.md` | **NUEVO** (lo exige el CLAUDE.md) |
| `pages/modal-create-edit/modal-create-edit.component.{ts,html,scss}` | 3er tab `'huevos'`, `@Input() fechaEncaset`, 12 controles, auto-cálculo memoizado (no getter → NG0103), payload, rehidratación |
| `pages/seguimiento-lote-levante-list/…{ts,html}` | bindear `[fechaEncaset]`, total readonly en el modal de cierre, **toast en el error** (hoy el 400 sería invisible) |
| `services/seguimiento-lote-levante.service.ts` | campos de huevos en los DTOs TS |
| `pages/modal-detalle-seguimiento/…` | sección «Huevos» |

### Tests
`backend/tests/ZooSanMarino.Application.Tests/HuevosLevanteCalculosTests.cs` — **NUEVO** (gate CI obligatorio).

---

## 5. Reglas de negocio
- **R1** Huevos en levante sólo desde la **semana 14** (`dias >= 91`), fórmula `(dias/7)+1`. Fail-closed si `fecha_encaset` es null.
- **R2** `Inc = limpio + tratado`; `Tot = Inc + los 9 no incubables`.
- **R3** Flag OFF ⇒ **byte a byte idéntico** al actual, mensajes incluidos.
- **R4** Al liquidar se crea/mergea **UNA** fila en `seguimiento_diario_produccion` con la fecha de inicio de producción + marca en `metadata` + recálculo del espejo. Total 0 ⇒ nada.
- **R5** Si el usuario registra producción ese día, los huevos se **SUMAN**; el resto de campos los define el usuario.
- **R6** Idempotencia por delta; reversibilidad por el borrado que ya hace `AbrirLoteAsync`.
- **R7** Los huevos de levante **no se borran ni se marcan** al arrastrarse: levante sigue siendo la fuente de verdad (el arrastre es una proyección recalculable).

## 6. Casos de prueba

**xUnit (cálculo puro)**
1. días→semana: `(0,1) (6,1) (7,2) (13,2) (14,3) (90,13) (91,14) (97,14) (98,15)` — **borde 13 vs 14**.
2. `PermiteHuevos`: `dias=90` ⇒ false; `dias=91` ⇒ true; `fechaEncaset=null` ⇒ false; fecha < encaset ⇒ false.
3. `RecalcularTotales` con valores conocidos y con ceros.
4. `Sumar` campo a campo; `Sumar(a, cero) == a`.
5. `PesoHuevoPonderado`: `[(60,100),(70,300)]` ⇒ `67.5`; vacío ⇒ null; todos `tot=0` ⇒ null (sin división por cero).
6. `EscribirArrastreEnMetadata` conserva `itemsHembras`/`consumoOriginalHembras` verbatim; round-trip con `LeerArrastreDeMetadata`.
7. `Delta`: iguales ⇒ 0 (idempotente); mayor ⇒ diferencia; menor ⇒ negativo (no clampear, para que el espejo cuadre).

**Smoke API**
- S1 semana 13 con huevos ⇒ 400 con el mensaje del gate · S2 semana 14 ⇒ 201 y el GET los devuelve.
- S3 PUT sin huevos sobre un registro que los tiene ⇒ los **conserva**.
- S4 fila de sólo huevos + POST del seguimiento normal ⇒ **mergea**, no la borra.
- S6 liquidación **sin** huevos ⇒ ninguna fila nueva · S7 **con** huevos y sin seguimiento ese día ⇒ 1 fila + marca + espejo.
- S8 **SUMA**: liquidar → POST producción misma fecha ⇒ **200**, `huevo_tot` = arrastre + request, **una sola fila**.
- S9 **400 preservado**: dos POST normales el mismo día sin marca ⇒ mensaje literal actual.
- S10 doble liquidación ⇒ 400, sin duplicar · S11 reapertura ⇒ desaparece todo, re-cerrar ⇒ mismos totales.
- S12 liquidar → reabrir → editar huevos (+100) → cerrar ⇒ total corregido, no acumulado.
- S14 **flag OFF** ⇒ tab oculto, POST con huevos los ignora, liquidación sin filas ⇒ cero cambios visibles.

**Smoke UI**: semana 13 tab oculto y el form guarda igual · semana 14 tab visible con totales readonly · cambiar la fecha a semana 13 oculta el tab y `levanteTab` vuelve a `'general'` · modal de cierre muestra el total a arrastrar · sin **NG0103** en consola.

## 7. Riesgos aceptados (documentados, no bloqueantes)
- **Pico artificial** en los indicadores de producción del día del arrastre (`% postura = HuevoTot / saldoHembras`, HTAA/HIAA del Reporte Técnico Semanal): es **exactamente lo que pidió el usuario** (todos los huevos de levante al primer registro). La marca en `metadata` deja la puerta abierta a anotarlo/excluirlo si más adelante molesta.
- Si el lote se liquida **antes de la semana 25**, `fn_indicadores_produccion_postura` (`DELETE … WHERE sem_vida < 25`) no mostrará la fila en indicadores; el dato igual queda correcto en el espejo, en la lista de seguimientos y en el Reporte Contable. Bajar ese corte es una restricción distribuida (fn + loop + clamp del front) y ya causó un incidente (REQ-012b) ⇒ fuera de alcance.
- `ReporteContableService` lee sólo `seguimiento_diario_levante` con `tipo='produccion'` ⇒ no verá el arrastre (inconsistencia entre reportes preexistente, **no** doble conteo).
- Carga masiva de levante (`MigracionEsquemas.SeguimientoLevante`, 15 columnas) no acepta huevos ⇒ históricos sin huevos hasta extenderla (fase 2).
- `backend/sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql` está **desactualizado** (apunta a `public.seguimiento_diario`, inexistente); la versión viva está en la migración `20260531180558`. **No reaplicar ese .sql.**


---

## 8. Hallazgo durante la validación — `date_trunc` y la zona de la sesión (bug preexistente)

El merge del mismo día **no se disparaba** en el smoke: se creaba una segunda fila para la misma fecha en
vez de sumar. Causa raíz encontrada leyendo el SQL que genera EF:

```sql
-- de:  .FirstOrDefaultAsync(s => ... && s.Fecha.Date == request.FechaRegistro.Date)
WHERE (s.lote_postura_produccion_id = @p0 OR s.lote_id = @p1)
  AND date_trunc('day', s.fecha_registro) = @p2   -- @p2 = DateTimeOffset en medianoche UTC
```

`date_trunc('day', timestamptz)` trunca **en la zona horaria de la SESIÓN**. Con la sesión local en
`America/Bogota`, una fila de `2026-07-20 12:00Z` da `2026-07-20 00:00-05` (= 05:00 UTC), que nunca
es igual al parámetro `2026-07-20 00:00+00` ⇒ la comparación da falso aunque la fecha sea la misma.

**Consecuencia (preexistente, no introducida por esta feature):** en cualquier BD cuya sesión no sea
UTC, los DOS chequeos de duplicado de `ProduccionService.CrearSeguimientoAsync` no detectaban nada y
se podían crear varias filas del mismo día para el mismo lote (la tabla local **no** tiene el índice
único `(lote_id, fecha_registro)` que el `HasIndex` del modelo declara, así que la BD tampoco frenaba).

**Fix aplicado:** `FechasPuras.RangoDiaUtc(fecha)` → rango semiabierto `[00:00Z, +1 día)`, usado en los
dos chequeos de `ProduccionService` y en el lookup de `ArrastreHuevosLevanteService`. Es correcto en
cualquier zona, es SARGABLE (puede usar índice) y cubre tanto filas legadas (00:00Z) como nuevas
(12:00Z). Cubierto por 4 tests nuevos en `FechasPurasTests`.

**Efecto colateral a tener en cuenta:** en una BD con sesión no-UTC, el 400 `"Ya existe un seguimiento
para esta fecha y lote."` ahora **sí** se dispara donde antes se colaba un duplicado. Es el
comportamiento documentado del módulo, pero conviene saberlo antes del deploy.

⚠️ **Pendiente sugerido (fuera del alcance de esta tarea):** la tabla `seguimiento_diario_produccion`
de la BD local no tiene el índice único `(lote_id, fecha_registro)` que declara
`SeguimientoProduccionConfiguration.HasIndex(...).IsUnique()`. Vale verificar si en RDS prod existe;
si no, la unicidad depende sólo del chequeo de aplicación.
