# Alimento a nivel galpón vs granja — CONFIGURABLE (empresa + granja)

> **Objetivo:** que el "alimento se maneja sobre galpón" vs "sobre granja" deje de ser **por país** (hoy hardcodeado en `InventarioConsumoGate.ResolverModelo(paisId)`) y pase a ser **configurable por empresa (default global) con override por granja (nullable/heredable)**. Motivo real: en Colombia hay granjas que manejan alimento por galpón (como Ecuador) y otras por granja — depende de cómo opera cada granja.
>
> **Decisiones del dueño (2026-07-03):** granja = **heredable (nullable)** (null hereda empresa; true/false fuerza) · migración = **preservar por país** (EC/PA→galpón true, CO→granja false, granjas→null).

## 1. Regla de resolución (por granja, para alimento)
```
efectivoPorGalpon(farm) = farm.ManejaAlimentoPorGalpon ?? company.ManejaAlimentoPorGalpon
```
- `true`  → alimento **sobre galpón** (núcleo/galpón obligatorio) — comportamiento "ModeloB con galpón" actual (Ecuador/Panamá).
- `false` → alimento **sobre granja** (sin núcleo/galpón) — comportamiento "ModeloBNivelGranja" actual (Colombia).
- Solo aplica a **alimento**. Otros conceptos siguen a nivel granja (como hoy).
- Todos operan sobre **modelo B unificado** (`inventario_gestion_stock` / `item_inventario_ecuador`); lo único que cambia es la granularidad del alimento.

## 2. Backend
### 2.1 Dominio + BD
- `Domain/Entities/Company.cs`: `public bool ManejaAlimentoPorGalpon { get; set; }`.
- `Domain/Entities/Farm.cs`: `public bool? ManejaAlimentoPorGalpon { get; set; }` (nullable = hereda).
- Configs EF (`CompanyConfiguration`, `FarmConfiguration`): mapear las columnas.
- **Migración EF idempotente** `AddManejaAlimentoPorGalpon`:
  - `ALTER TABLE companies ADD COLUMN IF NOT EXISTS maneja_alimento_por_galpon boolean NOT NULL DEFAULT false;`
  - `ALTER TABLE farms ADD COLUMN IF NOT EXISTS maneja_alimento_por_galpon boolean NULL;`
  - **Seed preservando por país** (idempotente, solo setea el default inicial): `UPDATE companies c SET maneja_alimento_por_galpon = true WHERE EXISTS (SELECT 1 FROM company_pais cp WHERE cp.company_id=c.id AND cp.pais_id IN (2,3));` (EC/PA). Colombia queda en false (default). Granjas quedan null (heredan).

### 2.2 Resolver (necesita EF → servicio, no gate puro)
- Nuevo método en un servicio de inventario (p.ej. `IInventarioGestionService.ResolverAlimentoPorGalponAsync(int farmId) → bool`): lee `farm.ManejaAlimentoPorGalpon ?? company.ManejaAlimentoPorGalpon`.
- Lógica pura testeable: `Application/Calculos/AlimentoNivelResolver.cs` → `static bool Resolver(bool? farm, bool company) => farm ?? company;` + tests.

### 2.3 Rewire (reemplazar la decisión por país)
- `InventarioConsumoGate`: hoy `ResolverModelo(paisId)` devuelve `ModeloB` (galpón) para EC/PA y `ModeloBNivelGranja` para CO. **Cambio:** el modelo pasa a ser SIEMPRE modelo B unificado para EC/PA/CO; la granularidad alimento (galpón vs granja) la decide el flag resuelto **en el servicio** (no en el gate puro). El gate se simplifica a "¿opera modelo B?" (EC/PA/CO = sí).
- Servicios de consumo (`SeguimientoLoteLevanteService`, `ProduccionService`, `ColombiaInventarioConsumoService` / `InventarioGestionService.RegistrarConsumo*`): al descontar alimento, usar el flag resuelto por granja para decidir si exige/usa galpón o va a nivel granja. **Aritmética idéntica**; solo cambia el gate de granularidad.
- **Ingreso** (`InventarioGestionService.RegistrarIngreso*`): exigir núcleo/galpón para alimento **solo si** `efectivoPorGalpon(farm)`; si no, nivel granja. (Hoy lo decide el país.)

## 3. Frontend
- **Módulo Empresa** (`config/company-management`): checkbox "Maneja alimento a nivel galpón (default de la empresa)" en crear/editar empresa. Wire al DTO.
- **Módulo Granja** (`config/farm-management` / farm-form): control **3-estados** "Guardado de alimento": `Heredar de la empresa` (null) · `Sobre galpón` (true) · `Sobre granja` (false). Wire al DTO.
- **Ingreso de alimento** (`gestion-inventario-page`): reemplazar la lógica por país (`Colombia: nivel granja` / `recepcionNeedsNucleoGalpon`) por el **flag resuelto de la granja seleccionada** (traído del backend o del detalle de la granja). El mensaje pasa a: "Este alimento se aplica sobre la **granja**" o "sobre el **galpón**", y la exigencia de núcleo/galpón sigue el flag.

## 4. Tests
- `AlimentoNivelResolverTests`: `Resolver(null,true)==true`, `Resolver(null,false)==false`, `Resolver(true,false)==true`, `Resolver(false,true)==false`.
- Ingreso/consumo: alimento con flag=true exige galpón; flag=false va a nivel granja; **no cambia** para EC/PA (siguen galpón por el seed); Colombia sigue granja salvo override.
- No-regresión: build 0/0 + `dotnet test` verde; front `yarn build`.

## 5. Orden de slices
1. **Backend BD+dominio+config+migración+resolver** (+ tests del resolver). Build/test.
2. **Rewire gate/servicios (consumo+ingreso)** a flag resuelto (preservando aritmética). Build/test.
3. **Front empresa + granja** (checkboxes/DTO). yarn build.
4. **Front ingreso** (mensaje + galpón dinámico por granja). yarn build + visual.

## 6. Fuera de alcance
- Migrar datos históricos de granularidad (stock ya cargado). El flag cambia el flujo NUEVO; el stock existente no se re-particiona.
