# Plan — robustecimiento del seguimiento de alimento por silo, galpón y granja

Fecha: 28-sep-2026

Estado: planificado, no iniciado

Dependencias: commits `7f37fab` y `97fcd6d`; planes
`seguimiento_postura_alcance_inventario_alimento_plan.md` y
`seguimiento_postura_movimientos_alimento_desde_bd_plan.md`.

## 1. Objetivo

Dejar el Seguimiento Diario de Levante y Producción preparado para mostrar ingresos y traslados de
alimento de forma históricamente estable, explicable y verificable en las tres modalidades reales de
inventario:

1. **Silo/bodega**: el stock vive en un silo de la granja y el lote declara de cuáles silos consume.
2. **Galpón**: el stock vive en granja + núcleo + galpón.
3. **Granja**: el stock vive únicamente en la granja.

La mejora debe impedir que una reasignación o un cambio de configuración reescriba visualmente el
pasado, indicar al usuario por qué ve cada movimiento y contar con una prueba de integración que
ejecute la función real en PostgreSQL antes de desplegar.

## 2. Resultado funcional esperado

En cada fecha del seguimiento se debe mostrar:

- ingresos directos de alimento;
- traslados de entrada y salida sin netearlos;
- cantidad y alimento;
- referencia o documento visible;
- alcance efectivo (`SILO`, `GALPON` o `GRANJA`);
- ubicación física que originó la visibilidad (nombre del silo/bodega, galpón o granja);
- origen y destino cuando el movimiento sea un traslado y los datos estén disponibles.

Los movimientos de un día sin registro diario siguen apareciendo como fila informativa. Un error de
consulta nunca se representa como «no hubo movimientos».

## 3. Semántica canónica

### 3.1 Precedencia del alcance

La decisión se mantiene por comportamiento y no por empresa o país:

| Prioridad | Condición | Alcance | Clave física persistida |
|---|---|---|---|
| 1 | `company.maneja_inventario_por_silo = true` | Silo/bodega | `farm_id + silo_id`; núcleo/galpón `NULL` |
| 2 | Sin silo y `farm.maneja_alimento_por_galpon ?? company.maneja_alimento_por_galpon = true` | Galpón | `farm_id + nucleo_id + galpon_id`; silo `NULL` |
| 3 | Resto | Granja | `farm_id`; núcleo/galpón/silo `NULL` |

El modo silo siempre tiene precedencia. El override de granja sólo participa cuando el flag de silo
está apagado.

### 3.2 Pertenencia del movimiento

Un ingreso o traslado de alimento pertenece a una **ubicación física**, no necesariamente a un lote
exclusivo:

- un movimiento de granja puede verse en todos los lotes de esa granja dentro de su rango de fase;
- un movimiento de un silo compartido puede verse en todos los lotes que consumían de ese silo en
  la fecha del movimiento;
- un movimiento de galpón sólo se ve en la ubicación exacta.

Por esta razón estos campos son informativos y no se deben sumar entre lotes para construir un total
corporativo. La interfaz debe explicarlo cuando el alcance sea compartido.

### 3.3 Traslados

Un traslado conserva sus dos patas:

- `INV_TRASLADO_SALIDA` en la ubicación origen;
- `INV_TRASLADO_ENTRADA` en la ubicación destino, al acreditarse o recibirse.

No se netean en la grilla. Si origen y destino pertenecen al conjunto de ubicaciones visibles para
un mismo lote, ambas patas pueden aparecer y deben identificarse claramente.

### 3.4 Rango de fase

Regla recomendada para evitar solapamientos:

- Levante: intervalo desde `fecha_encaset` hasta el día anterior a
  `fecha_inicio_produccion` cuando exista Producción.
- Producción: desde `fecha_inicio_produccion` hasta `fecha_fin_produccion`, inclusivas.
- Si Levante se cierra sin crear Producción, usar una fecha de cierre efectiva persistida.
- Un filtro solicitado por el usuario sólo intersecta el rango; nunca lo expande.

El día de inicio de Producción pertenece a Producción. Esta decisión debe quedar fijada en cálculo
puro y pruebas.

## 4. Problemas que resuelve esta fase

### 4.1 Asignaciones de silo evaluadas sólo con el estado actual

