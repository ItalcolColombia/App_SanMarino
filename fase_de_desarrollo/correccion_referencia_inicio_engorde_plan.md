# Corrección de la referencia `Inicio` + liquidación de corridas anteriores (pollo engorde)

**Fecha:** 2026-08-05 · Pedido: corregir con migración los datos verídicos y liquidar las corridas
anteriores ya cerradas en galpones que hoy corren otra corrida.

---

## Parte A — Corrección de datos (HECHA)

`fn_cuadre_aves_engorde` excluye los lotes cuyo historial `Inicio` no coincide con
`aves_encasetadas` (`referencia_confiable = false`): son 4 y quedan **fuera de toda auditoría**.
Dos causas opuestas, cada una con su evidencia:

| Bloque | Lotes | Qué está mal | Evidencia |
|---|---|---|---|
| 1 | **5** (Sacachun 3b · G0050), **7** (Sacachun 2 · G0051) | El `Inicio` es plantilla de carga | Seis lotes con el mismo `25.000/25.000/35-36` el 2026-03-23; el galpón manejó 22-25 mil en sus otros ciclos (50.000 = doble de capacidad); con el `Inicio` deducido, el lote 7 cierra en **0 exacto en ambos sexos** |
| 2 | **30** (SAN GUILLERMO · G0030) | `aves_encasetadas` y el maestro inflados | Bajo el `Inicio` (5.600/5.700) **ambos sexos cierran en 0 exacto**; bajo el encaset sobran 700 H y 700 M — el mismo excedente partido en dos |

**Fuera de alcance a propósito:**
- **132** (Sacachun 3b · G0049): activo y **sin ventas** ⇒ la conservación no discrimina entre 19.387
  y 19.187. Necesita el documento físico de encasetamiento. 200 aves, hoy muestra bien.
- **3, 4, 6, 8**: encaset 50.000 **y** `Inicio` de plantilla — los dos números son ficticios y no hay
  actividad de la cual deducir el real (cero movimientos). El detector no los ve porque su
  `referencia_confiable` compara `ih + im` sin las mixtas. Decisión de negocio.

**Implementación:** migración data-only `20260805170000_CorreccionInicioHistorialYEncasetEngorde`
(+ copia trazable en `backend/sql/correccion_inicio_historial_y_encaset_engorde.sql`). Ninguna regla
nombra ids: se apoyan en evidencia registrada, con guardas de exactitud (el total deducido debe dar
**exactamente** el encaset; el cierre en 0 se exige por sexo, no por total), no-negatividad e
idempotencia (`IS DISTINCT FROM`). `Down()` no-op deliberado.

---

## Parte B — Liquidación de corridas anteriores (BLOQUEADA: no puede ir por migración)

**Liquidar NO es cambiar un estado.** `LoteAveEngordeService` lo hace en una transacción de 5 pasos
([LoteAveEngordeService.cs:552](../backend/src/ZooSanMarino.Infrastructure/Services/LoteAveEngordeService.cs:552)):

1. `estado_operativo_lote = 'Cerrado'` + `liquidado_at` + `liquidado_por_user_id`
2. `AvanzarCodigoErpGranjaSiCicloCerradoAsync` — **avanza el código ERP de la granja +1**
3. `LiquidacionCongeladaAplicador.CongelarAsync` — la copia congelada
4. `SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync`
5. `CalcularResumenVivoAsync` + `ActualizarResumenCongeladoAsync`

El propio código lo dice: *«Si el congelado falla, la liquidación falla entera — **sin copia no hay
liquidación**»*. Una migración SQL que solo cambie el estado saltearía 4 de los 5 pasos y dejaría
lotes liquidados sin copia congelada: rompe el reporte de liquidación y desincroniza el ERP de granja.

### Además, el criterio pedido cerraría lotes vivos

«Galpón con corrida posterior» alcanza **75 lotes**, y medido da:

| Empresa | Grupo | Lotes | Aves pendientes | Último seguimiento |
|---|---|---:|---:|---|
| ItalcolEcuador | A) saldo ≤ 0 — vacío | **39** | 0 | 2026-07-11 |
| ItalcolEcuador | B) residual < 1 % | 12 | 602 | 2026-07-18 |
| ItalcolEcuador | C) saldo significativo | 2 | 1.119 | 2026-06-16 |
| ItalcolPanama | **D) SIN ventas — ACTIVO** | **22** | **801.882** | **2026-08-03** |

En Panamá **conviven varias corridas por galpón** (topología normal ahí): esos 22 lotes tienen 800 mil
aves vivas y seguimiento de anteayer. Cerrarlos bloquearía el registro diario de toda la operación.

### Camino correcto

Recorrer el endpoint real de cierre lote por lote sobre el **grupo A de Ecuador (39 lotes)**, que son
los únicos con saldo 0 verificado. Requiere backend levantado, sesión y `ClosedByUserId`, y es una
acción irreversible sobre producción ⇒ **necesita confirmación explícita de la lista antes de correr.**
Los grupos B y C se revisan aparte (tienen aves pendientes); Panamá no se toca.

---

## Orden obligatorio

**Primero A, después B.** El *Gate B1* bloquea editar `aves_encasetadas` de un lote liquidado
(invalida la copia congelada), así que el lote 30 debe corregirse **antes** de cerrarse.

## Validación (Parte A)

- `dotnet build` 0 errores / 0 advertencias · `dotnet test` 1.573 + 1 verdes
- Simulación en transacción + `ROLLBACK` antes de aplicar
- Migración aplicada en local (`ASPNETCORE_ENVIRONMENT=Development`, host 127.0.0.1:5433 confirmado en el log)
- Re-ejecución del SQL ⇒ `UPDATE 0` / `UPDATE 0` (idempotente)
- `fn_cuadre_aves_engorde`: **0 descuadrados** con referencia confiable; sin referencia confiable
  baja de **4 a 1** (solo el 132)
- Lote 30 tras la corrección: 11.300 − 2.484 bajas − 8.816 ventas = **0 exacto**
