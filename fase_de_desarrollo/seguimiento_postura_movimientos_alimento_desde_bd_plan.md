# Plan — movimientos de alimento armados en BD para Levante y Producción

Fecha: 27-sep-2026

## Objetivo

Mover a PostgreSQL la selección, alcance y agrupación diaria de ingresos/traslados de alimento que
hoy ejecutan el backend y Angular para las grillas de Seguimiento Diario de Levante y Producción.
La base de datos debe devolver por fecha las colecciones de ingresos, traslados y referencias,
incluidos los días con movimientos pero sin registro de seguimiento.

## Enfoque arquitectónico

1. Crear una función SQL compartida `fn_movimientos_alimento_postura_diario` que:
   - reciba empresa, lote, granja, núcleo, galpón y rango de fechas;
   - resuelva dentro de PostgreSQL el alcance efectivo silo > galpón > granja;
   - filtre empresa, granja, ítems de alimento, anulaciones, próximo ciclo, devoluciones y fechas;
   - agrupe por día y devuelva JSON ordenado para ingresos, traslados y referencias;
   - preserve cada movimiento y no netee entradas con salidas.
2. Aplicar la función mediante migración EF idempotente y mantener
   `backend/sql/fn_movimientos_alimento_postura_diario.sql` como espejo.
3. Incorporar un contrato diario tipado en Application. Infrastructure se limita a ejecutar la
   función y deserializar el JSON; no agrupa ni decide reglas.
4. Conservar los endpoints planos actuales para compatibilidad y agregar lecturas diarias para las
   dos pantallas. Los endpoints planos reutilizarán la misma fuente SQL.
5. Cambiar Levante y Producción para consumir el resumen diario de BD. Angular sólo indexa por fecha
   y formatea números; se elimina de los flujos vivos el agrupamiento/clasificación de movimientos.

## Archivos previstos

- `backend/sql/fn_movimientos_alimento_postura_diario.sql`
- nueva migración EF en `backend/src/ZooSanMarino.Infrastructure/Migrations/`
- DTOs e interfaces en `backend/src/ZooSanMarino.Application/`
- `MovimientosAlimentoSeguimientoConsultas.cs`
- servicios y controllers de Levante/Producción
- modelos, services y páginas Angular de Levante/Producción
- pruebas Application/Infrastructure/frontend relacionadas

## Base de datos

No se agregan tablas ni columnas. Se crea/reemplaza una función SQL `STABLE`; la migración es el
vehículo de despliegue. La función deriva el alcance con flags tipados y valida que empresa/granja,
silos e ítems pertenezcan al mismo tenant.

## Reglas de negocio

- Silo tiene precedencia cuando `companies.maneja_inventario_por_silo` está activo.
- Sin silo, `farms.maneja_alimento_por_galpon ?? companies.maneja_alimento_por_galpon` decide
  galpón o granja.
- Tipos visibles: `INV_INGRESO`, `INV_TRASLADO_ENTRADA`,
  `INV_TRASLADO_SALIDA`.
- Excluir anulados, próximo ciclo, cantidades no positivas y devoluciones por eliminación.
- Referencia visible = `referencia` no vacía; si no existe, `numero_documento`.
- Un movimiento aparece una sola vez y siempre conserva tipo, cantidad, alimento y documento.
- Los días sin seguimiento permanecen visibles como filas informativas en ambas pantallas.
- Los contratos planos existentes siguen disponibles para no romper consumidores externos.

## Casos de prueba

- Empresa por silo: sólo silos activos asignados al lote; cantidad, alimento y referencia íntegros.
- Empresa/granja por galpón: sólo núcleo/galpón exactos.
- Empresa por granja: incluye movimientos de toda la granja y aísla otras granjas/empresas.
- Varios ingresos/traslados el mismo día: JSON ordenado, sin neteo y referencias sin duplicados.
- Movimiento en día sin seguimiento: aparece en la sección informativa.
- Filtros desde/hasta y fase cerrada no expanden el rango.
- Compatibilidad: endpoint plano contiene los mismos movimientos que el resumen diario.
- Builds/tests backend y frontend, gate SQL→migración y puertos libres.