La consulta actual exige `lote_silos.activo`, `farm_silos.activo` y silo no eliminado. Al retirar un
silo del lote, los movimientos históricos de ese silo dejan de verse. Activar un silo nuevo también
puede hacer visibles movimientos anteriores que no correspondían al lote.

### 4.2 Flags actuales reinterpretan el histórico

La función decide silo/galpón/granja con los flags actuales. Cambiar un flag después de registrar
movimientos puede ocultar filas antiguas o ampliar el alcance a otros galpones sin tocar el
movimiento original.

### 4.3 Respuesta vacía ambigua

El contrato actual devuelve una lista. Una lista vacía no permite distinguir entre:

- no hubo movimientos;
- el lote no tiene silos asignados;
- todos los movimientos quedaron fuera del rango;
- la función SQL no existe o falló;
- la configuración de alcance es inconsistente.

### 4.4 Falta de prueba PostgreSQL real

Los tests actuales cubren cálculo puro, mapeo JSON y presentación Angular, pero no ejecutan la
función ni la migración contra PostgreSQL. Un error de firma, nombre de columna, JSON o predicado no
se detecta con EF InMemory.

### 4.5 Límite Levante/Producción no totalmente explícito

Levante cerrado usa hoy el último seguimiento como extremo porque no conserva una fecha de cierre
propia. Esto puede ocultar un movimiento sin seguimiento posterior o duplicar el día de transición
entre las dos fases.

## 5. Arquitectura objetivo

### 5.1 Base de datos como dueña del resultado

Crear una función versionada, sin reemplazar inicialmente la que está en uso:

`public.fn_resumen_movimientos_alimento_postura_v2(...) RETURNS jsonb`

La función debe devolver siempre un objeto, aun cuando no existan movimientos:

```json
{
  "alcance": "SILO",
  "rangoDesde": "2026-09-01",
  "rangoHasta": "2026-09-28",
  "ubicaciones": [
    { "tipo": "SILO", "id": 14, "nombre": "Silo 4" }
  ],
  "dias": [],
  "advertencias": []
}
```

Cada movimiento dentro de `dias` incluirá, además del contrato actual:

- `ubicacionTipo`, `ubicacionId`, `ubicacionNombre`;
- `origenNombre` y `destinoNombre` para traslados cuando puedan reconstruirse;
- `transferGroupId` si el origen sigue disponible o si se incorpora al espejo histórico;
- `esUbicacionCompartida` cuando aplique.

El backend sólo valida contexto, ejecuta la función y deserializa. No agrupa, no clasifica y no
reinterpreta alcance.

### 5.2 Vigencia histórica lote–silo

Evolucionar `lote_silos` para conservar períodos en vez de reactivar la misma fila:

- `vigente_desde date NOT NULL`;
- `vigente_hasta date NULL`;
- mantener `activo` durante la transición para los consumidores actuales;
- reemplazar la unicidad histórica `(lote_id, farm_silo_id)` por unicidad parcial del período
  abierto;
- índice para `(company_id, lote_id, farm_silo_id, vigente_desde, vigente_hasta)`;
- constraint `vigente_hasta IS NULL OR vigente_hasta >= vigente_desde`.

Reglas de escritura:

1. Asignar por primera vez crea un período abierto.
2. Retirar cierra el período; nunca borra ni reutiliza la fila.
3. Reasignar crea un período nuevo.
4. Sólo puede existir un período abierto por lote+silo.
5. Un silo inactivo no acepta asignaciones ni movimientos nuevos, pero su nombre e histórico siguen
   siendo consultables.

La función v2 relaciona el movimiento con el lote cuando `fecha_operacion` cae dentro de un período
de vigencia. No usa el estado actual del silo para ocultar historia.

### 5.3 Backfill de vigencias

Antes de definir fechas se ejecutará un diagnóstico de solo lectura que mida:

- asignaciones activas e inactivas;
- primera y última fecha de movimiento por silo;
- inicio de fase de cada lote;
- filas con `created_at` posterior al primer movimiento relevante;
- silos reactivados o movimientos anteriores a la asignación disponible.

Política recomendada si no existe auditoría suficiente:

- período activo: `vigente_desde = LEAST(created_at::date, inicio_fase)` y `vigente_hasta = NULL`;
- período inactivo: misma fecha inicial y `vigente_hasta = fecha_corte_de_la_migracion`;
- registrar en un reporte todas las filas cuya vigencia fue inferida;
- no modificar movimientos ni stock.

La política definitiva se cierra después del diagnóstico. No se inventarán fechas silenciosamente.

### 5.4 Inmutabilidad controlada del alcance

Una empresa o granja con movimientos de alimento no puede cambiar directamente entre silo, galpón
y granja desde el CRUD ordinario.

El backend debe:

- detectar movimientos existentes en la empresa/granja;
- rechazar el cambio con un mensaje que indique el alcance actual y la cantidad/rango afectado;
- permitir el cambio únicamente mediante una operación administrativa específica con simulación,
  migración de datos, validación y auditoría.

La UI debe deshabilitar el selector y explicar el motivo, pero la protección autoritativa queda en
el backend.

### 5.5 Fecha efectiva de cierre de Levante

Agregar una fecha nullable tipada —nombre sugerido `fecha_fin_levante`— a la entidad de Levante:

- al cerrar: guardar la fecha efectiva;
- al reabrir: limpiarla según la regla existente de reapertura;
- al crear Producción: validar que su inicio sea posterior al último día perteneciente a Levante;
- backfill: derivar primero de `fecha_inicio_produccion`; si no existe, usar el último seguimiento y
  reportar el fallback.

La migración debe ser idempotente y aditiva. No se ejecutará DDL manual en producción.

### 5.6 Compatibilidad y despliegue rodante

Durante la primera liberación se conservan:

- `fn_movimientos_alimento_postura_diario`;
- endpoints planos actuales;
- endpoints diarios v1.

Se agregan función y endpoint v2. Esto evita que una tarea ECS antigua falle mientras la nueva
migración se aplica. La eliminación de v1 será una fase posterior, después de verificar que no tiene
consumidores.

## 6. Backend .NET

### Crear

- DTO de contexto/resumen v2 en Application.
- cálculo puro para límites de fase y propiedad del día de transición.
- cálculo puro/validador para cambios de alcance con datos existentes.
- servicio de diagnóstico de alcance, sólo lectura.
- endpoint v2 para Levante y Producción.
- pruebas de integración PostgreSQL del contrato completo.

### Modificar

- `LoteSilo` y su configuración EF para vigencias.
- `LoteSiloService.AsignarAsync` para abrir/cerrar períodos y rechazar silos inactivos.
- cierre/reapertura de Levante para persistir la fecha efectiva.
- `CompanyService` y `FarmService` para bloquear cambios de alcance no migrados.
- servicios de seguimiento para invocar la función v2 sin composición de negocio.

### Reglas de error

- lote ajeno o fuera de alcance: fail-closed según el contrato actual;
- configuración incoherente: error funcional explícito, no lista vacía;
- función/migración ausente: 500 registrado con correlación; Angular muestra error de consulta;
- ningún movimiento válido: 200 con contexto y `dias: []`.

## 7. Frontend Angular

- Consumir el contrato v2 sin reagrupar datos.
- Mostrar badge de alcance: `Por silo`, `Por galpón` o `Por granja`.
- Mostrar el nombre de la ubicación en ingreso y traslado.
- Diferenciar estados `cargando`, `vacío` y `error`.
- Usar `ToastService` para el error y una acción «Reintentar» en la grilla.
- Advertir que un movimiento por granja o silo compartido puede aparecer en varios lotes y no debe
  sumarse entre ellos.
- Mantener la regla de pintar el resumen sólo en el primer registro cuando hay varias capturas el
  mismo día.
- Mantener la fila informativa para fechas sin seguimiento.
- Preservar exportación de Levante incluyendo alcance y ubicación; evaluar exportación de Producción
  sólo si ya existe un exportador de esa grilla.

No se agrega un componente nuevo salvo que la presentación lo exija. Si se crea uno, llevará
`changeDetection` explícito según la convención Angular 22 del repositorio.

## 8. Índices y rendimiento

El índice existente `(farm_id, fecha_operacion)` es la línea base. Antes de agregar otro índice se
ejecutará `EXPLAIN (ANALYZE, BUFFERS)` con datos representativos de las tres modalidades.

