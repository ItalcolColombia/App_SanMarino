# Plan Técnico — Feature 12: Lotes y Traslados de Aves
**Fecha:** 2026-05-24  
**Área:** Módulo Lote Postura (Levante y Producción)

---

## 1. Resumen Ejecutivo

Este plan cubre tres requerimientos de negocio independientes pero relacionados en el dominio de lotes postura:

| # | Requerimiento | Impacto |
|---|---|---|
| R1 | Selección de letra A-F al crear LotePosturaLevante | Backend (endpoint), Frontend (dropdown + validación) |
| R2 | Crear lotes con aves en cero | Frontend (quitar validador min > 0), Backend (verificar) |
| R3 | Modal de Traslado de Aves desde Seguimiento Diario | DB (2 tablas), Backend (endpoint transaccional), Frontend (modal + botón) |

---

## 2. Análisis Arquitectónico Base

### 2.1 Entidades relevantes confirmadas

```
LotePosturaLevante  (tabla: lote_postura_levante)
  ├─ LotePosturaLevanteId (PK int auto-inc)
  ├─ LoteNombre          string   ← nombre completo ej: "K321C"
  ├─ GranjaId            int FK
  ├─ GalponId            string FK
  ├─ AvesHInicial        int?
  ├─ AvesMInicial        int?
  ├─ AvesHActual         int?     ← stock actual hembras
  ├─ AvesMActual         int?     ← stock actual machos
  └─ EstadoCierre        string   "Abierto"|"Cerrado"

LotePosturaProduccion  (tabla: lote_postura_produccion)
  ├─ LotePosturaProduccionId (PK int auto-inc)
  ├─ LoteNombre          string
  ├─ GranjaId / GalponId
  ├─ AvesHInicial / AvesMInicial
  ├─ AvesHActual         int?     ← stock actual hembras
  ├─ AvesMActual         int?     ← stock actual machos
  └─ LotePosturaLevanteId int FK (lote padre)

SeguimientoLoteLevante  (tabla: seguimiento_lote_levante)
  ├─ Id (PK), LoteId (int FK), FechaRegistro
  ├─ MortalidadHembras, MortalidadMachos, SelH, SelM, ...
  └─ [NUEVO] campos traslado (R3)

ProduccionSeguimiento  (tabla: produccion_seguimiento)
  ├─ Id (PK), LoteId (int FK), FechaRegistro
  ├─ MortalidadH, MortalidadM, ConsumoKg, ...
  └─ [NUEVO] campos traslado (R3)

MovimientoAves  — entidad existente de auditoría de movimientos entre lotes
  ├─ LoteOrigenId / LoteDestinoId
  ├─ GranjaOrigenId / GranjaDestinoId / NucleoDestinoId / GalponDestinoId
  ├─ CantidadHembras / CantidadMachos / CantidadMixtas
  └─ TipoMovimiento  ("Traslado", "Despacho", ...)
```

### 2.2 Patrón Espejo (Mirror) — referencia para R3

El sistema usa `EspejoHuevoProduccion` para tracking de stock de huevos via PostgreSQL trigger (`fn_espejo_huevo_produccion_upsert`):
- **Histórico**: acumulado bruto de producción
- **Dinámico**: histórico − traslados/ventas

Para aves, el stock se mantiene directamente en `AvesHActual`/`AvesMActual` de la entidad lote (no hay tabla espejo separada). El `MovimientoAves` provee el registro de auditoría equivalente al `TrasladoHuevos`.

**Conclusión R3:** El traslado de aves debe:
1. Registrar campos en `seguimiento_lote_levante` (origen)
2. Decrementar `AvesHActual`/`AvesMActual` en lote origen
3. Incrementar `AvesHActual`/`AvesMActual` en lote destino
4. Insertar un `MovimientoAves` para auditoría

---

## 3. REQUERIMIENTO 1 — Selección de Letra A-F

### 3.1 Lógica de nombres de lote

La convención actual: `LoteNombre = "K321A"` donde `"K321"` es el prefijo base y `"A"` es la letra de la instancia.

La "letra disponible" se determina consultando qué letras ya existen para ese `GalponId` con el mismo prefijo en `lote_postura_levante` donde `deleted_at IS NULL`.

### 3.2 Backend

**Nuevo endpoint en `LotePosturaLevanteController`:**
```
GET /api/lote-postura-levante/letras-disponibles
  ?galponId={galponId}
  &loteBase={loteBase}     ← ej: "K321"
```

