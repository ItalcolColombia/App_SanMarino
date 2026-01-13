# 📊 ANÁLISIS: DATOS DEL REGISTRO DIARIO DE LEVANTE

## 🎯 OBJETIVO
Verificar qué datos se están recogiendo actualmente en el módulo de registro diario de levante y compararlos con la lista completa de datos requeridos.

---

## 📋 DATOS GENERALES

### ✅ Datos que TENEMOS (desde el Lote):
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **IDLoteRAP** | ✅ Disponible | `Lote.LoteId` | ID numérico del lote |
| **Regional** | ✅ Disponible | `Lote.Regional` | Campo en la entidad Lote |
| **GRANJA** | ✅ Disponible | `Lote.GranjaId` → `Farm.name` | Relación con tabla Farm |
| **Lote** | ✅ Disponible | `Lote.LoteNombre` | Nombre del lote |
| **RAZA** | ✅ Disponible | `Lote.Raza` | Campo en la entidad Lote |
| **AñoG** | ✅ Disponible | `Lote.AnoTablaGenetica` | Año de la tabla genética |
| **NÚCLEOL** | ✅ Disponible | `Lote.NucleoId` → `Nucleo.nucleoNombre` | Relación con tabla Nucleo |
| **AÑON** | ⚠️ Parcial | `Lote.AnoTablaGenetica` | Podría ser el mismo que AñoG |
| **Edad** | ✅ Calculado | `FechaRegistro - Lote.FechaEncaset` | Se calcula en días |
| **Fecha** | ✅ Disponible | `SeguimientoLoteLevante.FechaRegistro` | Fecha del registro diario |
| **SemAño** | ❌ FALTA | - | Semana del año (1-52/53) |
| **TRASLADO** | ⚠️ Indirecto | `SelH`/`SelM` negativos | Se detecta cuando SelH/SelM son negativos |
| **Observaciones** | ✅ Disponible | `SeguimientoLoteLevante.Observaciones` | Campo de texto libre |

---

## 📦 INVENTARIO INICIAL

### ✅ Datos que TENEMOS (desde el Lote):
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **HEMBRAINI** | ✅ Disponible | `Lote.HembrasL` | Hembras iniciales del lote |
| **MACHOINI** | ✅ Disponible | `Lote.MachosL` | Machos iniciales del lote |

---

## 🐔 HEMBRAS – PRODUCCIÓN Y MANEJO

### ✅ Datos que TENEMOS:
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **Hembra** | ✅ Calculado | Saldo de hembras vivas | Se calcula en el servicio |
| **MortH** | ✅ Disponible | `SeguimientoLoteLevante.MortalidadHembras` | Mortalidad diaria hembras |
| **SelH** | ✅ Disponible | `SeguimientoLoteLevante.SelH` | Selección/retiro hembras (puede ser negativo) |
| **ErrorH** | ✅ Disponible | `SeguimientoLoteLevante.ErrorSexajeHembras` | Errores de sexaje hembras |
| **ConsKgH** | ✅ Disponible | `SeguimientoLoteLevante.ConsumoKgHembras` | Consumo en kg hembras |
| **PesoH** | ✅ Disponible | `SeguimientoLoteLevante.PesoPromH` | Peso promedio hembras (nullable) |
| **UniformH** | ✅ Disponible | `SeguimientoLoteLevante.UniformidadH` | Uniformidad hembras (nullable) |
| **%CVH** | ✅ Disponible | `SeguimientoLoteLevante.CvH` | Coeficiente de variación hembras (nullable) |

---

## 🥗 HEMBRAS – NUTRICIÓN

### ✅ Datos que TENEMOS:
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **KcalAlH** | ✅ Disponible | `SeguimientoLoteLevante.KcalAlH` | Kcal por kg de alimento hembras (nullable) |
| **%ProtAlH** | ✅ Disponible | `SeguimientoLoteLevante.ProtAlH` | % Proteína por kg alimento hembras (nullable) |
| **KcalAveH** | ✅ Disponible | `SeguimientoLoteLevante.KcalAveH` | Kcal por ave por día hembras (nullable) |
| **ProtAveH** | ✅ Disponible | `SeguimientoLoteLevante.ProtAveH` | Proteína por ave por día hembras (nullable) |

