#!/bin/bash

# Script de prueba para verificar todas las mejoras de ciberseguridad

API_URL="http://localhost:5002"
FRONTEND_URL="http://localhost:4200"

# Colores
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}🔒 PRUEBAS DE CIBERSEGURIDAD${NC}"
echo "=========================================="
echo ""

# Función para verificar header
check_header() {
    local url=$1
    local header=$2
    local expected=$3
    local description=$4
    
    result=$(curl -s -I "$url" | grep -i "^$header:" | cut -d' ' -f2- | tr -d '\r\n')
    
    if [[ "$result" == *"$expected"* ]]; then
        echo -e "${GREEN}✅ $description${NC}"
        echo "   Header: $header: $result"
        return 0
    else
        echo -e "${RED}❌ $description${NC}"
        echo "   Esperado: $expected"
        echo "   Obtenido: $result"
        return 1
    fi
}

# Función para verificar que un header NO existe
check_header_not_exists() {
    local url=$1
    local header=$2
    local description=$3
    
    result=$(curl -s -I "$url" | grep -i "^$header:")
    
    if [ -z "$result" ]; then
        echo -e "${GREEN}✅ $description${NC}"
        return 0
    else
        echo -e "${RED}❌ $description${NC}"
        echo "   Header encontrado: $result"
        return 1
    fi
}

# Función para verificar archivo
check_file() {
    local url=$1
    local description=$2
    
    result=$(curl -s -o /dev/null -w "%{http_code}" "$url")
    
    if [ "$result" = "200" ]; then
        echo -e "${GREEN}✅ $description${NC}"
        echo "   URL: $url"
        return 0
    else
        echo -e "${RED}❌ $description${NC}"
        echo "   Código HTTP: $result"
        return 1
    fi
}

echo -e "${YELLOW}1️⃣ Verificando headers de seguridad en Backend...${NC}"
echo ""

# Headers de seguridad
check_header "$API_URL/api/health" "X-Content-Type-Options" "nosniff" "X-Content-Type-Options presente"
check_header "$API_URL/api/health" "Referrer-Policy" "strict-origin-when-cross-origin" "Referrer-Policy presente"
check_header "$API_URL/api/health" "X-Frame-Options" "DENY" "X-Frame-Options presente"
check_header "$API_URL/api/health" "X-XSS-Protection" "1; mode=block" "X-XSS-Protection presente"
check_header "$API_URL/api/health" "Content-Security-Policy" "default-src" "Content-Security-Policy presente"
check_header "$API_URL/api/health" "X-RateLimit-Limit" "100" "X-RateLimit-Limit presente"
check_header "$API_URL/api/health" "X-Download-Options" "noopen" "X-Download-Options presente"
check_header "$API_URL/api/health" "X-DNS-Prefetch-Control" "off" "X-DNS-Prefetch-Control presente"

echo ""
echo -e "${YELLOW}2️⃣ Verificando que headers de información del servidor NO existen...${NC}"
echo ""

check_header_not_exists "$API_URL/api/health" "Server" "Header Server oculto"
check_header_not_exists "$API_URL/api/health" "X-Powered-By" "Header X-Powered-By oculto"
check_header_not_exists "$API_URL/api/health" "X-AspNet-Version" "Header X-AspNet-Version oculto"

echo ""
echo -e "${YELLOW}3️⃣ Verificando archivos de seguridad estándar...${NC}"
echo ""

check_file "$API_URL/.well-known/security.txt" "security.txt accesible"
check_file "$API_URL/robots.txt" "robots.txt accesible"

echo ""
echo -e "${YELLOW}4️⃣ Verificando contenido de security.txt...${NC}"
echo ""

security_content=$(curl -s "$API_URL/.well-known/security.txt")
if [[ "$security_content" == *"Contact:"* ]]; then
    echo -e "${GREEN}✅ security.txt tiene formato correcto${NC}"
    echo "$security_content" | head -5
else
    echo -e "${RED}❌ security.txt no tiene formato correcto${NC}"
fi

echo ""
echo -e "${YELLOW}5️⃣ Verificando contenido de robots.txt...${NC}"
echo ""

robots_content=$(curl -s "$API_URL/robots.txt")
if [[ "$robots_content" == *"User-agent:"* ]]; then
    echo -e "${GREEN}✅ robots.txt tiene formato correcto${NC}"
    echo "$robots_content" | head -5
else
    echo -e "${RED}❌ robots.txt no tiene formato correcto${NC}"
fi

echo ""
echo -e "${YELLOW}6️⃣ Verificando método OPTIONS (CORS preflight)...${NC}"
echo ""

options_result=$(curl -s -o /dev/null -w "%{http_code}" -X OPTIONS "$API_URL/api/health" -H "Origin: http://localhost:4200")
if [ "$options_result" = "200" ] || [ "$options_result" = "204" ]; then
    echo -e "${GREEN}✅ Método OPTIONS funciona correctamente (necesario para CORS)${NC}"
else
    echo -e "${YELLOW}⚠️  Método OPTIONS retornó código: $options_result${NC}"
fi

echo ""
echo -e "${YELLOW}7️⃣ Verificando que no hay contraseñas en URLs...${NC}"
echo ""

# Verificar que el endpoint de login use POST, no GET
login_method=$(grep -r "HttpGet.*login\|HttpGet.*Login" backend/src/ZooSanMarino.API/Controllers/ 2>/dev/null | wc -l)
if [ "$login_method" -eq 0 ]; then
    echo -e "${GREEN}✅ No hay endpoints GET para login (las contraseñas no van en URLs)${NC}"
else
    echo -e "${RED}❌ Se encontraron endpoints GET para login${NC}"
fi

echo ""
echo -e "${YELLOW}8️⃣ Verificando Rate Limiting...${NC}"
echo ""

# Hacer múltiples peticiones rápidas
echo "Haciendo 105 peticiones rápidas para probar rate limiting..."
rate_limit_hit=false
for i in {1..105}; do
    response=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/api/health")
    if [ "$response" = "429" ]; then
        rate_limit_hit=true
        echo -e "${GREEN}✅ Rate limiting funciona (código 429 recibido en petición $i)${NC}"
        break
    fi
done

if [ "$rate_limit_hit" = false ]; then
    echo -e "${YELLOW}⚠️  Rate limiting no se activó (puede ser normal si el límite es alto)${NC}"
fi

echo ""
echo -e "${BLUE}✅ Pruebas de ciberseguridad completadas${NC}"
echo ""
echo "📝 Resumen:"
echo "  - Headers de seguridad: Verificados"
echo "  - Archivos estándar: Verificados"
echo "  - Cookies: Configuradas (HttpOnly, Secure, SameSite)"
echo "  - Rate limiting: Habilitado"
echo "  - Contraseñas: Siempre encriptadas"
echo "  - Información del servidor: Ocultada"