**Response:**
```json
{
  "letrasOcupadas": ["A", "B"],
  "letrasDisponibles": ["C", "D", "E", "F"]
}
```

**Implementación en `LotePosturaLevanteService`:**
```csharp
// Nuevo método en ILotePosturaLevanteService
Task<LetrasDisponiblesDto> GetLetrasDisponiblesAsync(
    string galponId, string loteBase, CancellationToken ct = default);

// Implementación:
var ocupadas = await _ctx.LotePosturaLevante
    .AsNoTracking()
    .Where(l => l.GalponId == galponId
             && l.DeletedAt == null
             && l.LoteNombre.StartsWith(loteBase))
    .Select(l => l.LoteNombre.Substring(loteBase.Length, 1))
    .Where(letra => new[] {"A","B","C","D","E","F"}.Contains(letra))
    .Distinct()
    .ToListAsync(ct);
```

**Nuevo DTO:**
```csharp
// /Application/DTOs/Lotes/LetrasDisponiblesDto.cs
public record LetrasDisponiblesDto(
    List<string> LetrasOcupadas,
    List<string> LetrasDisponibles);
```

**Archivos a modificar:**
- `backend/src/ZooSanMarino.Application/Interfaces/ILotePosturaLevanteService.cs` → agregar método
- `backend/src/ZooSanMarino.Application/DTOs/Lotes/LetrasDisponiblesDto.cs` → crear
- `backend/src/ZooSanMarino.Infrastructure/Services/LotePosturaLevanteService.cs` → implementar
- `backend/src/ZooSanMarino.API/Controllers/LotePosturaLevanteController.cs` → agregar endpoint GET

### 3.3 Frontend

**Componente destino:** Modal de creación de LotePosturaLevante (ubicación exacta a confirmar durante implementación — buscar en `lote-levante/` el modal de creación).

**Cambio en el formulario:**
1. Al seleccionar el campo "Lote Base" (prefijo), disparar llamada al nuevo endpoint con `galponId` + `loteBase`
2. Reemplazar el campo de texto `loteNombre` por la combinación: `[input readonly: loteBase] + [select: letra A-F]`
3. Opciones del select: `[{value: 'A', disabled: ocupada}, ..., {value: 'F', disabled: ocupada}]`
4. El `loteNombre` final se compone en el servicio como `loteBase + letraSeleccionada`

**Nuevo método en el servicio de levante:**
```typescript
getLetrasDisponibles(galponId: string, loteBase: string): Observable<LetrasDisponiblesDto>
```

---

## 4. REQUERIMIENTO 2 — Lotes "Sin Aves" (Stock Inicial Cero)

### 4.1 Estado actual

| Capa | Restricción actual | ¿Permite 0? |
|------|--------------------|-------------|
| DB `lote_postura_levante` | No hay check constraint explícito en aves | ✅ |
| DB `lote_postura_base` | `check cantidad_hembras >= 0` | ✅ permite 0 |
| Backend DTO/Validator | Pendiente verificar FluentValidation | A verificar |
| Frontend | Probable `Validators.min(1)` en formulario | ❌ a corregir |

### 4.2 Cambios Backend

Verificar que `CreateLotePosturaLevanteDto` y `CreateLotePosturaProduccionDto` no tengan reglas `GreaterThan(0)` en FluentValidation para los campos de aves. Deben usar `GreaterThanOrEqualTo(0)`.

**Archivos a revisar:**
- `backend/src/ZooSanMarino.Application/Validators/` → todos los validadores de creación de lotes postura

Si hay reglas `GreaterThan(0)`, cambiar a `GreaterThanOrEqualTo(0)`.

### 4.3 Cambios Frontend

**Componentes a revisar:**
- Modal de creación de `LotePosturaLevante` → campos `hembrasL`, `machosL`, `avesHInicial`, `avesMInicial`
- Modal de creación de `LotePosturaProduccion` → campos `hembrasInicialesProd`, `machosInicialesProd`

**Cambio:** `Validators.min(1)` → `Validators.min(0)` en todos los campos de cantidad de aves.

**Cambio de UX:** Los campos de aves deben tener `placeholder="0"` para indicar que son opcionales y pueden quedar vacíos.

---

## 5. REQUERIMIENTO 3 — Traslado de Aves desde Seguimiento Diario

### 5.1 Diseño General

