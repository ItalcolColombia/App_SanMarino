#!/bin/bash

# Script de prueba para el flujo de traslado de aves
# Requiere que el backend esté corriendo en http://localhost:5002

API_URL="http://localhost:5002/api"
LOTE_ID="13"

echo "🧪 PRUEBAS DEL FLUJO DE TRASLADO DE AVES"
echo "=========================================="
echo ""

# Colores para output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Función para imprimir resultados
print_result() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✅ $2${NC}"
    else
        echo -e "${RED}❌ $2${NC}"
    fi
}

# Verificar que el backend esté corriendo
echo "1️⃣ Verificando que el backend esté corriendo..."
BACKEND_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/Traslados/lote/$LOTE_ID/disponibilidad" 2>&1)
if [ "$BACKEND_STATUS" = "401" ] || [ "$BACKEND_STATUS" = "200" ] || [ "$BACKEND_STATUS" = "404" ]; then
    print_result 0 "Backend está respondiendo (código: $BACKEND_STATUS)"
else
    print_result 1 "Backend no está respondiendo. Asegúrate de que esté corriendo en http://localhost:5002"
    exit 1
fi

echo ""
echo "⚠️  NOTA: Este script requiere autenticación."
echo "Las peticiones necesitan:"
echo "  - Header: Authorization: Bearer <token>"
echo "  - Header: X-Secret-Up: <secret_encriptado>"
echo "  - Header: X-Active-Company: <company_id>"
echo ""
echo "Para hacer las pruebas completas:"
echo "  1. Abre el navegador en http://localhost:4200"
echo "  2. Inicia sesión"
echo "  3. Abre la consola del navegador (F12)"
echo "  4. Ejecuta las siguientes funciones desde la consola:"
echo ""

cat << 'EOF'

// ============================================
// FUNCIONES DE PRUEBA PARA EJECUTAR EN CONSOLA DEL NAVEGADOR
// ============================================

// 1. Consultar disponibilidad del lote
async function testDisponibilidad(loteId = '13') {
  try {
    const response = await fetch(`http://localhost:5002/api/Traslados/lote/${loteId}/disponibilidad`, {
      headers: {
        'Authorization': `Bearer ${sessionStorage.getItem('token')}`,
        'X-Active-Company': sessionStorage.getItem('activeCompany') || '',
        'X-Secret-Up': '...' // Se agrega automáticamente por el interceptor
      }
    });
    const data = await response.json();
    console.log('📊 Disponibilidad del lote:', data);
    return data;
  } catch (error) {
    console.error('❌ Error:', error);
  }
}

// 2. Consultar inventario del lote
async function testInventario(loteId = 13) {
  try {
    const response = await fetch(`http://localhost:5002/api/InventarioAves/lote/${loteId}`, {
      headers: {
        'Authorization': `Bearer ${sessionStorage.getItem('token')}`,
        'X-Active-Company': sessionStorage.getItem('activeCompany') || ''
      }
    });
    const data = await response.json();
    console.log('📦 Inventario del lote:', data);
    return data;
  } catch (error) {
    console.error('❌ Error:', error);
  }
}

// 3. Crear traslado de aves (Venta)
async function testCrearTrasladoVenta(loteId = '13', hembras = 10, machos = 5) {
  try {
    const response = await fetch('http://localhost:5002/api/Traslados/aves', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${sessionStorage.getItem('token')}`,
        'X-Active-Company': sessionStorage.getItem('activeCompany') || ''
      },
      body: JSON.stringify({
        loteId: loteId,
        fechaTraslado: new Date().toISOString(),
        tipoOperacion: 'Venta',
        cantidadHembras: hembras,
        cantidadMachos: machos,
        motivo: 'Prueba automatizada - Venta',
        descripcion: 'Prueba de traslado de aves desde consola',
        observaciones: 'Prueba automatizada'
      })
    });
    const data = await response.json();
    console.log('✅ Traslado creado:', data);
    return data;
  } catch (error) {
    console.error('❌ Error:', error);
  }
}

// 4. Consultar movimientos del lote
async function testMovimientosLote(loteId = 13) {
  try {
    const response = await fetch(`http://localhost:5002/api/MovimientoAves/lote/${loteId}`, {
      headers: {
        'Authorization': `Bearer ${sessionStorage.getItem('token')}`,
        'X-Active-Company': sessionStorage.getItem('activeCompany') || ''
      }
    });
    const data = await response.json();
    console.log('📋 Movimientos del lote:', data);
    return data;
  } catch (error) {
    console.error('❌ Error:', error);
  }
}

