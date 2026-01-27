# 📊 Análisis de Comparativos con Guía Genética

## 📋 Resumen Ejecutivo

Este documento analiza qué datos están disponibles en la **guía genética** (`produccion_avicola_raw`) y qué comparativos por semana se están realizando actualmente en los reportes de **Levante** y **Producción**.

---

## 🗂️ Campos Disponibles en la Guía Genética (`ProduccionAvicolaRaw`)

### Información Básica
- `anio_guia` - Año de la guía genética
- `raza` - Raza de las aves
- `edad` - Edad en semanas

### Mortalidad y Retiro
- `mort_sem_h` - % Mortalidad semanal hembras
- `mort_sem_m` - % Mortalidad semanal machos
- `retiro_ac_h` - Retiro acumulado hembras
- `retiro_ac_m` - Retiro acumulado machos

### Consumo
- `cons_ac_h` - Consumo acumulado hembras (gramos)
- `cons_ac_m` - Consumo acumulado machos (gramos)
- `alim_h` - Alimento hembras
- `alim_m` - Alimento machos

### Ganancia Diaria
- `gr_ave_dia_h` - Gramos ave/día hembras
- `gr_ave_dia_m` - Gramos ave/día machos

### Peso
- `peso_h` - Peso hembras (gramos)
- `peso_m` - Peso machos (gramos)
- `peso_mh` - Peso M/H

### Uniformidad
- `uniformidad` - % Uniformidad

### Producción (Reproductoras - Semanas 26+)
- `h_total_aa` - Huevos total ave alojada
- `h_inc_aa` - Huevos incubables ave alojada
- `prod_porcentaje` - % Producción
- `aprov_sem` - % Aprovechamiento semanal
- `aprov_ac` - % Aprovechamiento acumulado
- `peso_huevo` - Peso huevo (gramos)
- `masa_huevo` - Masa huevo (gramos)
- `grasa_porcentaje` - % Grasa
- `nacim_porcentaje` - % Nacimiento
- `pollito_aa` - Pollitos ave alojada
- `gr_huevo_t` - Gramos/huevo total
- `gr_huevo_inc` - Gramos/huevo incubable
- `gr_pollito` - Gramos/pollito
- `valor_1000` - Valor 1000
- `valor_150` - Valor 150
- `apareo` - % Apareo

### Consumo Energético
- `kcal_ave_dia_h` - Kcal ave/día hembras
- `kcal_ave_dia_m` - Kcal ave/día machos
- `kcal_h` - Kcal hembras
- `kcal_m` - Kcal machos
- `prot_h` - Proteína hembras
- `prot_m` - Proteína machos
- `kcal_sem_h` - Kcal semanal hembras
- `prot_h_sem` - Proteína semanal hembras
- `kcal_sem_m` - Kcal semanal machos
- `prot_sem_m` - Proteína semanal machos

---

## 🔍 Comparativos Actuales en LEVANTE (Semanas 1-25)

### ✅ Campos que SÍ se Comparan

| Campo Guía Genética | Campo Real Calculado | Diferencia % | Estado |
|---------------------|----------------------|---------------|--------|
| `mort_sem_h` + `mort_sem_m` (promedio) | `mortalidadSem` | ❌ No se calcula diferencia % | ⚠️ Parcial |
| `cons_ac_h` + `cons_ac_m` (promedio g/ave/día) | `consumoDiario` (g/ave/día) | ❌ No se calcula diferencia % | ⚠️ Parcial |
| `peso_h` + `peso_m` (promedio) | `pesoCierre` | `difPesoPct` | ✅ Completo |
| `uniformidad` | `unifReal` | ❌ No se calcula diferencia % | ⚠️ Parcial |
| `gr_ave_dia_h` + `gr_ave_dia_m` (promedio) | `gananciaSemana` | ❌ No se calcula diferencia % | ⚠️ Parcial |

### 📊 Indicadores Calculados (sin comparación con guía)

- **Conversión Alimenticia (FCR)**: `consumoTotalPorAve / gananciaSemana`
- **Eficiencia**: `gananciaSemana / consumoTotalPorAve`
- **IP (Índice de Productividad)**: `eficiencia * supervivencia`
- **VPI (Índice de Vitalidad)**: `supervivencia * eficiencia`
- **Piso Térmico**: Se valida contra guía genética (sí/no)

### ❌ Campos de Guía Genética NO Utilizados en Levante

1. **Retiro Acumulado**: `retiro_ac_h`, `retiro_ac_m` - No se compara
2. **Consumo Energético**: `kcal_ave_dia_h`, `kcal_ave_dia_m`, `kcal_h`, `kcal_m`, `prot_h`, `prot_m` - No se compara
3. **Ganancia Diaria Específica**: `gr_ave_dia_h`, `gr_ave_dia_m` - No se compara directamente (solo se calcula ganancia semanal)
4. **Peso M/H**: `peso_mh` - No se compara

---

