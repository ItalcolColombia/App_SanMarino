# 📐 FÓRMULAS DEL REGISTRO DIARIO DE LEVANTE

## 🎯 OBJETIVO
Documentar todas las fórmulas necesarias para calcular los campos del registro diario de levante, basándose en la estructura del Excel proporcionado.

---

## 📋 DATOS DE ENTRADA (Campos Verdes - Se Capturan)

### Datos Generales:
- `IDLoteRAP`: ID del lote (desde `Lote.LoteId`)
- `Regional`: Desde `Lote.Regional`
- `GRANJA`: Desde `Lote.GranjaId` → `Farm.name`
- `Lote`: Desde `Lote.LoteNombre`
- `RAZA`: Desde `Lote.Raza`
- `AñoG`: Desde `Lote.AnoTablaGenetica`
- `NÚCLEOL`: Desde `Lote.NucleoId` → `Nucleo.nucleoNombre`
- `AÑON`: Desde `Lote.AnoTablaGenetica` (mismo que AñoG)
- `Edad`: Calculado = `(FechaRegistro - Lote.FechaEncaset)` en días
- `Fecha`: `SeguimientoLoteLevante.FechaRegistro`
- `SemAño`: Semana del año (1-52/53) - **CALCULAR**
- `TRASLADO`: Detectado cuando `SelH < 0` o `SelM < 0`
- `Observaciones`: `SeguimientoLoteLevante.Observaciones`

### Inventario Inicial:
- `HEMBRAINI`: `Lote.HembrasL`
- `MACHOINI`: `Lote.MachosL`

### Hembras - Producción y Manejo:
- `Hembra`: **CALCULAR** = `HEMBRAINI - MortCajaH - AcMortH - AcSelH - AcErrH`
- `MortH`: `SeguimientoLoteLevante.MortalidadHembras`
- `SelH`: `SeguimientoLoteLevante.SelH` (puede ser negativo)
- `ErrorH`: `SeguimientoLoteLevante.ErrorSexajeHembras`
- `ConsKgH`: `SeguimientoLoteLevante.ConsumoKgHembras`
- `PesoH`: `SeguimientoLoteLevante.PesoPromH`
- `UniformH`: `SeguimientoLoteLevante.UniformidadH`
- `%CVH`: `SeguimientoLoteLevante.CvH`

### Hembras - Nutrición:
- `KcalAlH`: `SeguimientoLoteLevante.KcalAlH` (o desde catálogo de alimentos)
- `%ProtAlH`: `SeguimientoLoteLevante.ProtAlH` (o desde catálogo de alimentos)
- `KcalAveH`: **CALCULAR** = `ConsKgH * KcalAlH` (si KcalAlH está disponible)
- `ProtAveH`: **CALCULAR** = `ConsKgH * %ProtAlH / 100` (si %ProtAlH está disponible)

### Machos - Producción y Manejo:
- `SaldoMacho`: **CALCULAR** = `MACHOINI - MortCajaM - AcMortM - AcSelM - AcErrM`
- `MortM`: `SeguimientoLoteLevante.MortalidadMachos`
- `SelM`: `SeguimientoLoteLevante.SelM` (puede ser negativo)
- `ErrorM`: `SeguimientoLoteLevante.ErrorSexajeMachos`
- `ConsKgM`: `SeguimientoLoteLevante.ConsumoKgMachos`
- `PesoM`: `SeguimientoLoteLevante.PesoPromM`
- `UniformM`: `SeguimientoLoteLevante.UniformidadM`
- `%CVM`: `SeguimientoLoteLevante.CvM`

### Machos - Nutrición:
- `KcalAlM`: **FALTA** - Necesita agregarse a la entidad
- `%ProtAlM`: **FALTA** - Necesita agregarse a la entidad
- `KcalAveM`: **CALCULAR** = `ConsKgM * KcalAlM` (si KcalAlM está disponible)
- `ProtAveM`: **CALCULAR** = `ConsKgM * %ProtAlM / 100` (si %ProtAlM está disponible)

---

## 🧮 FÓRMULAS DE CÁLCULO

