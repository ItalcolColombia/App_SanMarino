# 26 — Extracción de math puro a `Application/Calculos/` (Movimientos Pollo Engorde)

> **Objetivo:** sacar la lógica de **cálculo puro** (sin EF/BD) de los métodos largos del servicio a
> un cálculo estático determinista en `Application/Calculos/`, siguiendo la convención existente
> (`IndicadorEcuadorCalculos`). Mejora testabilidad y legibilidad **sin cambiar comportamiento**
> (aritmética y redondeos idénticos). Continuación del refactor backend (plan 25).

---

## 1. Qué se extrae (math puro identificado)

### a) Prorrateo de peso por línea (de `CreateVentaGranjaDespachoAsync`)
Distribuye peso bruto/tara/neto global proporcional a las aves de cada línea, con **ajuste de
residuo de redondeo (3 decimales)** a la línea con más aves. Es el **espejo exacto** del frontend
`prorateo-peso.funcion.ts`. Hoy son ~38 líneas inline (VentaGranja.cs 66-103).

### b) Máximo vendible + exceso por sexo (de `AuditarVentasEngordeAsync`)
- `maxVendible = max(0, encasetadas − mortCaja − mortSeg − selección − errorSexaje − asignadas)`.
- `exceso = max(0, vendidas − maxVendible)`.
Inline en Auditoria.cs (189-199) y recalculado para el DTO (273-275).

> **No se toca** el prorrateo simple de `OrganizarPesoAsync` (peso/ave por grupo): es otro algoritmo,
> trivial y entrelazado con la mutación de entidades; su extracción aporta poco y suma riesgo.

---

## 2. Nuevo archivo

`Application/Calculos/MovimientoPolloEngordeCalculos.cs` — `public static class` con:

```csharp
public readonly record struct PesoLineaProrrateado(double? Bruto, double? Tara, double? Neto, double? Promedio);

// Prorrateo con ajuste de residuo (espejo del front). Todo null si no hay aves.
public static PesoLineaProrrateado[] ProrratearPesoPorLinea(double pesoBrutoGlobal, double pesoTaraGlobal, IReadOnlyList<int> avesPorLinea);

// Auditoría: límite vendible y exceso por sexo.
public static int MaxVendiblePorSexo(int encasetadas, int mortalidadCaja, int mortalidadSeguimiento, int seleccion, int errorSexaje, int asignadas);
public static int Exceso(int totalVendidas, int maxVendible);
```

Aritmética **idéntica** a la actual: `Math.Round(x, 3)` (ToEven), mismos factores, mismo orden, mismo
ajuste de residuo a la línea con más aves (primer máximo gana, comparación estricta `>`).

---

## 3. Cambios en los callers (delegación, sin cambiar lógica)

- **VentaGranja.cs**: reemplazar el bloque 66-103 por: armar `avesPorLinea`, llamar
  `ProrratearPesoPorLinea` (solo si `tienePeso`; si no, arreglo de structs default = null) y leer
  `prorrateo[i].Bruto/.Tara/.Neto/.Promedio` al construir cada DTO (154-157). Se elimina
  `totalAvesDespacho` (lo calcula el helper). `pesoNetoGlobal` se mantiene (se usa para el DTO).
- **Auditoria.cs**: `maxH/maxM` → `MaxVendiblePorSexo(...)`; `excesoH/M/X` (197-199 y 273-275) →
  `Exceso(...)`. `maxX = Math.Max(0, encX)` se deja inline (caso sin descuentos).
- El `using ZooSanMarino.Application.Calculos;` ya está en ambos archivos partial.

---

## 4. Tests (validan que se preservó la aritmética)

`tests/ZooSanMarino.Application.Tests/MovimientoPolloEngordeCalculosTests.cs` (xUnit, como
`IndicadorEcuadorCalculosTests`):
- Prorrateo: caso multi-lote con residuo → la suma de bruto/tara/neto por línea == global (sin
  pérdida por redondeo); el residuo cae en la línea con más aves; sin aves → todo null.
- MaxVendiblePorSexo / Exceso: casos con y sin exceso, con piso en 0.

---

## 5. Validación
1. `dotnet build` backend → 0 errores.
2. `dotnet test tests/ZooSanMarino.Application.Tests` → verde.
3. Sin BD ni schema involucrados; sin procesos vivos.

## 6. Fuera de alcance
- `OrganizarPesoAsync` (math trivial inline).
- Mover `MovimientoPolloEngordeFilterDataService.cs` a la carpeta del módulo.