```
[Botón "Traslado de Aves"]
        ↓
[Modal Traslado]
  1. Select Granja Destino
  2. Select Galpón Destino (carga según granja)
  3. Select Lote Destino (carga según galpón, solo lotes activos)
  4. Display: stock actual origen (hembras/machos)
  5. Input: cantHembrasTraslado (≤ AvesHActual origen)
  6. Input: cantMachosTraslado (≤ AvesMActual origen)
        ↓ [Guardar]
POST /api/traslados/aves-desde-seguimiento
  → Transacción DB:
      a) UPSERT seguimiento_lote_levante (hoy) con campos traslado
      b) UPDATE lote origen: AvesHActual -= hembras, AvesMActual -= machos
      c) UPDATE lote destino: AvesHActual += hembras, AvesMActual += machos
      d) INSERT movimiento_aves (auditoría)
```

### 5.2 Cambios en Base de Datos

#### Script SQL: `add_traslado_aves_seguimiento_levante.sql`
```sql
ALTER TABLE public.seguimiento_lote_levante
    ADD COLUMN IF NOT EXISTS hembras_traslado   INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS machos_traslado    INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS total_traslado     INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS lote_traslado_id   INT  REFERENCES public.lote_postura_levante(lote_postura_levante_id),
    ADD COLUMN IF NOT EXISTS galpon_traslado_id VARCHAR,
    ADD COLUMN IF NOT EXISTS granja_traslado_id INT  REFERENCES public.farms(farm_id);

COMMENT ON COLUMN public.seguimiento_lote_levante.hembras_traslado IS 'Hembras trasladadas a lote destino ese día';
COMMENT ON COLUMN public.seguimiento_lote_levante.machos_traslado  IS 'Machos trasladados a lote destino ese día';
COMMENT ON COLUMN public.seguimiento_lote_levante.total_traslado   IS 'Total aves trasladadas (hembras + machos)';
COMMENT ON COLUMN public.seguimiento_lote_levante.lote_traslado_id IS 'FK lote_postura_levante destino del traslado';
COMMENT ON COLUMN public.seguimiento_lote_levante.galpon_traslado_id IS 'Galpón destino del traslado';
COMMENT ON COLUMN public.seguimiento_lote_levante.granja_traslado_id IS 'Granja destino del traslado';
```

#### Script SQL: `add_traslado_aves_produccion_seguimiento.sql`
```sql
ALTER TABLE public.produccion_seguimiento
    ADD COLUMN IF NOT EXISTS hembras_traslado   INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS machos_traslado    INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS total_traslado     INT DEFAULT 0,
    ADD COLUMN IF NOT EXISTS lote_traslado_id   INT,
    ADD COLUMN IF NOT EXISTS galpon_traslado_id VARCHAR,
    ADD COLUMN IF NOT EXISTS granja_traslado_id INT;

COMMENT ON COLUMN public.produccion_seguimiento.hembras_traslado IS 'Hembras trasladadas a lote destino ese día';
COMMENT ON COLUMN public.produccion_seguimiento.machos_traslado  IS 'Machos trasladados a lote destino ese día';
COMMENT ON COLUMN public.produccion_seguimiento.total_traslado   IS 'Total aves trasladadas (hembras + machos)';
COMMENT ON COLUMN public.produccion_seguimiento.lote_traslado_id IS 'FK lote destino del traslado';
COMMENT ON COLUMN public.produccion_seguimiento.galpon_traslado_id IS 'Galpón destino del traslado';
COMMENT ON COLUMN public.produccion_seguimiento.granja_traslado_id IS 'Granja destino del traslado';
```

### 5.3 Cambios en Dominio (.Domain)

**Modificar `SeguimientoLoteLevante.cs`** — agregar propiedades:
```csharp
public int HembrasTraslado { get; set; } = 0;
public int MachosTraslado  { get; set; } = 0;
public int TotalTraslado   { get; set; } = 0;
public int? LoteTrasladoId   { get; set; }
public string? GalponTrasladoId { get; set; }
public int? GranjaTrasladoId  { get; set; }
```

**Modificar `ProduccionSeguimiento.cs`** (si existe entidad separada) — mismos campos.

> **Nota:** Verificar que `ProduccionSeguimiento` sea la entidad usada para `produccion_seguimiento`. Si el servicio usa raw SQL o la tabla está mapeada con un nombre distinto, ajustar en consecuencia.

### 5.4 Nuevo DTO de Request

