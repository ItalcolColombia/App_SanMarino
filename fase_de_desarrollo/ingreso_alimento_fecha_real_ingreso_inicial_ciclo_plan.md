# Plan/Propuesta — Alimento que llega ANTES del encaset: fecha real para contabilidad + visible como «ingreso inicial del ciclo» (engorde y postura)

**Fecha:** 2026-08-07 · **Estado: IMPLEMENTADO 2026-08-08** (D1-D4 aprobadas con las recomendaciones; workflow de 7 agentes, QA GO con gate multipaís 0 diferencias en 5.804 filas/197 lotes; migraciones `20260808120000` y `20260808130000`). Hallazgo aparte SIN tocar: el tope de paginado 20 de `FarmInventoryMovementService.GetPagedAsync` estrangula los bultos históricos del Reporte Contable — tarea propia.

**Pedido del usuario:** el alimento llega a la granja 2-7 días antes del encasetamiento. Contabilidad
necesita la fecha REAL de llegada, pero hoy hay que decirle a cada persona que registre el ingreso con
la fecha del primer día de consumo para que el seguimiento diario lo tome y los valores cuadren.
Se quiere que el sistema registre la fecha verdadera Y que el reporte diario igual muestre ese alimento
como ingreso inicial en el primer registro del ciclo — gestionable desde Gestión de Inventario, sin
tocar los otros módulos. Aplica a pollo engorde y a postura.

---

## 0. Diagnóstico (workflow de 5 agentes + mediciones sobre la BD local con dump tipo prod)

### 0.1 La raíz del problema: UNA sola fecha para dos propósitos incompatibles

`inventario_gestion_movimiento` **no tiene columna de fecha del movimiento**: la única fecha es
`created_at`. El `FechaMovimiento` que tipea el usuario **la pisa** (se materializa a mediodía UTC,
`InventarioGestionService.cs:91-97`, asignada en `:506,:1399,:1460,:2354`), y el trigger deriva
`lote_registro_historico_unificado.fecha_operacion = created_at::date`
(`create_lote_registro_historico_unificado.sql:139`). Consecuencias:

- **Fecha contable y fecha operativa son el mismo campo** → falsear una destruye la otra. Por eso
  contabilidad pierde el día verdadero de llegada: no hay a dónde volver.
- **La auditoría de captura también se pierde**: `created_at` deja de decir cuándo se digitó.
- La pantalla de stock muestra «Fecha ingreso» = `stock.CreatedAt` (cuándo nació la fila, no la fecha
  del movimiento) y eso es lo que sale al Excel — otra fuente de confusión contable.

### 0.2 Engorde: el mecanismo pedido YA EXISTE… pero es invisible

`companies.dias_alimento_previo_encaset` (default 10, clamp 0-30, todas las empresas hoy en 10 — nadie
la configuró nunca y **no hay UI ni endpoint** para hacerlo). `fn_seguimiento_diario_engorde` absorbe en
la **apertura** del ciclo todo movimiento de alimento fechado en `[fecha_encaset − N, primer_seguimiento)`
(fn `:287`, CTE `apert_mov` `:424-456`), con las guardas v11 (`lotes_ajenos`) y v12 (`corte_apertura =
GREATEST(encaset − N, fin_ciclo_anterior + 1)`).

**⇒ Un ingreso fechado con su fecha real 2-7 días antes del encaset YA entra al saldo del día 1 sin
tocar una línea de código.** El problema es de **visibilidad**:

- La apertura es un **escalar interno**: no hay columna `apertura_kg` en el `RETURNS TABLE`, el ingreso
  previo NO aparece en `ingreso_alimento_kg` del día 1 (`hist_alimento` arranca en `fecha_min`, `:644`),
  y su **documento** tampoco (`docs_por_fecha` `:670`). El usuario ve un saldo que «aparece de la nada».
- Peor: **mientras el lote no tiene seguimiento**, el ingreso previo SÍ se ve como fila propia con su
  fecha; al cargar el primer seguimiento la fila **desaparece** dentro de la apertura (`:702`). Ese
  comportamiento intermitente es lo que hace desconfiar a la operación y la empuja a re-fechar al día 1.
- Un ingreso fechado **antes** de `encaset − N` (o antes del fin del ciclo anterior) se pierde del
  reporte por completo (los 4 CTEs lo descartan) aunque siga en el stock.