## 🔍 Comparativos Actuales en PRODUCCIÓN (Semanas 26+)

### ✅ Campos que SÍ se Comparan

| Campo Guía Genética | Campo Real Calculado | Diferencia % | Estado |
|---------------------|----------------------|---------------|--------|
| `mort_sem_h` | `mortalidadHembras` | `diferenciaMortalidadHembras` | ✅ Completo |
| `mort_sem_m` | `mortalidadMachos` | `diferenciaMortalidadMachos` | ✅ Completo |
| `cons_ac_h` (g/ave/día) | `consumoRealH` (g/ave/día) | `diferenciaConsumoHembras` | ✅ Completo |
| `cons_ac_m` (g/ave/día) | `consumoRealM` (g/ave/día) | `diferenciaConsumoMachos` | ✅ Completo |
| `peso_h` (kg) | `pesoPromedioHembras` (kg) | `diferenciaPesoHembras` | ✅ Completo |
| `peso_m` (kg) | `pesoPromedioMachos` (kg) | `diferenciaPesoMachos` | ✅ Completo |
| `uniformidad` | `uniformidadPromedio` | `diferenciaUniformidad` | ✅ Completo |
| `h_total_aa` | `promedioHuevosPorDia` | `diferenciaHuevosTotales` | ✅ Completo |
| `h_inc_aa` | `huevosIncubables / dias` | `diferenciaHuevosIncubables` | ✅ Completo |
| `prod_porcentaje` | `eficienciaProduccion` | `diferenciaPorcentajeProduccion` | ✅ Completo |
| `peso_huevo` | `pesoHuevoPromedio` | `diferenciaPesoHuevo` | ✅ Completo |

### 📊 Indicadores Calculados (sin comparación con guía)

- **Eficiencia de Producción**: `(huevosTotales / avesHembras) * 100`
- **Coeficiente de Variación (CV)**: Se calcula pero no se compara
- **Clasificadora de Huevos**: Se registra pero no se compara (no hay campos en guía genética)

### ❌ Campos de Guía Genética NO Utilizados en Producción

1. **Aprovechamiento**: `aprov_sem`, `aprov_ac` - No se compara
2. **Masa Huevo**: `masa_huevo` - No se compara
3. **Grasa Corporal**: `grasa_porcentaje` - No se compara (no se registra en seguimiento)
4. **Nacimientos**: `nacim_porcentaje` - No se compara (no se registra en seguimiento)
5. **Pollitos**: `pollito_aa` - No se compara (no se registra en seguimiento)
6. **Gramos/Huevo**: `gr_huevo_t`, `gr_huevo_inc` - No se compara
7. **Gramos/Pollito**: `gr_pollito` - No se compara
8. **Valores Comerciales**: `valor_1000`, `valor_150` - No se compara
9. **Apareo**: `apareo` - No se compara
10. **Consumo Energético**: `kcal_ave_dia_h`, `kcal_ave_dia_m`, `kcal_h`, `kcal_m`, `prot_h`, `prot_m` - No se compara
11. **Retiro Acumulado**: `retiro_ac_h`, `retiro_ac_m` - No se compara (en producción se usa "Selección")

---

## 📈 Recomendaciones de Mejora

### 🎯 Prioridad ALTA

#### 1. **Completar Comparativos en Levante**
- ✅ Agregar cálculo de diferencia % para:
  - Mortalidad (hembras y machos por separado)
  - Consumo (hembras y machos por separado)
  - Uniformidad
  - Ganancia diaria vs `gr_ave_dia_h` y `gr_ave_dia_m`

#### 2. **Agregar Comparativos de Consumo Energético**
- Comparar `kcal_ave_dia_h` y `kcal_ave_dia_m` de la guía con consumo energético calculado
- Comparar `prot_h` y `prot_m` de la guía con proteína calculada
- **Nota**: Requiere calcular kcal y proteína desde el consumo de alimento y tipo de alimento

#### 3. **Agregar Comparativos de Aprovechamiento en Producción**
- Comparar `aprov_sem` con aprovechamiento semanal calculado
- Comparar `aprov_ac` con aprovechamiento acumulado

### 🎯 Prioridad MEDIA

#### 4. **Agregar Comparativos de Masa Huevo**
- Comparar `masa_huevo` con masa calculada (peso huevo * cantidad huevos)

#### 5. **Agregar Comparativos de Gramos/Huevo**
- Comparar `gr_huevo_t` con gramos/huevo total calculado
- Comparar `gr_huevo_inc` con gramos/huevo incubable calculado

#### 6. **Agregar Comparativos de Retiro/Selección**
- En Levante: Comparar `retiro_ac_h` y `retiro_ac_m` con selección acumulada
- En Producción: Comparar selección con retiro de la guía (si aplica)

### 🎯 Prioridad BAJA

