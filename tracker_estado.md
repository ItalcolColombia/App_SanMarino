# Tracker — Corrección aves disponibles lotes engorde "2601" ✅ COMPLETO

**Plan:** [fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md](fase_de_desarrollo/correccion_aves_disponibles_engorde_2601_plan.md)

## Diagnóstico
- [x] Identificar cálculo de aves disponibles (`LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync`)
- [x] Identificar cálculo de saldo en tabla diaria (`fn_seguimiento_diario_engorde` v6)
- [x] Cuantificar descuadre por género en BD para los 33 lotes "2601" → 8 cerrados con fantasma (lotes 14, 19, 20, 23, 28, 33, 55, 56)
- [x] Lote 23: sobrante contable = 24 machos; el "8" de la tabla = venta tardía 2026-05-13 (8 machos, mov. #967, doc 76) fuera del rango de la fn

## Implementación
- [x] fn v7: `backend/sql/fn_seguimiento_diario_engorde.sql` (VENTA_AVES sin tope de rango en `fechas_universo` y `docs_por_fecha`)
- [x] Migración EF `20260611033748_FixFnSeguimientoEngordeVentasPostCierre` (CREATE OR REPLACE idempotente, SQL sincronizado)
- [x] DTOs `CorreccionAvesDisponiblesEngordeDtos.cs`
- [x] Interface `ICorreccionAvesDisponiblesEngordeService.cs`
- [x] Servicio `CorreccionAvesDisponiblesEngordeService.cs` (validar + corregir, dryRun, auditoría `TipoRegistro='Ajuste'`)
- [x] Endpoints en `LoteAveEngordeController` (`GET aves-disponibles/validar`, `POST aves-disponibles/corregir`)
- [x] Registro DI en `Program.cs`

## Validación (BD local + API local :5002, token real del usuario)
- [x] `dotnet build` → 0 errores, 0 advertencias nuevas · `dotnet test` → 2/2 OK
- [x] Migración v7 aplicada al arrancar la API; lote 23: fila nueva 2026-05-13 (despacho 8 machos, doc 76) y `saldo_aves` final **0**; lote 24 (sin ventas post-cierre) idéntico
- [x] `GET validar?loteNombre=2601` → 33 evaluados, 8 con `requiereCorreccion` y ajustes exactos (563/154, 0/1, 0/457, 0/24, 8/0, 0/290, 0/4, 42/9)
- [x] `POST corregir dryRun=true` → reporta 8 y NO modifica BD (verificado por SQL)
- [x] `POST corregir dryRun=false` → 8 corregidos; lote 23 `aves-disponibles` = **0/0/0**; 8 filas de auditoría `Ajuste` en `historial_lote_pollo_engorde`
- [x] Idempotencia: 2ª corrida → descuadre=0, corregidos=0 · Lotes abiertos intactos (hembras_l/machos_l sin cambios)
- [x] Procesos: API quedó corriendo en :5002 con el build nuevo (reemplaza la instancia previa del usuario, que hubo que detener para compilar)

## Pendiente (decisión del usuario)
- [ ] Aplicar en PROD: mergear/desplegar (la migración v7 se aplica sola) y luego ejecutar `POST /api/LoteAveEngorde/aves-disponibles/corregir {"loteNombre":"2601","dryRun":true→false}` con un token de prod — **requiere OK explícito**