**⚠️ NOTA:** Estos campos están en la entidad pero **NO se están capturando en el formulario** del frontend. Solo se pueden establecer manualmente o calcular en el backend.

---

## 📈 HEMBRAS – INDICADORES Y ACUMULADOS

### ⚠️ Datos que se CALCULAN (no se capturan directamente):
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **%MortH** | ✅ Calculado | `(MortH / Hembra) * 100` | Porcentaje de mortalidad |
| **%MortHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **DifMortH** | ✅ Calculado | `%MortH - %MortHGUIA` | Diferencia con guía |
| **ACMortH** | ✅ Calculado | Acumulado de mortalidad | Suma acumulada |
| **%SelH** | ✅ Calculado | `(SelH / Hembra) * 100` | Porcentaje de selección |
| **ACSelH** | ✅ Calculado | Acumulado de selección | Suma acumulada |
| **%ErrH** | ✅ Calculado | `(ErrorH / Hembra) * 100` | Porcentaje de error |
| **ACErrH** | ✅ Calculado | Acumulado de error | Suma acumulada |
| **M+S+EH** | ✅ Calculado | `MortH + SelH + ErrorH` | Total retiradas |
| **RetAcH** | ✅ Calculado | Retiro acumulado hembras | Suma acumulada |
| **%RetiroH** | ✅ Calculado | `(RetAcH / HEMBRAINI) * 100` | % Retiro acumulado |
| **RetiroHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **AcConsH** | ✅ Calculado | Consumo acumulado hembras | Suma acumulada |
| **ConsAcGrH** | ✅ Calculado | Consumo acumulado en gramos | `AcConsH * 1000` |
| **ConsAcGrHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **GrAveDiaH** | ✅ Calculado | Gramos por ave por día | `ConsAcGrH / (Hembra * Días)` |
| **GrAveDiaGUIAH** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **IncrConsH** | ⚠️ Parcial | Incremento de consumo | Diferencia diaria |
| **IncrConsHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **%DifConsH** | ✅ Calculado | `(ConsAcGrH - ConsAcGrHGUIA) / ConsAcGrHGUIA * 100` | % Diferencia consumo |
| **PesoHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **%DifPesoH** | ✅ Calculado | `(PesoH - PesoHGUIA) / PesoHGUIA * 100` | % Diferencia peso |
| **UnifHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |

**📌 NOTA:** Estos indicadores se calculan en el servicio `SeguimientoLoteLevanteService` y se devuelven en `ResultadoLevanteResponse`.

---

## 🐓 MACHOS – PRODUCCIÓN Y MANEJO

### ✅ Datos que TENEMOS:
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **SaldoMacho** | ✅ Calculado | Saldo de machos vivos | Se calcula en el servicio |
| **MortM** | ✅ Disponible | `SeguimientoLoteLevante.MortalidadMachos` | Mortalidad diaria machos |
| **SelM** | ✅ Disponible | `SeguimientoLoteLevante.SelM` | Selección/retiro machos (puede ser negativo) |
| **ErrorM** | ✅ Disponible | `SeguimientoLoteLevante.ErrorSexajeMachos` | Errores de sexaje machos |
| **ConsKgM** | ✅ Disponible | `SeguimientoLoteLevante.ConsumoKgMachos` | Consumo en kg machos (nullable) |
| **PesoM** | ✅ Disponible | `SeguimientoLoteLevante.PesoPromM` | Peso promedio machos (nullable) |
| **UniformM** | ✅ Disponible | `SeguimientoLoteLevante.UniformidadM` | Uniformidad machos (nullable) |
| **%CVM** | ✅ Disponible | `SeguimientoLoteLevante.CvM` | Coeficiente de variación machos (nullable) |

**⚠️ NOTA:** `ConsumoKgMachos` está en la entidad pero **NO se está capturando en el formulario** del frontend. Solo se captura `ConsumoKgHembras`.

---

## 🥗 MACHOS – NUTRICIÓN

### ❌ Datos que FALTAN:
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **KcalAlM** | ❌ FALTA | - | Kcal por kg de alimento machos |
| **%ProtAlM** | ❌ FALTA | - | % Proteína por kg alimento machos |
| **KcalAveM** | ❌ FALTA | - | Kcal por ave por día machos |
| **ProtAveM** | ❌ FALTA | - | Proteína por ave por día machos |

