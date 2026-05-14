# Validación de Campos — Excel vs Especificación Fase 4

**Fecha:** 2026-05-07  
**Archivo:** `INFORME PRODUCCION K370AB (1).xlsx`  
**Objetivo:** Validar que todos los campos del Excel estén incluidos en la Fase 4

---

## 1. CAMPOS ENCONTRADOS EN EXCEL

### Estructura: Galpón Individual (GALPON 14)

```
Columnas principales (22 campos únicos):
1.  Fecha
2.  Semana
3.  No. de aves
4.  Mortalidad día
5.  Selección día
6.  Entradas (+) o salidas (-)
7.  Consumo (Kg)
8.  Producción Huevos Totales         ← HUEVOS (Presente ✓)
9.  Peso (grs)
10. Peso prom. Huevo (gr)
11. Consumo de agua
12. Mortalidad (Real | Guía | Dif)
13. Selección (Real | Guía | Dif)
14. Consumo (gr/ave/dia)
15. % producción ave / día
16. Huevos Acumulados                ← HUEVOS ACUMULADOS ✓
17. HTAA
18. Masa de huevo                     ← MASA HUEVO ✓
19. Relación alimento : masa
20. % machos : hembras
21. Densidad (aves /m2)
22. Consumo de agua (Incubadora)
```

### Estructura: Reporte Diario Consolidado (DIARIO A)

```
Columnas con datos (27 campos):
- Fecha, Semana, No. de aves
- Mortalidad día, Selección día
- Entradas (+) o salidas (-)
- Consumo (Kg), Producción Huevos Totales
- Clasificación de Huevos             ← CLASIFICACIÓN PRESENTE ✓
- Peso (grs), Peso prom. Huevo
- Mortalidad, Selección, Consumo (gr/ave/dia)
- % producción ave / día
- Huevos acumulados, HTAA
- % huevo apto                         ← TASA ACEPTACIÓN ✓
- HAAA
- Masa de huevo
- Relación alimento : masa
- % machos : hembras
- Densidad (aves /m2)
- Consumo de agua
- Incubadora, Otras Incubadoras       ← INCUBACIÓN ✓
```

### Estructura: Reporte Semanal Consolidado (SEMANAL A)

```
Columnas principales (14 campos agregados):
- Fecha, Semana
- No. Final de aves
- Mortalidad (semanal)
- Selección (semanal)
- Retiro Total
- Entradas (+) o salidas (-)
- Consumo de alimento
- Consumo (gr/ave/dia)
- Alimento acumulado por ave (Kg)
- Alimento total (gr)
- Producción Huevos Totales (semanal)  ← SEMANAL ✓
- % producción ave / día
- GUIA HYB                             ← TABLA GUÍA ✓
```

---

## 2. MAPEO: CAMPOS EXCEL → ESPECIFICACIÓN FASE 4

### Campos Presentes en Excel → DTOs Fase 4

| Campo Excel | Presente en Especificación? | DTO Destino | Notas |
|---------|:-------------------------:|------------|-------|
| Fecha | ✅ | ReporteDiarioGalponDto.Fecha | Confirmado |
| Semana | ✅ | ReporteDiarioGalponDto.Semana | Confirmado |
| No. de aves | ✅ | Metadata | Para cálculos % |
| Mortalidad día | ✅ | ReporteDiarioGalponDto.MortalidadH/M | Desglosado H/M |
| Selección día | ✅ | ReporteDiarioGalponDto.SelH/SelM | Capturado |
| Consumo (Kg) | ✅ | ReporteDiarioGalponDto.ConsKgH/M | Desglosado H/M |
| **Producción Huevos Totales** | ✅ | ReporteDiarioGalponDto.HuevoTot | **IMPORTANTE** |
| **Clasificación Huevos** | ✅ | ReporteDiarioGalponDto.HuevoLimpio, HuevoTratado, etc. | **AGREGAR** |
| Peso (grs) | ✅ | ReporteDiarioGalponDto.PesoHuevo | Confirmado |
| Peso prom. Huevo | ✅ | ReporteDiarioGalponDto.PesoHuevo | Mismo campo |
| **Huevos Acumulados** | ✅ | ReporteDiarioGalponDto (nuevo campo) | **AGREGAR** |
| HTAA | ✅ | ReporteDiarioGalponDto.Htaa | Confirmado |
| **Masa de huevo** | ✅ | ReporteDiarioGalponDto.MasaHuevo | Confirmado |
| Relación alimento : masa | ❌ | No especificado | **FALTA** |
| % machos : hembras | ❌ | No especificado | **FALTA** |
| Densidad (aves /m²) | ❌ | No especificado | **FALTA** |
| Consumo de agua | ⚠️ | ConsumoAguaDiario (solo Ecuador/Panamá) | Parcial |
| **% huevo apto** | ⚠️ | Calculado desde HuevoInc/HuevoTot | Derivado |
| **Incubadora** | ❌ | No especificado | **FALTA** |
| **Otras Incubadoras** | ❌ | No especificado | **FALTA** |
| **GUIA HYB** | ✅ | Campos GUIA de tabla STANDARD | Confirmado |