Sólo si la medición lo justifica, considerar índices parciales para eventos visibles, por ejemplo:

- empresa + granja + fecha + silo;
- empresa + granja + fecha + núcleo/galpón;
- predicado limitado a eventos de ingreso/traslado no anulados y no destinados al próximo ciclo.

No crear índices especulativos: deben quedar la consulta antes/después y el costo medido en el plan.

## 9. Estrategia de pruebas

### 9.1 Application

- precedencia silo > galpón > granja;
- override nullable de granja;
- límites de Levante/Producción sin solapamiento;
- fase abierta/cerrada y filtros de usuario;
- rechazo de cambio de alcance con movimientos;
- creación, cierre y reapertura de períodos lote–silo.

### 9.2 Integración PostgreSQL obligatoria

Ejecutar la migración y la función real en PostgreSQL, preferiblemente con Testcontainers o con el
servicio PostgreSQL del pipeline. Cada test usa empresa/granja propias y transacción reversible.

Matriz mínima:

1. **Silo**: ingreso con núcleo/galpón `NULL`, silo asignado y referencia visible.
2. **Silo histórico**: retirar/inactivar el silo no elimina el movimiento pasado.
3. **Silo nuevo**: una asignación nueva no captura movimientos anteriores a su vigencia.
4. **Silo compartido**: dos lotes ven el movimiento durante sus vigencias, sin atribución exclusiva.
5. **Galpón**: sólo coincide granja+nucleo+galpón exactos.
6. **Granja**: incluye todos los movimientos de la granja y excluye otra granja.
7. **Tenant**: una empresa nunca ve filas de otra.
8. **Traslado interno**: salida y entrada conservan ubicación y referencia.
9. **Intergranja**: salida al despachar y entrada al recibir.
10. **Exclusiones**: anulado, próximo ciclo, devolución por eliminación, cantidad no positiva e
    ítem no alimento.
11. **Día sin seguimiento**: sigue incluido en `dias`.
12. **Transición de fase**: un movimiento aparece en una sola fase.
13. **Vacío válido**: 200 con contexto y `dias: []`.
14. **Migración idempotente**: aplicar dos veces el script idempotente no falla.

### 9.3 API

- endpoint Levante con lote permitido/no permitido;
- endpoint Producción por `loteId` y por `lotePosturaProduccionId`;
- filtros `desde/hasta` idénticos entre seguimientos y movimientos;
- diferencia entre error funcional, no autorizado, vacío válido y fallo de infraestructura;
- compatibilidad de endpoints v1 durante la transición.

### 9.4 Angular

- render de alcance y ubicación;
- ingreso, traslado entrada y traslado salida;
- varios movimientos y referencias el mismo día;
- varias capturas diarias sin duplicar el resumen;
- fila sin seguimiento;
- estados cargando/vacío/error y reintento;
- cambio rápido de lote/filtros sin aceptar una respuesta HTTP obsoleta.

## 10. Diagnósticos previos de solo lectura

Crear scripts `verificar_*` para medir, nunca corregir datos:

- movimientos por alcance efectivo y empresa;
- movimientos que hoy desaparecen por `lote_silos.activo = false` o silo inactivo;
- movimientos que una asignación actual hace visibles antes de `created_at`;
- lotes con fecha de transición solapada;
- cambios históricos de flags que puedan inferirse de auditoría;
- latencia y plan de ejecución de la función actual/v2;
- paridad v1 vs v2 para los casos que no deben cambiar.

Los scripts deben producir un resumen por empresa y detalle exportable para revisión. No ejecutarán
`UPDATE`, `DELETE`, `ALTER` ni tocarán producción.

## 11. Fases de implementación

### Fase A — línea base y decisiones de backfill

1. Ejecutar diagnósticos de solo lectura en copia local/restaurada.
2. Congelar conteos v1 por empresa, modo, lote, fecha y tipo.
3. Validar la política de backfill de vigencias.
4. Confirmar la regla del día de transición Levante→Producción.

Gate: informe revisado; ninguna mutación de datos.

### Fase B — modelo temporal y función v2