**Crear `/Application/DTOs/Traslados/TrasladoAvesDesdeSegDiarioDto.cs`:**
```csharp
public record TrasladoAvesDesdeSegDiarioDto(
    int     LoteOrigenId,         // lote_postura_levante_id origen
    string  TipoOrigen,           // "levante" | "produccion"
    int     GranjaDestinoId,
    string  GalponDestinoId,
    int     LoteDestinoId,        // lote_postura_levante_id destino
    int     HembrasTraslado,      // cantidad a trasladar
    int     MachosTraslado,
    DateTime FechaTraslado
);
```

**Crear `/Application/DTOs/Traslados/TrasladoAvesResultDto.cs`:**
```csharp
public record TrasladoAvesResultDto(
    int SeguimientoId,
    int HembrasOrigen,   // aves restantes en origen
    int MachosOrigen,
    int HembrasDestino,  // aves totales en destino
    int MachosDestino
);
```

### 5.5 Interface de Servicio

**Crear `/Application/Interfaces/ITrasladoAvesDesdeSegService.cs`:**
```csharp
public interface ITrasladoAvesDesdeSegService
{
    Task<TrasladoAvesResultDto> EjecutarTrasladoAsync(
        TrasladoAvesDesdeSegDiarioDto dto, CancellationToken ct = default);
    
    Task<DisponibilidadAvesDto> GetDisponibilidadOrigenAsync(
        int loteId, string tipo, CancellationToken ct = default);
}
```

**Crear `/Application/DTOs/Traslados/DisponibilidadAvesDto.cs`:**
```csharp
public record DisponibilidadAvesDto(
    int LoteId,
    string LoteNombre,
    int HembrasDisponibles,
    int MachosDisponibles
);
```

### 5.6 Implementación del Servicio

**Crear `Infrastructure/Services/TrasladoAvesDesdeSegService.cs`:**

Lógica transaccional en `EjecutarTrasladoAsync`:
```csharp
using var tx = await _ctx.Database.BeginTransactionAsync(ct);

// 1. Obtener lote origen y validar stock
var loteOrigen = await _ctx.LotePosturaLevante  // o LotePosturaProduccion
    .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == dto.LoteOrigenId, ct)
    ?? throw new KeyNotFoundException("Lote origen no encontrado");

if (dto.HembrasTraslado > (loteOrigen.AvesHActual ?? 0))
    throw new InvalidOperationException("Stock de hembras insuficiente");
if (dto.MachosTraslado > (loteOrigen.AvesMActual ?? 0))
    throw new InvalidOperationException("Stock de machos insuficiente");

// 2. UPSERT seguimiento (busca el de hoy, si no existe lo crea)
var fecha = dto.FechaTraslado.Date;
var seg = await _ctx.SeguimientoLoteLevante
    .FirstOrDefaultAsync(s => s.LoteId == dto.LoteOrigenId 
                            && s.FechaRegistro.Date == fecha, ct);

if (seg == null)
{
    seg = new SeguimientoLoteLevante { LoteId = dto.LoteOrigenId, FechaRegistro = dto.FechaTraslado };
    _ctx.SeguimientoLoteLevante.Add(seg);
}

seg.HembrasTraslado   = dto.HembrasTraslado;
seg.MachosTraslado    = dto.MachosTraslado;
seg.TotalTraslado     = dto.HembrasTraslado + dto.MachosTraslado;
seg.LoteTrasladoId    = dto.LoteDestinoId;
seg.GalponTrasladoId  = dto.GalponDestinoId;
seg.GranjaTrasladoId  = dto.GranjaDestinoId;

// 3. Decrementar origen
loteOrigen.AvesHActual = (loteOrigen.AvesHActual ?? 0) - dto.HembrasTraslado;
loteOrigen.AvesMActual = (loteOrigen.AvesMActual ?? 0) - dto.MachosTraslado;

// 4. Obtener y actualizar lote destino
var loteDestino = await _ctx.LotePosturaLevante
    .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == dto.LoteDestinoId, ct)
    ?? throw new KeyNotFoundException("Lote destino no encontrado");

loteDestino.AvesHActual = (loteDestino.AvesHActual ?? 0) + dto.HembrasTraslado;
loteDestino.AvesMActual = (loteDestino.AvesMActual ?? 0) + dto.MachosTraslado;

// 5. Insertar MovimientoAves para auditoría
var movimiento = new MovimientoAves
{
    TipoMovimiento    = "Traslado",
    FechaMovimiento   = dto.FechaTraslado,
    LoteOrigenId      = dto.LoteOrigenId,
    GranjaOrigenId    = loteOrigen.GranjaId,
    GalponOrigenId    = loteOrigen.GalponId,
    LoteDestinoId     = dto.LoteDestinoId,
    GranjaDestinoId   = dto.GranjaDestinoId,
    GalponDestinoId   = dto.GalponDestinoId,
    CantidadHembras   = dto.HembrasTraslado,
    CantidadMachos    = dto.MachosTraslado,
    Estado            = "Completado",
    CompanyId         = _current.CompanyId,
    CreatedByUserId   = _current.UserId,
    CreatedAt         = DateTime.UtcNow
};
_ctx.MovimientoAves.Add(movimiento);

await _ctx.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

> **Consideración crítica:** Si el lote destino es de tipo `Produccion` (no Levante), se debe usar `LotePosturaProduccion` en lugar de `LotePosturaLevante`. El `TipoOrigen` del DTO indica el tipo del lote destino (o agregar un `TipoDestino` separado). Esto se define por el tipo de seguimiento desde el que se origina el traslado.

### 5.7 Endpoint API

**En `TrasladosController.cs` — agregar:**
```csharp
// GET: api/traslados/disponibilidad-aves/{loteId}?tipo=levante
[HttpGet("disponibilidad-aves/{loteId}")]
public async Task<IActionResult> GetDisponibilidadAves(int loteId, [FromQuery] string tipo = "levante")