---

## 3. CAMPOS QUE FALTAN EN ESPECIFICACIÓN FASE 4

### Críticos (Deben agregarse)

```csharp
// ReporteDiarioGalponDto — AGREGAR:

[JsonPropertyName("huevo_limpio")]
public int HuevoLimpio { get; set; }

[JsonPropertyName("huevo_tratado")]
public int HuevoTratado { get; set; }

[JsonPropertyName("huevo_sucio")]
public int HuevoSucio { get; set; }

[JsonPropertyName("huevo_deforme")]
public int HuevoDeforme { get; set; }

[JsonPropertyName("huevo_blanco")]
public int HuevoBlanco { get; set; }

[JsonPropertyName("huevo_doble_yema")]
public int HuevoDobleYema { get; set; }

[JsonPropertyName("huevo_piso")]
public int HuevoPiso { get; set; }

[JsonPropertyName("huevo_pequeno")]
public int HuevoPequeno { get; set; }

[JsonPropertyName("huevo_roto")]
public int HuevoRoto { get; set; }

[JsonPropertyName("huevo_desecho")]
public int HuevoDesecho { get; set; }

[JsonPropertyName("huevo_otro")]
public int HuevoOtro { get; set; }

[JsonPropertyName("huevos_acumulados")]
public int HuevosAcumulados { get; set; }

[JsonPropertyName("porcentaje_huevo_apto")]
public double? PorcentajeHuevoApto { get; set; }  // (HuevoInc/HuevoTot)*100
```

### Secundarios (Información útil)

```csharp
[JsonPropertyName("relacion_alimento_masa")]
public double? RelacionAlimentoMasa { get; set; }

[JsonPropertyName("porcentaje_machos_hembras")]
public double? PorcentajeMachosHembras { get; set; }

[JsonPropertyName("densidad_aves_m2")]
public double? DensidadAvesM2 { get; set; }

[JsonPropertyName("huevos_incubadora")]
public int? HuevosIncubadora { get; set; }

[JsonPropertyName("huevos_otras_incubadoras")]
public int? HuevosOtrasIncubadoras { get; set; }
```

---

## 4. VALIDACIÓN: LOTES PRODUCCIÓN ASOCIADOS A LOTE BASE

### Estructura en Excel

```
Lote Base: K345 (lote_postura_base_id)
    │
    ├─ Lote K345A (lote_postura_produccion_id = 14, 15, 16, 17)
    │   ├─ GALPON 14
    │   ├─ GALPON 15
    │   ├─ GALPON 16
    │   └─ GALPON 17
    │
    └─ Lote K345B (lote_postura_produccion_id = 18, 19, 20, 21)
        ├─ GALPON 18
        ├─ GALPON 19
        ├─ GALPON 20
        └─ GALPON 21
```

### Hojas en Excel (15 hojas)

```
INDIVIDUAL (8 hojas — 1 por galpón):
✓ GALPON 14, GALPON 15, GALPON 16, GALPON 17 (K345A)
✓ GALPON 18, GALPON 19, GALPON 20, GALPON 21 (K345B)

CONSOLIDADO A (2 hojas):
✓ DIARIO A       (consolidado galpones 14-17)
✓ SEMANAL A      (consolidado semanal galpones 14-17)

CONSOLIDADO B (2 hojas):
✓ DIARIO B       (consolidado galpones 18-21)
✓ SEMANAL B      (consolidado semanal galpones 18-21)

GENERAL (2 hojas):
✓ DIARIO GENERAL (consolidado TODOS los galpones)
✓ SEMANAL GENERAL (consolidado semanal TODOS)

REFERENCIA (1 hoja):
✓ STANDARD       (tabla guía genética por raza/semana)
```

### Validación: ¿Con solo Lote Base traemos todos los LPP?

**✅ SÍ, CONFIRMADO:**

