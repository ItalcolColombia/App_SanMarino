# Tracker — Seguimiento diario: alimento desde inventario NUEVO + alimento distinto para machos

Plan: [inventario_nuevo_y_alimento_macho_seguimiento_plan.md](fase_de_desarrollo/inventario_nuevo_y_alimento_macho_seguimiento_plan.md)

**Alcance:** Levante + Producción · Colombia · 100 % frontend (sin migración/backend).
**Decisiones usuario:** (1) módulos = Levante + Producción postura · (2) UI machos = selector de ítem por sexo.

---

## Fase 0 — Validación (COMPLETA)
- [x] Confirmar que el dropdown de Colombia lee del inventario viejo (`getInventory`)
- [x] Confirmar "moises" (208) solo en `item_inventario_ecuador`, no en `catalogo_items`
- [x] Confirmar backend ya suma (mismo alimento) y descuenta ambos caminos (camino-1/camino-2)
- [x] Medir contrato de ids: 1 ítem nuevo (moises), 0 colisiones id iie↔catalogo_items
- [x] Plan + tracker

## Fase 1 — Levante · Parte 1 (catálogo desde inventario nuevo) ✅ build OK
- [x] Cargar catálogo Colombia desde `getItemsByType` + stock `getStock({farmId})` nivel granja (flag `usaInventarioGestion`)
- [x] Cargar mapa `codigo→catalogo_items.id` (disambiguación de ids)
- [x] Helper `buildItemPersistFields` (migrado→catalogItemId / nuevo→itemInventarioEcuadorId) + `nombre` por ítem
- [x] `onSave`: enviar id por ítem según contrato; quitar `colombiaApplyInventoryDelta` + `buildFoodKgMap*` (doble descuento muerto)
- [x] Compat edición: `traducirIdsColombiaAlEditar` (registros viejos con id de catalogo_items → id de dropdown por código)
- [ ] Ocultar "Tipo de ítem" en Colombia — DEFERIDO (selector sigue visible, default 'alimento' funciona)
- [x] `yarn build` OK (78s, solo warning bundle budget preexistente)

## Fase 2 — Levante · Parte 2 (alimento distinto para machos) ✅ build OK (modal)
- [x] Decoplar bloque alimento: sub-bloque Hembras + sub-bloque Machos (ítem independiente) — HTML + form groups
- [x] Mapear a `itemsHembras[]` / `itemsMachos[]` independientes en `onSave` (machos desde su propio array)
- [x] `populateForm`: filas de hembras sin `cantidadMachos`; machos cargan en su array
- [x] `yarn build` OK (64s)
- [ ] Desglose H vs M visible en la TABLA de seguimiento diario — PENDIENTE (hoy la columna colapsa a código corto PRE/INI; datos H/M ya separados en `consumoKgHembras/Machos` y en Excel)

## Fase 3 — Producción (replicar Parte 1 + Parte 2) ✅ build OK
- [x] Catálogo Colombia nuevo + stock nivel granja en `modal-seguimiento-diario` (flag `usaInventarioGestion`)
- [x] Contrato de ids (`buildItemPersistFields`) + `nombre` por ítem + compat edición (`traducirIdsColombiaAlEditar`)
- [x] Guards en `cargarInventarioGranja`/`consultarInventario` (no tocar inventario viejo para CO/EC/PA)
- [x] Selector de ítem por sexo: YA existía en Producción (Hembras/Machos con item propio) — sin cambios de HTML
- [x] `yarn build` OK (65s)
- [ ] NO tiene doble-descuento manual (ya era 100% backend) — nada que quitar

## Fase 3b — Bug: "disponible" no descontaba reservas del propio formulario (sin guardar) ✅ build OK
- Reportado por el usuario probando Levante: mismo ítem en Hembras y Machos mostraba "Disponible: 6900 kg" en
  AMBAS filas aunque Hembras ya tenía 900 kg cargados (sin guardar) — debía mostrar 6000 kg restantes en Machos.
  Riesgo: permitía asignar más de lo que hay en stock sin ningún aviso hasta que el backend rechazara/reportes mal.
- [x] Levante: `sumaReservadaEnOtrasFilas` (suma kg del mismo ítem en otras filas de itemsHembras/Machos/Generales,
      excluyendo la fila actual) + `getCantidadDisponibleAjustada` + `getMaxPermitidoKg`/`cantidadExcedeDisponible`
      ahora reciben `excludeControl` y descuentan esa reserva. `getItemDisplayText` también ajustado (dropdown Y hint).
- [x] Producción: mismo patrón agregado desde cero (`getCantidadDisponibleAjustada`, `cantidadExcedeDisponible`,
      `hasCantidadExcedida` — antes NO existía ninguna validación de tope ahí) + wired a `[disabled]` del submit.
- [x] `yarn build` OK (66s) — todos los call sites de `getItemDisplayText`/`getCantidadDisponible*` verificados

## Fase 4 — Validación E2E (PRUEBA HUMANA en curso)
- [x] Levante Colombia: confirmado que el dropdown lista inventario nuevo, incluye "moises" (6900 kg)
- [x] Bug encontrado y arreglado en la prueba: disponible no descontaba reservas de otras filas (ver Fase 3b)
- [ ] Guardar con "moises" (nuevo) y con ítem migrado → descuenta el inventario NUEVO; el viejo no se toca
- [ ] Alimento distinto para machos → dos descuentos; mismo alimento → suma en uno
- [ ] Producción Colombia: idem
- [ ] EC/PA sin regresión (catálogo/stock/guardado idénticos)
- [ ] (Backend `X-Secret-Up` rotativo impide verificación por curl; se valida en navegador)

## Fase 5 — Commits micro por componente ✅
Historial reconstruido en commits pequeños y revisables (uno por servicio/componente/concern), en vez de un
solo commit gigante. Cada commit de código quedó verificado con `yarn build` en el estado final:
- `feat(seguimiento-levante): ItemSeguimientoDto gana campo nombre`
- `feat(seguimiento-produccion): ItemSeguimientoDto gana campo nombre`
- `feat(seguimiento-levante): Colombia lee alimento del inventario nuevo + alimento independiente por sexo`
- `feat(seguimiento-levante): UI bloques Hembras/Machos independientes`
- `feat(seguimiento-produccion): Colombia lee alimento del inventario nuevo`
- `fix(seguimiento-levante): disponible no descontaba reservas de otras filas del mismo formulario`
- `fix(seguimiento-levante): wiring del disponible ajustado en dropdown y hints`
- `fix(seguimiento-produccion): disponible ajustado + validacion de tope (no existia)`
- `fix(seguimiento-produccion): wiring del disponible ajustado + bloquear guardado si excede`
- `docs(seguimiento-inventario): plan de catalogo de alimento nuevo + alimento por sexo`
- `docs(seguimiento-inventario): tracker de estado`
