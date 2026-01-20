# 🧪 PRUEBAS DEL FLUJO DE TRASLADO DE AVES

## 📋 Objetivo
Validar que el flujo completo de traslado de aves funcione correctamente:
1. Mostrar disponibilidad correctamente
2. Actualizar inventario al procesar movimiento
3. Validar que no se puedan trasladar más aves de las disponibles

---

## 🔍 PREPARACIÓN

### 1. Obtener información de un lote de prueba

**Endpoint**: `GET /api/Traslados/lote/{loteId}/disponibilidad`

**Ejemplo**:
```bash
curl -X GET "http://localhost:5002/api/Traslados/lote/13/disponibilidad" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Respuesta esperada**:
```json
{
  "loteId": 13,
  "loteNombre": "Lote de Prueba",
  "tipoLote": "Levante",
  "aves": {
    "hembrasVivas": 1000,
    "machosVivos": 500,
    "totalAves": 1500,
    "hembrasIniciales": 1200,
    "machosIniciales": 600,
    "mortalidadAcumuladaHembras": 150,
    "mortalidadAcumuladaMachos": 80,
    "retirosAcumuladosHembras": 50,
    "retirosAcumuladosMachos": 20
  },
  "granjaId": 5,
  "granjaNombre": "NIZA III"
}
```

**Anotar**:
- Lote ID: `13`
- Hembras disponibles: `1000`
- Machos disponibles: `500`
- Total disponible: `1500`

---

## ✅ PRUEBA 1: Verificar Disponibilidad Inicial

### Objetivo
Confirmar que el endpoint de disponibilidad funciona y muestra las cantidades correctas.

### Pasos
1. Consultar disponibilidad del lote 13
2. Verificar que retorne información de aves (tipoLote = "Levante")
3. Anotar las cantidades disponibles

### Resultado Esperado
- ✅ Endpoint responde con código 200
- ✅ `tipoLote` = "Levante"
- ✅ `aves.hembrasVivas` > 0
- ✅ `aves.machosVivos` > 0
- ✅ `aves.totalAves` = hembrasVivas + machosVivos

---

## ✅ PRUEBA 2: Verificar Inventario Actual del Lote

### Objetivo
Confirmar el estado actual del inventario antes de hacer el traslado.

### Endpoint
`GET /api/InventarioAves/lote/{loteId}`

### Pasos
1. Consultar inventario del lote 13
2. Anotar las cantidades actuales

### Resultado Esperado
- ✅ Si existe inventario: muestra cantidades actuales
- ✅ Si no existe: se creará automáticamente al procesar el primer movimiento

---

## ✅ PRUEBA 3: Crear Traslado de Aves (Venta)

### Objetivo
Crear un traslado de tipo "Venta" y verificar que:
1. Se crea el movimiento
2. Se procesa automáticamente
3. Se actualiza el inventario del lote origen

### Endpoint
`POST /api/Traslados/aves`

### Request Body
```json
{
  "loteId": "13",
  "fechaTraslado": "2025-12-02T00:00:00Z",
  "tipoOperacion": "Venta",
  "cantidadHembras": 50,
  "cantidadMachos": 25,
  "motivo": "Venta directa - Prueba",
  "descripcion": "Prueba de traslado de aves",
  "observaciones": "Prueba automatizada"
}
```

### Pasos
1. Enviar request POST con el body anterior
2. Verificar respuesta (debe ser 201 Created)
3. Anotar el ID del movimiento creado
4. Consultar disponibilidad nuevamente
5. Consultar inventario del lote

### Resultado Esperado
- ✅ Respuesta 201 Created con `MovimientoAvesDto`
- ✅ `estado` = "Completado" (procesado automáticamente)
- ✅ Disponibilidad actualizada:
  - Hembras: `1000 - 50 = 950`
  - Machos: `500 - 25 = 475`
  - Total: `1500 - 75 = 1425`
- ✅ Inventario del lote actualizado con las nuevas cantidades

---

## ✅ PRUEBA 4: Verificar Actualización del Inventario

### Objetivo
Confirmar que el inventario se actualizó correctamente después del traslado.

### Endpoint
`GET /api/InventarioAves/lote/13`

### Pasos
1. Consultar inventario del lote 13
2. Verificar cantidades

### Resultado Esperado
- ✅ `cantidadHembras` = 950 (1000 - 50)
- ✅ `cantidadMachos` = 475 (500 - 25)
- ✅ `fechaActualizacion` = fecha actual
- ✅ `estado` = "Activo"

---

## ✅ PRUEBA 5: Crear Traslado de Aves (Traslado entre Lotes)

### Objetivo
Crear un traslado entre lotes y verificar que:
1. Se descuentan aves del lote origen
2. Se suman aves al lote destino (si existe)

### Endpoint
`POST /api/Traslados/aves`

### Request Body
```json
{
  "loteId": "13",
  "fechaTraslado": "2025-12-02T00:00:00Z",
  "tipoOperacion": "Traslado",
  "cantidadHembras": 30,
  "cantidadMachos": 15,
  "granjaDestinoId": 5,
  "tipoDestino": "Granja",
  "observaciones": "Traslado entre lotes - Prueba"
}
```

### Pasos
1. Enviar request POST
2. Verificar respuesta
3. Consultar disponibilidad del lote origen
4. Si hay lote destino, consultar su inventario

### Resultado Esperado
- ✅ Respuesta 201 Created
- ✅ Lote origen: disponibilidad reducida
  - Hembras: `950 - 30 = 920`
  - Machos: `475 - 15 = 460`
- ✅ Si hay lote destino, su inventario aumenta

---

## ✅ PRUEBA 6: Validar que No se Puede Exceder Disponibilidad

### Objetivo
Confirmar que el sistema valida y rechaza traslados que exceden la disponibilidad.

### Endpoint
`POST /api/Traslados/aves`

### Request Body (Cantidad excesiva)
```json
{
  "loteId": "13",
  "fechaTraslado": "2025-12-02T00:00:00Z",
  "tipoOperacion": "Venta",
  "cantidadHembras": 10000,
  "cantidadMachos": 5000,
  "motivo": "Prueba de validación",
  "descripcion": "Debe fallar"
}
```

### Pasos
1. Enviar request POST con cantidades excesivas
2. Verificar respuesta

### Resultado Esperado
- ✅ Respuesta 400 Bad Request
- ✅ Mensaje: "No hay suficientes aves disponibles para este traslado"

---

## ✅ PRUEBA 7: Consultar Movimientos del Lote

### Objetivo
Verificar que los movimientos se registran correctamente y se pueden consultar.

### Endpoint
`GET /api/MovimientoAves/lote/13`

### Pasos
1. Consultar movimientos del lote 13
2. Verificar que aparezcan los movimientos creados en las pruebas anteriores

### Resultado Esperado
- ✅ Lista de movimientos del lote
- ✅ Incluye los movimientos de las pruebas 3 y 5
- ✅ Todos con `estado` = "Completado"
- ✅ Cantidades correctas

---

## 📊 RESUMEN DE VALIDACIONES

### Backend
- ✅ `ProcesarMovimientoAsync` actualiza el inventario
- ✅ `ActualizarInventarioPorMovimientoAsync` resta del origen y suma al destino
- ✅ `ObtenerOCrearInventarioAsync` crea inventario si no existe
- ✅ Validación de disponibilidad antes de crear movimiento

### Frontend
- ✅ Muestra disponibilidad en el modal
- ✅ Campos muestran "Disponible: X" al lado de cada input
- ✅ Validación visual cuando se excede disponibilidad
- ✅ Recarga disponibilidad después de crear traslado

---

## 🔧 COMANDOS ÚTILES PARA PRUEBAS

### Obtener token de autenticación
```bash
# Primero autenticarse y obtener token
curl -X POST "http://localhost:5002/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"usuario@ejemplo.com","password":"password"}'
```

### Consultar disponibilidad
```bash
curl -X GET "http://localhost:5002/api/Traslados/lote/13/disponibilidad" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Consultar inventario
```bash
curl -X GET "http://localhost:5002/api/InventarioAves/lote/13" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Crear traslado
```bash
curl -X POST "http://localhost:5002/api/Traslados/aves" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "loteId": "13",
    "fechaTraslado": "2025-12-02T00:00:00Z",
    "tipoOperacion": "Venta",
    "cantidadHembras": 50,
    "cantidadMachos": 25,
    "motivo": "Prueba",
    "descripcion": "Prueba de traslado"
  }'
```

### Consultar movimientos del lote
```bash
curl -X GET "http://localhost:5002/api/MovimientoAves/lote/13" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## 📝 NOTAS IMPORTANTES

1. **Lote de Prueba**: Usar un lote de tipo "Levante" (no "Produccion")
2. **Token**: Reemplazar `YOUR_TOKEN` con el token real de autenticación
3. **Lote ID**: Ajustar el ID del lote según los datos disponibles en la base de datos
4. **Fecha**: Usar fecha actual o futura para el traslado

---

## ✅ CHECKLIST DE VALIDACIÓN

- [ ] Disponibilidad se muestra correctamente en el frontend
- [ ] Campos muestran "Disponible: X" al lado de cada input
- [ ] Validación visual funciona cuando se excede disponibilidad
- [ ] Backend valida disponibilidad antes de crear movimiento
- [ ] Inventario se actualiza al procesar movimiento
- [ ] Disponibilidad se recarga después de crear traslado
- [ ] Movimientos se registran correctamente
- [ ] No se pueden trasladar más aves de las disponibles