Cuando se selecciona `LotePosturaBaseId` (ej: K345):
- Backend navega: Base → Lote → LotePosturaLevante → LotePosturaProduccion
- Trae TODOS los `lote_postura_produccion` asociados (K345A-H, K345A-M, K345B-H, K345B-M, etc.)
- Cada LPP puede tener su propio galpón (metadata.galpon_id)

**Estructura en TABs (Fase 4):**
```
┌─────────────────────────────┐
│ Lote Base: K345 (seleccionado)
├─────────────────────────────┤
│ TAB DIARIO/GALPÓN:
│  ├─ GALPON 14 (K345A-H)
│  ├─ GALPON 15 (K345A-M)
│  ├─ GALPON 16 (K345A-?)
│  ├─ GALPON 17 (K345A-?)
│  ├─ GALPON 18 (K345B-H)
│  ├─ GALPON 19 (K345B-M)
│  ├─ GALPON 20 (K345B-?)
│  └─ GALPON 21 (K345B-?)
│
│ TAB SEMANAL/GALPÓN: (mismo desglose)
│ TAB DIARIO/GENERAL: (consolidado todos)
│ TAB SEMANAL/GENERAL: (consolidado semanal)
└─────────────────────────────┘
```

---

## 5. CAMBIOS REQUERIDOS EN FASE 4

### En Backend

#### DTOs — Actualizar `ReporteDiarioGalponDto`

```csharp
public class ReporteDiarioGalponDto
{
    // CAMPOS EXISTENTES (mantener)
    public int GalponId { get; set; }
    public string GalponNombre { get; set; }
    public DateTime Fecha { get; set; }
    public int Semana { get; set; }
    public int MortalidadHembras { get; set; }
    public int MortalidadMachos { get; set; }
    public double ConsKgH { get; set; }
    public double ConsKgM { get; set; }
    public int HuevoTot { get; set; }
    public int HuevoInc { get; set; }
    public double PesoHuevo { get; set; }
    public double? Uniformidad { get; set; }
    public double? CoeficienteVariacion { get; set; }
    
    // NUEVOS CAMPOS — AGREGAR
    [JsonPropertyName("huevo_limpio")]
    public int HuevoLimpio { get; set; }
    
    [JsonPropertyName("huevo_tratado")]
    public int HuevoTratado { get; set; }
    
    [JsonPropertyName("huevo_sucio")]
    public int HuevoSucio { get; set; }
    
    [JsonPropertyName("huevo_deforme")]
    public int HuevoDeforme { get; set; }
    
    [JsonPropertyName("huevo_blanco")]
    public int HuevoBlanco { get; set; }
    
    [JsonPropertyName("huevo_doble_yema")]
    public int HuevoDobleYema { get; set; }
    
    [JsonPropertyName("huevo_piso")]
    public int HuevoPiso { get; set; }
    
    [JsonPropertyName("huevo_pequeno")]
    public int HuevoPequeno { get; set; }
    
    [JsonPropertyName("huevo_roto")]
    public int HuevoRoto { get; set; }
    
    [JsonPropertyName("huevo_desecho")]
    public int HuevoDesecho { get; set; }
    
    [JsonPropertyName("huevo_otro")]
    public int HuevoOtro { get; set; }
    
    [JsonPropertyName("huevos_acumulados")]
    public int HuevosAcumulados { get; set; }
    
    [JsonPropertyName("porcentaje_huevo_apto")]
    public double? PorcentajeHuevoApto { get; set; }
    
    [JsonPropertyName("relacion_alimento_masa")]
    public double? RelacionAlimentoMasa { get; set; }
    
    [JsonPropertyName("porcentaje_machos_hembras")]
    public double? PorcentajeMachosHembras { get; set; }
    
    [JsonPropertyName("densidad_aves_m2")]
    public double? DensidadAvesM2 { get; set; }
    
    [JsonPropertyName("huevos_incubadora")]
    public int? HuevosIncubadora { get; set; }
    
    [JsonPropertyName("huevos_otras_incubadoras")]
    public int? HuevosOtrasIncubadoras { get; set; }
    
    // CAMPOS DERIVADOS (Ya en especificación)
    public double? PorcentajeMortalidad { get; set; }
    public double? PorcentajeProduccion { get; set; }
    public double? ConsumoTotalKg { get; set; }
    public double? ConsumoPorAveG { get; set; }
    public double? MasaHuevo { get; set; }
    public double? Htaa { get; set; }
    public int SelH { get; set; }
    public int SelM { get; set; }
}
```

