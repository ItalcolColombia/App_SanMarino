# 27 — Venta de Pollo Engorde Panamá (modal + servicio separados)

> **Objetivo:** botón **"Nueva venta Panamá"** (visible solo si el usuario es de Panamá) que abre un
> **modal nuevo** de venta por despacho. El usuario elige **granja → galpón → lotes del galpón**;
> ve las **mixtas** disponibles y asigna **H/M** sobre ellas. Se crea **un registro por lote**
> (mismo despacho/factura). El registro guarda el split en `CantidadHembras/CantidadMachos` (el
> reporte los muestra divididos) y una **marca `es_venta_mixta`**; el **inventario sale de las
> MIXTAS** del lote (descuenta H+M de mixtas). Lógica **separada** en back (servicio Panamá) y front
> (`funciones/`+`models/`), siguiendo la metodología del proyecto.

**Decisiones confirmadas por el usuario:** (1) un registro por lote del galpón; (2) split en
`Hembras/Machos` + columna marca, inventario desde mixtas. **Asunciones:** misma tabla
`movimiento_pollo_engorde`; el registro queda **Pendiente** y se completa/elimina desde la lista
como las demás ventas; ciclo de vida compartido con branch por la marca.

---

## 1. Modelo de negocio (la "venta mixta")

- El lote (engorde Panamá) tiene aves en **mixtas**. El usuario asigna cuántas vende como **H** y
  cuántas como **M** (para el comprador/reporte). Ej.: mixtas=100, asigna H=10 / M=50 ⇒ se
  descuentan **60 de mixtas**; el reporte muestra H=10 / M=50.
- **Registro:** `CantidadHembras=10`, `CantidadMachos=50`, `CantidadMixtas=0`, `EsVentaMixta=true`.
- **Efecto en stock (helper único):**
  `CantidadesEfectivasEnLote(m) = EsVentaMixta ? (0, 0, H+M) : (H, M, X)`.
  Se usa en los 4 puntos del ciclo de vida para que H+M salgan/vuelvan de **mixtas**.

---

## 2. Backend

### 2.1 Schema + entidad
- **Migración EF idempotente**: `ALTER TABLE movimiento_pollo_engorde ADD COLUMN IF NOT EXISTS es_venta_mixta boolean NOT NULL DEFAULT false;`
- `Domain/Entities/MovimientoPolloEngorde.cs`: `public bool EsVentaMixta { get; set; }`
- `Configurations/MovimientoPolloEngordeConfiguration.cs`: mapear `EsVentaMixta` → `es_venta_mixta` (default false).

### 2.2 Helper compartido (ciclo de vida)
En el servicio compartido (`MovimientoPolloEngorde/`): `static (int H,int M,int X) CantidadesEfectivasEnLote(MovimientoPolloEngorde m)`.
Integrarlo (branch por `EsVentaMixta`) en:
1. **CompleteAsync** (descuento del lote origen) — usar efectivas en H/M/X.
2. **RevertirEfectoCompletadoEnLotes** (crédito al revertir) — usar efectivas.
3. **GetAvesDisponiblesLotesAsync** (reserva Pendiente por lote) — sumar efectivas (una venta mixta
   Pendiente reserva H+M sobre mixtas, 0 sobre H/M). Añadir `EsVentaMixta` al `Select` y computar la
   reserva con condicional.

### 2.3 Servicio Panamá (separado)
`Infrastructure/Services/MovimientoPolloEngordePanama/MovimientoPolloEngordePanamaService.cs`
(`IMovimientoPolloEngordePanamaService` en `Application/Interfaces`), namespace plano.
- `CreateVentaPanamaDespachoAsync(CreateVentaPanamaDespachoDto dto)`:
  * Valida líneas (galpón único; lote no repetido; `H+M>0`).
  * **Validación de disponibilidad propia:** `H+M ≤ mixtasDisponibles` por lote (reusa
    `GetAvesDisponiblesLotesAsync` del servicio compartido vía DI).
  * **Prorrateo de peso** por línea reusando `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea`.
  * Crea **un movimiento por línea** (`EsVentaMixta=true`, `CantidadHembras=H`, `CantidadMachos=M`,
    `CantidadMixtas=0`), mismo `FacturaId`/`NumeroDespacho`, estado **Pendiente**, en transacción.
- DTO `Application/DTOs/CreateVentaPanamaDespachoDto.cs` + `VentaPanamaDespachoLineaDto`
  (`LoteAveEngordeOrigenId, GranjaOrigenId, NucleoOrigenId, GalponOrigenId, CantidadHembras, CantidadMachos`)
  + campos de despacho (igual que `CreateVentaGranjaDespachoDto`).
- **Controller** `API/Controllers/MovimientoPolloEngordePanamaController.cs`:
  `POST /api/MovimientoPolloEngordePanama/venta-despacho`. DI en `Program.cs`.

### 2.4 DTO de lectura
`MovimientoPolloEngordeDto` (+ `ToDto`): exponer `EsVentaMixta` para que el front/reporte lo distinga.

---

## 3. Frontend (módulo `movimientos-pollo-engorde`)

- **Botón** "Nueva venta Panamá" en el list, **`*ngIf="isPanama"`** (inyectar `CountryFilterService`).
  Habilitado con granja seleccionada (como `create()`).
- **Modal nuevo** `components/modal-venta-panama/modal-venta-panama.component.ts`:
  * Inputs: granja activa, catálogo de galpones/lotes de la granja.
  * **Selector de galpón** dentro del modal → carga los **lotes de ese galpón** con sus **mixtas
    disponibles** (`postAvesDisponiblesLotes`).
  * Por lote: inputs **H** y **M** con tope `H+M ≤ mixtasDisponibles` (aviso de exceso como el modal actual).
  * Campos de despacho (fecha, despacho, placa, peso bruto/tara, conductor, etc.) como el modal de venta.
  * Vista previa de prorrateo de peso reusando `funciones/prorateo-peso`.
  * Submit → `VentaPanamaPolloEngordeService.createVentaPanamaDespacho(dto)`.
- **Servicio front** `services/venta-panama-pollo-engorde.service.ts` → `POST .../MovimientoPolloEngordePanama/venta-despacho`.
- **`models/`**: `venta-panama.model.ts` (`VentaPanamaLinea`, `CreateVentaPanamaDespachoDto`, …).
- **`funciones/`**: `mapear-venta-panama-dto.funcion.ts` (form → DTO, puro) y reuso de `formato`/`prorateo-peso`.
- Tras guardar: cerrar modal, recargar movimientos (toast con nº de registros, como venta-granja).
- El reporte/listado ya muestra `Hembras/Machos` → el split sale natural; opcional: badge "Mixta" si `esVentaMixta`.

---

## 4. Validación
1. `dotnet build` (0 errores) + `dotnet ef migrations add` aplica local si hay BD (si no, en deploy).
2. Tests del helper `CantidadesEfectivasEnLote` y del prorrateo (xUnit, Application.Tests).
3. `yarn build` front.
4. Prueba de flujo (si hay entorno): crear venta Panamá → Pendiente → completar descuenta mixtas →
   eliminar devuelve a mixtas.
5. Sin procesos vivos.

---

## 5. Fuera de alcance
- Reporte/Excel específico Panamá (el listado actual ya divide H/M). Se puede afinar después.
- Edición del movimiento Panamá desde el modal de edición genérico (se crea y se gestiona ciclo de
  vida; editar cantidades mixtas se evalúa aparte si se pide).
