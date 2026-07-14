# Plan — Bloquear venta de lotes cerrados / corridas anteriores en "Venta por granja" (Movimientos Pollo Engorde)

## 1. Problema

En el modal **"Nueva venta por granja (despacho)"** (`movimientos-pollo-engorde`), la tabla
"Cantidades por galpón y lote" muestra **todos** los lotes Ave Engorde de la granja/núcleo
seleccionados, sin filtrar por estado ni por "corrida vigente". Confirmado en el código:

- `LoteAveEngordeService.GetAllAsync()` (backend) no filtra por `estado_operativo_lote`.
- `MovimientoPolloEngordeFilterDataService.GetFilterDataAsync()` solo filtra por granjas permitidas.
- `movimientos-pollo-engorde-list.component.ts#refreshLotesParaVentaGranja()` (front) no filtra por
  estado ni por corrida — trae todos los lotes de la granja/núcleo, abiertos o cerrados.

Consecuencia: en un galpón con varias corridas históricas (ej. lotes `2601`, `2602`, `2603`), el
usuario puede cargar cantidades en `2601`/`2602` (cerrados o de una corrida anterior) aunque la
corrida vigente del galpón ya sea `2603`. Esto se agrava porque lotes `Cerrado` pueden reportar
`disponibles > 0` por el bug ya documentado en
`fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md`.

## 2. Alcance de esta iteración (decisión del usuario)

**Solo FRONT-END.** No se toca el backend de `MovimientoPolloEngorde` ni el filtrado de
`filter-data` — la regla se aplica íntegramente en el modal Angular, deshabilitando los campos de
cantidad de los lotes bloqueados. Sí se crea el **permiso** nuevo vía migración EF (dato en la
tabla `permissions`), para que el módulo de Roles y Permisos pueda asignarlo a los roles que deban
poder saltarse el bloqueo (ej. soporte / corrección de datos).

> Nota para una futura iteración (fuera de alcance): reforzar la misma regla en
> `CreateVentaGranjaDespachoAsync` (backend) como defensa en profundidad real contra un usuario que
> edite el DOM o llame al endpoint directamente. Se documenta pero no se implementa ahora.

## 3. Regla de negocio

Por cada **galpón** dentro de las líneas de venta (`ventaLineasGranja`, agrupadas en
`gruposVentaPorGalpon`):

1. Se determina el **lote vigente** (corrida activa) = el lote con `fechaEncaset` más reciente
   dentro de ese galpón (desempate: `loteAveEngordeId` más alto, mismo criterio que ya usa la
   convención de nomenclatura `2601 &lt; 2602 &lt; 2603` del proyecto).
2. Una línea queda **bloqueada** si:
   - su lote tiene `estadoOperativoLote` == `"Cerrado"` (case/trim-insensitive, mismo patrón que
     `seguimiento-aves-engorde-list.component.ts` y `modal-liquidacion-lote-engorde.component.ts`), **o**
   - su lote **no es** el vigente del galpón (existe otro lote más reciente en el mismo galpón).
3. Si el usuario tiene el permiso `movimientos_pollo_engorde.vender_lotes_cerrados`, el bloqueo no
   aplica (puede cargar cantidades igual que hoy).
4. Un galpón con un solo lote nunca queda bloqueado por "corrida anterior" (es trivialmente el
   vigente); solo se bloquea si ese único lote está `Cerrado`.

## 4. Cambios — Frontend

Módulo `frontend/src/app/features/movimientos-pollo-engorde/` (referencia canónica de clean code
del proyecto, ya usa `funciones/` + `models/`).

1. **`models/venta-granja.model.ts`** — agregar a `VentaLineaGranja`:
   `bloqueada?: boolean; motivoBloqueo?: string;`

2. **`funciones/detectar-lotes-bloqueados-venta.funcion.ts`** (nuevo, función pura) —
   `marcarLotesBloqueadosVenta(lineas: VentaLineaGranja[], lotes: LoteAveEngordeDto[]): VentaLineaGranja[]`
   implementando la regla de la sección 3. Se agrega al índice de `funciones/README.md`.