### 1. SEMANA DEL AÑO (SemAño)
```sql
-- Calcular semana del año (ISO 8601)
EXTRACT(WEEK FROM FechaRegistro)
-- O alternativamente:
FLOOR((EXTRACT(DOY FROM FechaRegistro) - 1) / 7) + 1
```

### 2. HEMBRAS - INDICADORES Y ACUMULADOS

#### 2.1. Porcentajes Diarios:
```sql
%MortH = CASE 
    WHEN Hembra > 0 THEN (MortH / Hembra) * 100 
    ELSE 0 
END

%SelH = CASE 
    WHEN Hembra > 0 THEN (SelH / Hembra) * 100 
    ELSE 0 
END

%ErrH = CASE 
    WHEN Hembra > 0 THEN (ErrorH / Hembra) * 100 
    ELSE 0 
END
```

#### 2.2. Totales Diarios:
```sql
M+S+EH = MortH + SelH + ErrorH
```

#### 2.3. Acumulados (desde inicio del lote hasta fecha actual):
```sql
ACMortH = SUM(MortH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

ACSelH = SUM(SelH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

ACErrH = SUM(ErrorH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

AcConsH = SUM(ConsKgH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)
```

#### 2.4. Consumo Acumulado en Gramos:
```sql
ConsAcGrH = AcConsH * 1000
```

#### 2.5. Gramos por Ave por Día:
```sql
GrAveDiaH = CASE 
    WHEN Hembra > 0 AND Edad > 0 
    THEN (ConsAcGrH / (Hembra * Edad))
    ELSE 0 
END
```

#### 2.6. Retiro Acumulado:
```sql
RetAcH = ACMortH + ACSelH + ACErrH

%RetiroH = CASE 
    WHEN HEMBRAINI > 0 THEN (RetAcH / HEMBRAINI) * 100 
    ELSE 0 
END
```

#### 2.7. Incremento de Consumo:
```sql
IncrConsH = ConsKgH - LAG(ConsKgH, 1) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro
)
```

#### 2.8. Comparaciones con Guía Genética:
```sql
%MortHGUIA = [Valor desde tabla de guía genética según edad y raza]

DifMortH = %MortH - %MortHGUIA

PesoHGUIA = [Valor desde tabla de guía genética según edad y raza]

%DifPesoH = CASE 
    WHEN PesoHGUIA > 0 THEN ((PesoH - PesoHGUIA) / PesoHGUIA) * 100 
    ELSE NULL 
END

UnifHGUIA = [Valor desde tabla de guía genética según edad y raza]

ConsAcGrHGUIA = [Valor desde tabla de guía genética según edad y raza]

GrAveDiaGUIAH = [Valor desde tabla de guía genética según edad y raza]

%DifConsH = CASE 
    WHEN ConsAcGrHGUIA > 0 
    THEN ((ConsAcGrH - ConsAcGrHGUIA) / ConsAcGrHGUIA) * 100 
    ELSE NULL 
END

IncrConsHGUIA = [Valor desde tabla de guía genética según edad y raza]

RetiroHGUIA = [Valor desde tabla de guía genética según edad y raza]
```

### 3. MACHOS - INDICADORES Y ACUMULADOS

#### 3.1. Porcentajes Diarios:
```sql
%MortM = CASE 
    WHEN SaldoMacho > 0 THEN (MortM / SaldoMacho) * 100 
    ELSE 0 
END

%SelM = CASE 
    WHEN SaldoMacho > 0 THEN (SelM / SaldoMacho) * 100 
    ELSE 0 
END

%ErrM = CASE 
    WHEN SaldoMacho > 0 THEN (ErrorM / SaldoMacho) * 100 
    ELSE 0 
END
```

#### 3.2. Totales Diarios:
```sql
M+S+EM = MortM + SelM + ErrorM
```

#### 3.3. Acumulados:
```sql
ACMortM = SUM(MortM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

ACSelM = SUM(SelM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

ACErrM = SUM(ErrorM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

AcConsM = SUM(ConsKgM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)
```

#### 3.4. Consumo Acumulado en Gramos:
```sql
ConsAcGrM = AcConsM * 1000
```

#### 3.5. Gramos por Ave por Día:
```sql
GrAveDiaM = CASE 
    WHEN SaldoMacho > 0 AND Edad > 0 
    THEN (ConsAcGrM / (SaldoMacho * Edad))
    ELSE 0 
END
```

