# Diagnóstico E2E multi-país — flujo completo por perfil

> Estado vivo del pipeline pedido por el usuario (2026-07-02): validar cada país con su perfil real,
> desde crear información hasta ver resultados, módulo por módulo.
> **Pipeline: FASE A (diagnóstico por país) → FASE B (QA/triaje) → FASE C (desarrollo de fixes).**
> Credenciales de prueba (local): admin.ecuador@italcol.com · admin.panama@italcol.com · solangyramirez@sanmarino.com.co (clave 123456789).
> Reglas: crear datos de prueba está permitido en BD LOCAL; marcarlos con observación "PRUEBA-E2E" y eliminarlos al validar si el módulo lo permite. Login por automatización: `form.requestSubmit()`.

## FASE A — Diagnóstico por país

### A1. Ecuador (admin.ecuador) — CERRADA 2026-07-02
- [x] Seguimiento Diario Pollo Engorde (Kilometro 61 / N1 / Galpon-1 / lote 2603): **crear** registro 26/06 con ítem AV0410 10 kg + obs PRUEBA-E2E → **ver** en tabla (38→39 filas, consumo y saldo alimento recalculados) → pestaña **Indicadores** (tabla 39 filas + guía genética Ross 308-AP 2022 detectada) → **Gráficas** (22 charts renderizan) → **eliminar** con confirmación (39→38, BD limpia). Modal unificado correcto para Ecuador: campos H/M + agua, SIN sección QQ ✔
- [x] Liquidación técnica: reporte multi-lote Kilometro 61 generado sin errores; columna TOTAL correcta; merma única (5 uds del lote 2602) en el TOTAL; kilos a cliente totalizan todos los lotes (274.767,96) → fix C2 del descuadre operando ✔
- [x] Gestión de Inventario (EC/PA): carga 130 filas de stock sin errores ✔
- [x] Venta de Aves: validado en ciclo 24 (peso obligatorio, bloqueo + aviso) ✔
- Hallazgos: **H1** registrado abajo (NG0103)

### A2. Panamá (admin.panama) — GOTCHA: ItalcolPanama sin granjas/lotes en BD local
- [ ] Documentar límite: sin datos no hay flujo de creación posible → validar carga de módulos, estados vacíos sin errores de consola, y Liquidación técnica / Informe Semanal en vacío
- [ ] Decidir con el usuario si se crean datos semilla de Panamá (granja+lote de prueba) para E2E completo
- Hallazgos: (pendiente)

### A3. Colombia (solangyramirez, postura) — módulos: Lote Postura, Seg. Levante, Seg. Producción, Seg. Reproductora, Inventario, Traslados
- [ ] Seguimiento Diario de Levante: crear registro, ver en tabla/indicadores, editar/eliminar
- [ ] Seguimiento Diario de Producción: crear registro, ver resultados, liquidación técnica postura
- [ ] Lote Postura: revisar listado/ficha (crear solo si hay flujo de borrado)
- [ ] Inventario y Traslados: carga y flujo básico
- Hallazgos: (pendiente)

## FASE B — QA / triaje (cuando A1-A3 estén cerradas)
- [ ] Consolidar hallazgos → tabla: severidad (bloqueante/alta/media/baja), módulo, país, repro, hipótesis de causa
- [ ] Verificar duplicados con problemas ya conocidos (tracker_estado.md, memorias)
- [ ] Priorizar backlog para FASE C

## FASE C — Desarrollo (un fix por ciclo, patrón del refactor)
- [ ] (se llena desde FASE B) — cada fix: implementar → build back+front → tests → validación E2E con el perfil del país → commit

## Registro de hallazgos (se llena en FASE A)
| # | País | Módulo | Severidad prelim. | Descripción | Evidencia |
|---|---|---|---|---|---|
| H1 | Ecuador | front (por aislar) | **Alta** | Ráfaga de ~100 `NG0103: Infinite change detection` en consola durante el flujo E2E (sesión con: modal venta granja ciclo 24 → CRUD seguimiento → tabs → liquidación → gestion-inventario). NO reproducido al re-ejecutar individualmente: gestion-inventario (0), indicador-ecuador+generar (0), gráficas (0), indicadores (0), modal venta+cantidad (0). Sospechosos para QA: modal-seguimiento-engorde durante guardar/eliminar (recarga de datos), y getters que crean arrays por ciclo (`prorateoPreview`, patrón vetado por CLAUDE.md) bajo algún estado intermedio. NG0103 detiene el ciclo de CD → riesgo de UI congelada para el usuario. | preview_console_logs 108 entradas; contadores por página en 0 al reintentar |
