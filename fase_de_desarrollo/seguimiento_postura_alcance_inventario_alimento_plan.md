# Seguimiento Levante/Producción — alcance efectivo del inventario de alimento

## Objetivo

Corregir la lectura de ingresos y traslados de alimento en los seguimientos diarios de Levante y
Producción para respetar las tres modalidades reales de inventario: por silo, por galpón y por
granja. Santa Reyes debe ver los movimientos del silo asignado al lote, con fecha, cantidad,
alimento y referencia; las empresas legacy deben conservar su comportamiento según la configuración
efectiva de empresa/granja.

## Diagnóstico

- La migración `AddInventarioPorSiloEnStockYMovimiento` agregó `silo_id` a
  `lote_registro_historico_unificado` y el trigger lo llena.
- La entidad/configuración EF del histórico no mapea `silo_id`, por lo que la consulta nueva no puede
  usar el dato ya persistido.
- La consulta actual exige coincidencia exacta de núcleo/galpón para toda empresa. En inventario por
  silo esos campos se guardan en `NULL`, por diseño, y por eso Santa Reyes obtiene una lista vacía.
- La consulta tampoco respeta el modo por granja (`farm.ManejaAlimentoPorGalpon ??
  company.ManejaAlimentoPorGalpon`), porque sigue filtrando núcleo/galpón.

## Enfoque arquitectónico

- Mantener `lote_registro_historico_unificado` como fuente trazable y conservar todos los filtros
  actuales (empresa, alimento, fecha, anulado, próximo ciclo y devoluciones).
- Mapear `SiloId` en entidad/configuración y alinear el `ModelSnapshot`; no crear migración ni DDL,
  porque la columna y el trigger ya fueron desplegados por la migración de agosto.
- Resolver el alcance de forma pura y tipada:
  1. `Silo` cuando `company.ManejaInventarioPorSilo` está activo.
  2. `Galpon` cuando el flag de silo está apagado y
     `AlimentoNivelResolver.ManejaPorGalpon(farmOverride, companyDefault)` es verdadero.
  3. `Granja` en el resto de casos.
- Ejecutar el filtrado pesado en SQL:
  - silo: `farm_id` + `silo_id` dentro de los `lote_silos` activos del lote y de la misma empresa y
    granja;
  - galpón: `farm_id` + núcleo/galpón exactos;
  - granja: sólo `farm_id`, sin exigir núcleo/galpón.
- Si faltan empresa, granja o silos asignados, devolver vacío (fail-closed).
- Reutilizar la consulta para Levante y Producción pasando el `lote_id` maestro ya validado.

## Archivos a modificar/crear

- `Application/Calculos/MovimientosAlimentoSeguimientoCalculos.cs`: modo de alcance puro.
- `Domain/Entities/LoteRegistroHistoricoUnificado.cs`: propiedad `SiloId`.
- `Infrastructure/Persistence/Configurations/LoteRegistroHistoricoUnificadoConfiguration.cs`:
  mapeo de `silo_id`.
- `Infrastructure/Migrations/ZooSanMarinoContextModelSnapshot.cs`: alinear el modelo con la columna
  ya creada por la migración existente.
- `Infrastructure/Services/MovimientosAlimentoSeguimientoConsultas.cs`: resolución de flags y
  predicado SQL por alcance.
- Callers de Levante y Producción: pasar el lote maestro.
- Tests de Application e Infrastructure para los tres modos y aislamiento multiempresa.

## Base de datos / SQL

- Sin cambios de esquema, sin migración nueva y sin escritura de datos.
- La prueba de flujo usará EF InMemory con datos aislados: ingreso en silo, ingreso por galpón e
  ingreso por granja. No tocará RDS ni la base local del usuario.

## Reglas de negocio y seguridad

1. Nunca mostrar movimientos de otra empresa o granja.
2. En modo silo, sólo mostrar silos activos asignados al lote y pertenecientes a su granja.
3. En modo galpón, conservar coincidencia exacta de núcleo/galpón.
4. En modo granja, mostrar los movimientos de alimento de la granja aunque núcleo/galpón sean nulos.
5. Mantener exclusiones actuales y el rango real de la fase.
6. No modificar stock, consumos, reservas, movimientos ni referencias.

## Casos de prueba

- Santa Reyes/silo: un `INV_INGRESO` con `nucleo_id` y `galpon_id` nulos, `silo_id` asignado al lote,
  aparece con cantidad, alimento y referencia.
- Silo de otro lote, otra granja u otra empresa no aparece.
- Modo galpón muestra sólo la ubicación exacta y no movimientos globales de la granja.
- Modo granja muestra el ingreso de la granja sin requerir núcleo/galpón.
- Traslado de entrada/salida conserva tipo y referencia.
- Anulado, próximo ciclo, devolución e ítem no alimento siguen excluidos.
- `dotnet build`, `dotnet test`, gates backend, `yarn build`, tests/gates frontend quedan verdes.