#### 3.6. Retiro Acumulado:
```sql
RetAcM = ACMortM + ACSelM + ACErrM

%RetAcM = CASE 
    WHEN MACHOINI > 0 THEN (RetAcM / MACHOINI) * 100 
    ELSE 0 
END
```

#### 3.7. Incremento de Consumo:
```sql
IncrConsM = ConsKgM - LAG(ConsKgM, 1) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro
)
```

#### 3.8. Comparaciones con Guía Genética:
```sql
%MortMGUIA = [Valor desde tabla de guía genética según edad y raza]

DifMortM = %MortM - %MortMGUIA

PesoMGUIA = [Valor desde tabla de guía genética según edad y raza]

%DifPesoM = CASE 
    WHEN PesoMGUIA > 0 THEN ((PesoM - PesoMGUIA) / PesoMGUIA) * 100 
    ELSE NULL 
END

UnifMGUIA = [Valor desde tabla de guía genética según edad y raza]

ConsAcGrMGUIA = [Valor desde tabla de guía genética según edad y raza]

GrAveDiaMGUIA = [Valor desde tabla de guía genética según edad y raza]

DifConsM = ConsAcGrM - ConsAcGrMGUIA

IncrConsMGUIA = [Valor desde tabla de guía genética según edad y raza]

RetiroMGUIA = [Valor desde tabla de guía genética según edad y raza]
```

### 4. COMPARATIVOS Y ERRORES DE SEXAJE

```sql
%RelM/H = CASE 
    WHEN Hembra > 0 THEN (SaldoMacho / Hembra) * 100 
    ELSE NULL 
END

ErrSexAcH = SUM(ErrorH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

%ErrSxAcH = CASE 
    WHEN HEMBRAINI > 0 THEN (ErrSexAcH / HEMBRAINI) * 100 
    ELSE 0 
END

ErrSexAcM = SUM(ErrorM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

%ErrSxAcM = CASE 
    WHEN MACHOINI > 0 THEN (ErrSexAcM / MACHOINI) * 100 
    ELSE 0 
END

DifConsAcH = ConsAcGrH - ConsAcGrHGUIA

DifConsAcM = ConsAcGrM - ConsAcGrMGUIA
```

### 5. NUTRICIÓN SEMANAL - HEMBRAS

```sql
-- Agrupar por semana (desde inicio del lote)
ALIMHGUÍA = [Alimento de la guía genética para la semana actual]

-- Suma semanal de KcalAveH
KcalSemH = SUM(KcalAveH) OVER (
    PARTITION BY LoteId, 
                 EXTRACT(WEEK FROM FechaRegistro) - EXTRACT(WEEK FROM MIN(FechaRegistro) OVER (PARTITION BY LoteId))
    ORDER BY FechaRegistro
)

-- Acumulado semanal
KcalSemAcH = SUM(KcalSemH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

KcalSemHGUIA = [Valor desde tabla de guía genética según semana]

KcalSemAcHGUIA = [Valor acumulado desde tabla de guía genética]

-- Suma semanal de ProtAveH
ProtSemH = SUM(ProtAveH) OVER (
    PARTITION BY LoteId, 
                 EXTRACT(WEEK FROM FechaRegistro) - EXTRACT(WEEK FROM MIN(FechaRegistro) OVER (PARTITION BY LoteId))
    ORDER BY FechaRegistro
)

-- Acumulado semanal
ProtSemAcH = SUM(ProtSemH) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

ProtSemHGUIA = [Valor desde tabla de guía genética según semana]

ProtSemAcHGUIA = [Valor acumulado desde tabla de guía genética]
```

### 6. NUTRICIÓN SEMANAL - MACHOS