1. Agregar vigencias lote–silo y fecha final de Levante mediante migración EF idempotente.
2. Ejecutar backfill documentado dentro de la migración.
3. Crear función v2 y espejo SQL en el mismo commit.
4. Ajustar `LoteSiloService` y guardas de cambio de alcance.
5. Agregar tests puros e integración PostgreSQL.

Gate: migración desde una copia de producción, rollback ensayado, tests verdes y paridad aprobada.

### Fase C — API compatible

1. Agregar DTOs y endpoints v2.
2. Conservar v1.
3. Registrar métricas/errores y duración de consulta.
4. Smoke de endpoints en las tres empresas de referencia.

Gate: contrato v2 estable, v1 intacto.

### Fase D — interfaz

1. Consumir v2 en Levante y Producción.
2. Mostrar alcance, ubicación, estado de error y reintento.
3. Actualizar Excel de Levante.
4. Cubrir respuestas obsoletas por cambio rápido de filtros/lote.

Gate: build Angular, specs y prueba manual de apertura/cierre dos veces cuando aplique.

### Fase E — despliegue controlado

1. Desplegar en ambiente de pruebas y verificar migración.
2. Smoke por silo, galpón y granja con movimientos creados para la prueba.
3. Comparar conteos y latencia con línea base.
4. Desplegar en horario de baja operación.
5. Verificar TaskDef e imagen ECS realmente activas; revisar rollback silencioso.
6. Mantener v1 durante al menos una liberación estable.

Gate: aprobación explícita antes de producción y evidencia post-deploy.

## 12. Estrategia de rollback

- Frontend: volver a consumir v1 mediante rollback de imagen; v1 permanece desplegado.
- Backend: endpoint v2 es aditivo y puede dejar de usarse sin quitar la función.
- BD: no borrar vigencias ni fecha de cierre durante el rollback operativo; son datos aditivos.
- No ejecutar `Down()` destructivo en producción para resolver una incidencia.
- Si el backfill resulta incorrecto, detener el despliegue antes de activar el frontend, restaurar la
  copia de prueba y corregir la migración. No marcarla manualmente como aplicada.

## 13. Criterios de aceptación

- Reasignar o inactivar un silo no cambia lo que se veía en fechas anteriores.
- Asignar un silo nuevo no hace aparecer movimientos anteriores a su vigencia.
- Cambiar el alcance con movimientos existentes se rechaza o pasa por migración controlada.
- Cada movimiento muestra alcance y ubicación comprensibles.
- Una falla SQL es visible como error, no como lista vacía.
- Levante y Producción no duplican el día de transición.
- Silo, galpón y granja pasan la matriz de integración PostgreSQL.
- Empresas fuera del objetivo conservan paridad documentada.
- Migración, función y espejo SQL viajan en el mismo commit.
- Backend y frontend compilan; todas las suites y gates quedan verdes.
- El despliegue queda verificado contra TaskDef e imagen reales.

## 14. Fuera de alcance

- Cambiar la aritmética de saldos o consumos.
- Modificar stock o movimientos para «cuadrar» la visualización.
- Atribuir artificialmente un ingreso compartido a un único lote.
- Eliminar endpoints o función v1 en la misma liberación.
- Ejecutar DDL o backfills directamente en RDS desde una máquina local.

## 15. Archivos/módulos previstos

- `backend/sql/fn_resumen_movimientos_alimento_postura_v2.sql`
- `backend/sql/verificar_*movimientos_alimento_postura*.sql`
- nueva migración EF idempotente y su Designer
- `Domain/Entities/LoteSilo.cs`
- `Domain/Entities/LotePosturaLevante.cs`
- configuraciones EF correspondientes
- `Application/Calculos/` para vigencias, alcance y límites de fase
- DTOs e interfaces de seguimiento
- `Infrastructure/Services/Silos/LoteSiloService.cs`
- `Infrastructure/Services/MovimientosAlimentoSeguimientoConsultas.cs`
- services/controllers de Levante y Producción
- pruebas Application, Infrastructure PostgreSQL y API
- modelos, services, páginas y specs Angular de Levante/Producción

Los nombres definitivos de migración y DTO se decidirán al iniciar la fase para respetar el estado
del repositorio en ese momento. La arquitectura y las reglas anteriores no dependen de esos nombres.