3. **`components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component.ts`**:
   - Inyectar `UserPermissionService` (mismo patrón que otros componentes del core/auth).
   - Getter `puedeVenderLotesCerrados: boolean` → `permService.has('movimientos_pollo_engorde.vender_lotes_cerrados')`.
   - En `loadVentaGranjaLineas()`, tras armar `ventaLineasGranja`, aplicar
     `marcarLotesBloqueadosVenta` antes de `rebuildGruposVentaPorGalpon()`.
   - En `onLineaCantidadInput()`: si `line.bloqueada &amp;&amp; !puedeVenderLotesCerrados`, ignorar el
     input (revertir el valor del DOM al string actual) y salir — refuerzo además del `disabled` del
     HTML.
   - En `onSubmit()` (rama venta por granja): si `!puedeVenderLotesCerrados` y alguna línea
     `bloqueada` tiene cantidad &gt; 0, bloquear el envío con mensaje de error (defensa adicional
     ante manipulación del DOM).

4. **`modal-movimiento-pollo-engorde.component.html`**:
   - Cada input H/M/X: extender `[disabled]` y `[class.form-input--readonly]` con
     `|| (line.bloqueada &amp;&amp; !puedeVenderLotesCerrados)`.
   - Fila (`&lt;tr&gt;`): clase `venta-granja-table__row-bloqueada` cuando bloqueada y sin permiso.
   - Junto al nombre del lote: badge con `line.motivoBloqueo` ("Lote cerrado" / "Corrida anterior en
     este galpón") cuando `line.bloqueada`.
   - Hint explicativo bajo el título de sección cuando exista al menos una línea bloqueada sin
     permiso.

5. **`modal-movimiento-pollo-engorde.component.scss`** — estilos `.venta-granja-table__row-bloqueada`
   (gris neutro, no rojo — no es un estado de error/peligro, regla de marca del proyecto) y
   `.venta-granja-lote-badge` (ámbar, mismo tono que `.modal__estado[data-estado='pendiente']`).

## 5. Cambios — Backend (solo el permiso, sin lógica de negocio)

Nueva migración EF **pura de datos** (mismo patrón que
`20260606191515_SeedPermisosBotonesMovimientosPolloEngorde.cs`), generada con
`dotnet ef migrations add` (Designer.cs correcto) y editada para el `INSERT` idempotente:

```sql
INSERT INTO public.permissions (key, description)
SELECT 'movimientos_pollo_engorde.vender_lotes_cerrados',
       'Movimientos Pollo Engorde: permite cargar cantidades en Venta por granja para lotes cerrados o de una corrida anterior en el mismo galpón (bypass del bloqueo por defecto)'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions WHERE key = 'movimientos_pollo_engorde.vender_lotes_cerrados');
```

`Down()` elimina de `menu_permissions`/`role_permissions`/`permissions` igual que la referencia. No
se asigna a ningún rol desde la migración (se hace desde la pantalla de Roles y Permisos, como ya
es la convención del proyecto).

## 6. Casos de prueba

- Galpón con 1 solo lote abierto → nunca bloqueado.
- Galpón con lotes `2601` (Cerrado), `2602` (Abierto, más antiguo), `2603` (Abierto, más nuevo) →
  solo `2603` editable sin permiso; `2601` y `2602` deshabilitados con badge.
- Usuario con `movimientos_pollo_engorde.vender_lotes_cerrados` → todas las líneas editables
  (sujeto igual al tope `maxH/maxM/maxX === 0`, que es una regla distinta y se mantiene).
- Intentar forzar cantidad en una línea bloqueada vía teclado → el valor no se aplica al modelo
  (`onLineaCantidadInput` revierte el input).
- Envío del formulario con una línea bloqueada en cero → no afecta (el filtro de `h+m+x&gt;0` de
  `buildVentaGranjaDespachoDto` ya la excluye).
- `dotnet ef database update` local aplica el nuevo permiso sin error; reintentar es idempotente
  (`WHERE NOT EXISTS`).
- `yarn build` (frontend) 0 errores.

## 7. Validación

- `cd frontend && yarn build` (0 errores).
- `cd backend && dotnet build` (0 errores, sin nuevas advertencias).
- `dotnet ef database update` local (DB `sanmarinoapplocal`, puerto 5433) sin error.
- Prueba manual en navegador: abrir "Nueva venta por granja" en una granja con un galpón
  multi-corrida y verificar el bloqueo visual + funcional, con y sin el permiso asignado al rol del
  usuario de prueba.
