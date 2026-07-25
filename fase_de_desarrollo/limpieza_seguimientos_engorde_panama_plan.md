# Plan — Limpieza seguimientos diarios Panamá (reproductora + pollo engorde) para re-carga masiva

**Fecha:** 2026-07-25 · **BD de trabajo:** local `sanmarinoapplocal:5433` (dump de producción descargado hoy) · **Despliegue:** migración EF solo-datos (se aplica sola en el deploy, `Database__RunMigrations=true`).

## Objetivo

Dejar en cero los registros de **seguimiento diario reproductora engorde** y **seguimiento diario pollo engorde** de TODOS los lotes de Panamá (empresa `ItalcolPanama`, id 5), para re-digitarlos con la **carga masiva** recién implementada (evita errores de digitación). La limpieza debe dejar el estado consistente para que la re-carga no duplique efectos (stock de alimento, histórico unificado).

## Auditoría previa (BD local = copia de prod, 2026-07-25)

| Qué | Cifra |
|---|---|
| Granjas Panamá (company 5) | 13 (ids 96–108, todas con departamento 21/23 → país 3) |
| Lotes engorde | 39 (32 vigentes **todos Abiertos** + 7 soft-deleted) · 0 liquidaciones |
| Lotes reproductora engorde | 64 |
| `seguimiento_diario_aves_engorde` (Panamá) | **678** (178 `origen_cruce` de SYSTEM_CRUCE; 24 en lotes soft-deleted; 482 creados por Puente/usuario 1369984321, 18 manuales) |
| `seguimiento_diario_lote_reproductora_aves_engorde` (Panamá) | **325** (274 confirmados) |
| `inventario_gestion_movimiento` ligados a seguimientos (granjas Panamá) | **154** = 140 `Consumo` (124.268,160 kg) + 14 `Ingreso` devoluciones (9.569,110 kg) |
| `lote_registro_historico_unificado` ligados (espejo por trigger INSERT) | **154** = 140 `INV_CONSUMO` (4 anulados) + 14 `INV_INGRESO` |
| `inventario_gestion_stock` Panamá | 14 filas, 51.355,959 kg |
| Neto de alimento a devolver al stock | **114.699,050 kg** (consumos − devoluciones), 13 ubicaciones; **4 sin fila de stock** (granja 106: fueron eliminadas con "EliminacionStock") |
| `inventario_aves` / `movimiento aves` Panamá | **0** (el retiro de aves busca en `lotes`, que company 5 no usa → nunca aplicó) |
| FKs que referencien las tablas de seguimiento | Ninguna |
| Guard otras empresas (NO deben cambiar) | seg engorde 4.810 · seg repro 2 · stock 475 filas / 2.056.837,970 kg · movimientos 8.110 · histórico 10.144 |

### Efectos colaterales confirmados en código (qué revierte la limpieza y por qué)

- `SeguimientoAvesEngordeService.CreateAsync` y `SeguimientoDiarioLoteReproductoraService.CreateAsync` descuentan alimento vía `InventarioGestionService.RegistrarConsumoAsync` → mutan `inventario_gestion_stock` + insertan `inventario_gestion_movimiento` (referencia `Seguimiento aves engorde #id …` / `Seguimiento reproductora #id …`), que un trigger espeja en `lote_registro_historico_unificado` (`INV_CONSUMO`/`INV_INGRESO`). La anulación manual **borra** el movimiento tras revertir stock ⇒ todo movimiento presente tiene su efecto vivo ⇒ `neto = Σ Consumo − Σ Ingreso` por (granja, núcleo, galpón, ítem) es exactamente lo que hay que devolver.
- La carga masiva de engorde **reutiliza `CreateAsync`** (replica los efectos) y la de reproductora confirma automático (dispara el cruce) ⇒ sin devolución de stock la re-carga descontaría doble (y el consumo con stock insuficiente se traga el error → inventario quedaría mal en silencio).
- Retiro de aves: no aplica en Panamá (auditado, 0 filas). Los saldos de aves se derivan de los seguimientos ⇒ borrar los registros "devuelve" las aves solo.
- Trigger `trg_cruce_reproductora_engorde` (AFTER I/U/D por fila): al borrar primero los seguimientos de engorde y después los de reproductora, cada `fn_cruce` corre sobre estado vacío → no-op (no regenera nada). No hace falta `DISABLE TRIGGER` (evita riesgo de permisos/lock en prod).