**📌 NOTA:** Estos campos no existen en la entidad `SeguimientoLoteLevante`. Solo existen para hembras.

---

## 📈 MACHOS – INDICADORES Y ACUMULADOS

### ⚠️ Datos que se CALCULAN (similar a hembras):
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **%MortM** | ✅ Calculado | `(MortM / SaldoMacho) * 100` | Porcentaje de mortalidad |
| **%MortMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **DifMortM** | ✅ Calculado | `%MortM - %MortMGUIA` | Diferencia con guía |
| **ACMortM** | ✅ Calculado | Acumulado de mortalidad | Suma acumulada |
| **%SelM** | ✅ Calculado | `(SelM / SaldoMacho) * 100` | Porcentaje de selección |
| **ACSelM** | ✅ Calculado | Acumulado de selección | Suma acumulada |
| **%ErrM** | ✅ Calculado | `(ErrorM / SaldoMacho) * 100` | Porcentaje de error |
| **ACErrM** | ✅ Calculado | Acumulado de error | Suma acumulada |
| **M+S+EM** | ✅ Calculado | `MortM + SelM + ErrorM` | Total retiradas |
| **RetAcM** | ✅ Calculado | Retiro acumulado machos | Suma acumulada |
| **%RetAcM** | ✅ Calculado | `(RetAcM / MACHOINI) * 100` | % Retiro acumulado |
| **RetiroMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **AcConsM** | ✅ Calculado | Consumo acumulado machos | Suma acumulada |
| **ConsAcGrM** | ✅ Calculado | Consumo acumulado en gramos | `AcConsM * 1000` |
| **ConsAcGrMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **GrAveDiaM** | ✅ Calculado | Gramos por ave por día | `ConsAcGrM / (SaldoMacho * Días)` |
| **GrAveDiaMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **IncrConsM** | ⚠️ Parcial | Incremento de consumo | Diferencia diaria |
| **IncrConsMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **DifConsM** | ✅ Calculado | `ConsAcGrM - ConsAcGrMGUIA` | Diferencia consumo |
| **PesoMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **%DifPesoM** | ✅ Calculado | `(PesoM - PesoMGUIA) / PesoMGUIA * 100` | % Diferencia peso |
| **UnifMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |

---

## 🔄 COMPARATIVOS Y ERRORES DE SEXAJE

### ✅ Datos que se CALCULAN:
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **%RelM/H** | ✅ Calculado | `(SaldoMacho / Hembra) * 100` | Relación machos/hembras |
| **ErrSexAcH** | ✅ Calculado | Acumulado de errores hembras | Suma acumulada |
| **%ErrSxAcH** | ✅ Calculado | `(ErrSexAcH / HEMBRAINI) * 100` | % Error acumulado hembras |
| **ErrSexAcM** | ✅ Calculado | Acumulado de errores machos | Suma acumulada |
| **%ErrSxAcM** | ✅ Calculado | `(ErrSexAcM / MACHOINI) * 100` | % Error acumulado machos |
| **DifConsAcH** | ✅ Calculado | `ConsAcGrH - ConsAcGrHGUIA` | Diferencia consumo acumulado hembras |
| **DifConsAcM** | ✅ Calculado | `ConsAcGrM - ConsAcGrMGUIA` | Diferencia consumo acumulado machos |

---

## 📅 NUTRICIÓN SEMANAL – HEMBRAS

### ⚠️ Datos que se CALCULAN (agrupados por semana):
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **ALIMHGUÍA** | ✅ Calculado | Desde guía genética | Alimento guía hembras |
| **KcalSemH** | ⚠️ Parcial | Suma semanal de KcalAveH | Kcal semanal hembras |
| **KcalSemAcH** | ⚠️ Parcial | Acumulado semanal | Kcal semanal acumulado hembras |
| **KcalSemHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **KcalSemAcHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **ProtSemH** | ⚠️ Parcial | Suma semanal de ProtAveH | Proteína semanal hembras |
| **ProtSemAcH** | ⚠️ Parcial | Acumulado semanal | Proteína semanal acumulado hembras |
| **ProtSemHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **ProtSemAcHGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |

**📌 NOTA:** Estos cálculos semanales requieren agrupar los datos diarios por semana.

---

## 📅 NUTRICIÓN SEMANAL – MACHOS

