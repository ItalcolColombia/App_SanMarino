# Plan — Upgrade Angular 20 → 22 (+ refactor de deprecaciones)

> Estado inicial: Angular **20.3.x**, TS 5.8, RxJS 7.8, zone.js 0.15, Node 22.15, Yarn 1.22.
> Objetivo: **Angular 22** (última estable). Angular obliga un major a la vez → **20→21→22**.
> Trabajo de sesión ya commiteado (backend/front/docs) → el upgrade queda AISLADO y reversible.

## Enfoque (oficial, seguro)
1. Cada salto vía `ng update @angular/core@N @angular/cli@N @angular/cdk@N` (aplica migraciones/schematics automáticas que arreglan la mayoría de deprecaciones).
2. Tras cada salto: `yarn build` + `yarn test` (o `ng test`), arreglar errores/deprecaciones manuales que queden.
3. Validar en preview que la app corre (login + módulos clave).
4. Deprecaciones restantes → refactor manual (según warnings del build/compilador).

## Riesgos / cuidados
- Breaking changes acumulados de 2 majors. Validar en CADA salto (no encadenar a ciegas).
- Peers: TypeScript puede necesitar bump (21/22 exigen TS más nuevo) → lo maneja `ng update` o bump manual.
- `ng update` exige árbol git ~limpio → usar `--allow-dirty` (los pendientes son local/junk fuera de frontend).
- Refactor ≠ cambio de comportamiento: preservar contratos/lógica; el rebrand y features de la sesión no se tocan.

## Pasos
- [ ] Salto 1: `ng update @angular/core@21 @angular/cli@21 @angular/cdk@21` → build + test + fix
- [ ] Salto 2: `ng update @angular/core@22 @angular/cli@22 @angular/cdk@22` → build + test + fix
- [ ] Refactor deprecaciones restantes (warnings compilador)
- [ ] Validación preview (login + módulos) + commit por salto

## Casos de prueba
- `yarn build` 0 errores tras cada salto.
- App levanta en :4200, login OK, gestión-inventario / lotes / seguimientos renderizan sin errores de consola.
- Sin regresión visual del rebrand ni de la funcionalidad de la sesión.
