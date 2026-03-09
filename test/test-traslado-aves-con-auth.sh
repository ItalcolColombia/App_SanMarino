#!/bin/bash

# Script de prueba para el flujo de traslado de aves con autenticación
# Usa los headers proporcionados por el usuario

API_URL="http://localhost:5002/api"
LOTE_ID="13"

# Headers de autenticación
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjkyYWZlNGM4LWJmM2UtNGFiMC1hMzFhLTQ2Nzg5MDQ2MzU0MiIsInN1YiI6IjkyYWZlNGM4LWJmM2UtNGFiMC1hMzFhLTQ2Nzg5MDQ2MzU0MiIsInVuaXF1ZV9uYW1lIjoibW9pZXNiYnVnYUBnbWFpbC5jb20iLCJlbWFpbCI6Im1vaWVzYmJ1Z2FAZ21haWwuY29tIiwiZmlyc3ROYW1lIjoiUnZlcmEiLCJzdXJOYW1lIjoiam9zZSBtb2lzZXMiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiIsImNvbXBhbnlfaWQiOiIxIiwiY29tcGFueSI6IkFncmljb2xhIHNhbm1hcmlubyIsInBlcm1pc3Npb24iOiJyb2xlczptYW5hZ2UiLCJleHAiOjE3NjQ3MDY4MzEsImlzcyI6Ilpvb1Nhbk1hcmluby5BUEkiLCJhdWQiOiJab29TYW5NYXJpbm8uQ2xpZW50In0.7TUYV96jWlcyV9k3S5IKrWZKsnQQRaq7Cxb3RAhl3JI"
SECRET_UP="MXfk92HUw9KaYGJALLphcZgDlJ0VJvvTqArqwYqDE2HOyIxi6rOctdrPi9s/uZI+"
ACTIVE_COMPANY="Agricola sanmarino"

# Colores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}🧪 PRUEBAS DEL FLUJO DE TRASLADO DE AVES${NC}"
echo "=========================================="
echo ""

# Función para hacer peticiones
make_request() {
    local method=$1
    local endpoint=$2
    local data=$3
    
    if [ "$method" = "GET" ]; then
        curl -s -X GET "$API_URL$endpoint" \
            -H "Authorization: Bearer $TOKEN" \
            -H "X-Secret-Up: $SECRET_UP" \
            -H "X-Active-Company: $ACTIVE_COMPANY" \
            -H "Content-Type: application/json" \
            -H "Accept: application/json"
    else
        curl -s -X POST "$API_URL$endpoint" \
            -H "Authorization: Bearer $TOKEN" \
            -H "X-Secret-Up: $SECRET_UP" \
            -H "X-Active-Company: $ACTIVE_COMPANY" \
            -H "Content-Type: application/json" \
            -H "Accept: application/json" \
            -d "$data"
    fi
}

# Función para imprimir JSON formateado
print_json() {
    echo "$1" | python3 -m json.tool 2>/dev/null || echo "$1"
}

echo -e "${YELLOW}1️⃣ Consultando disponibilidad del lote $LOTE_ID...${NC}"
DISPO_INICIAL=$(make_request "GET" "/Traslados/lote/$LOTE_ID/disponibilidad")
echo "$DISPO_INICIAL" | python3 -m json.tool 2>/dev/null || echo "$DISPO_INICIAL"
echo ""

# Extraer valores de disponibilidad
HEMBRAS_INICIAL=$(echo "$DISPO_INICIAL" | python3 -c "import sys, json; d=json.load(sys.stdin); print(d.get('aves', {}).get('hembrasVivas', 0))" 2>/dev/null || echo "0")
MACHOS_INICIAL=$(echo "$DISPO_INICIAL" | python3 -c "import sys, json; d=json.load(sys.stdin); print(d.get('aves', {}).get('machosVivos', 0))" 2>/dev/null || echo "0")

echo -e "${GREEN}✅ Disponibilidad inicial:${NC}"
echo "   Hembras: $HEMBRAS_INICIAL"
echo "   Machos: $MACHOS_INICIAL"
echo ""

echo -e "${YELLOW}2️⃣ Consultando inventario del lote $LOTE_ID...${NC}"
INVENTARIO_INICIAL=$(make_request "GET" "/InventarioAves/lote/$LOTE_ID")
echo "$INVENTARIO_INICIAL" | python3 -m json.tool 2>/dev/null || echo "$INVENTARIO_INICIAL"
echo ""