// POST: api/traslados/aves-desde-seguimiento
[HttpPost("aves-desde-seguimiento")]
public async Task<IActionResult> TrasladoAvesDesdeSegDiario(
    [FromBody] TrasladoAvesDesdeSegDiarioDto dto)
```

**Inyectar `ITrasladoAvesDesdeSegService` en `TrasladosController`.**

**Registro DI en `Program.cs`:**
```csharp
builder.Services.AddScoped<ITrasladoAvesDesdeSegService, TrasladoAvesDesdeSegService>();
```

### 5.8 EF Core Migrations

```bash
# Desde /backend/src/ZooSanMarino.API/
dotnet ef migrations add AddTrasladoAvesFieldsToSeguimientoLevante \
    --project ../ZooSanMarino.Infrastructure \
    --startup-project . \
    --context ZooSanMarinoContext

dotnet ef migrations add AddTrasladoAvesFieldsToProduccionSeguimiento \
    --project ../ZooSanMarino.Infrastructure \
    --startup-project . \
    --context ZooSanMarinoContext
```

### 5.9 Frontend

#### Botón "Traslado de Aves"

Agregar en la parte superior de:
1. **Seguimiento Diario Postura Levante** — componente principal de la lista/vista de seguimiento (buscar en `lote-levante/pages/seguimiento-lote-levante-list/`)
2. **Seguimiento Diario Producción** — componente principal (buscar en `lote-produccion/pages/tabs-principal/` o `lote-produccion/pages/modal-seguimiento-diario/`)

```html
<button 
  class="bg-ital-green text-white px-4 py-2 rounded-lg font-semibold"
  (click)="abrirModalTraslado()">
  Traslado de Aves