// 5. Flujo completo de prueba
async function testFlujoCompleto(loteId = '13') {
  console.log('🧪 Iniciando flujo completo de prueba...');
  console.log('');
  
  // Paso 1: Consultar disponibilidad inicial
  console.log('1️⃣ Consultando disponibilidad inicial...');
  const disponibilidadInicial = await testDisponibilidad(loteId);
  if (!disponibilidadInicial) {
    console.error('❌ No se pudo obtener disponibilidad');
    return;
  }
  console.log(`   Hembras disponibles: ${disponibilidadInicial.aves?.hembrasVivas || 0}`);
  console.log(`   Machos disponibles: ${disponibilidadInicial.aves?.machosVivos || 0}`);
  console.log('');
  
  // Paso 2: Consultar inventario inicial
  console.log('2️⃣ Consultando inventario inicial...');
  const inventarioInicial = await testInventario(parseInt(loteId));
  console.log('');
  
  // Paso 3: Crear traslado
  console.log('3️⃣ Creando traslado de prueba (10 hembras, 5 machos)...');
  const traslado = await testCrearTrasladoVenta(loteId, 10, 5);
  if (!traslado) {
    console.error('❌ No se pudo crear el traslado');
    return;
  }
  console.log(`   Movimiento ID: ${traslado.id}`);
  console.log(`   Estado: ${traslado.estado}`);
  console.log('');
  
  // Esperar un momento para que se procese
  console.log('⏳ Esperando 2 segundos para que se procese el movimiento...');
  await new Promise(resolve => setTimeout(resolve, 2000));
  console.log('');
  
  // Paso 4: Consultar disponibilidad después del traslado
  console.log('4️⃣ Consultando disponibilidad después del traslado...');
  const disponibilidadFinal = await testDisponibilidad(loteId);
  if (disponibilidadFinal) {
    console.log(`   Hembras disponibles: ${disponibilidadFinal.aves?.hembrasVivas || 0}`);
    console.log(`   Machos disponibles: ${disponibilidadFinal.aves?.machosVivos || 0}`);
    
    // Verificar que se actualizó
    const hembrasReducidas = (disponibilidadInicial.aves?.hembrasVivas || 0) - (disponibilidadFinal.aves?.hembrasVivas || 0);
    const machosReducidos = (disponibilidadInicial.aves?.machosVivos || 0) - (disponibilidadFinal.aves?.machosVivos || 0);
    
    if (hembrasReducidas === 10 && machosReducidos === 5) {
      console.log('✅ Disponibilidad actualizada correctamente');
    } else {
      console.log(`⚠️  Reducción esperada: H=10, M=5. Reducción real: H=${hembrasReducidas}, M=${machosReducidos}`);
    }
  }
  console.log('');
  
  // Paso 5: Consultar inventario final
  console.log('5️⃣ Consultando inventario final...');
  const inventarioFinal = await testInventario(parseInt(loteId));
  console.log('');
  
  // Paso 6: Consultar movimientos
  console.log('6️⃣ Consultando movimientos del lote...');
  await testMovimientosLote(parseInt(loteId));
  console.log('');
  
  console.log('✅ Flujo completo de prueba finalizado');
}

// Para ejecutar el flujo completo:
// testFlujoCompleto('13')

EOF

echo ""
echo "📝 INSTRUCCIONES:"
echo "  1. Copia las funciones anteriores"
echo "  2. Pégalas en la consola del navegador (F12)"
echo "  3. Ejecuta: testFlujoCompleto('13')"
echo ""
echo "O ejecuta las funciones individualmente:"
echo "  - testDisponibilidad('13')"
echo "  - testInventario(13)"
echo "  - testCrearTrasladoVenta('13', 10, 5)"
echo "  - testMovimientosLote(13)"
echo ""

