#!/bin/bash
# Script de prueba para API de Movimiento de Aves
# Uso: ./test-api-movimiento.sh [TOKEN] [LOTE_ID]

API_URL="http://localhost:5002/api"
TOKEN="${1}"
LOTE_ID="${2:-1}"

if [ -z "$TOKEN" ]; then
  echo "❌ Error: Se requiere un token de autenticación"
  echo "Uso: ./test-api-movimiento.sh [TOKEN] [LOTE_ID]"
  exit 1
fi

echo "=== Probando API de Movimiento de Aves ==="
echo "API URL: $API_URL/MovimientoAves"
echo "Lote ID: $LOTE_ID"
echo ""

# Datos del movimiento
curl -X POST "$API_URL/MovimientoAves" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Active-Company: Agricola sanmarino" \
  -H "X-Active-Company-Id: 1" \
  -H "X-Active-Pais: 1" \
  -H "X-Active-Pais-Nombre: Ecuador" \
  -H "X-Secret-Up: hDLh7Rs5L" \
  -d "{
    \"fechaMovimiento\": \"2026-02-02T00:00:00Z\",
    \"tipoMovimiento\": \"Venta\",
    \"loteOrigenId\": $LOTE_ID,
    \"cantidadHembras\": 3333,
    \"cantidadMachos\": 0,
    \"cantidadMixtas\": 0,
    \"motivoMovimiento\": \"Cliente\",
    \"descripcion\": \"prueba desarrollo\",
    \"observaciones\": null,
    \"edadAves\": 277,
    \"raza\": \"Ross 308\",
    \"placa\": \"frt565\",
    \"horaSalida\": \"17:25:00\",
    \"guiaAgrocalidad\": \"ga-456443\",
    \"sellos\": \"4324324jk3j4k2j34k2\",
    \"ayuno\": \"si\",
    \"conductor\": \"jose dario marin\",
    \"pesoBruto\": 50000.0,
    \"pesoTara\": 2000.0,
    \"usuarioMovimientoId\": 0
  }" \
  -w "\n\nHTTP Status: %{http_code}\n" \
  -s | jq '.' 2>/dev/null || cat

