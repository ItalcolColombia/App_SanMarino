# Integración de ramas pendientes a `main` + PR a producción (13-sep-2026)

## Objetivo

Traer a `main` todo el trabajo terminado que vive en ramas y no llegó, dejar `main` compilando y con
los tests en verde (el deploy del PR #106 cayó en el gate), y abrir el PR `main → main-produccion`.

## Inventario (medido con `git cherry` contra `origin/main`, no por nombre de rama)

PRs abiertos en GitHub: **0**. Commits locales de `main` sin pushear: **2** (`9de63bd`, `16917e4`).

| Rama | Commit | Veredicto | Motivo |
|---|---|---|---|
| `claude/stoic-davinci-f7883f` | `956c7be` refactor(catalogo-alimentos): elimina form huérfano | **Se integra** (cherry-pick limpio) | `CatalogoAlimentosFormComponent` sigue en `main` sin caller. |
| `claude/priceless-bhabha-c60ee5` | `84bf74f` fix(gestion-inventario): Concepto duplicado | **Se porta con cambios** | El defecto sigue vivo (empresas 3 y 5 con `Otros insumos`/`Otros Insumos`). Ver abajo. |
| `claude/eager-dijkstra-a4ddda` | `f68e8b9` SetAuditFields "Collection was modified" | **Descartada** | `TouchUserUpdatedAt` se eliminó de verdad en `f33c700` (19-ago); el bloque que arreglaba ya no existe. |
| `claude/heuristic-perlman-f10ea4` | `b853e95` saldo de bultos restaba doble | **Descartada** | Portado en `473ac16` (V41) + `d3a91dd`/`26007db`; `main` ya tiene `ReasonConsumoDiario`. |
| `devpilot/e37cb258` | `9079492` "fix proceso" | **Descartada** | Solo artefactos `.devpilot/` del 10-jul, sin código. |

## Port de `84bf74f` — lo que cambió respecto del commit original

1. **La migración apuntaba a una tabla que ya no existe.** Su SQL usa `item_inventario_ecuador` /
   `item_inventario_ecuador_id`; el rename neutro dejó `item_inventario` / `item_inventario_id`.
   Mergeada tal cual, el arranque en ECS habría fallado la migración (exit 139 + rollback silencioso).
   Se reescriben los nombres en la migración y en sus dos espejos `.sql`.
2. **Timestamp nuevo `20260913170000`** (antes `20260805180000`): queda después de todas las
   migraciones actuales. Designer clonado de `20260913160000` (data-only, snapshot sin tocar).
3. **`InventarioGestionService` se partió en `Funciones/`**: el cambio de `GetHistoricoFiltrosAsync`
   (GROUP BY + `EtiquetasFiltroInventarioCalculos`) va en `InventarioGestionService.Consulta.cs`.
4. README de `funciones/` del front: se conservan las filas de `main` y se agrega la nueva.

## Validación

- Migración simulada en transacción + `ROLLBACK` sobre `sanmarinoapplocal`: regla 1 = 12, regla 2 = 1,
  regla 3 = 1; después 0 grupos duplicados, ítems por empresa idénticos; 2.ª pasada `UPDATE 0` ×3.
- `dotnet test -c Release` (backend), `dotnet build` API, `yarn test` headless, `yarn build`.
- Los 7 gates del workflow (`verificar-*.js`).

## Casos de prueba

- `EtiquetasFiltroInventarioCalculosTests` (back) y `conceptos-catalogo.funcion.spec.ts` (front):
  una etiqueta por grupo, gana la variante más usada, empate por orden ordinal.
- Con flag/datos de otras empresas: la regla es por empresa ⇒ ninguna empresa sin duplicados cambia.

## Entrega

Rebase lineal sobre `main`, fast-forward de `main` (local y `origin`), PR `main → main-produccion`.
El merge del PR dispara el deploy: queda a decisión del usuario.
