# Fix `ENGORDE_EC`: la doble validación de engorde apunta a una tabla fantasma

**Fecha:** 15ago26 · **Origen:** validación módulo por módulo de los 14 flags de comportamiento
(pedido del usuario mientras corría back+front para probar).

---

## 1. El problema (auditado, no supuesto)

El único formulario de seguimiento diario de pollo engorde del front hace su CRUD contra el
controller **Ecuador**:

```
frontend/src/app/features/aves-engorde/services/seguimiento-aves-engorde.service.ts:170
  baseUrl = `${environment.apiUrl}/SeguimientoAvesEngordeEcuador`
```

…pero pide pendientes y valida con el literal **`'ENGORDE'`**:

```
frontend/src/app/features/aves-engorde/pages/seguimiento-aves-engorde-list/…component.ts:186,207
  this.validacionSvc.pendientes('ENGORDE', loteId)
  this.validacionSvc.validar('ENGORDE', seguimientoId)
```

Y los **dos** services de engorde escriben en la **misma** tabla `seguimiento_diario_aves_engorde`
(`SeguimientoAvesEngordeEcuadorService.Crud.cs:130` usa `_ctx.SeguimientoDiarioAvesEngorde`), mientras
que las tres ramas `ENGORDE_EC` de `ValidacionSeguimientoService` leen el DbSet
`SeguimientoDiarioAvesEngordeEcuador` → tabla `seguimiento_diario_aves_engorde_ecuador`.

Esa tabla **no existe**: `to_regclass('public.seguimiento_diario_aves_engorde_ecuador')` devuelve
NULL en la BD local, aunque la migración `20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry`
figure como aplicada en `__EFMigrationsHistory` (la `_panama` sí existe). La propia migración V1 ya la
trataba como opcional: `IF to_regclass(...) IS NOT NULL … RAISE NOTICE 'no existe en este entorno'`.

### Consecuencias con el flag encendido (ItalcolPanama, `companies.id = 5`)

1. **Guardar revienta.** `SeguimientoAvesEngordeEcuadorService.Crud.cs:40` llama
   `AsegurarPuedeRegistrarDiaAsync(ENGORDE_EC, …)` → `LeerPendientesDelLoteAsync` consulta la tabla
   inexistente → Postgres 42P01.
2. **Validar no descontaría.** Aunque la tabla existiera estaría vacía. El front valida con
   `'ENGORDE'`, `LeerEstadoAsync` encuentra el registro (misma tabla) pero las reservas se
   escribieron con `OrigenModulo = 'ENGORDE_EC'` y `ValidarAsync` las filtra por módulo
   (`ValidacionSeguimientoService.Validar.cs:44`) → **0 filas** → marca `validado = true` sin
   descontar alimento ni aves, y la reserva queda activa para siempre.

**Regla aplicada (CLAUDE.md §🔍 EL CÓDIGO MANDA):** manda el código de hoy. El service escribe en la
tabla compartida ⇒ la validación tiene que leer la compartida. No se resucita la tabla partida.

---

## 2. Enfoque arquitectónico

`ENGORDE` y `ENGORDE_EC` son **dos services sobre el mismo esquema**, no dos esquemas. Se colapsan las
ramas del `switch` para que ambos módulos resuelvan contra `_ctx.SeguimientoDiarioAvesEngorde`.

Colapsar la tabla **no alcanza**: aunque el registro se encuentre, `ValidarAsync` filtra las reservas
por `OrigenModulo == modulo`, así que una reserva escrita como `ENGORDE_EC` sigue siendo invisible para
un `validar('ENGORDE')`. Por eso se agrega **`ModuloSeguimiento.Canonico()`**: la reserva se **guarda y
se busca** siempre con `ENGORDE`.

Se unifica la **clave**, no el **vocabulario**: los dos literales siguen siendo válidos en la API y
`EsEngorde()` sigue reconociendo a los dos. Y se puede unificar sin migrar datos porque **no hay una
sola fila** en `seguimiento_reserva_alimento` ni en `seguimiento_reserva_aves` (verificado en local; en
prod el flag recién se enciende con el deploy de la migración `20260815130000`). Si algún día hubiera
reservas viejas con el literal de Ecuador, habría que backfillearlas antes.

El permiso no cambia: `PermisoValidar` ya mandaba los dos literales al mismo
`seguimiento_engorde.validar` por su rama `_`.

