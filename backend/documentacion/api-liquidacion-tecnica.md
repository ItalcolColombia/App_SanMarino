# 📡 API Reference - Liquidación Técnica

## 🎯 Descripción General

La API de Liquidación Técnica proporciona endpoints para calcular automáticamente todas las métricas necesarias para el cierre o liquidación técnica de lotes de levante. Todos los endpoints requieren autenticación JWT y filtran automáticamente por CompanyId.

## 🔐 Autenticación

Todos los endpoints requieren un token JWT válido en el header:

```http
Authorization: Bearer {jwt_token}
```

## 📋 Endpoints Disponibles

### 1. Calcular Liquidación Básica

Calcula la liquidación técnica de un lote específico.

```http
GET /api/LiquidacionTecnica/{loteId}
```

#### Parámetros

| Parámetro | Tipo | Ubicación | Requerido | Descripción |
|-----------|------|-----------|-----------|-------------|
| `loteId` | string | Path | ✅ | ID único del lote |
| `fechaHasta` | DateTime | Query | ❌ | Fecha límite para el cálculo (default: fecha actual) |

#### Ejemplo de Solicitud

```bash
curl -X GET "http://localhost:5002/api/LiquidacionTecnica/L001?fechaHasta=2024-10-02" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Respuesta Exitosa (200)

```json
{
  "loteId": "L001",
  "loteNombre": "Lote Cobb 500 - Granja Norte",
  "fechaEncaset": "2024-03-01T00:00:00Z",
  "raza": "Cobb 500",
  "anoTablaGenetica": 2024,
  "codigoGuiaGenetica": "COBB500-2024",
  
  "hembrasEncasetadas": 5000,
  "machosEncasetados": 500,
  "totalAvesEncasetadas": 5500,
  
  "porcentajeMortalidadHembras": 3.2,
  "porcentajeMortalidadMachos": 4.1,
  "porcentajeSeleccionHembras": 2.1,
  "porcentajeSeleccionMachos": 1.8,
  "porcentajeErrorSexajeHembras": 0.5,
  "porcentajeErrorSexajeMachos": 0.3,
  
  "porcentajeRetiroTotalHembras": 5.8,
  "porcentajeRetiroTotalMachos": 6.2,
  "porcentajeRetiroTotalGeneral": 5.9,
  "porcentajeRetiroGuia": 5.0,
  
  "consumoAlimentoRealGramos": 125000.0,
  "consumoAlimentoGuiaGramos": 120000.0,
  "porcentajeDiferenciaConsumo": 4.17,
  
  "pesoSemana25RealHembras": 2.1,
  "pesoSemana25RealMachos": 2.8,
  "pesoSemana25GuiaHembras": 2.0,
  "pesoSemana25GuiaMachos": 2.7,
  "porcentajeDiferenciaPesoHembras": 5.0,
  "porcentajeDiferenciaPesoMachos": 3.7,
  
  "uniformidadRealHembras": 85.5,
  "uniformidadRealMachos": 82.3,
  "uniformidadGuiaHembras": 88.0,
  "uniformidadGuiaMachos": 85.0,
  "porcentajeDiferenciaUniformidadHembras": -2.84,
  "porcentajeDiferenciaUniformidadMachos": -3.18,
  
  "fechaCalculo": "2024-10-02T15:30:45Z",
  "totalRegistrosSeguimiento": 175,
  "fechaUltimoSeguimiento": "2024-09-30T00:00:00Z"
}
```

---

### 2. Obtener Liquidación Completa

Obtiene la liquidación técnica con detalles del seguimiento diario y datos de guía.

```http
GET /api/LiquidacionTecnica/{loteId}/completa
```

#### Parámetros

| Parámetro | Tipo | Ubicación | Requerido | Descripción |
|-----------|------|-----------|-----------|-------------|
| `loteId` | string | Path | ✅ | ID único del lote |
| `fechaHasta` | DateTime | Query | ❌ | Fecha límite para el cálculo |

#### Ejemplo de Solicitud

```bash
curl -X GET "http://localhost:5002/api/LiquidacionTecnica/L001/completa" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Respuesta Exitosa (200)