### ❌ Datos que FALTAN (similar a hembras):
| Campo Requerido | Estado | Fuente | Notas |
|----------------|--------|--------|-------|
| **ALIMMGUÍA** | ✅ Calculado | Desde guía genética | Alimento guía machos |
| **KcalSemM** | ❌ FALTA | - | Kcal semanal machos (no hay KcalAveM) |
| **KcalSemAcM** | ❌ FALTA | - | Kcal semanal acumulado machos |
| **KcalSemMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **KcalSemAcMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **ProtSemM** | ❌ FALTA | - | Proteína semanal machos (no hay ProtAveM) |
| **ProtSemAcM** | ❌ FALTA | - | Proteína semanal acumulado machos |
| **ProtSemMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |
| **ProtSemAcMGUIA** | ✅ Calculado | Desde guía genética | Comparación con guía |

---

## 📝 RESUMEN DE ESTADO

### ✅ DATOS CAPTURADOS EN EL FORMULARIO:
1. ✅ Fecha de registro
2. ✅ Lote (selección)
3. ✅ Mortalidad hembras
4. ✅ Mortalidad machos
5. ✅ Selección hembras (SelH)
6. ✅ Selección machos (SelM)
7. ✅ Error sexaje hembras
8. ✅ Error sexaje machos
9. ✅ Tipo de alimento (texto libre)
10. ✅ Consumo kg hembras
11. ✅ Observaciones
12. ✅ Ciclo (solo "Normal")

### ⚠️ DATOS EN LA ENTIDAD PERO NO EN EL FORMULARIO:
1. ⚠️ Consumo kg machos (`ConsumoKgMachos`)
2. ⚠️ Peso promedio hembras (`PesoPromH`)
3. ⚠️ Peso promedio machos (`PesoPromM`)
4. ⚠️ Uniformidad hembras (`UniformidadH`)
5. ⚠️ Uniformidad machos (`UniformidadM`)
6. ⚠️ Coeficiente de variación hembras (`CvH`)
7. ⚠️ Coeficiente de variación machos (`CvM`)
8. ⚠️ KcalAlH, ProtAlH, KcalAveH, ProtAveH (nutrición hembras)

### ❌ DATOS QUE FALTAN COMPLETAMENTE:
1. ❌ SemAño (semana del año)
2. ❌ KcalAlM, %ProtAlM, KcalAveM, ProtAveM (nutrición machos)
3. ❌ Campos de nutrición semanal (requieren cálculo/agrupación)

### ✅ DATOS CALCULADOS (en el servicio):
- Todos los indicadores y acumulados se calculan correctamente en `SeguimientoLoteLevanteService`
- Las comparaciones con guía genética se realizan cuando hay datos disponibles

---

## 🔧 RECOMENDACIONES

### 1. **Agregar campos faltantes al formulario:**
   - Consumo kg machos
   - Peso promedio hembras y machos
   - Uniformidad hembras y machos
   - Coeficiente de variación hembras y machos

### 2. **Agregar campos de nutrición machos:**
   - KcalAlM, %ProtAlM, KcalAveM, ProtAveM

### 3. **Calcular automáticamente:**
   - SemAño (semana del año) desde la fecha
   - Campos de nutrición desde el catálogo de alimentos (si están disponibles)

### 4. **Mejorar captura de datos:**
   - Permitir seleccionar alimento desde catálogo (similar a producción)
   - Calcular automáticamente valores nutricionales desde el alimento seleccionado

---

## 📂 ARCHIVOS RELEVANTES

### Backend:
- `backend/src/ZooSanMarino.Domain/Entities/SeguimientoLoteLevante.cs`
- `backend/src/ZooSanMarino.Application/DTOs/SeguimientoLoteLevanteDto.cs`
- `backend/src/ZooSanMarino.Application/DTOs/CreateSeguimientoLoteLevanteRequest.cs`
- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoLoteLevanteService.cs`

### Frontend:
- `frontend/src/app/features/lote-levante/pages/seguimiento-lote-form/seguimiento-lote-levante-form.component.ts`
- `frontend/src/app/features/lote-levante/pages/seguimiento-lote-form/seguimiento-lote-levante-form.component.html`
- `frontend/src/app/features/lote-levante/seguimiento-calculos/seguimiento-calculos.component.ts`