### 0.3 Evidencia del workaround (medida en el dump)

| señal | ItalcolEcuador | ItalcolPanama |
|---|---|---|
| ciclos con ingreso fechado EXACTO el día del primer seguimiento | **110 de 110 (100 %)** | 2 de 30 (6,7 %) |
| kg promedio ese día vs. resto del ciclo | **4.778 vs 1.694 (2,8×)** | 695 vs 2.744 |
| ciclos con ingreso fechado 2-7 días ANTES del primer seg | 13 | **9 de 30 (fecha real)** |
| retro-registro (se digita ≥2 días después de la fecha tipeada) | 57 % | 96 % (mediana 15 d) |

**Ecuador aplica el workaround al 100 %; Panamá ya registra la fecha real** (y su grilla abre un día
antes del encaset, con lo que el día 1 absorbe el preiniciador naturalmente). Además, el 76 % de los kg
del «día 1» de Ecuador entran con estado `Entrada planta` — carga en bloque tipo asiento contable, no
recepción física. Y donde SÍ existe un campo de fecha separado (`inventario_gasto.fecha`), la operación
lo usa para retrofechar en el 50 % de los casos: **la fecha real se registra si hay dónde**.

### 0.4 El límite estructural: galpones encadenados (Ecuador)

28 de 75 ciclos encadenados tienen **menos de 10 días** entre el fin del ciclo anterior y su encaset
(22 con ≤ 7; uno solapado). Para ellos `corte_apertura` recorta la ventana a `fin_prev + 1`, y una
llegada real 2-7 días antes del encaset **puede caer dentro del ciclo anterior y descartarse**. La
fecha sola no alcanza para atribuir el alimento al ciclo correcto — v11/v12 existen precisamente porque
esa ambigüedad ya rompió Ecuador una vez. **Hace falta atribución explícita** para ese subconjunto.

### 0.5 Postura: el mecanismo NO existe (nada que absorber)

- `fn_seguimiento_diario_produccion` **no lee inventario** (0 referencias); levante **no tiene fn
  diaria**; no hay saldo de alimento por lote, ni apertura, ni ventana; el histórico unificado guarda
  los movimientos de postura pero con `lote_ave_engorde_id = NULL` (la atribución solo mira engorde).
- El sitio donde la operación «cuadra» postura es el **Reporte Contable** (columnas de bultos), y tiene
  el defecto que fuerza el workaround: `ReporteContableService.cs:589-592` hace `continue` si el lote no
  tiene dato propio ese día ⇒ **un ingreso fechado antes del encaset desaparece del reporte** y el saldo
  de bultos arranca sin él. Fecharlo el primer día de consumo es exactamente lo que lo hace reaparecer.
- Bloqueo adicional: postura **prohíbe encaset futuro** (`LoteService.cs:776-780` + `noFechaFutura` en
  el modal) ⇒ el lote no puede existir cuando llega el alimento (engorde sí lo permite).
- Higiene detectada de paso: el consumo de postura al inventario se fecha con `UtcNow` (día de
  digitación), no con la fecha del seguimiento — levante, producción y hasta la carga masiva
  (`MigracionService.AlimentoPostura.cs:137` no pasa `FechaMovimiento`, a diferencia de engorde).

### 0.6 Colisión con el trabajo SIN COMMITEAR de la sesión paralela

La otra sesión está agregando `VentanaFechaMovimientoInventarioCalculos` (fecha manual restringida a
`[día 1 del mes en curso, hoy]`, aplicada en las 5 puertas del controller). **Choca de frente con
registrar la fecha real cuando cruza el mes**: alimento que llega el 29-31 para un encaset del 1-3 del
mes siguiente, o el retro-registro habitual (Ecuador 57 %, Panamá 96 % con mediana 15 días). Medido:
643 ingresos de Ecuador y 70 de Panamá cruzan mes. Ambos pedidos son del mismo usuario — hay que
conciliarlos (D4), no elegir uno.

---

## 1. Decisiones pendientes (confirmar antes de escribir código)

