# 🔐 Configuración Segura de Credenciales - Zoo San Marino

## 📋 **Resumen de Soluciones Implementadas**

Se han implementado **5 estrategias diferentes** para manejar credenciales de forma segura:

### **1. 🎯 Variables de Entorno (.env) - RECOMENDADA**
- **Archivo**: `backend/.env` (NO se sube a Git)
- **Ventajas**: Simple, estándar, seguro
- **Uso**: Desarrollo y producción

### **2. 🗄️ Base de Datos (SystemConfiguration) - AVANZADA**
- **Tabla**: `SystemConfigurations`
- **Ventajas**: Centralizada, encriptada, auditada
- **Uso**: Producción empresarial

### **3. 🔄 Configuración Híbrida - ÓPTIMA**
- **Prioridad**: Variables de entorno > Base de datos > Archivos
- **Ventajas**: Flexibilidad máxima
- **Uso**: Todos los entornos

### **4. 🤖 Script Automático - FÁCIL**
- **Archivo**: `configurar-credenciales.sh`
- **Ventajas**: Automatizado, guiado
- **Uso**: Configuración inicial

### **5. ☁️ Azure Key Vault - ENTERPRISE**
- **Servicio**: Azure Key Vault
- **Ventajas**: Máxima seguridad, escalable
- **Uso**: Producción enterprise

---

## 🚀 **Guía de Implementación**

### **Opción A: Variables de Entorno (Más Simple)**

#### **Paso 1: Crear archivo .env**
```bash
# Copiar el archivo de ejemplo
cp backend/env.example backend/.env

# Editar con tus credenciales reales
nano backend/.env
```

#### **Paso 2: Configurar credenciales**
```env
# Base de Datos
DB_HOST=tu-host-db
DB_USERNAME=tu-usuario
DB_PASSWORD=tu-password
DB_NAME=tu-database

# Email SMTP
SMTP_USERNAME=tu-email@gmail.com
SMTP_PASSWORD=tu-app-password
FROM_EMAIL=tu-email@gmail.com

# JWT
JWT_SECRET_KEY=tu-clave-super-secreta-y-larga
```

#### **Paso 3: Usar en Program.cs**
```csharp
// El sistema ya está configurado para leer variables de entorno
// No necesitas cambios adicionales
```

---

### **Opción B: Base de Datos (Más Avanzada)**

#### **Paso 1: Ejecutar migración**
```bash
cd backend/src/ZooSanMarino.API
dotnet ef migrations add AddSystemConfiguration
dotnet ef database update
```

#### **Paso 2: Inicializar configuraciones**
```bash
# Via API (requiere autenticación de Admin)
POST /api/Configuration/initialize
```

#### **Paso 3: Configurar credenciales via API**
```bash
# Configurar email SMTP
PUT /api/Configuration/SMTP_USERNAME
{
  "value": "tu-email@gmail.com",
  "encrypt": true
}

PUT /api/Configuration/SMTP_PASSWORD
{
  "value": "tu-app-password",
  "encrypt": true
}
```

---

### **Opción C: Script Automático (Más Fácil)**

#### **Paso 1: Ejecutar script**
```bash
chmod +x configurar-credenciales.sh
./configurar-credenciales.sh
```

#### **Paso 2: Seguir las instrucciones**
- El script te guiará paso a paso
- Generará automáticamente los archivos
- Creará JWT secret seguro

---

## 🛡️ **Mejores Prácticas de Seguridad**

### **✅ HACER:**
- Usar contraseñas de aplicación para Gmail
- Rotar credenciales regularmente
- Usar variables de entorno en producción
- Encriptar credenciales sensibles
- Auditar accesos a configuraciones

### **❌ NO HACER:**
- Subir archivos `.env` a Git
- Hardcodear credenciales en código
- Usar contraseñas débiles
- Compartir credenciales por chat
- Dejar credenciales en logs

---

## 🔧 **Configuración de Email**

### **Gmail (Recomendado para desarrollo):**
1. Habilita verificación en 2 pasos
2. Genera "Contraseña de aplicación"
3. Usa esa contraseña en la configuración

### **SendGrid (Recomendado para producción):**
```env
SMTP_HOST=smtp.sendgrid.net
SMTP_PORT=587
SMTP_USERNAME=apikey
SMTP_PASSWORD=tu-api-key-sendgrid
```

---

## 📊 **Comparación de Estrategias**

| Estrategia | Seguridad | Facilidad | Escalabilidad | Costo |
|------------|-----------|-----------|---------------|-------|
| Variables de Entorno | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Gratis |
| Base de Datos | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ | Gratis |
| Configuración Híbrida | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Gratis |
| Script Automático | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Gratis |
| Azure Key Vault | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | Pago |

---

## 🚨 **Solución de Problemas**

### **Error: "Email settings not configured"**
```bash
# Verificar variables de entorno
echo $SMTP_USERNAME
echo $SMTP_PASSWORD

# O verificar en base de datos
GET /api/Configuration/SMTP_USERNAME
```

### **Error: "Database connection failed"**
```bash
# Verificar conexión
echo $DB_HOST
echo $DB_USERNAME
echo $DB_PASSWORD
```

### **Error: "JWT secret not found"**
```bash
# Generar nuevo JWT secret
openssl rand -base64 64
```

---

## 📞 **Soporte**

Si tienes problemas con la configuración:
1. Revisa los logs del backend
2. Verifica las variables de entorno
3. Consulta la documentación de la API
4. Contacta al equipo de desarrollo

---

## 🔄 **Migración desde Configuración Actual**

### **Si ya tienes credenciales en appsettings.json:**

1. **Extraer credenciales**:
```bash
# Copiar credenciales de appsettings.json
# Crear archivo .env con esas credenciales
```

2. **Limpiar historial**:
```bash
# Ejecutar script de limpieza
./limpiar-credenciales.sh
```

3. **Configurar nuevo sistema**:
```bash
# Usar script de configuración
./configurar-credenciales.sh
```

---

**🎉 ¡Configuración segura implementada exitosamente!**
