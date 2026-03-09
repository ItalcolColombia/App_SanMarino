#!/bin/bash

# Script para probar la API de creación de movimiento de aves
# Uso: ./test-movimiento-aves-api.sh [TOKEN] [LOTE_ID]

API_URL="http://localhost:5002/api"
TOKEN="${1:-YOUR_TOKEN_HERE}"
LOTE_ID="${2:-1}"

echo "=== Probando API de Movimiento de Aves ==="
echo "API URL: $API_URL"
echo "Lote ID: $LOTE_ID"
echo ""

# Datos del movimiento basados en lo que el usuario pasó
JSON_DATA=$(cat <<EOF
{
  "fechaMovimiento": "2026-02-02T00:00:00Z",
  "tipoMovimiento": "Venta",
  "loteOrigenId": $LOTE_ID,
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
  "totalPollosGalpon": null,
  "pesoBruto": 50000.0,
  "pesoTara": 2000.0,
  "usuarioMovimientoId": 0
}
EOF
)

echo "Enviando petición POST a $API_URL/MovimientoAves"
echo "Datos:"
echo "$JSON_DATA" | jq '.' 2>/dev/null || echo "$JSON_DATA"
echo ""

RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
  -X POST "$API_URL/MovimientoAves" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Active-Company: Agricola sanmarino" \
  -H "X-Active-Company-Id: 1" \
  -H "X-Active-Pais: 1" \
  -H "X-Active-Pais-Nombre: Ecuador" \
  -H "X-Secret-Up: hDLh7Rs5L" \
  -d "$JSON_DATA")

HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS" | cut -d: -f2)
BODY=$(echo "$RESPONSE" | sed '/HTTP_STATUS/d')

echo "=== Respuesta ==="
echo "HTTP Status: $HTTP_STATUS"
echo "Body:"
echo "$BODY" | jq '.' 2>/dev/null || echo "$BODY"
echo ""

if [ "$HTTP_STATUS" = "201" ]; then
  echo "✅ Movimiento creado exitosamente"
  MOVIMIENTO_ID=$(echo "$BODY" | jq -r '.id' 2>/dev/null)
  if [ ! -z "$MOVIMIENTO_ID" ] && [ "$MOVIMIENTO_ID" != "null" ]; then
    echo "ID del movimiento creado: $MOVIMIENTO_ID"
  fi
elif [ "$HTTP_STATUS" = "400" ]; then
  echo "❌ Error de validación (400)"
  echo "Revisa los datos enviados"
elif [ "$HTTP_STATUS" = "401" ]; then
  echo "❌ Error de autenticación (401)"
  echo "Verifica que el token sea válido"
else
  echo "❌ Error: HTTP $HTTP_STATUS"
fi