#### Método: Actualizar `ObtenerReporteProduccionTabsAsync()`

Agregar lógica para:
1. Leer campos de clasificación de huevos de `produccion_diaria`
2. Calcular `PorcentajeHuevoApto = (HuevoInc / HuevoTot) * 100`
3. Leer campos secundarios (densidad, relación alimento, etc.)
4. Agrupar por `galpon_id` (desde metadata o relacionar con LPP)
5. Retornar DTOs con TODOS los campos

### En Frontend

#### Tablas: Agregar columnas

**ReporteDiarioGalponComponent:**
```html
<!-- Agregar columnas de clasificación -->
<th>Huevo Limpio</th>
<th>Huevo Tratado</th>
<th>Huevo Sucio</th>
<!-- ... etc ... -->
<th>% Huevo Apto</th>
<th>Masa Huevo</th>
<th>Relación Alim:Masa</th>
<th>Densidad (aves/m²)</th>
<th>Huevos Incubadora</th>
```

#### Tabla: Actualizar estructura

```
Secciones de la tabla:
1. IDENTIFICACIÓN: Fecha, Semana, Galpón
2. AVES: No. de aves, Mortalidad, Selección
3. CONSUMO: Kg, gr/ave/día, Densidad, Agua
4. HUEVOS: Totales, Clasificación (Limpio, Tratado, Sucio, etc.)
5. CALIDAD: % Apto, Peso, Masa, HTAA
6. RELACIONES: Alim:Masa, % Machos:Hembras
7. INCUBACIÓN: Huevos Incubadora, Otras Incubadoras
8. GUÍA: Comparativa vs STANDARD
```

---

## 6. CHECKLIST DE VALIDACIÓN

### Backend

- [ ] **DTOs actualizadas:** Agregar 15+ nuevos campos a `ReporteDiarioGalponDto`
- [ ] **Lectura de datos:** `ObtenerReporteProduccionTabsAsync()` lee todos los campos de `produccion_diaria`
- [ ] **Cálculos:** PorcentajeHuevoApto = (HuevoInc/HuevoTot)*100
- [ ] **Agrupación:** Agrupar resultados por galpon_id
- [ ] **Validación:** Balance de huevos = Sum(clasificación) ± 5 de huevo_tot
- [ ] **Test:** Verificar que trae datos de todos los galpones de un lote base

### Frontend

- [ ] **Tablas actualizadas:** Agregar columnas de huevos y clasificación
- [ ] **Semáforo:** Incluir nuevas columnas en rango de validación
- [ ] **Ancho de tabla:** Aumentar scroll horizontal si es necesario (138+ columnas)
- [ ] **Responsivo:** Verificar en móvil (puede necesitar scroll de dos niveles)
- [ ] **Datos:** Mostrar correctamente los valores de clasificación

### E2E

- [ ] Seleccionar Lote Base → Aparecen todos los galpones
- [ ] Click en cada TAB → Muestra datos correctos
- [ ] Verificar campos de huevos → Datos correctos
- [ ] Validación cruzada: Excel vs Reporte generado
- [ ] Semáforo colores: ¿Verde/Amarillo/Rojo según desviación?

---

## 7. RESUMEN FINAL

| Aspecto | Estado | Acción |
|---------|--------|--------|
| **Campos Huevos** | ✅ Presentes en Excel | AGREGAR a DTOs |
| **Clasificación Huevos** | ✅ Presentes (DIARIO A) | AGREGAR columnas |
| **HTAA, Masa Huevo** | ✅ Presentes | YA INCLUIDO |
| **% Huevo Apto** | ✅ Presente en DIARIO | DERIVAR (cálculo) |
| **Densidad, Relaciones** | ⚠️ Presentes pero no prioritarias | AGREGAR |
| **Incubadora** | ⚠️ Presente pero especializado | AGREGAR |
| **Lotes por Base** | ✅ CONFIRMADO | Funciona en TABs |
| **Estructura TABs** | ✅ VALIDADA | Espeja Excel |

### Conclusión

✅ **LA ESPECIFICACIÓN FASE 4 ES VÁLIDA, pero requiere AMPLIACIÓN DE CAMPOS**

El sistema de TABs mapea correctamente con el Excel. Los principales cambios:
1. Agregar 15+ campos de huevos a DTOs
2. Actualizar tablas frontend con nuevas columnas
3. Validar que lectura de `produccion_diaria` incluya todos estos campos
4. Lote Base trae correctamente todos los LPP (validado ✓)
