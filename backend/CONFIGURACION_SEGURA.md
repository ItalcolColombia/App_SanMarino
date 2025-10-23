# 🔐 Configuración Segura - Zoo San Marino

## ⚠️ IMPORTANTE: Seguridad de Credenciales

**NUNCA** subas credenciales reales a GitHub. Este repositorio está configurado para proteger información sensible.

## 📋 Pasos para Configurar el Proyecto

### 1. **Configurar Base de Datos**
```bash
# Copia el archivo de ejemplo
cp backend/src/ZooSanMarino.API/appsettings.json.example backend/src/ZooSanMarino.API/appsettings.json

# Edita el archivo con tus credenciales reales
# Reemplaza:
# - YOUR_DB_HOST
# - YOUR_DB_USER  
# - YOUR_DB_PASSWORD
# - YOUR_DB_NAME
```

### 2. **Configurar Email SMTP**
```bash
# Copia el archivo de desarrollo
cp backend/src/ZooSanMarino.API/appsettings.Development.json.example backend/src/ZooSanMarino.API/appsettings.Development.json

# Edita con tus credenciales de email:
# - YOUR_EMAIL_USERNAME
# - YOUR_EMAIL_PASSWORD
# - YOUR_FROM_EMAIL
```

### 3. **Configurar JWT Secret**
```json
{
  "JwtSettings": {
    "Key": "TU_CLAVE_SECRETA_SUPER_LARGA_Y_SEGURA_AQUI"
  }
}
```

## 🛡️ Mejores Prácticas de Seguridad

### ✅ **HACER:**
- Usar variables de entorno en producción
- Generar claves JWT únicas y largas
- Usar contraseñas de aplicación para Gmail
- Rotar credenciales regularmente
- Usar servicios profesionales (SendGrid, AWS SES)

### ❌ **NO HACER:**
- Subir archivos `appsettings.json` con credenciales reales
- Usar contraseñas débiles
- Compartir credenciales por chat/email
- Hardcodear credenciales en el código

## 🔧 Configuración de Email

### Gmail (Recomendado para desarrollo):
1. Habilita verificación en 2 pasos
2. Genera una "Contraseña de aplicación"
3. Usa esa contraseña en la configuración

### SendGrid (Recomendado para producción):
```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "SmtpUsername": "apikey",
    "SmtpPassword": "TU_API_KEY_DE_SENDGRID"
  }
}
```

## 🚨 Si Detectas Credenciales Expuestas

1. **Cambia inmediatamente** todas las credenciales
2. **Revisa el historial** de commits en GitHub
3. **Usa `git filter-branch`** para limpiar el historial
4. **Configura GitGuardian** para monitoreo continuo

## 📞 Soporte

Si tienes problemas con la configuración, contacta al equipo de desarrollo.
