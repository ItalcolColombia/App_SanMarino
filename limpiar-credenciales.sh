# Script para Limpiar Credenciales del Historial de Git
# =====================================================

echo "🚨 LIMPIEZA DE CREDENCIALES EXPUESTAS"
echo "====================================="
echo ""

echo "📋 Archivos que serán eliminados del historial:"
echo "- backend/src/ZooSanMarino.API/appsettings.json"
echo "- backend/src/ZooSanMarino.API/appsettings.Development.json" 
echo "- backend/config-ejemplo-email.txt"
echo ""

echo "⚠️  IMPORTANTE:"
echo "1. Haz backup de tus credenciales antes de continuar"
echo "2. Este proceso modificará el historial de Git"
echo "3. Todos los colaboradores necesitarán hacer 'git pull --force'"
echo ""

read -p "¿Continuar con la limpieza? (y/N): " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🧹 Iniciando limpieza..."
    
    # Eliminar archivos del historial
    git filter-branch --force --index-filter \
        'git rm --cached --ignore-unmatch backend/src/ZooSanMarino.API/appsettings.json' \
        --prune-empty --tag-name-filter cat -- --all
    
    git filter-branch --force --index-filter \
        'git rm --cached --ignore-unmatch backend/src/ZooSanMarino.API/appsettings.Development.json' \
        --prune-empty --tag-name-filter cat -- --all
        
    git filter-branch --force --index-filter \
        'git rm --cached --ignore-unmatch backend/config-ejemplo-email.txt' \
        --prune-empty --tag-name-filter cat -- --all
    
    # Limpiar referencias
    git for-each-ref --format='delete %(refname)' refs/original | git update-ref --stdin
    git reflog expire --expire=now --all
    git gc --prune=now --aggressive
    
    echo "✅ Limpieza completada!"
    echo ""
    echo "📝 Próximos pasos:"
    echo "1. Copia los archivos .example a archivos reales"
    echo "2. Configura tus credenciales reales"
    echo "3. Haz commit de los cambios"
    echo "4. Haz 'git push --force' para actualizar el repositorio remoto"
    echo "5. Notifica a todos los colaboradores sobre el cambio"
    
else
    echo "❌ Operación cancelada"
fi
