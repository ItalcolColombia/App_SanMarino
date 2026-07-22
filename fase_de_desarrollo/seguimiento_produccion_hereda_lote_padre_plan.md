# Plan — Seguimiento Diario Producción: heredar Lote padre al cerrar Levante (Postura)

> Módulo Postura (Levante + Producción). Empresa donde se detectó: **Demo (company_id 4)**, pero
> el módulo es compartido → afecta a **todas** las empresas (SanMarino incluida).

## Síntoma reportado

- En **Seguimiento Diario Producción**, guardar un seguimiento devuelve **400**:
  `POST /api/Produccion/seguimiento` →
  `{ "message": "El lote postura producción no tiene LoteId asociado (requerido para guardar en produccion_diaria)." }`
- Ocurre en lotes de producción que se **abrieron automáticamente al cerrar un lote de Levante**.
- El usuario espera que Producción "herede el lote padre" igual que lo hace Levante.

## Causa raíz (confirmada en código)

1. `LotePosturaProduccion` **solo** se crea en un lugar: al cerrar el levante,
   `LotePosturaLevanteService.CerrarLoteYCrearProduccionAsync` →
   `LotePosturaLevanteService.CrearLoteProduccion(...)`
   (`backend/src/ZooSanMarino.Infrastructure/Services/LotePosturaLevanteService.cs:86`).
   No existe endpoint/servicio de creación directa (el `LotePosturaProduccionController` es solo GET).
2. Ese método copia casi todos los campos del levante **pero omite `LoteId` y `LotePadreId`**.
   → Toda fila de `lote_postura_produccion` nace con `lote_id = NULL`.
3. Al guardar el seguimiento, `ProduccionService.CrearSeguimientoAsync` /
   `ActualizarSeguimientoAsync` hacen `loteId = lpp.LoteId ?? 0;` y si `loteId <= 0` lanzan el 400
   (`ProduccionService.cs:268-270` y `:442-444`). `produccion_diaria.lote_id` es requerido.
4. Levante **siempre** tiene `LoteId`: `LoteService` lo pobla al crear el `Lote`
   (`LoteService.cs:366` → `LoteId = loteIdValue`, `LotePadreId = ent.LotePadreId`).
   Por eso la herencia a producción siempre tendría valor válido; hoy simplemente se pierde.

**Conclusión:** el fix es heredar `LoteId`/`LotePadreId` del levante al crear el lote de producción,
igual que ya hace Levante. No hace falta relajar la validación de `ProduccionService`: tras el fix
+ backfill, todo lote de producción tendrá `lote_id` válido.

## Alcance del arreglo

### A) Código (backend) — herencia hacia adelante
- `LotePosturaLevanteService.CrearLoteProduccion(...)`: agregar
  - `LoteId = lev.LoteId`
  - `LotePadreId = lev.LotePadreId`
  - (mantener `LotePosturaLevanteId = lev.LotePosturaLevanteId`, ya estaba)
- Refactor ≠ cambio de comportamiento en el resto: solo se **agregan** 2 asignaciones.

### B) Datos (backend/sql) — backfill de lotes ya creados
- Todas las filas `lote_postura_produccion` existentes tienen `lote_id = NULL` por este bug.
- Script idempotente `backend/sql/backfill_lote_postura_produccion_lote_id.sql`:
  ```sql
  UPDATE public.lote_postura_produccion p
  SET lote_id       = lev.lote_id,
      lote_padre_id = COALESCE(p.lote_padre_id, lev.lote_padre_id)
  FROM public.lote_postura_levante lev
  WHERE p.lote_postura_levante_id = lev.lote_postura_levante_id
    AND (p.lote_id IS NULL OR p.lote_id <= 0)
    AND lev.lote_id IS NOT NULL;
  ```
- Solo toca filas con `lote_id` nulo/≤0 → re-ejecutable sin efecto adicional.
- **No** se corre contra la BD compartida (:5433) ni contra prod desde aquí: lo aplica el flujo de
  deploy / la sesión que controle la BD. (Si se prefiere auto-aplicar en deploy, envolverlo en una
  migración EF idempotente — ver nota de coordinación abajo.)

### C) Inventario "400 vs 4000" — NO es bug
- `GET /api/inventario/items?activo=true` → ítem 208, **código `"4000"`**, nombre "Alimneto ERP".
- `GET /api/inventario-gestion/stock?farmId=88` → **quantity `400` kg** (stock real).
- El dropdown arma el label en `modal-seguimiento-diario.component.ts:464`:
  `` `${item.codigo} — ${item.nombre} (Disp.: ${cantidad.quantity.toFixed(2)} ${cantidad.unit})` ``
  → **"4000 — Alimneto ERP (Disp.: 400.00 kg)"**. El `4000` es el **código**; el disponible real
  es `400.00 kg`. La validación de consumo usa 400 kg (correcto).
- Acción: **ninguna funcional**. Opcional (UX): prefijar el código (p.ej. `Cód. 4000`) para no
  confundirlo con la cantidad. Verificación aparte: si el usuario realmente **ingresó** 4000 kg y
  solo quedaron 400, es un tema del **ingreso** en inventario-gestión, no del consumo.

## Reglas de negocio / invariantes
- Un lote de producción proviene 1:1 de un levante cerrado → hereda su `Lote` base (`lote_id`).
- `produccion_diaria.lote_id` = `lote_postura_produccion.lote_id` (mismo `Lote` base que levante).
- Jerarquía de lotes máx. 2 niveles (ya validado en `LoteService`); no se altera.

## Casos de prueba
1. **Unit (Application/Calculos o servicio):** cerrar levante con `LoteId=X`, `LotePadreId=Y` →
   el `LotePosturaProduccion` resultante tiene `LoteId=X`, `LotePadreId=Y`.
2. **Regresión:** demás campos del lote de producción idénticos a los previos (nombre `P-...`,
   aves iniciales, huevos, etc.).
3. **E2E:** cerrar levante → abrir producción → guardar seguimiento producción → **200** (no 400).
4. **Backfill:** fila producción con `lote_id NULL` + levante con `lote_id` → tras script, queda
   poblado; re-ejecutar script = 0 filas afectadas.
5. **Inventario:** con stock 400 kg, ingresar 3000 → bloquea (correcto); ingresar ≤400 → permite.

## Validación
- `cd backend && dotnet build` (0 errores, sin nuevas advertencias) + `dotnet test`.
- No levantar procesos huérfanos; no tocar la BD compartida.

## Nota de coordinación (repo/BD compartidos)
- Hay una sesión paralela trabajando en el mismo repo (Corrida Panamá Engorde) que **también**
  planea una migración EF. Para **no** chocar el `ModelSnapshot`/historial EF, este fix usa
  **SQL crudo** para el backfill (no toca migraciones). Si se decide auto-aplicar en deploy vía
  migración EF, coordinar el orden de `migrations add` con esa sesión.
- BD local `sanmarinoapplocal:5433` es **compartida** entre checkouts → no migrar/actualizar sin avisar.
