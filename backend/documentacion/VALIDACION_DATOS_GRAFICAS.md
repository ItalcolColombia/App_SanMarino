# Validación de Datos para Gráficas Avanzadas

## 📊 Datos Actualmente Disponibles en `indicadoresSemanales`

### ✅ Datos Disponibles
- `semana`: Número de semana
- `fechaInicio`: Fecha de inicio de semana
- `avesInicioSemana`: Aves al inicio
- `avesFinSemana`: Aves al final
- `consumoReal`: Consumo real por ave (gramos)
- `consumoTabla`: Consumo de guía por ave (gramos)
- `conversionAlimenticia`: Conversión alimenticia
- `mortalidadSem`: Mortalidad semanal (%)
- `seleccionSem`: Selección semanal (%)
- `pesoCierre`: Peso promedio (gramos)
- `pesoInicial`: Peso inicial
- `eficiencia`: Eficiencia
- `ip`: Índice de productividad
- `gananciaSemana`: Ganancia de peso semanal
- `gananciaDiariaAcumulada`: Ganancia diaria

## 🔍 Datos Necesarios para Gráficas Tipo Dashboard

### 1. Gráfico: Uniformidad y CV
**Necesario:**
- `uniformidadH`: Uniformidad hembras (porcentaje)
- `uniformidadM`: Uniformidad machos (porcentaje)
- `cvH`: Coeficiente de variación hembras (%)
- `cvM`: Coeficiente de variación machos (%)
- `uniformidadGuia`: Uniformidad según guía genética

**Cálculo requerido:**
- Uniformidad: Requiere datos de pesos individuales o desviación estándar
- CV: `CV = (Desviación Estándar / Media) × 100`

**Estado:** ⚠️ NO DISPONIBLE - Requiere datos de pesos individuales por ave

---

### 2. Gráfico: Diferencias de Consumo y Peso
**Necesario:**
- `difConsumoPorc`: Diferencia porcentual de consumo real vs guía
- `difPesoPorc`: Diferencia porcentual de peso real vs guía

**Cálculo:**
```typescript
difConsumoPorc = ((consumoReal - consumoTabla) / consumoTabla) * 100
difPesoPorc = ((pesoCierre - pesoGuia) / pesoGuia) * 100
```

**Estado:** ✅ PARCIALMENTE DISPONIBLE - Necesitamos peso de guía genética

---

### 3. Gráfico: Incrementos de Consumo
**Necesario:**
- `incrConsumoReal`: Incremento semanal de consumo real
- `incrConsumoGuia`: Incremento semanal de consumo según guía

**Cálculo:**
```typescript
incrConsumoReal = consumoReal[semana] - consumoReal[semana-1]
incrConsumoGuia = consumoTabla[semana] - consumoTabla[semana-1]
```

**Estado:** ✅ DISPONIBLE - Se puede calcular con datos actuales

---

### 4. Gráfico: Mortalidad y Retiros
**Necesario:**
- `mortalidadPorc`: Mortalidad porcentual (ya disponible como `mortalidadSem`)
- `retiroPorc`: Retiro porcentual (mortalidad + selección + error sexaje)
- `retiroGuia`: Retiro según guía genética

**Cálculo:**
```typescript
retiroPorc = mortalidadSem + seleccionSem + errorSexajePorc
```

**Estado:** ⚠️ PARCIALMENTE DISPONIBLE - Necesitamos error de sexaje en indicadores semanales

---

## 🎯 Plan de Implementación

### Fase 1: Datos Básicos Disponibles (Implementar Ahora)
1. ✅ Gráfica combinada de consumo real vs consumo tabla
2. ✅ Gráfica de incrementos de consumo
3. ✅ Gráfica de diferencias porcentuales (consumo y peso si tenemos peso guía)
4. ✅ Gráfica de mortalidad y selección combinadas

### Fase 2: Mejoras con Datos Adicionales
1. ⚠️ Agregar cálculo de diferencias de peso (requiere peso de guía genética)
2. ⚠️ Agregar retiros totales (requiere error de sexaje en seguimientos)
3. ⚠️ Implementar uniformidad y CV (requiere datos de pesos individuales)

### Fase 3: Escalas Duales
1. Implementar gráficas con dos ejes Y para métricas con diferentes escalas
2. Ejemplo: Uniformidad (40-100%) vs CV (0-7%)

---

## 📝 Notas de Implementación

### Escalas Duales en Chart.js
Para implementar escalas duales necesitamos:
```typescript
scales: {
  y: {
    type: 'linear',
    position: 'left',
    // Configuración para primera métrica
  },
  y1: {
    type: 'linear',
    position: 'right',
    grid: {
      drawOnChartArea: false, // Solo mostrar grid del eje izquierdo
    },
    // Configuración para segunda métrica
  }
}
```

Y en los datasets:
```typescript
{
  label: 'Serie 1',
  yAxisID: 'y', // Usa eje izquierdo
  data: [...]
},
{
  label: 'Serie 2',
  yAxisID: 'y1', // Usa eje derecho
  data: [...]
}
```

### Combinación de Barras y Líneas
En Chart.js, todos los datasets pueden ser de diferentes tipos:
```typescript
datasets: [
  {
    type: 'bar', // Barras
    label: 'Barras',
    data: [...]
  },
  {
    type: 'line', // Líneas
    label: 'Líneas',
    data: [...]
  }
]
```