#### 7. **Agregar Campos Adicionales al Seguimiento**
- **Grasa Corporal**: Para comparar con `grasa_porcentaje`
- **Nacimientos**: Para comparar con `nacim_porcentaje`
- **Pollitos**: Para comparar con `pollito_aa`
- **Gramos/Pollito**: Para comparar con `gr_pollito`
- **Apareo**: Para comparar con `apareo`

#### 8. **Agregar Comparativos de Valores Comerciales**
- Comparar `valor_1000` y `valor_150` con valores calculados
- **Nota**: Requiere fórmulas específicas de cálculo

---

## 🔧 Implementación Técnica Sugerida

### Backend

#### 1. Extender `IndicadorSemanal` (Levante)
```csharp
// Agregar campos de diferencia %
public decimal? DiferenciaMortalidadHembras { get; set; }
public decimal? DiferenciaMortalidadMachos { get; set; }
public decimal? DiferenciaConsumoHembras { get; set; }
public decimal? DiferenciaConsumoMachos { get; set; }
public decimal? DiferenciaUniformidad { get; set; }
public decimal? DiferenciaGananciaHembras { get; set; }
public decimal? DiferenciaGananciaMachos { get; set; }
```

#### 2. Extender `IndicadorProduccionSemanalDto` (Producción)
```csharp
// Agregar campos de aprovechamiento
public decimal? AprovechamientoSemanal { get; set; }
public decimal? AprovechamientoAcumulado { get; set; }
public decimal? AprovechamientoSemanalGuia { get; set; }
public decimal? AprovechamientoAcumuladoGuia { get; set; }
public decimal? DiferenciaAprovechamientoSemanal { get; set; }
public decimal? DiferenciaAprovechamientoAcumulado { get; set; }

// Agregar campos de masa huevo
public decimal? MasaHuevoPromedio { get; set; }
public decimal? MasaHuevoGuia { get; set; }
public decimal? DiferenciaMasaHuevo { get; set; }
```

#### 3. Agregar Servicio de Cálculo de Consumo Energético
```csharp
public class ConsumoEnergeticoService
{
    public (decimal Kcal, decimal Proteina) CalcularConsumoEnergetico(
        decimal consumoKg, 
        int tipoAlimentoId)
    {
        // Obtener kcal/kg y proteína % del catálogo de alimentos
        // Calcular: kcal = consumoKg * kcalPorKg
        // Calcular: proteina = consumoKg * (proteinaPorcentaje / 100)
    }
}
```

### Frontend

#### 1. Actualizar Tabla de Indicadores de Levante
- Agregar columnas de diferencia % para mortalidad, consumo, uniformidad
- Agregar colores/iconos según nivel de desviación:
  - Verde: ≤ 5%
  - Amarillo: 5-15%
  - Rojo: > 15%

#### 2. Actualizar Tabla de Indicadores de Producción
- Agregar columnas de aprovechamiento (semanal y acumulado)
- Agregar columna de masa huevo
- Agregar sección de consumo energético (kcal y proteína)

---

## 📝 Notas Importantes

1. **Conversión de Unidades**: La guía genética almacena pesos en gramos, pero algunos cálculos usan kg. Asegurar conversiones consistentes.

2. **Cálculo de Consumo Diario**: 
   - En Levante: `consumoTotalGramos / (avesPromedio * diasConRegistro)`
   - En Producción: `(consumoKg * 1000) / (diasConRegistro * avesInicioSemana)`

3. **Diferencia Porcentual**: Fórmula estándar:
   ```csharp
   diferencia = ((valorReal - valorGuia) / valorGuia) * 100
   ```

4. **Campos Opcionales**: Muchos campos de la guía genética pueden ser `null`, por lo que las comparaciones deben ser condicionales.

5. **Rangos de Semanas**:
   - **Levante**: Semanas 1-25
   - **Producción**: Semanas 26+

---

## ✅ Checklist de Implementación

### Fase 1: Completar Comparativos Básicos en Levante
- [ ] Agregar diferencia % de mortalidad (H y M)
- [ ] Agregar diferencia % de consumo (H y M)
- [ ] Agregar diferencia % de uniformidad
- [ ] Agregar diferencia % de ganancia diaria (H y M)
- [ ] Actualizar frontend para mostrar diferencias

### Fase 2: Agregar Comparativos de Aprovechamiento en Producción
- [ ] Calcular aprovechamiento semanal y acumulado
- [ ] Comparar con `aprov_sem` y `aprov_ac`
- [ ] Agregar columnas en tabla de indicadores

### Fase 3: Agregar Comparativos de Consumo Energético
- [ ] Crear servicio de cálculo de kcal y proteína
- [ ] Integrar con catálogo de alimentos
- [ ] Agregar comparativos en Levante y Producción
- [ ] Actualizar frontend

### Fase 4: Agregar Comparativos Adicionales
- [ ] Masa huevo
- [ ] Gramos/huevo
- [ ] Retiro/Selección acumulado

---

**Fecha de Análisis**: 2026-01-20  
**Versión del Documento**: 1.0