```sql
-- Similar a hembras pero con datos de machos
ALIMMGUÍA = [Alimento de la guía genética para la semana actual]

KcalSemM = SUM(KcalAveM) OVER (
    PARTITION BY LoteId, 
                 EXTRACT(WEEK FROM FechaRegistro) - EXTRACT(WEEK FROM MIN(FechaRegistro) OVER (PARTITION BY LoteId))
    ORDER BY FechaRegistro
)

KcalSemAcM = SUM(KcalSemM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

KcalSemMGUIA = [Valor desde tabla de guía genética según semana]

KcalSemAcMGUIA = [Valor acumulado desde tabla de guía genética]

ProtSemM = SUM(ProtAveM) OVER (
    PARTITION BY LoteId, 
                 EXTRACT(WEEK FROM FechaRegistro) - EXTRACT(WEEK FROM MIN(FechaRegistro) OVER (PARTITION BY LoteId))
    ORDER BY FechaRegistro
)

ProtSemAcM = SUM(ProtSemM) OVER (
    PARTITION BY LoteId 
    ORDER BY FechaRegistro 
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
)

ProtSemMGUIA = [Valor desde tabla de guía genética según semana]

ProtSemAcMGUIA = [Valor acumulado desde tabla de guía genética]
```

---

## 🔧 IMPLEMENTACIÓN EN C#

### Estructura de Cálculo Recomendada:

```csharp
public class CalculadoraRegistroLevante
{
    // Calcular semana del año
    public static int CalcularSemanaDelAnio(DateTime fecha)
    {
        var culture = CultureInfo.CurrentCulture;
        var calendar = culture.Calendar;
        return calendar.GetWeekOfYear(fecha, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
    
    // Calcular hembras vivas
    public static int CalcularHembrasVivas(
        int hembrasIniciales, 
        int mortCajaH, 
        int acMortH, 
        int acSelH, 
        int acErrH)
    {
        return Math.Max(0, hembrasIniciales - mortCajaH - acMortH - acSelH - acErrH);
    }
    
    // Calcular machos vivos
    public static int CalcularMachosVivos(
        int machosIniciales, 
        int mortCajaM, 
        int acMortM, 
        int acSelM, 
        int acErrM)
    {
        return Math.Max(0, machosIniciales - mortCajaM - acMortM - acSelM - acErrM);
    }
    
    // Calcular porcentaje
    public static double? CalcularPorcentaje(double? valor, double? total)
    {
        if (total == null || total == 0) return null;
        return (valor ?? 0) / total * 100;
    }
    
    // Calcular KcalAveH
    public static double? CalcularKcalAveH(double? consumoKgH, double? kcalAlH)
    {
        if (consumoKgH == null || kcalAlH == null) return null;
        return consumoKgH * kcalAlH;
    }
    
    // Calcular ProtAveH
    public static double? CalcularProtAveH(double? consumoKgH, double? protAlH)
    {
        if (consumoKgH == null || protAlH == null) return null;
        return consumoKgH * protAlH / 100;
    }
    
    // Calcular gramos por ave por día
    public static double? CalcularGrAveDia(
        double? consumoAcumuladoGr, 
        int? avesVivas, 
        int? edadDias)
    {
        if (consumoAcumuladoGr == null || avesVivas == null || edadDias == null) return null;
        if (avesVivas == 0 || edadDias == 0) return null;
        return consumoAcumuladoGr / (avesVivas * edadDias);
    }
}
```

---

## 📝 NOTAS IMPORTANTES

1. **Acumulados**: Todos los acumulados se calculan desde el inicio del lote hasta la fecha actual del registro.

2. **Window Functions**: Se recomienda usar `SUM() OVER()` con `ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW` para calcular acumulados eficientemente.

3. **Guía Genética**: Los valores de guía genética deben obtenerse de una tabla de referencia basada en:
   - Raza del lote
   - Edad (en días o semanas)
   - Año de la tabla genética

4. **División por Cero**: Todas las fórmulas que involucran división deben validar que el denominador no sea cero.

5. **Valores NULL**: Los campos opcionales (como peso, uniformidad) pueden ser NULL y las fórmulas deben manejarlo correctamente.

---

## 🚀 PRÓXIMOS PASOS

1. Crear/actualizar el stored procedure `sp_recalcular_seguimiento_levante` con todas estas fórmulas
2. Agregar campos faltantes a la entidad `SeguimientoLoteLevante`:
   - `KcalAlM`
   - `ProtAlM`
   - `KcalAveM`
   - `ProtAveM`
3. Actualizar el formulario del frontend para capturar todos los campos de entrada
4. Implementar cálculo de `SemAño` en el backend
5. Integrar con tabla de guía genética para comparaciones

