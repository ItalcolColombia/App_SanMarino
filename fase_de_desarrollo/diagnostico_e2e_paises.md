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

### A2. Panamá (admin.panama) — CERRADA 2026-07-02 (limitada por datos)
- [x] Módulos cargados en vacío SIN errores de consola: Liquidación técnica (`/indicador-ecuador` renderiza **"Indicador Panamá"** con 🇵🇦 — country-aware ✔, "Calcular Indicadores" en vacío no explota), Informe Semanal Pollo Engorde (encabezado 🇵🇦), Lote Aves de Engorde (config), aves-engorde (filtros vacíos)
- [x] **LÍMITE DOCUMENTADO**: ItalcolPanama (company 5) tiene 0 granjas y 0 lotes en la BD local → imposible probar crear→ver→eliminar. **DECISIÓN USUARIO pendiente**: crear datos semilla de Panamá (granja+galpón+lote de prueba) para E2E completo, o esperar copia de prod con datos Panamá
- Hallazgos: ninguno nuevo (estados vacíos sanos)

### A3-resultado. Colombia (solangyramirez, postura) — CERRADA 2026-07-02
- [x] Seguimiento Diario de Producción (NIZA III / Modulo I=324 / Galpon 3=G0010 / lote P-K345A): 301 registros cargan sin errores → **crear** registro 02/07 (100 huevos, 90 incubables, peso 60, obs PRUEBA-E2E) → guardado y visible (302 filas) → **eliminar** con confirmación (301, BD limpia) ✔
- [x] Pestaña Indicadores postura: 43 semanas con comparación vs guía genética Colombia y semáforo (Óptimo ≤5% / Aceptable ≤15% / atención >15%) ✔
- [x] Seguimiento de Levante: sin lotes activos en la cascada (lotes ya trasladados a producción) — no es bug; CRUD no probado por falta de lote abierto de levante
- Hallazgos: **H2** registrado abajo

### A3. Colombia (solangyramirez, postura) — módulos: Lote Postura, Seg. Levante, Seg. Producción, Seg. Reproductora, Inventario, Traslados
- [ ] Seguimiento Diario de Levante: crear registro, ver en tabla/indicadores, editar/eliminar
- [ ] Seguimiento Diario de Producción: crear registro, ver resultados, liquidación técnica postura
- [ ] Lote Postura: revisar listado/ficha (crear solo si hay flujo de borrado)
- [ ] Inventario y Traslados: carga y flujo básico
- Hallazgos: (pendiente)

### A-extra (barrido estático NG0103, 2026-07-02)
- [x] Barrido de `*ngFor` sobre métodos que alocan (misma causa que H1) en TODO el front. La mayoría de `foo()` en `*ngFor` son **signals** (referencia estable, OK). Confirmado el mismo patrón alocador de `getAlimentosFiltradosPorTipo` en **dos modales más de Colombia postura**: `lote-produccion/modal-seguimiento-diario` y `lote-levante/modal-create-edit` → **H3**, corregidos con la misma memoización. Validado E2E: modal producción Colombia (lote P-K345A) abre e interactúa con **0 NG0103**.
- [x] **H4 CORREGIDO** — `gestion-inventario-page` (Ecuador/Panamá, uso diario): NG0103 reproducido al seleccionar granja en Ingresos (2-4 errores). Causa: **16 getters + 8 métodos** que devolvían arrays nuevos por acceso (`farmsDestino/farmsOrigen`, `nucleosFiltered/galponesFiltered`, `historico*Options/Filtered`, `*NucleosFiltered/*GalponesFiltered`, `recepcion*ForFarm`) usados en `*ngFor`. Fix: helper `listaEstable` (memoización por firma de contenido + `_emptyList` compartido). Verificado 0 NG0103 en Ingresos, Traslados e Histórico tras el fix; `ng build` OK. Confirmado vía Angular API que los getters ahora devuelven referencia estable.
- [ ] Candidatos menores restantes (baja prioridad, no reproducidos): `indicador-ecuador/auditoria-liquidacion-modal columnasRegistro(h)`, `catalogo-alimentos metaChips()/getMetadataAsArray()`, `traslados-aves/traslado-navigation-* getLoteInfo()/getPaginas()`.

## FASE B — QA / triaje (cuando A1-A3 estén cerradas)
- [ ] Consolidar hallazgos → tabla: severidad (bloqueante/alta/media/baja), módulo, país, repro, hipótesis de causa
- [ ] Verificar duplicados con problemas ya conocidos (tracker_estado.md, memorias)
- [ ] Priorizar backlog para FASE C

## FASE C — Desarrollo (un fix por ciclo, patrón del refactor)
- [x] **H2 CORREGIDO** — galpones con nombre duplicado: helper `disambiguateGalponLabels` en los dos `filtro-select` (levante y producción) agrega `(código)` solo cuando el nombre se repite. Validado E2E (NIZA I / Modulo I): dropdown ahora muestra "Galpon 3 (G0023)" y "Galpon 3 (G0024)"; los únicos ("Galpon 1", "Galpon 2") quedan sin código. ng build OK.
- [x] **H1 CORREGIDO** — NG0103 Infinite change detection. **Causa raíz**: en `modal-seguimiento-engorde.component.html:75` un `*ngFor` iteraba sobre `getAlimentosFiltradosPorTipo(...)`, método que SIEMPRE alocaba (`.map`/`.filter` + `filtrarAlimentosConStockDisponible`) → array nuevo por ciclo de CD → loop infinito (patrón vetado por CLAUDE.md). **Fix**: memoización por igualdad de contenido (mismos ids/orden) que devuelve la MISMA referencia cuando no cambian los datos; el cómputo se movió a `computeAlimentosFiltradosPorTipo`. Reproducido de forma confiable (contador de NG0103 antes del fix >0 al abrir modal) y verificado 0 después (abrir modal → seleccionar ítem → cantidad → guardar → eliminar, todo con contador fresco en 0). ng build OK.

## Registro de hallazgos (se llena en FASE A)
| # | País | Módulo | Severidad prelim. | Descripción | Evidencia |
|---|---|---|---|---|---|
| H2 | Colombia | Filtros levante/producción | Media (UX/datos) | Dropdown de galpón muestra "Galpon 3" DOS veces (son galpones distintos: G0023 y G0024 con el mismo nombre) — el usuario no puede distinguirlos. Aplica a levante y producción (mismo componente de filtros). Fix candidato: mostrar `nombre (código)` en las opciones; opcional: informe de datos con nombres duplicados por núcleo. | Opciones del select con values `3: G0023` y `4: G0024`, mismo texto |
| H1 | Ecuador | front (por aislar) | **Alta** | Ráfaga de ~100 `NG0103: Infinite change detection` en consola durante el flujo E2E (sesión con: modal venta granja ciclo 24 → CRUD seguimiento → tabs → liquidación → gestion-inventario). NO reproducido al re-ejecutar individualmente: gestion-inventario (0), indicador-ecuador+generar (0), gráficas (0), indicadores (0), modal venta+cantidad (0). Sospechosos para QA: modal-seguimiento-engorde durante guardar/eliminar (recarga de datos), y getters que crean arrays por ciclo (`prorateoPreview`, patrón vetado por CLAUDE.md) bajo algún estado intermedio. NG0103 detiene el ciclo de CD → riesgo de UI congelada para el usuario. | preview_console_logs 108 entradas; contadores por página en 0 al reintentar |
