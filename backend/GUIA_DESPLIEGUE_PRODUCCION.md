# 🚀 Guía de Despliegue a Producción - AWS ECS

## 📋 **Resumen**

Esta guía te ayudará a desplegar la aplicación Zoo San Marino a producción en AWS ECS con todas las nuevas funcionalidades implementadas:

- ✅ **Recuperación de contraseña** por email
- ✅ **Módulo de perfil de usuario** 
- ✅ **Configuración segura de credenciales**
- ✅ **Validación fuerte de contraseñas**

---

## 🛠️ **Prerrequisitos**

### **Software Requerido:**
- ✅ **Docker Desktop** - Instalado y ejecutándose
- ✅ **AWS CLI** - Configurado con credenciales válidas
- ✅ **PowerShell** - Para ejecutar los scripts
- ✅ **Git** - Para control de versiones

### **Configuración AWS:**
- ✅ **Cuenta AWS** con permisos ECS/ECR
- ✅ **Cluster ECS** configurado (`sanmarino-cluster`)
- ✅ **Servicio ECS** configurado (`sanmarino-api-svc`)
- ✅ **Repositorio ECR** configurado (`sanmarino-backend`)

---

## 🔧 **Preparación del Despliegue**

### **Paso 1: Verificación Previa**
```powershell
# Ejecutar verificación completa
.\verify-deployment.ps1
```

Este script verificará:
- ✅ Compilación del proyecto
- ✅ Archivos de configuración
- ✅ Nuevas funcionalidades implementadas
- ✅ Dockerfile y scripts de despliegue

### **Paso 2: Configurar Credenciales**

#### **Opción A: Variables de Entorno (Recomendada)**
```powershell
# Copiar archivo de ejemplo
cp env.production.example .env

# Editar con credenciales reales
notepad .env
```

#### **Opción B: Configuración en ECS Task Definition**
Configurar las variables de entorno directamente en AWS Console:
- `DB_HOST`, `DB_USERNAME`, `DB_PASSWORD`
- `SMTP_USERNAME`, `SMTP_PASSWORD`
- `JWT_SECRET_KEY`

---

## 🚀 **Proceso de Despliegue**

### **Método 1: Script Automático (Recomendado)**
```powershell
# Ejecutar script completo de despliegue
.\deploy-to-production.ps1
```

Este script:
1. 🔍 Verifica prerrequisitos (Docker, AWS CLI)
2. 🔐 Autentica con AWS ECR
3. 🏗️ Construye la imagen Docker
4. 📤 Sube la imagen a ECR
5. 🔄 Actualiza la Task Definition
6. 🚀 Despliega el servicio ECS
7. ✅ Verifica el despliegue

### **Método 2: Script Manual**
```powershell
# Ejecutar directamente el script de ECS
.\deploy-ecs.ps1 -Profile sanmarino -Region us-east-2 `
  -Cluster sanmarino-cluster -Service sanmarino-api-svc `
  -Family sanmarino-backend -Container api `
  -EcrUri 021891592771.dkr.ecr.us-east-2.amazonaws.com/sanmarino-backend
```

---

## 📊 **Monitoreo del Despliegue**

### **AWS Console ECS:**
1. Ir a [ECS Console](https://us-east-2.console.aws.amazon.com/ecs/)
2. Seleccionar cluster `sanmarino-cluster`
3. Verificar servicio `sanmarino-api-svc`
4. Comprobar que las tareas estén `RUNNING`

### **CloudWatch Logs:**
1. Ir a [CloudWatch Logs](https://us-east-2.console.aws.amazon.com/cloudwatch/)
2. Buscar grupo de logs `/ecs/sanmarino-backend`
3. Verificar logs de aplicación

### **Health Check:**
```bash
# Verificar endpoint de salud
curl https://tu-dominio.com/health
```

---

## 🔍 **Verificación de Funcionalidades**

### **1. Recuperación de Contraseña:**
```bash
# Probar endpoint de recuperación
curl -X POST https://tu-dominio.com/api/Auth/recover-password \
  -H "Content-Type: application/json" \
  -d '{"email": "usuario@ejemplo.com"}'
```

### **2. Módulo de Perfil:**
- Ir a `https://tu-dominio.com/profile`
- Verificar que se carguen los datos del usuario
- Probar actualización de datos personales
- Probar cambio de contraseña con validación

### **3. Configuración Segura:**
```bash
# Verificar configuración (requiere autenticación de Admin)
curl -H "Authorization: Bearer TOKEN" \
  https://tu-dominio.com/api/Configuration
```

---

## 🛡️ **Configuración de Seguridad**

### **Variables de Entorno Críticas:**
```env
# JWT Secret (generar uno nuevo para producción)
JWT_SECRET_KEY=tu-clave-super-secreta-y-larga

# Credenciales de Base de Datos
DB_PASSWORD=tu-password-seguro

# Credenciales de Email
SMTP_PASSWORD=tu-app-password-gmail
```

### **Configuración de Email:**
1. **Gmail**: Usar contraseña de aplicación (no la contraseña normal)
2. **SendGrid**: Usar API Key
3. **Amazon SES**: Usar credenciales IAM

---

## 🚨 **Solución de Problemas**

### **Error: "Docker daemon not available"**
```powershell
# Abrir Docker Desktop y esperar a que esté listo
# Luego reintentar el despliegue
```

### **Error: "AWS credentials not configured"**
```powershell
# Configurar AWS CLI
aws configure
# O usar variables de entorno
$env:AWS_ACCESS_KEY_ID="tu-key"
$env:AWS_SECRET_ACCESS_KEY="tu-secret"
```

### **Error: "Task definition registration failed"**
```powershell
# Verificar que la Task Definition base existe
aws ecs describe-task-definition --task-definition sanmarino-backend
```

### **Error: "Service update failed"**
```powershell
# Verificar que el cluster y servicio existen
aws ecs describe-services --cluster sanmarino-cluster --services sanmarino-api-svc
```

---

## 📈 **Optimizaciones de Producción**

### **Configuración de ECS:**
- **CPU**: 512 (0.5 vCPU)
- **Memoria**: 1024 MB (1 GB)
- **Plataforma**: Fargate
- **Red**: VPC con subnets privadas

### **Configuración de Aplicación:**
- **Logging**: Nivel Information
- **DbStudio**: Deshabilitado en producción
- **CORS**: Solo dominios de producción
- **HTTPS**: Habilitado con certificado SSL

---

## 🔄 **Rollback (Si es necesario)**

### **Rollback a versión anterior:**
```powershell
# Obtener Task Definition anterior
aws ecs describe-task-definition --task-definition sanmarino-backend:REVISION-ANTERIOR

# Actualizar servicio con versión anterior
aws ecs update-service --cluster sanmarino-cluster --service sanmarino-api-svc --task-definition sanmarino-backend:REVISION-ANTERIOR
```

---

## 📞 **Soporte**

Si encuentras problemas durante el despliegue:

1. **Revisar logs** en CloudWatch
2. **Verificar configuración** de variables de entorno
3. **Comprobar conectividad** de red en ECS
4. **Validar credenciales** de base de datos y email

---

## 🎉 **¡Despliegue Exitoso!**

Una vez completado el despliegue, tendrás:

- ✅ **API funcionando** en producción
- ✅ **Recuperación de contraseña** operativa
- ✅ **Módulo de perfil** disponible
- ✅ **Configuración segura** implementada
- ✅ **Monitoreo** configurado

**¡Felicidades! Tu aplicación Zoo San Marino está lista para producción!** 🚀
