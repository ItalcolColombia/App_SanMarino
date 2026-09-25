# Plan — Carga masiva de postura (Levante/Producción): hasta 4 alimentos por sexo

## Contexto

La plantilla de carga masiva de Seguimiento Levante y Seguimiento Producción hoy solo ofrece **2
slots de alimento del inventario por sexo** ("Alimento 1/2 H", "Alimento 1/2 M", con su "Consumo
Alimento N X" y, en empresas con `maneja_inventario_por_silo`, "Silo Alimento N X"). El usuario pide
subir el tope a **4 alimentos por sexo**, igual que ya soporta el modelo de datos
(`metadata.itemsHembras[]`/`itemsMachos[]`, ver memoria `alimentos-multiples-genero-levante`) y la
columna `tipo_alimento` (ya ampliada a `varchar(500)` en Levante — commit `2a35d63` — y `text` en
Producción, así que **no hace falta ninguna migración de BD** para este cambio).

El esquema de columnas de la hoja "Datos" es **compartido** entre Levante y Producción
(`MigracionEsquemas.AlimentosPorSexoPostura()`), así que el cambio se hace en un solo lugar y
alcanza a las dos líneas.

## Enfoque

Cálculo puro sin migración: extender de 2 a 4 los slots en los 4 puntos que hoy están cableados a
`{1, 2}`:

1. **`backend/src/ZooSanMarino.Application/Calculos/MigracionEsquemas.cs`** —
   `AlimentosPorSexoPostura()`: agregar los slots 3 y 4 (H y M), mismas 3 columnas por slot
   (Alimento/Consumo/Silo) con sus alias, mismo patrón que 1 y 2.
2. **`backend/src/ZooSanMarino.Application/Calculos/PlantillaPosturaCalculos.cs`** —
   - `AlimentoMachos`: agregar "Alimento 3 M"/"Consumo Alimento 3 M"/"Alimento 4 M"/"Consumo
     Alimento 4 M" (se ocultan junto con los demás slots de machos).
   - `SilosPorSlot`: agregar "Silo Alimento 3 H/M" y "Silo Alimento 4 H/M" (se ocultan en modo
     clásico, igual que 1 y 2).
3. **`backend/src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Historicos.cs`** —
   - `GenerarPlantillaSeguimientoAsync`: los dos `foreach` que arman los dropdowns de alimento y de
     silo (`new[] { "Alimento 1 H", "Alimento 2 H", "Alimento 1 M", "Alimento 2 M" }` y su par de
     silo) pasan a listar los 4 slots por sexo.
   - `LeerAlimentosPostura` (parseo): `foreach (var n in new[] { 1, 2 })` → `new[] { 1, 2, 3, 4 }`.
     Es el único cambio que hace que el importador LEA los slots nuevos; el resto de la función
     (`LeerAlimentoSlot`, `ResolverSiloSlotPostura`) ya es genérico por nombre de columna.
4. **`backend/src/ZooSanMarino.Application/Calculos/MigracionEjemploPosturaCalculos.cs`** —
   `ValorDatos`: agregar los casos de "Alimento 3/4 H/M" y "Silo Alimento 3/4 H/M" con el mismo
   criterio que el slot 2 (vacíos a propósito, opcionales). Sin esto igual no rompe nada (el
   `_ => ""` por defecto ya cubre columnas no reconocidas), pero documenta la decisión igual que el
   resto del switch.

## Lo que NO cambia

- **Sin migración EF**: `tipo_alimento` ya soporta 4 nombres concatenados (Levante `varchar(500)`,
  Producción `text`); `ResolverTipoAlimento` sigue recortando con `TipoAlimentoCalculos.Recortar`.
- **Sin cambios de negocio**: el consumo por ítem, el descuento de inventario, el silo por slot y la
  idempotencia ya son genéricos por lista de ítems (`ItemSeguimientoDto`), no por cantidad de slots.
- **Sin cambios en el modal de seguimiento diario** (`lote-levante/modal-create-edit`): el pedido es
  específicamente sobre la carga masiva/plantilla. Ese modal tiene su propio historial (varchar +
  UI) y queda fuera de este alcance.
- **Sin cambios de frontend**: el módulo de Migraciones no hardcodea nombres de columna de alimento;
  la plantilla y el parseo son 100% backend-driven.

## Casos de prueba

- `MigracionEsquemas`: el esquema de Levante y Producción exponen 4 slots por sexo (8 slots totales:
  4H + 4M), cada uno con sus 3 columnas (Alimento/Consumo/Silo).
- `PlantillaPosturaCalculosTests`:
  - `Sanmarino_SoloOcultaLasColumnasDeSilo`: la lista exacta de columnas de silo ocultas pasa de 4 a
    8 (los 4 slots × 2 sexos).
  - `Sanmarino_LaPlantillaConservaLas43ColumnasHistoricas`: el conteo de columnas emitidas sube de
    43 a 51 (12 columnas nuevas − 4 de silo que Sanmarino ya ocultaba de más, netas +8... revisar
    número exacto al implementar) — se corrige el número esperado, no el criterio (delta cero de
    columnas *viejas*, las nuevas se suman).
  - Santa Reyes: los slots 3/4 de machos se ocultan igual que 1/2 (con `OcultaMachosEnPostura`).
  - `NingunaColumnaOcultableEsRequerida` / `TodaColumnaOcultableExisteEnElEsquema`: siguen en verde
    sin tocar (cálculo genérico).
- Nuevo test (o extensión de uno existente) que confirme que `LeerAlimentosPostura`/el import real
  lee un 3º y 4º alimento por sexo — vía el proyecto de tests de Infrastructure si hay fixture
  disponible; si no, se documenta como validado por el esquema + smoke manual (dry-run con un Excel
  de 4 alimentos por sexo).

## Validación

- `dotnet build` (0 errores, sin advertencias nuevas).
- `dotnet test` — proyecto `ZooSanMarino.Application.Tests` (y `ZooSanMarino.Infrastructure.Tests`
  si aplica) en verde.
- Smoke manual: descargar la plantilla de un lote elegible (Sanmarino, Levante y Producción),
  confirmar que aparecen las columnas "Alimento 3/4 H/M" + "Consumo Alimento 3/4 H/M", completarlas
  y validar/importar en dry-run.