```json
{
  "liquidacion": {
    // Objeto LiquidacionTecnicaDto completo (ver endpoint anterior)
  },
  "detalleSeguimiento": [
    {
      "fecha": "2024-03-01T00:00:00Z",
      "semana": 1,
      "mortalidadHembras": 5,
      "mortalidadMachos": 2,
      "seleccionHembras": 3,
      "seleccionMachos": 1,
      "errorSexajeHembras": 0,
      "errorSexajeMachos": 1,
      "consumoKgHembras": 150.5,
      "consumoKgMachos": 15.2,
      "pesoPromHembras": 0.045,
      "pesoPromMachos": 0.048,
      "uniformidadHembras": 92.5,
      "uniformidadMachos": 90.8
    },
    {
      "fecha": "2024-03-02T00:00:00Z",
      "semana": 1,
      "mortalidadHembras": 3,
      "mortalidadMachos": 1,
      "seleccionHembras": 2,
      "seleccionMachos": 0,
      "errorSexajeHembras": 1,
      "errorSexajeMachos": 0,
      "consumoKgHembras": 155.2,
      "consumoKgMachos": 16.1,
      "pesoPromHembras": 0.047,
      "pesoPromMachos": 0.050,
      "uniformidadHembras": 91.8,
      "uniformidadMachos": 89.5
    }
    // ... más registros hasta semana 25
  ],
  "datosGuia": {
    "anioGuia": "2024",
    "raza": "Cobb 500",
    "edad": "175",
    "pesoHembras": 2.0,
    "pesoMachos": 2.7,
    "uniformidad": 88.0,
    "consumoAcumulado": 120000.0,
    "porcentajeRetiro": 5.0
  }
}
```

---

### 3. Calcular Liquidación (POST)

Calcula la liquidación técnica usando datos del cuerpo de la petición.

```http
POST /api/LiquidacionTecnica/calcular
```

#### Cuerpo de la Solicitud

```json
{
  "loteId": "L001",
  "fechaHasta": "2024-10-02T00:00:00Z"
}
```

#### Ejemplo de Solicitud

```bash
curl -X POST "http://localhost:5002/api/LiquidacionTecnica/calcular" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "loteId": "L001",
    "fechaHasta": "2024-10-02T00:00:00Z"
  }'
```

#### Respuesta Exitosa (200)

Retorna el mismo objeto `LiquidacionTecnicaDto` que el endpoint GET.

---

### 4. Validar Lote

Verifica si un lote puede ser procesado para liquidación técnica.

```http
GET /api/LiquidacionTecnica/{loteId}/validar
```

#### Parámetros

| Parámetro | Tipo | Ubicación | Requerido | Descripción |
|-----------|------|-----------|-----------|-------------|
| `loteId` | string | Path | ✅ | ID único del lote |

#### Ejemplo de Solicitud