La entidad `SeguimientoDiarioAvesEngordeEcuador`, su `Configuration` y el `DbSet` **se dejan mapeados**
(no se tocan): desmapearlos cambia el ModelSnapshot y obliga a una migración de esquema sobre una
tabla que en algunos entornos existe y en otros no. Quedan sin uso y documentados como tales.

---

## 3. Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Infrastructure/Services/ValidacionSeguimiento/ValidacionSeguimientoService.cs` | Colapsar las 3 ramas `ENGORDE_EC` (`LeerEstadoAsync:123`, `MarcarValidadoAsync:184`, `LeerPendientesDelLoteAsync:247`) con las de `ENGORDE`, leyendo `_ctx.SeguimientoDiarioAvesEngorde` |
| `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` | Agregar `AsegurarPuedeRegistrarDiaAsync(Reproductora, …)` en el alta (único de los 5 que no lo tenía) |
| `Domain/Entities/SeguimientoDiarioAvesEngordeEcuador.cs` | Sólo doc-comment: queda sin uso, la tabla partida no es la fuente |
| `tests/ZooSanMarino.Application.Tests/ValidacionSeguimientoCalculosTests.cs` | Tests de que los dos literales de engorde son equivalentes |
| Migración data-only `2026081514xxxx_SolucionarTicketEngordeEcTablaCompartida` | Caso + historia + tareas en ItalJira, en `SOLUCIONADO`/`LISTO` |

**Sin cambios de BD/schema.** Sin cambios en el front (el `'ENGORDE'` que ya manda pasa a ser correcto
para las dos vías).

---

## 4. Reglas de negocio

- Con el flag **OFF** el comportamiento queda **byte a byte idéntico**: las ramas tocadas sólo se
  ejecutan cuando `RequiereValidacionSeguimientoDiario` está encendido.
- Un registro de engorde es **el mismo registro** llegue por `ENGORDE` o `ENGORDE_EC`: mismo id, misma
  tabla, mismo lote.
- La reserva sigue siendo la cola de efectos: lo que se descuenta al validar es exactamente lo que se
  separó al guardar.
- Reproductora: con vencidos sin confirmar, el alta de un día nuevo se rechaza — que es lo que el flag
  promete por escrito en `Company.RequiereValidacionSeguimientoDiario`.

---

## 5. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | `EsEngorde('ENGORDE')` y `EsEngorde('ENGORDE_EC')` | ambos `true` |
| 2 | Los dos literales comparten permiso de validar | mismo string |
| 3 | Flag OFF, alta de engorde | sin reserva, descuento al guardar (igual que antes) |
| 4 | Flag ON, alta por el controller Ecuador | **no revienta**; reserva creada |
| 5 | Flag ON, `validar('ENGORDE', id)` de un registro reservado como `ENGORDE_EC` | descuenta de verdad (kg y aves > 0) |
| 6 | Flag ON, reproductora con un vencido sin confirmar | el alta del día nuevo se rechaza |
| 7 | Flag OFF, reproductora | el alta pasa (sin bloqueo) |

---

## 6. Hallazgo que NO se resuelve acá

**«Disponible = stock − reservas activas» no está enganchado.**
`IValidacionSeguimientoService.ReservadoPorItemAsync` y `ReservadoDeAvesAsync` están declarados e
implementados (`ValidacionSeguimientoService.Reservas.cs:121,143`) pero **no los llama nadie** en todo
el backend. El disponible que ven inventario y los formularios sigue siendo el stock completo.

No entra en esta entrega porque exige una decisión de diseño que no es mía: o se le resta la reserva a
`Quantity` en `GET /api/InventarioGestion/stock` —y entonces la pantalla de inventario deja de mostrar
la existencia física, que es justo lo que operación concilia—, o se agrega un campo `Reservado`/
`Disponible` al DTO y se toca el front de los 4 módulos. Queda como tarea **pendiente** en el mismo
caso de ItalJira, no como solucionada.

---

## 7. Validación

- `dotnet build` 0 errores, sin advertencias nuevas.
- `dotnet test` en verde (2574 + los nuevos).
- Smoke con el flag ON en ItalcolPanama: guardar engorde no revienta, validar descuenta.
- Smoke con el flag OFF en otra empresa: cero cambios visibles.
- Backend local apagado y `:5002` libre al terminar.