| # | Decisión | Recomendación |
|---|---|---|
| **D1** | ¿Doble fecha (`fecha_llegada_real` contable separada de la operativa) o **UNA fecha (la real) + apertura visible**? | **Una sola fecha = la real.** Si el reporte muestra el alimento previo como «ingreso inicial del ciclo» en el día 1, la fecha operativa y la contable pueden ser LA MISMA (la verdadera) y el conflicto desaparece. La doble fecha agrega columnas, se pierde en el DELETE físico del ingreso y ningún reporte la leería — descartada salvo que contabilidad exija ver «llegó el 28, se aplicó el 2» como dos datos. |
| **D2** | ¿Marca explícita «este ingreso es para el PRÓXIMO ciclo del galpón» en el alta/edición de inventario? | **Sí.** Es lo único que resuelve los 28 ciclos encadenados de Ecuador (§0.4) donde la fecha sola no puede atribuir. Checkbox opcional; editable después desde el historial de Gestión de Inventario (el «desde acá podamos modificar los datos» del pedido). |
| **D3** | Alcance postura | **Mínimo primero:** fix del `continue` del Reporte Contable + entradas previas al encaset visibles en la primera fila (saldo inicial de bultos). Construir saldo/apertura por lote estilo engorde es un proyecto aparte (requiere atribución movimiento→lote de postura que hoy no existe). |
| **D4** | Conciliación con la ventana de mes de la sesión paralela | **Excepción acotada:** aceptar fecha del mes anterior SOLO si cae dentro de la ventana de alimento previo (`hoy − fecha ≤ dias_alimento_previo_encaset` y el galpón tiene un encaset reciente/próximo), o bien mantener la regla dura y establecer «se registra el día que llega» como norma operativa. A definir junto con esa sesión antes de que commitee. |

---

## 2. Parte A — Engorde: exponer el «ingreso inicial del ciclo» (fn v15, solo visibilidad)

**El saldo NO cambia** — el alimento previo ya está sumado. Cambia lo que se VE:

- `fn_seguimiento_diario_engorde` v15 (sobre la v14 de la sesión paralela, cuando esté commiteada):
  - nueva columna `apertura_alimento_kg` en el `RETURNS TABLE` (aditiva), poblada solo en la primera
    fila del ciclo; y/o **fila sintética «Ingreso inicial del ciclo»** fechada el día 1 con
    `seg_id = NULL`, los kg absorbidos y `documento` = STRING_AGG de los documentos/referencias reales
    de los movimientos de la ventana (hoy invisibles).
  - la rama congelada (`liquidacion_lote_engorde_congelada_fila`) NO se toca: los lotes ya liquidados
    conservan su foto; divergencia documentada en el changelog.
- Grilla de engorde (`tabs-principal-engorde`): mostrar la apertura en la fila del día 1 con etiqueta
  «Ingreso inicial (alimento previo al encaset)» — deja de «aparecer de la nada».
- `fn_reporte_diario_costos_engorde` la hereda por el `CROSS JOIN LATERAL` (verificar contrato).
- Migración EF idempotente + espejo `backend/sql/*.sql` en el mismo commit + `Down` = v14 verbatim.
- **UI de administración** para `companies.dias_alimento_previo_encaset` (hoy solo por UPDATE a mano):
  campo numérico 0-30 en la pantalla de empresas + DTO en todas las proyecciones de `CompanyDto`.

**Validación (gate multipaís OBLIGATORIO):** `verificar_paridad_saldo_engorde.sql` antes/después —
**cero diferencias de saldo en todas las empresas** (el cambio es de visibilidad); paridad fila a fila
de la grilla; `fn_cuadre_alimento_engorde` y `fn_cuadre_aves_engorde` en 0 descuadrados; comparación
byte a byte de `fn_reporte_diario_costos_engorde`; `dotnet build` + `dotnet test`.

## 3. Parte B — Gestión de Inventario: fecha real + atribución explícita al ciclo

- **B1 (norma, no código):** el ingreso se registra SIEMPRE con la fecha real de llegada. Con la Parte A
  visible ya no hay motivo para falsearla. Actualizar el instructivo de operación
  (`INSTRUCTIVO_OPERACION_saldos_alimento_engorde.md`) y comunicar.