### Qué NO se toca

Lotes engorde/reproductora (fechas encaset, aves iniciales, corridas), `historial_lote_pollo_engorde` (solo snapshots "Inicio"), ventas (`movimiento_pollo_engorde` + `VENTA_AVES`), ingresos/traslados reales de alimento (Entrada planta/granja, traslados, `EliminacionStock`, `INV_OTRO`), guía genética, y absolutamente nada de otras empresas.

## Diseño — migración EF solo-datos (idempotente)

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Migrations/20260725120000_LimpiezaSeguimientosEngordePanama.cs` + Designer clonado de `20260724100000` (sin tocar ModelSnapshot; sin cambios de schema). Empresa resuelta **por nombre** `ItalcolPanama` (no id hardcodeado). 5 sentencias en un solo `Sql(@"…")` (una transacción de migración):

1. `DELETE seguimiento_diario_aves_engorde` de lotes `lote_ave_engorde.company_id = ItalcolPanama` (incluye `origen_cruce` y lotes soft-deleted).
2. `DELETE seguimiento_diario_lote_reproductora_aves_engorde` (join lr→lae, misma empresa). El trigger de cruce queda no-op (paso 1 ya vació engorde).
3. Devolver stock: CTE `mov` = Σ(Consumo) − Σ(Ingreso) por (farm, núcleo, galpón, ítem) de movimientos con referencia de seguimiento en granjas Panamá → **3a** UPDATE de filas de stock existentes (`IS NOT DISTINCT FROM` para núcleo/galpón) y **3b** INSERT de las inexistentes (`neto > 0`, company de la granja, país por `departamentos.pais_id`, unidad kg).
4. `DELETE inventario_gestion_movimiento` ligados (granjas Panamá).
5. `DELETE lote_registro_historico_unificado` ligados (el espejo no cascadea: trigger solo-INSERT).

**Orden crítico:** 3 antes que 4 (el ajuste se calcula de los movimientos que aún existen). **Idempotencia:** re-ejecución → CTE vacía y DELETEs en 0 filas (no-op). **Down():** no-op documentado (los datos se reponen re-cargando por carga masiva, no hay reverso).

## Casos de prueba / verificación (local primero, mismas queries para prod post-deploy)

1. Aplicar con `dotnet-ef` 10 (`~/.dotnet/tools-ef10/dotnet-ef.exe database update`, desde Infrastructure, `--startup-project .`).
2. Post: seg engorde Panamá = 0 · seg repro Panamá = 0 · movimientos ligados = 0 · histórico ligado = 0.
3. Stock Panamá = **18 filas** (14 + 4 nuevas en granja 106) y total ≈ **166.055,009 kg** (51.355,959 + 114.699,050).
4. Guards intactos: otras empresas 4.810 / 2 / 475 filas·2.056.837,970 kg / 8.110 / 10.144; historial "Inicio" 78; ventas 1.
5. Idempotencia: re-ejecutar el mismo SQL por psql → 0 filas afectadas en todo, stock sin cambio.
6. `dotnet build` 0 errores + `dotnet test` verde.

## Riesgos

- **Prod entre hoy y el deploy:** si digitan seguimientos nuevos de Panamá, la migración también los borrará (correcto para el objetivo: TODO se re-carga por carga masiva). Avisar al equipo de Panamá antes del deploy.
- Estado `Reabierto` de lotes reproductora queda como esté (no bloquea: el borrado es por SQL, no por endpoint).
- La re-carga requiere stock suficiente: la devolución del paso 3 lo garantiza para las mismas cantidades. *(Superado por la Parte 2: el inventario arranca de 0 → primero registrar ingresos, después cargar seguimientos.)*

---

# Parte 2 — Inventario de alimento Panamá desde CERO

**Pedido (2026-07-25):** además de los seguimientos, la empresa arranca de 0 también en los registros de inventario: ingresos de alimento, traslados, stock e histórico de inventario.

## Auditoría previa (tras aplicar la Parte 1 en local)

| Qué (empresa ItalcolPanama) | Cifra |
|---|---|
| `inventario_gestion_movimiento` | 40 = 24 Ingreso "Entrada planta" (202.180,129 kg) + 10 traslados/tránsitos (23.687 + 2.596,84 + 500 kg por lado) + 6 EliminacionStock |
| `inventario_gestion_stock` | 18 filas / 166.055,009 kg |
| `lote_registro_historico_unificado` eventos `INV_*` | 42 = 26 INV_INGRESO + 6 INV_OTRO + 5 INV_TRASLADO_ENTRADA + 5 INV_TRASLADO_SALIDA |
| Ventas de aves (`VENTA_AVES` + `movimiento_pollo_engorde`) | 1 — **se conserva** (no es inventario) |
| Catálogo `item_inventario_ecuador` Panamá | 148 ítems — **se conserva** (la carga masiva lo referencia) |
| Tablas legacy (`farm_product_inventory`, `farm_inventory_movements`, `historial_inventario`, `inventario_gasto*`, `inventario_aves`) | 0 filas Panamá |
| Cross-checks | scope por `company_id` = scope por granja (1:1 en las 3 tablas) · 0 traslados cross-empresa · sin FKs hacia las tablas borradas |

## Diseño — migración `20260725130000_LimpiezaInventarioAlimentoPanama`

3 DELETEs por `company_id` (empresa por nombre), en este orden: histórico `INV_*` (LIKE `INV\_%` escapado; conserva `VENTA_AVES`) → movimientos → stock. El trigger espejo es solo-INSERT (no re-inserta al borrar); el runtime recrea las filas de stock al registrar el próximo ingreso. Idempotente (re-run = 0 filas). Down no-op. Corre DESPUÉS de la Parte 1 (cada una consistente por sí sola; ésta supersede la devolución de stock de aquélla).

## Estado (checklist de ambas partes — el tracker global está ocupado por otro desarrollo)

- [x] Parte 1: migración `20260725120000` aplicada en local, verificada (seguimientos 678+325 → 0, stock devuelto exacto, guards intactos), idempotente, build + tests verdes
- [x] Parte 2: auditoría inventario post-Parte 1
- [x] Parte 2: migración `20260725130000` + Designer clonado
- [x] Parte 2: aplicada en local (`Done.`) — stock Panamá 0 filas, movimientos 0, histórico solo `VENTA_AVES`; catálogo 148 y venta 1 conservados; otras empresas idénticas (475 filas stock / 2.056.837,970 kg · 8.110 mov · 10.144 hist, 8.420 `INV_*`)
- [x] Parte 2: idempotencia (`DELETE 0 / DELETE 0 / DELETE 0`)
- [x] `dotnet build` 0 errores/0 warnings · `dotnet test` 661/661 verdes
- [ ] Commit + deploy (ambas migraciones se aplican solas, en orden, al arrancar ECS)

## Verificación post-deploy (prod)

```sql
-- todo debe dar 0
SELECT COUNT(*) FROM seguimiento_diario_aves_engorde s JOIN lote_ave_engorde l ON l.lote_ave_engorde_id=s.lote_ave_engorde_id WHERE l.company_id=5;
SELECT COUNT(*) FROM seguimiento_diario_lote_reproductora_aves_engorde s JOIN lote_reproductora_ave_engorde lr ON lr.id=s.lote_reproductora_ave_engorde_id JOIN lote_ave_engorde l ON l.lote_ave_engorde_id=lr.lote_ave_engorde_id WHERE l.company_id=5;
SELECT COUNT(*) FROM inventario_gestion_stock WHERE company_id=5;
SELECT COUNT(*) FROM inventario_gestion_movimiento WHERE company_id=5;
SELECT COUNT(*) FROM lote_registro_historico_unificado WHERE company_id=5 AND tipo_evento LIKE 'INV\_%';
-- deben conservarse: catálogo (148), venta (1), lotes/granjas intactos
SELECT COUNT(*) FROM item_inventario_ecuador WHERE company_id=5;
```

## Orden operativo tras el deploy (importante)

1. Registrar los **ingresos de alimento** reales (Entrada planta/granja) por el módulo de inventario.
2. Recién después correr la **carga masiva** de seguimientos (reproductora y engorde): el descuento de consumo con stock insuficiente **se ignora en silencio** (try/catch) y ese consumo quedaría sin registrar en el inventario.