echo -e "${YELLOW}3️⃣ Creando traslado de prueba (10 hembras, 5 machos)...${NC}"
FECHA_TRASLADO=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
TRASLADO_DATA=$(cat <<EOF
{
  "loteId": "$LOTE_ID",
  "fechaTraslado": "$FECHA_TRASLADO",
  "tipoOperacion": "Venta",
  "cantidadHembras": 10,
  "cantidadMachos": 5,
  "motivo": "Prueba automatizada - Venta",
  "descripcion": "Prueba de traslado de aves desde script",
  "observaciones": "Prueba automatizada"
}
EOF
)

TRASLADO_RESULT=$(make_request "POST" "/Traslados/aves" "$TRASLADO_DATA")
echo "$TRASLADO_RESULT" | python3 -m json.tool 2>/dev/null || echo "$TRASLADO_RESULT"
echo ""

# Verificar si se creó correctamente
MOVIMIENTO_ID=$(echo "$TRASLADO_RESULT" | python3 -c "import sys, json; d=json.load(sys.stdin); print(d.get('id', 'N/A'))" 2>/dev/null || echo "N/A")
ESTADO=$(echo "$TRASLADO_RESULT" | python3 -c "import sys, json; d=json.load(sys.stdin); print(d.get('estado', 'N/A'))" 2>/dev/null || echo "N/A")

if [ "$MOVIMIENTO_ID" != "N/A" ] && [ "$MOVIMIENTO_ID" != "null" ]; then
    echo -e "${GREEN}✅ Traslado creado exitosamente${NC}"
    echo "   Movimiento ID: $MOVIMIENTO_ID"
    echo "   Estado: $ESTADO"
    echo ""
    
    echo -e "${YELLOW}⏳ Esperando 3 segundos para que se procese el movimiento...${NC}"
    sleep 3
    echo ""
    
    echo -e "${YELLOW}4️⃣ Consultando disponibilidad después del traslado...${NC}"
    DISPO_FINAL=$(make_request "GET" "/Traslados/lote/$LOTE_ID/disponibilidad")
    echo "$DISPO_FINAL" | python3 -m json.tool 2>/dev/null || echo "$DISPO_FINAL"
    echo ""
    
    # Extraer valores finales
    HEMBRAS_FINAL=$(echo "$DISPO_FINAL" | python3 -c "import sys, json; d=json.load(sys.stdin); print(d.get('aves', {}).get('hembrasVivas', 0))" 2>/dev/null || echo "0")
    MACHOS_FINAL=$(echo "$DISPO_FINAL" | python3 -c "import sys, json; d=json.load(sys.stdin); print(d.get('aves', {}).get('machosVivos', 0))" 2>/dev/null || echo "0")
    
    echo -e "${GREEN}✅ Disponibilidad final:${NC}"
    echo "   Hembras: $HEMBRAS_FINAL"
    echo "   Machos: $MACHOS_FINAL"
    echo ""
    
    # Calcular reducción
    HEMBRAS_REDUCIDAS=$((HEMBRAS_INICIAL - HEMBRAS_FINAL))
    MACHOS_REDUCIDOS=$((MACHOS_INICIAL - MACHOS_FINAL))
    
    echo -e "${BLUE}📊 Análisis:${NC}"
    echo "   Reducción de hembras: $HEMBRAS_REDUCIDAS (esperado: 10)"
    echo "   Reducción de machos: $MACHOS_REDUCIDOS (esperado: 5)"
    echo ""
    
    if [ "$HEMBRAS_REDUCIDAS" -eq 10 ] && [ "$MACHOS_REDUCIDOS" -eq 5 ]; then
        echo -e "${GREEN}✅ ¡ÉXITO! El inventario se actualizó correctamente${NC}"
    else
        echo -e "${YELLOW}⚠️  La reducción no coincide exactamente con lo esperado${NC}"
    fi
    echo ""
    
    echo -e "${YELLOW}5️⃣ Consultando inventario final...${NC}"
    INVENTARIO_FINAL=$(make_request "GET" "/InventarioAves/lote/$LOTE_ID")
    echo "$INVENTARIO_FINAL" | python3 -m json.tool 2>/dev/null || echo "$INVENTARIO_FINAL"
    echo ""
    
    echo -e "${YELLOW}6️⃣ Consultando movimientos del lote...${NC}"
    MOVIMIENTOS=$(make_request "GET" "/MovimientoAves/lote/$LOTE_ID")
    echo "$MOVIMIENTOS" | python3 -m json.tool 2>/dev/null || echo "$MOVIMIENTOS"
    echo ""
    
else
    echo -e "${RED}❌ Error al crear el traslado${NC}"
    echo "$TRASLADO_RESULT"
fi

echo -e "${BLUE}✅ Pruebas completadas${NC}"