- **B2 (D2):** columna `para_proximo_ciclo BOOLEAN NOT NULL DEFAULT false` en
  `inventario_gestion_movimiento` + espejo en `lote_registro_historico_unificado` (trigger actualizado;
  migración idempotente). Checkbox en el alta de ingreso (solo alimento con galpón): «Este alimento es
  para el próximo encasetamiento de este galpón». **Editable desde el historial** (PUT nuevo
  `/ingresos/{id}/destino-ciclo`, espejo sincronizado con el patrón de `ActualizarFechaIngresoAsync`,
  cuidando el fallback frágil por cantidad idéntica). La fn lo usa como **override**: movimiento marcado
  ⇒ entra a la apertura del ciclo siguiente y se excluye de la grilla del ciclo vigente, sin depender
  de `corte_apertura`. `AvisoFechaFueraDeCicloCalculos` deja de avisar si la marca está puesta.
- **B3 (auditoría):** dejar de destruir el instante de captura — columna `registrado_at TIMESTAMPTZ
  NOT NULL DEFAULT now()` que NUNCA se sobrescribe (o dejar `created_at` puro y que la fecha tipeada
  viva en columna propia — decidir junto con D1; barato en la misma migración).
- **B4 (D4):** excepción o norma frente a la ventana de mes en curso — coordinado con la sesión paralela.

## 4. Parte C — Postura (alcance mínimo, D3)

- **C1 (mayor impacto / menor costo):** fix del `continue` de `ReporteContableService.cs:589-592` — una
  fecha con entradas de bultos SIN dato del lote genera fila igual (o acumula a la primera fila). Deja
  de desaparecer el alimento fechado fuera de los días con registro.
- **C2:** «ingreso inicial» en el Reporte Contable: entradas fechadas en `[encaset − N, primer registro)`
  del lote padre se presentan en la primera fila como saldo inicial de bultos (reusar
  `companies.dias_alimento_previo_encaset` — es un parámetro operativo de la empresa, no de engorde).
- **C3 (backlog, proyecto aparte):** fn diaria de levante + saldo de alimento por lote estilo engorde +
  atribución movimiento→lote de postura. No lo necesita este pedido.
- **C4 (higiene, evaluar impacto antes):** pasar `FechaMovimiento` (fecha del seguimiento) en los
  consumos de postura — hoy `UtcNow` desalinea el kardex por el lado del consumo también.

## 5. Casos de prueba

- **A:** lote nuevo con ingreso real 5 días antes del encaset → día 1 muestra «ingreso inicial» con esos
  kg y su documento; saldo idéntico al actual; lote liquidado congelado NO cambia; Panamá y Ecuador con
  paridad de saldo total 0.
- **A (flicker):** mismo lote ANTES de cargar el primer seguimiento → el ingreso se ve como fila propia;
  tras cargar el primer seguimiento → pasa a «ingreso inicial» del día 1 (los kg nunca desaparecen).
- **B2:** galpón encadenado con gap 3 días: ingreso real 5 días antes del encaset marcado «próximo
  ciclo» → aparece en la apertura del ciclo nuevo y NO en la grilla del viejo; sin marca → comportamiento
  actual intacto (retrocompatibilidad byte a byte).
- **C1/C2:** lote de postura con ingreso 4 días antes del encaset → Reporte Contable lo muestra (fila
  propia o saldo inicial); el total de bultos del período no cambia respecto de sumar a mano.
- **Regresión:** carga masiva, devoluciones por edición/borrado de seguimiento y gastos siguen
  escribiendo fecha histórica sin pasar por ninguna guarda nueva.

## 6. Invariantes que NO se pueden romper

- Gate multipaís en TODO cambio a `fn_seguimiento_diario_engorde` (comparación fila a fila, todas las
  empresas; toda empresa no objetivo sale en 0).
- El histórico unificado se ANULA, nunca se borra; columnas nuevas en el espejo → replicar en el trigger
  y en los caminos de anulación.
- Una sola fórmula por número: la marca `para_proximo_ciclo` se lee en la fn y su espejo C#
  (`SeguimientoAvesEngordeCalculos` / `SaldoAlimentoEngordeCalculos`) con tests de equivalencia.
- Refactor ≠ cambio de comportamiento: sin la marca nueva y sin tocar config, TODO byte a byte igual.

## 7. Puntos de mejora encontrados (aunque queden fuera del alcance)

