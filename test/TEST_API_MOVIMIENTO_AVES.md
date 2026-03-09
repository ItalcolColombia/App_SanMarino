# Prueba de API de Movimiento de Aves

## Script de Prueba

Se ha creado un script de prueba: `test-api-movimiento.sh`

### Uso:

```bash
# Necesitas un token JWT válido (obtenerlo del login)
./test-api-movimiento.sh [TOKEN] [LOTE_ID]
```

### Ejemplo:

```bash
./test-api-movimiento.sh "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." 1
```

## Datos de Prueba

El script envía los siguientes datos:

```json
{
  "fechaMovimiento": "2026-02-02T00:00:00Z",
  "tipoMovimiento": "Venta",
  "loteOrigenId": 1,
  "cantidadHembras": 3333,
  "cantidadMachos": 0,
  "cantidadMixtas": 0,
  "motivoMovimiento": "Cliente",
  "descripcion": "prueba desarrollo",
  "observaciones": null,
  "edadAves": 277,
  "raza": "Ross 308",
  "placa": "frt565",
  "horaSalida": "17:25:00",
  "guiaAgrocalidad": "ga-456443",
  "sellos": "4324324jk3j4k2j34k2",
  "ayuno": "si",
  "conductor": "jose dario marin",
  "pesoBruto": 50000.0,
  "pesoTara": 2000.0,
  "usuarioMovimientoId": 0
}
```

## Verificación del Backend

El backend está configurado para recibir y guardar todos estos campos en:
- `MovimientoAvesService.CreateAsync()` - Guarda todos los campos
- `MovimientoAvesController.Create()` - Endpoint POST `/api/MovimientoAves`

## Verificación del Frontend

El frontend envía los datos a través de:
- `MovimientosAvesService.crearMovimientoAves()` - Método POST
- `ModalMovimientoAvesComponent.executeSave()` - Construye el DTO

## Logs de Depuración

El frontend ahora incluye logs detallados:
1. `onSubmitMovimiento llamado` - Cuando se hace clic en guardar
2. `Mostrando modal de confirmación...` - Cuando se muestra el modal
3. `ConfirmationModal: onConfirm llamado` - Cuando se confirma
4. `onConfirmSave llamado` - Cuando se procesa la confirmación
5. `=== executeSave llamado ===` - Cuando se ejecuta el guardado
6. `DTO a enviar para crear:` - Muestra el DTO completo
7. `Movimiento creado exitosamente:` - Confirmación de éxito

## Problemas Comunes

1. **Token inválido**: Obtén un nuevo token desde el login
2. **Lote no existe**: Verifica que el loteId sea válido
3. **Campos requeridos faltantes**: Revisa que todos los campos requeridos estén presentes
