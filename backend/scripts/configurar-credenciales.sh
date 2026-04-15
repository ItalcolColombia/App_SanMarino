#!/bin/bash

# Script de Configuración Segura - Zoo San Marino
# ===============================================

echo "🔐 CONFIGURACIÓN SEGURA DE CREDENCIALES"
echo "======================================="
echo ""

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Función para leer input seguro
read_secure() {
    local prompt="$1"
    local var_name="$2"
    local is_password="$3"
    
    if [ "$is_password" = "true" ]; then
        echo -n "$prompt: "
        read -s value
        echo ""
    else
        echo -n "$prompt: "
        read value
    fi
    
    eval "$var_name='$value'"
}

# Función para generar archivo .env
generate_env_file() {
    local env_file="backend/.env"
    
    echo "📝 Generando archivo $env_file..."
    
    cat > "$env_file" << EOF
# Configuración Segura - Zoo San Marino
# Generado automáticamente el $(date)
# ⚠️  NO SUBIR ESTE ARCHIVO A GIT

# Base de Datos
DB_HOST=$db_host
DB_PORT=$db_port
DB_USERNAME=$db_username
DB_PASSWORD=$db_password
DB_NAME=$db_name

# JWT
JWT_SECRET_KEY=$jwt_secret
JWT_ISSUER=ZooSanMarino.API
JWT_AUDIENCE=ZooSanMarino.Client
JWT_DURATION_MINUTES=60

# Email SMTP
SMTP_HOST=$smtp_host
SMTP_PORT=$smtp_port
SMTP_USERNAME=$smtp_username
SMTP_PASSWORD=$smtp_password
FROM_EMAIL=$from_email
FROM_NAME=$from_name

# Otros
ALLOWED_ORIGINS=http://localhost:4200,http://127.0.0.1:4200,https://sanmarinoapp.com
EOF

    echo -e "${GREEN}✅ Archivo $env_file generado correctamente${NC}"
}

# Función para generar appsettings.json
generate_appsettings() {
    local appsettings_file="backend/src/ZooSanMarino.API/appsettings.json"
    
    echo "📝 Generando archivo $appsettings_file..."
    
    cat > "$appsettings_file" << EOF
{
  "ConnectionStrings": {
    "ZooSanMarinoContext": "Host=$db_host;Port=$db_port;Username=$db_username;Password=$db_password;Database=$db_name;SSL Mode=Require;Trust Server Certificate=true;Timeout=15;Command Timeout=30"
  },
  "JwtSettings": {
    "Key": "$jwt_secret",
    "Issuer": "ZooSanMarino.API",
    "Audience": "ZooSanMarino.Client",
    "DurationInMinutes": 60
  },
  "Database": {
    "RunMigrations": true,
    "RunSeed": false
  },
  "AllowedScopes": [ "ZooSanMarinoAPI" ],
  "AllowedOrigins": [
    "http://localhost:4200",
    "http://127.0.0.1:4200",
    "http://localhost:3000",
    "http://localhost:8080",
    "https://sanmarinoapp.com"
  ],
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DbStudio": {
    "Enabled": true,
    "WritableSchemas": [ "public" ],
    "SelectMaxLimit": 500
  },
  "EmailSettings": {
    "SmtpHost": "$smtp_host",
    "SmtpPort": $smtp_port,
    "SmtpUsername": "$smtp_username",
    "SmtpPassword": "$smtp_password",
    "FromEmail": "$from_email",
    "FromName": "$from_name"
  }
}
EOF

    echo -e "${GREEN}✅ Archivo $appsettings_file generado correctamente${NC}"
}

# Función para generar contraseña JWT segura
generate_jwt_secret() {
    local length=${1:-64}
    openssl rand -base64 $length | tr -d "=+/" | cut -c1-$length
}

# Función principal
main() {
    echo -e "${YELLOW}Este script te ayudará a configurar las credenciales de forma segura.${NC}"
    echo ""
    
    # Configuración de Base de Datos
    echo -e "${YELLOW}📊 CONFIGURACIÓN DE BASE DE DATOS${NC}"
    echo "----------------------------------------"
    read_secure "Host de la base de datos" "db_host" "false"
    read_secure "Puerto (por defecto 5432)" "db_port" "false"
    read_secure "Usuario de la base de datos" "db_username" "false"
    read_secure "Contraseña de la base de datos" "db_password" "true"
    read_secure "Nombre de la base de datos" "db_name" "false"
    
    echo ""
    
    # Configuración de Email
    echo -e "${YELLOW}📧 CONFIGURACIÓN DE EMAIL SMTP${NC}"
    echo "------------------------------------"
    read_secure "Host SMTP (ej: smtp.gmail.com)" "smtp_host" "false"
    read_secure "Puerto SMTP (por defecto 587)" "smtp_port" "false"
    read_secure "Usuario SMTP (tu email)" "smtp_username" "false"
    read_secure "Contraseña SMTP" "smtp_password" "true"
    read_secure "Email de envío" "from_email" "false"
    read_secure "Nombre del remitente" "from_name" "false"
    
    echo ""
    
    # Generar JWT Secret
    echo -e "${YELLOW}🔑 GENERANDO JWT SECRET${NC}"
    echo "------------------------"
    jwt_secret=$(generate_jwt_secret 64)
    echo -e "${GREEN}JWT Secret generado automáticamente${NC}"
    
    echo ""
    
    # Confirmar configuración
    echo -e "${YELLOW}📋 RESUMEN DE CONFIGURACIÓN${NC}"
    echo "=============================="
    echo "Base de datos: $db_host:$db_port/$db_name"
    echo "Usuario DB: $db_username"
    echo "Email SMTP: $smtp_host:$smtp_port"
    echo "Usuario SMTP: $smtp_username"
    echo "Email de envío: $from_email"
    echo ""
    
    read -p "¿Continuar con la generación de archivos? (y/N): " -n 1 -r
    echo ""
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        # Generar archivos
        generate_env_file
        generate_appsettings
        
        echo ""
        echo -e "${GREEN}🎉 CONFIGURACIÓN COMPLETADA${NC}"
        echo "=============================="
        echo ""
        echo "📝 Archivos generados:"
        echo "- backend/.env (variables de entorno)"
        echo "- backend/src/ZooSanMarino.API/appsettings.json"
        echo ""
        echo "⚠️  IMPORTANTE:"
        echo "- El archivo .env NO debe subirse a Git"
        echo "- Cambia las credenciales si es necesario"
        echo "- Usa contraseñas de aplicación para Gmail"
        echo ""
        echo "🚀 Próximos pasos:"
        echo "1. Revisa los archivos generados"
        echo "2. Inicia el backend: dotnet run"
        echo "3. Prueba la funcionalidad de email"
        
    else
        echo -e "${RED}❌ Operación cancelada${NC}"
    fi
}

# Ejecutar función principal
main
