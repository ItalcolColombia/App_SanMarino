# 🔍 VERIFICACIÓN DE CÁLCULOS DE MORTALIDAD Y PORCENTAJES

## 📊 ANÁLISIS DEL PROBLEMA REPORTADO

### Datos de la Semana 2:
```json
{
  "avesInicioSemana": 12496,
  "avesFinSemana": 12462,
  "mortalidadTotalSemana": 37,
  "mortalidadPorcentajeSemana": 0.0423649771059578571322001943
}
```

### Cálculos:
- **Diferencia de aves**: 12496 - 12462 = **34 aves**
- **Mortalidad reportada**: **37 aves**
- **Diferencia**: 37 - 34 = **3 aves de diferencia**

## 🔎 CAUSAS POSIBLES DE LA DISCREPANCIA

### 1. Error de Sexaje
El **error de sexaje** puede **aumentar** el número de aves si se corrige una clasificación incorrecta:
- Si se detecta que un ave clasificada como macho es en realidad hembra, aumenta el conteo
- Esto compensa parcialmente las pérdidas por mortalidad

**Ejemplo**:
- Mortalidad: 37 aves
- Error de sexaje: +3 aves (corrección)
- Diferencia neta: 37 - 3 = 34 aves ✅

### 2. Selección/Descarte
La **selección/descarte** también resta aves:
- Si hay selección de aves (SelH, SelM), estas se restan del total
- Si hay traslados (valores negativos en SelH/SelM), también se restan

**Ejemplo**:
- Mortalidad: 37 aves
- Selección: 0 aves (según datos)
- Diferencia neta: 37 aves

Pero si hubiera selección:
- Mortalidad: 37 aves
- Selección: 3 aves
- Diferencia neta: 40 aves (pero solo se reporta 34)

### 3. Fórmula Correcta de Aves
```
avesFinSemana = avesInicioSemana 
                - mortalidadTotalSemana 
                - seleccionVentasSemana 
                - descarteSemana (si es positivo)
                - trasladosSemana (en valor absoluto)
                + errorSexajeSemana (si aumenta aves)
```

## ✅ CORRECCIONES IMPLEMENTADAS

### 1. Porcentaje de Mortalidad Semanal
**Antes**:
```csharp
MortalidadPorcentajeSemana = datosSemana.Average(d => d.MortalidadPorcentajeDiario)
```
❌ **Problema**: Promediaba los porcentajes diarios, lo cual no es correcto.

**Ahora**:
```csharp
MortalidadPorcentajeSemana = (mortalidadTotalSemana / avesInicioSemana) * 100
```
✅ **Correcto**: Calcula el porcentaje sobre el total de aves al inicio de la semana.

### 2. Cálculo de Aves Actuales
El cálculo de `avesActuales` ahora considera correctamente:
- ✅ Mortalidad (resta)
- ✅ Selección normal (resta)
- ✅ Traslados (resta)
- ✅ Error de sexaje (puede aumentar o disminuir, pero típicamente no afecta el total)

## 📋 VERIFICACIÓN DE DATOS

Para verificar correctamente, necesitamos ver:
1. **Mortalidad total de la semana**: ✅ 37 aves
2. **Selección/Ventas de la semana**: 0 aves (según datos)
3. **Descarte de la semana**: Necesita verificación
4. **Error de sexaje de la semana**: Necesita verificación
5. **Traslados de la semana**: Necesita verificación

### Cálculo Esperado:
```
avesFinSemana = avesInicioSemana 
                - mortalidadTotalSemana 
                - seleccionVentasSemana
                - descarteSemana
                - trasladosSemana
                + errorSexajeSemana (si aplica)

12462 = 12496 - 37 - 0 - descarte - traslados + errorSexaje
34 = 37 - descarte - traslados + errorSexaje
```

Si `descarte = 0` y `traslados = 0`, entonces:
- `34 = 37 + errorSexaje`
- `errorSexaje = -3` (imposible, el error de sexaje no puede ser negativo)

O si hay descarte/traslados:
- `34 = 37 - descarte - traslados + errorSexaje`
- `descarte + traslados - errorSexaje = 3`

## 🔧 RECOMENDACIONES

1. **Agregar campos al DTO semanal** para mostrar:
   - `ErrorSexajeSemana`: Total de errores de sexaje de la semana
   - `DescarteSemana`: Total de descarte de la semana
   - `TrasladosSemana`: Total de traslados de la semana

2. **Validar la fórmula**:
   ```
   avesFinSemana = avesInicioSemana 
                   - mortalidadTotalSemana 
                   - seleccionVentasSemana
                   - descarteSemana
                   - trasladosSemana
                   + errorSexajeSemana
   ```

3. **Agregar logging** para rastrear cuando hay discrepancias entre:
   - Diferencia de aves (inicio - fin)
   - Suma de factores (mortalidad + selección + descarte + traslados - error sexaje)

## 📝 NOTA IMPORTANTE

El **porcentaje de mortalidad semanal** ahora se calcula correctamente como:
```
(mortalidadTotalSemana / avesInicioSemana) * 100
```

Esto es más preciso que promediar los porcentajes diarios, ya que:
- Los porcentajes diarios se calculan sobre aves actuales (que van disminuyendo)
- El porcentaje semanal debe ser sobre aves al inicio de la semana

