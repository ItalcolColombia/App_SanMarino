# Plan — El nombre de lote es único POR GALPÓN, no por granja

**Origen:** ticket «Falla en fecha registro levante semana 6 lote A374A galpón 4» (LA ESMERALDA /
Módulo II). Al diagnosticarlo aparecieron dos defectos laterales, ninguno de los cuales es la causa
del ticket (esa fue `tipo_alimento varchar(100)`, resuelta en `2a35d63` y desplegada el 07-ago-2026).

---

## 1. Qué está mal

### 1.1 Backend — la guarda de nombre duplicado usa el alcance equivocado

`LoteService.EnsureLoteNombreNoDuplicadoAsync` (agregada el 17-jul-2026, commit `b917ad9`) rechaza el
alta/edición cuando ya existe un lote activo con el mismo nombre **en la misma compañía + granja**:

```
Ya existe un lote activo con el nombre 'A374A' en esta granja.
```

**Regla de negocio real (confirmada por el usuario):** un mismo nombre de sublote **puede repetirse en
galpones distintos** de la misma granja. Es el patrón vigente en producción:

| lote_id | nombre | galpón | aves | creado |
|---|---|---|---|---|
| 114 | A374A | G0326 (4) | 15.487 H + 2.246 M | 22-may-2026 |
| 115 | A374B | G0325 (3) | 15.346 H + 2.293 M | 26-may-2026 |
| 116 | A374A | G0324 (2) | — (se puebla por traslado) | 28-may-2026 |
| 117 | A374B | G0323 (1) | — (se puebla por traslado) | 28-may-2026 |

(114 + 115 = 30.833 H + 4.539 M = exactamente el lote base `A374`.) La empresa 4 tiene el mismo patrón
(`LOTE 235A` en `BG200101` y en `BG180201`).

⇒ Con la guarda de hoy **ninguno de esos lotes podría volver a crearse**. Es una regresión: bloquea una
operación legítima. El selector de letra (`GetLetrasDisponiblesAsync`, alcance **por galpón**) sí está
bien y **no se toca** — es la guarda la que quedó fuera de fase con él.

### 1.2 Frontend — el combo «Lote» del modal de seguimiento diario muestra «— Seleccione —»

`modal-create-edit.component.ts:1211` fija el control con `String(this.selectedLoteId)` (texto `"114"`)
mientras las opciones bindean `[ngValue]="l.loteId"` (número `114`). Angular compara por identidad ⇒
ninguna opción matchea ⇒ el select pinta el placeholder aunque el lote esté fijado por el contexto.
No impide guardar (`onSave` resuelve el lote comparando con `String(...)` sobre `getRawValue()`), pero
le hace creer al operario que no hay lote seleccionado — es lo que se ve en la captura del ticket.

---

## 2. Enfoque

| Capa | Cambio |
|---|---|
| `Application/Calculos` | **NUEVO** `LoteNombreDuplicadoCalculos` — lógica PURA: normalización del nombre y del galpón, decisión de duplicado y mensaje. Sin EF. |
| `Infrastructure/Services/LoteService.cs` | `EnsureLoteNombreNoDuplicadoAsync` recibe el `galponId`, trae los homónimos activos de la granja (conjunto mínimo) y **delega la decisión** al cálculo puro. Los 2 llamadores (Create/Update) pasan `dto.GalponId`. |
| `frontend/.../modal-create-edit` | `[compareWith]` en el `<select>` de lote + método `compararLoteId`. Sin tocar el valor del control ⇒ el payload sigue viajando igual. |
| `tests/ZooSanMarino.Application.Tests` | **NUEVO** `LoteNombreDuplicadoCalculosTests`. |

**Sin cambios de BD.** No hay índice único sobre `lotes(lote_nombre)`; la unicidad es de aplicación.

---

## 3. Reglas de negocio (después del cambio)

1. Dos lotes activos de la **misma compañía + granja + galpón** no pueden llamarse igual (comparación
   case-insensitive, con `Trim`).
2. El mismo nombre **sí** puede repetirse en **otro galpón** de la misma granja, y en otra granja.
3. Un lote **sin galpón** (`null`/vacío) forma su propio grupo: no colisiona con los que sí tienen
   galpón, y entre ellos rige la regla 1.
4. `Update` no se auto-reporta como duplicado (`excludeLoteId`).
5. Los lotes con `deleted_at` no cuentan.
6. Nombre vacío ⇒ no se valida (lo cubre el validador de campo requerido).

---

## 4. Casos de prueba (xUnit, `LoteNombreDuplicadoCalculosTests`)

| # | Escenario | Esperado |
|---|---|---|
| 1 | `A374A` nuevo en `G0324`, homónimo activo en `G0326` | **permite** (caso real 114/116) |
| 2 | `A374A` nuevo en `G0326`, homónimo activo en `G0326` | **rechaza** |
| 3 | Homónimo en el mismo galpón con distinta capitalización (`a374a`) | **rechaza** |
| 4 | Homónimo en el mismo galpón con espacios (` G0326 `) | **rechaza** (normaliza `Trim`) |
| 5 | Nuevo sin galpón, homónimo con galpón | **permite** |
| 6 | Nuevo sin galpón, homónimo sin galpón | **rechaza** |
| 7 | Nuevo con galpón, homónimo sin galpón | **permite** |
| 8 | Sin homónimos | **permite** |
| 9 | Nombre vacío / solo espacios | **permite** (no valida) |
| 10 | Mensaje con galpón vs mensaje sin galpón | textos distintos y explícitos |

**Smoke manual (no automatizado):** crear en local un lote `A374A` en un galpón libre de granja 20 ⇒
201; repetirlo en el mismo galpón ⇒ 400 con el mensaje nuevo.

---

## 5. Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas)
- `cd backend && dotnet test` (verde, incluidos los 10 casos nuevos)
- `cd frontend && yarn build` (0 errores; solo el warning preexistente de *bundle budget*)
- Sin procesos huérfanos.

## 6. Fuera de alcance

- **No** se renombran los lotes 116/117: su nombre repetido es legítimo (regla 2).
- **No** se toca `GetLetrasDisponiblesAsync` — su alcance por galpón es el correcto.
- **No** se agrega índice único en BD: la regla admite `null` en galpón y la comparación es
  case-insensitive; un índice acá pediría migración + limpieza previa y no aporta al ticket.
