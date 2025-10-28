# 🚀 Instrucciones de Despliegue Backend - Nuevo AWS

## 📋 Información del Nuevo AWS

- **Account ID**: 196080479890
- **Región**: us-east-2
- **Cluster**: devSanmarinoZoo
- **Service**: sanmarino-back-task-service-75khncfa
- **Task Definition Family**: sanmarino-back-task
- **Container Name**: backend
- **ECR URI**: 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend
- **Puerto**: 5002

---

## ⚠️ ANTES DE COMENZAR

Necesitas la siguiente información del RDS:

1. **Endpoint de RDS**: `_____________________`
2. **Usuario de RDS**: `_____________________`
3. **Password de RDS**: `_____________________`
4. **Database Name**: `_____________________`

---

## 🔧 Paso 1: Obtener Información de RDS

Si no la tienes, busca en la consola AWS o pregúntale a tu administrador de AWS.

Una vez tengas esta información, actualiza el archivo `ecs-taskdef-new-aws.json` en la línea que dice:

```json
"value": "Host=TU_RDS_ENDPOINT_AQUI;Port=5432;Username=TU_USERNAME;Password=TU_PASSWORD;Database=TU_DATABASE;SSL Mode=Require;Trust Server Certificate=true;Timeout=15;Command Timeout=30"
```

---

## 📦 Paso 2: Configurar Task Definition

### Opción A: Registrar Task Definition con Variables de Entorno

Primero necesitas obtener la Task Definition actual y actualizar las variables de entorno:

```bash
cd backend

# Obtener Task Definition actual
aws ecs describe-task-definition --task-definition sanmarino-back-task:2 --region us-east-2 --output json > current-task-def.json

# Editar el archivo current-task-def.json y agregar las variables de entorno en "environment"
```

### Opción B: Registrar Task Definition desde JSON

```bash
cd backend

# Editar ecs-taskdef-new-aws.json con los datos de RDS
# Luego registrar
aws ecs register-task-definition --cli-input-json file://ecs-taskdef-new-aws.json --region us-east-2
```

---

## 🐳 Paso 3: Verificar Docker

Asegúrate de que Docker Desktop esté corriendo:

```bash
docker version
```

---

## 🚀 Paso 4: Desplegar Backend

### Opción A: Usar el Script de PowerShell (Windows/macOS con PowerShell)

```powershell
cd backend
.\deploy-new-aws.ps1
```

### Opción B: Usar el Script de Bash (macOS/Linux)

```bash
cd backend
chmod +x deploy-backend-new-aws.sh
./deploy-backend-new-aws.sh
```

### Opción C: Despliegue Manual

```bash
cd backend

# 1. Login a ECR
aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 196080479890.dkr.ecr.us-east-2.amazonaws.com

# 2. Build la imagen
docker build -t sanmarino-back-task:latest .

# 3. Tag la imagen
docker tag sanmarino-back-task:latest 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend:latest
docker tag sanmarino-back-task:latest 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend:$(date +%Y%m%d-%H%M)

# 4. Push al ECR
docker push 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend:latest
docker push 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend:$(date +%Y%m%d-%H%M)
```

---

## ✅ Paso 5: Verificar Despliegue

### Ver estado del servicio:

```bash
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2 --query 'services[0].[runningCount,desiredCount,status]' --output table
```

### Ver logs:

```bash
aws logs tail /ecs/sanmarino-back-task --follow --region us-east-2
```

### Ver tareas:

```bash
aws ecs list-tasks --cluster devSanmarinoZoo --service-name sanmarino-back-task-service-75khncfa --region us-east-2
```

---

## 🔍 Troubleshooting

### Error: "tasks failed to start"

1. Verifica los logs:
```bash
aws logs tail /ecs/sanmarino-back-task --follow --region us-east-2
```

2. Verifica las variables de entorno en la Task Definition

3. Verifica que la conexión a RDS sea correcta

4. Verifica los Security Groups

### Error: "Cannot connect to database"

1. Verifica que el endpoint de RDS sea correcto
2. Verifica que el Security Group de RDS permita conexiones desde el Security Group de ECS
3. Verifica las credenciales de la base de datos

### Error: "Image not found"

1. Verifica que la imagen se haya pusheado correctamente:
```bash
aws ecr describe-images --repository-name sanmarino/zootecnia/granjas/backend --region us-east-2
```

### Puerto no disponible

Verifica que el puerto 5002 esté mapeado correctamente en la Task Definition:

```json
"portMappings": [
  { 
    "containerPort": 5002, 
    "hostPort": 5002, 
    "protocol": "tcp" 
  }
]
```

---

## 📝 Verificar Conectividad Backend → RDS

Una vez que la tarea esté corriendo, puedes verificar la conectividad:

```bash
# Obtener IP de la tarea
TASK_ARN=$(aws ecs list-tasks --cluster devSanmarinoZoo --service-name sanmarino-back-task-service-75khncfa --region us-east-2 --query 'taskArns[0]' --output text)

# Obtener detalles de red
aws ecs describe-tasks --cluster devSanmarino lograr --tasks $TASK_ARN --region us-east-2 --query 'tasks[0].attachments[0].details' --output table
```

---

## 🔄 Próximos Pasos (Después del Backend)

Una vez que el backend esté funcionando:

1. **Actualizar Frontend**: Configurar la URL del backend en `frontend/src/environments/environment.prod.ts`
2. **Desplegar Frontend**: Similar proceso para el frontend en ECS
3. **Verificar Integración**: Probar que el frontend puede comunicarse con el backend

---

## 📞 Comandos Útiles

### Ver detalles completos del servicio:
```bash
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2
```

### Forzar nuevo despliegue:
```bash
aws ecs update-service --cluster devSanmarinoZoo --service sanmarino-back-task-service-75khncfa --force-new-deployment --region us-east-2
```

### Ver eventos recientes:
```bash
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2 --query 'services[0].events[0:5]' --output table
```

### Ver logs en tiempo real:
```bash
aws logs tail /ecs/sanmarino-back-task --follow --region us-east-2
```

---

## ✅ Checklist Final

- [ ] Docker Desktop está corriendo
- [ ] Credenciales de RDS obtenidas
- [ ] Task Definition actualizada con variables de entorno
- [ ] Imagen Docker construida exitosamente
- [ ] Imagen pushada a ECR exitosamente
- [ ] Task Definition registrada
- [ ] Servicio actualizado
- [ ] Backend corriendo (runningCount > 0)
- [ ] Logs sin errores
- [ ] Conectividad a RDS verificada

---

**¡Listo para desplegar!** 🚀