```bash
curl -X GET "http://localhost:5002/api/LiquidacionTecnica/L001/validar" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Respuesta Exitosa (200)

```json
{
  "loteId": "L001",
  "esValido": true,
  "mensaje": "Lote válido para liquidación"
}
```

#### Respuesta - Lote Inválido (200)

```json
{
  "loteId": "L999",
  "esValido": false,
  "mensaje": "Lote no válido o sin datos de seguimiento"
}
```

---

### 5. Validar Múltiples Lotes

Valida múltiples lotes en una sola solicitud.

```http
POST /api/LiquidacionTecnica/validar-multiples
```

#### Cuerpo de la Solicitud

```json
["L001", "L002", "L003", "L004"]
```

#### Ejemplo de Solicitud

```bash
curl -X POST "http://localhost:5002/api/LiquidacionTecnica/validar-multiples" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '["L001", "L002", "L003", "L004"]'
```

#### Respuesta Exitosa (200)

```json
{
  "total": 4,
  "validos": 3,
  "resultados": [
    { "loteId": "L001", "esValido": true },
    { "loteId": "L002", "esValido": true },
    { "loteId": "L003", "esValido": false },
    { "loteId": "L004", "esValido": true }
  ]
}
```

## ❌ Respuestas de Error

### Error 400 - Bad Request

Ocurre cuando el lote no existe o no pertenece a la compañía.

```json
{
  "error": "Lote 'L999' no encontrado o no pertenece a la compañía."
}
```

### Error 401 - Unauthorized

Ocurre cuando no se proporciona token JWT o es inválido.

```json
{
  "error": "Unauthorized"
}
```

### Error 500 - Internal Server Error

Ocurre cuando hay un error interno del servidor.

```json
{
  "error": "Error interno del servidor"
}
```

## 📊 Códigos de Estado HTTP

| Código | Descripción | Cuándo Ocurre |
|--------|-------------|---------------|
| 200 | OK | Solicitud exitosa |
| 400 | Bad Request | Lote no encontrado o parámetros inválidos |
| 401 | Unauthorized | Token JWT faltante o inválido |
| 500 | Internal Server Error | Error interno del servidor |

## 🔍 Filtros y Validaciones

### Filtros Automáticos

1. **CompanyId**: Todos los datos se filtran automáticamente por la compañía del usuario autenticado
2. **DeletedAt**: Solo se consideran registros activos (no eliminados)
3. **Semana 25**: Los cálculos se limitan a máximo 175 días desde la fecha de encaset

### Validaciones

1. **Existencia del Lote**: Verifica que el lote exista en la base de datos
2. **Pertenencia**: Confirma que el lote pertenezca a la compañía del usuario
3. **Datos de Seguimiento**: Valida que existan registros de seguimiento para el lote
4. **Fechas**: Valida que las fechas estén en formato correcto

## 📈 Métricas Calculadas

### Datos Base
- Número total de aves hembras/machos encasetadas
- Fecha de encaset y información del lote

### Porcentajes Acumulados
- **Mortalidad**: `(Total Mortalidad / Aves Iniciales) × 100`
- **Selección**: `(Total Selección / Aves Iniciales) × 100`
- **Error de Sexaje**: `(Total Error / Aves Iniciales) × 100`
- **Retiro Total**: `Mortalidad + Selección + Error de Sexaje`

### Comparaciones con Guía
- **Consumo**: `((Real - Guía) / Guía) × 100`
- **Peso**: `((Real - Guía) / Guía) × 100`
- **Uniformidad**: `((Real - Guía) / Guía) × 100`

## 🚀 Ejemplos de Uso

### Caso 1: Liquidación Estándar

```javascript
// JavaScript/TypeScript
const response = await fetch('/api/LiquidacionTecnica/L001', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  }
});

const liquidacion = await response.json();
console.log(`Retiro total: ${liquidacion.porcentajeRetiroTotalGeneral}%`);
```

### Caso 2: Validación Antes de Calcular

```javascript
// Validar primero
const validacion = await fetch('/api/LiquidacionTecnica/L001/validar', {
  headers: { 'Authorization': `Bearer ${token}` }
});

const { esValido } = await validacion.json();

if (esValido) {
  // Proceder con el cálculo
  const liquidacion = await fetch('/api/LiquidacionTecnica/L001');
  // Procesar resultado...
}
```

### Caso 3: Procesamiento por Lotes

```javascript
// Validar múltiples lotes
const lotes = ['L001', 'L002', 'L003'];
const validaciones = await fetch('/api/LiquidacionTecnica/validar-multiples', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify(lotes)
});

const { resultados } = await validaciones.json();
const lotesValidos = resultados.filter(r => r.esValido).map(r => r.loteId);

// Procesar solo lotes válidos
for (const loteId of lotesValidos) {
  const liquidacion = await fetch(`/api/LiquidacionTecnica/${loteId}`);
  // Procesar cada liquidación...
}
```

## 🔧 Configuración del Cliente

### Headers Requeridos

```http
Authorization: Bearer {jwt_token}
Content-Type: application/json  # Solo para POST
Accept: application/json
```

### Base URL

```
Desarrollo: http://localhost:5002
Producción: https://api.zoosanmarino.com
```

### Timeout Recomendado

- **Liquidación básica**: 30 segundos
- **Liquidación completa**: 60 segundos (incluye más datos)
- **Validaciones**: 10 segundos

---

**Versión de la API**: 1.0.0  
**Última actualización**: Octubre 2024  
**Formato de respuesta**: JSON  
**Autenticación**: JWT Bearer Token