1. `created_at` sobrecargado (fecha tipeada pisa auditoría) — §0.1 / B3.
2. «Fecha ingreso» del stock y su Excel = `stock.CreatedAt`, no la fecha del movimiento.
3. `AvisoFechaFueraDeCiclo` NO dispara justo en el escenario de este pedido (lote sin seguimiento aún)
   y es ciego a postura.
4. `fecha_operacion` se deriva en UTC ⇒ cargas después de las ~19:00 Bogotá caen al día siguiente
   (4,1 % de los ingresos medidos) — afecta cualquier frontera por fecha.
5. Consumo de postura fechado el día de digitación (incluida la carga masiva de postura, asimetría
   con engorde).
6. `DELETE /ingresos/{id}` borra la fila física — cualquier dato nuevo del movimiento debe replicarse
   al espejo para sobrevivir.
7. `dias_alimento_previo_encaset` sin UI (todas las empresas en el default).
8. Divergencia SQL/C# al normalizar valores negativos de la ventana (GREATEST(0) vs default 10).
9. `vw_validacion_alimento_engorde` ya calcula `kg_antes_encaset` y nadie la muestra en pantalla —
   candidata a tile de diagnóstico.

---

## 8. Validación en BD del escenario «llega el 15, encaseto el 25» (simulación real, 2026-08-07)

Simulado contra la BD local (dump tipo prod) en **transacción con ROLLBACK** (script
`sim_15_25.sql` del scratchpad; verificado 0 rastro después). Estructura sintética: núcleo `NSIM` +
galpón `GSIM` en la granja 40 (Ecuador, ventana default 10), lote `SIM-1525` con encaset
**2026-08-25**, tres ingresos con fecha REAL al histórico: **3.000 kg el 14-ago** (11 días antes),
**5.000 kg el 15-ago** (10 días antes — el caso del usuario), **2.000 kg el 26-ago** (entre encaset y
primer registro). Primer seguimiento el 27-ago (patrón Ecuador encaset+2) con consumo 200 kg.

**[1] ANTES del primer seguimiento**, `fn_seguimiento_diario_engorde` devuelve:

| fecha | seg_id | ingreso_kg | saldo_kg | documento |
|---|---|---|---|---|
| 2026-08-15 | — | 5.000 | **8.000** | FAC-0015 |
| 2026-08-26 | — | 2.000 | **10.000** | FAC-0026 |

(El del 14 no tiene fila —`fechas_universo` corta en `encaset − 10`— pero **sí está sumado** en el
saldo: 8.000 = 3.000 + 5.000.)

**[2] DESPUÉS de cargar el primer seguimiento (27-ago):**

| fecha | seg_id | ingreso_kg | saldo_kg | documento |
|---|---|---|---|---|
| 2026-08-27 | 10948 | **0** | **6.800** | *(vacío)* |

**Conclusiones confirmadas:**
1. **El caso 15→25 funciona HOY con la fecha real** — 10 días cae justo DENTRO de la ventana default
   (`fecha >= encaset − 10`, inclusive) y los 5.000 entran a la apertura: saldo 6.800 = 5.000 + 2.000 − 200.
2. **Pero es invisible**: la fila del día 1 muestra ingreso 0 y documento vacío — el saldo «aparece de
   la nada». Exactamente lo que la Parte A corrige.
3. **El flicker es peor de lo descrito**: al cargar el primer seguimiento las filas de los ingresos
   desaparecen Y **el saldo BAJA de 10.000 a 6.800** — los 3.000 del día 14 (un solo día fuera de la
   ventana) se pierden en silencio del reporte aunque sigan en el stock. Para la operación eso es «el
   sistema se comió alimento» ⇒ desconfianza ⇒ workaround.
4. **El default 10 es filo de navaja para este caso de uso**: llega el 15/encaseta el 25 entra; llega
   el 14 se pierde. Refuerza dos piezas de la propuesta: (a) **UI para subir la ventana por empresa**
   (tope 30 ya existente) y (b) la **marca «para el próximo ciclo»** (D2), que atribuye sin depender
   de la fecha y cubre >30 días y galpones encadenados.
5. `fn_reporte_diario_costos_engorde` hereda todo esto tal cual (es un `CROSS JOIN LATERAL` sobre la
   misma fn). En **postura** no hay simulación posible: no existe mecanismo alguno que absorber — el
   Reporte Contable directamente pierde esas fechas (Parte C).