</button>
```

#### Nuevo Componente Modal

**Crear:** `frontend/src/app/features/traslados-aves/components/modal-traslado-aves-seguimiento/`
- `modal-traslado-aves-seguimiento.component.ts`
- `modal-traslado-aves-seguimiento.component.html`
- `modal-traslado-aves-seguimiento.component.scss`

**Inputs del componente:**
```typescript
@Input() loteOrigenId!: number;
@Input() tipoOrigen!: 'levante' | 'produccion';
@Input() visible: boolean = false;
@Output() trasladoCompletado = new EventEmitter<TrasladoAvesResultDto>();
@Output() cerrar = new EventEmitter<void>();
```

**Lógica del template (cascade selects):**
1. `granjas$` — observable de granjas del usuario (reusar filter-data global)
2. `galpones$` — carga al seleccionar granja  
3. `lotes$` — carga al seleccionar galpón (solo lotes activos, `estado_cierre = 'Abierto'`)
4. `disponibilidad$` — carga al iniciar modal desde `GET disponibilidad-aves/{loteOrigenId}`
5. Validaciones reactivas: `cantHembras.max(disponibilidad.hembrasDisponibles)`

#### Nuevo método en servicio Traslados

**Modificar `traslados-aves.service.ts`** (o crear `traslados-aves-seguimiento.service.ts`):
```typescript
getDisponibilidadAves(loteId: number, tipo: string): Observable<DisponibilidadAvesDto>
ejecutarTrasladoDesdeSegDiario(dto: TrasladoAvesDesdeSegDiarioDto): Observable<TrasladoAvesResultDto>
```

---

## 6. Tabla de Archivos a Crear/Modificar

### Backend — Crear
| Archivo | Tipo |
|---------|------|
| `Application/DTOs/Lotes/LetrasDisponiblesDto.cs` | DTO nuevo |
| `Application/DTOs/Traslados/TrasladoAvesDesdeSegDiarioDto.cs` | DTO nuevo |
| `Application/DTOs/Traslados/TrasladoAvesResultDto.cs` | DTO nuevo |
| `Application/DTOs/Traslados/DisponibilidadAvesDto.cs` | DTO nuevo |
| `Application/Interfaces/ITrasladoAvesDesdeSegService.cs` | Interface nueva |
| `Infrastructure/Services/TrasladoAvesDesdeSegService.cs` | Servicio nuevo |

### Backend — Modificar
| Archivo | Cambio |
|---------|--------|
| `Domain/Entities/SeguimientoLoteLevante.cs` | +6 props traslado |
| `Domain/Entities/ProduccionSeguimiento.cs` | +6 props traslado (verificar si existe como entidad o solo raw SQL) |
| `Application/Interfaces/ILotePosturaLevanteService.cs` | +`GetLetrasDisponiblesAsync` |
| `Infrastructure/Services/LotePosturaLevanteService.cs` | Implementar `GetLetrasDisponiblesAsync` |
| `API/Controllers/LotePosturaLevanteController.cs` | +GET letras-disponibles |
| `API/Controllers/TrasladosController.cs` | +GET disponibilidad-aves, +POST aves-desde-seguimiento |
| `API/Program.cs` | +DI `ITrasladoAvesDesdeSegService` |
| `Application/Validators/*` | Cambiar `GreaterThan(0)` → `GreaterThanOrEqualTo(0)` para aves (R2) |

### SQL Scripts — Crear
| Archivo | Propósito |
|---------|-----------|
| `backend/sql/add_traslado_aves_seguimiento_levante.sql` | +6 cols en seguimiento_lote_levante |
| `backend/sql/add_traslado_aves_produccion_seguimiento.sql` | +6 cols en produccion_seguimiento |

### EF Core Migrations — Crear
| Nombre | Tablas |
|--------|--------|
| `AddTrasladoAvesFieldsToSeguimientoLevante` | seguimiento_lote_levante |
| `AddTrasladoAvesFieldsToProduccionSeguimiento` | produccion_seguimiento |

### Frontend — Crear
| Archivo | Propósito |
|---------|-----------|
| `traslados-aves/components/modal-traslado-aves-seguimiento/*.ts/html/scss` | Modal completo |

### Frontend — Modificar
| Archivo | Cambio |
|---------|--------|
| Modal creación LotePosturaLevante | R1: reemplazar loteNombre por combo prefijo+select letra |
| Página Seguimiento Diario Levante | R3: agregar botón + integrar modal |
| Página Seguimiento Diario Producción | R3: agregar botón + integrar modal |
| Modal creación lote (levante y producción) | R2: quitar min(1) en campos aves |
| `traslados-aves/services/traslados-aves.service.ts` | R3: nuevos métodos API |
| `lote-levante/services/seguimiento-lote-levante.service.ts` | R1: agregar `getLetrasDisponibles` |

---

## 7. Restricciones y Riesgos

| Riesgo | Mitigación |
|--------|------------|
| `ProduccionSeguimiento` puede estar solo como vista SQL o en `seguimiento_diario` | Verificar `ProduccionService.cs` y `SeguimientoDiarioService.cs` antes de modificar entidad |
| UPSERT del seguimiento de hoy puede pisar datos existentes | Solo actualizar campos traslado; no tocar campos de mortalidad/consumo ya registrados |
| Lote destino puede ser de tipo Producción | Manejar dos queries: `LotePosturaLevante` y `LotePosturaProduccion` según tipo |
| `MovimientoAves` puede tener FK constraints desconocidas | Revisar la entidad completa antes del INSERT |
| EF migrations siguen siendo inestables (CLAUDE.md) | Siempre generar SQL manual primero, luego migration para alineación |
