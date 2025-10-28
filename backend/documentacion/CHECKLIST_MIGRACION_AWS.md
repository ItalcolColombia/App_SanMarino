# ✅ Checklist de Migración a Nuevo AWS

## 🎯 Información Crítica Requerida

### 1. AWS Account
```
Account ID: _________________________
Región: _________________________
Profile CLI: _________________________
```

### 2. Credenciales de Acceso
```
AWS_ACCESS_KEY_ID: _________________________
AWS_SECRET_ACCESS_KEY: _________________________
```

### 3. Base de Datos RDS
```
Endpoint: _________________________
Puerto: ________ (default: 5432)
Usuario: _________________________
Password: _________________________
Database: _________________________
```

### 4. ECR Repositories
```
Backend URI: __________________________________________________________
Frontend URI: __________________________________________________________
```

### 5. ECS Configuration
```
Cluster Circuit Breaker: _________________________
Backend Service: _________________________
Frontend Service: _________________________
```

### 6. IAM Roles
```
Task Execution Role ARN: __________________________________________________________
Task App Role ARN: __________________________________________________________
```

### 7. Security Groups
```
Backend SG ID: _________________________
Frontend SG ID: _________________________
RDS SG ID: _________________________
```

### 8. Network
```
VPC ID: _________________________
Subnet 1: _________________________
Subnet 2: _________________________
```

### 9. SMTP Configuration
```
Host: _________________________
Port: ________
Username: ________________ básicamente ____________
Password: _________________________
From Email: _________________________
From Name: _________________________
```

### 10. JWT Secret
```
JWT Secret Key: __________________________________________________________
Issuer: _________________________
Audience: _________________________
```

---

## 🚀 Orden de Ejecución

- [ ] **Paso 1**: Recopilar toda la información de arriba
- [ ] **Paso 2**: Configurar AWS CLI con nuevas credenciales
- [ ] **Paso 3**: Crear/Verificar repositorios ECR
- [ ] **Paso 4**: Crear/Verificar Security Groups y reglas
- [ ] **Paso 5**: Crear/Verificar IAM Roles
- [ ] **Paso 6**: Crear/Verificar CloudWatch Log Groups
- [ ] **Paso 7**: Crear/Verificar ECS Cluster
- [ ] **Paso 8**: Actualizar Task Definition del Backend
- [ ] **Paso 9**: Construir y desplegar Backend a ECS
- [ ] **Paso 10**: Verificar que Backend se conecta a RDS
- [ ] **Paso 11**: Actualizar configuración del Frontend
- [ ] **Paso 12**: Construir y desplegar Frontend
- [ ] **Paso 13**: Probar login y funcionalidades principales
- [ ] **Paso 14**: Verificar logs en CloudWatch
- [ ] **Paso 15**: Configurar dominio y certificados SSL

---

## 🔧 Comandos Rápidos

### Configurar AWS CLI
```bash
aws configure --profile [NUEVO_PROFILE]
# Ingresar: Access Key, Secret Key, Region
```

### Login a ECR
```bash
aws ecr get-login-password --region [REGION] --profile [PROFILE] | \
  docker login --username AWS --password-stdin [ACCOUNT_ID].dkr.ecr.[REGION].amazonaws.com
```

### Desplegar Backend
```bash
cd backend
.\deploy-ecs.ps1 -Profile [PROFILE] -Region [REGION] \
  -Cluster [CLUSTER] -Service [SERVICE] \
  -Family [FAMILY] -Container api \
  -EcrUri [ECR_URI]
```

### Ver logs en tiempo real
```bash
aws logs tail /ecs/sanmarino-backend --follow --region [REGION] --profile [PROFILE]
```

---

## ⚠️ Notas Importantes

1. **RDS debe estar en la misma VPC** que los servicios ECS
2. **Security Groups deben permitir tráfico**:
   - RDS → Puerto 5432 desde Backend SG
   - Backend → Puerto 5002 desde ALB/Frontend SG
3. **JWT Secret debe ser diferente** al anterior
4. **CORS debe incluir** el nuevo dominio de producción
5. **Backup de base de datos** antes de migrar

---

## 📞 Contacto/Soporte

Si algo no funciona:
1. Revisar logs en CloudWatch
2. Verificar Security Groups
3. Verificar variables de entorno
4. Verificar conectividad de red
5. Revisar estado de tareas en ECS Console

