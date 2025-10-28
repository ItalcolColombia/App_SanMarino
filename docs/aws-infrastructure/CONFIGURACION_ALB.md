# 🌐 Configuración Application Load Balancer - San Marino

## ✅ ALB Configurado Exitosamente

**Fecha**: 27 de Octubre 2025

---

## 📋 Información del ALB

### Application Load Balancer
- **Nombre**: sanmarino-alb
- **ARN**: `arn:aws:elasticloadbalancing:us-east-2:196080479890:loadbalancer/app/sanmarino-alb/c0dfbafacd4feeae`
- **DNS**: `sanmarino-alb-878335997.us-east-2.elb.amazonaws.com`
- **Scheme**: Internet-facing
- **Type**: Application Load Balancer

### Security Group
- **ID**: sg-0b3d019a0578d75da
- **Nombre**: sanmarino-alb-sg
- **Puertos abiertos**: 80 (HTTP) y 443 (HTTPS)

### Subnets
- subnet-16cbb35a (us-east-2c)
- subnet-ebdfcf91 (us-east-2b)
- subnet-89cf15e2 (us-east-2a)

---

## 🎯 Target Groups

### Backend Target Group
- **Nombre**: sanmarino-backend-tg
- **ARN**: `arn:aws:elasticloadbalancing:us-east-2:196080479890:targetgroup/sanmarino-backend-tg/488ffbf2f71b32e5`
- **Puerto**: 5002
- **Protocol**: HTTP
- **Health Check**: `/health`
- **Target Type**: IP

### Frontend Target Group
- **Nombre**: sanmarino-frontend-tg
- **ARN**: `arn:aws:elasticloadbalancing:us-east-2:196080479890:targetgroup/sanmarino-frontend-tg/d7e143a37b1806ba`
- **Puerto**: 80
- **Protocol**: HTTP
- **Health Check**: `/`
- **Target Type**: IP

---

## 🔀 Reglas de Enrutamiento

### Listener HTTP (Puerto 80)
- **ARN**: `arn:aws:elasticloadbalancing:us-east-2:196080479890:listener/app Received-alb/c0dfbafacd4feeae/212f62c2a5f75429`

#### Reglas:
1. **Priority 100**: Path Pattern `/api/*` → Backend Target Group
2. **Default**: Todas las demás rutas → Frontend Target Group

### Listener HTTPS (Puerto 443)
- ⏳ **Pendiente configuración** (será configurado por otra persona)

---

## 🔗 Arquitectura con ALB

```
Usuario
   ↓
ALB: sanmarino-alb-878335997.us-east-2.elb.amazonaws.com
   ↓
   ├─ /api/* → Backend Target Group (Puerto 5002)
   │   └─ sanmarino-back-task-service-75khncfa
   │       └─ RDS PostgreSQL
   │
   └─ /* → Frontend Target Group (Puerto 80)
       └─ sanmarino-front-task-service-zp2f403l
```

---

## 📊 Configuración de Servicios ECS

### Backend Service
- **Service**: sanmarino-back-task-service-75khncfa
- **Load Balancer**: Registrado con sanmarino-backend-tg
- **Container**: backend
- **Port**: 5002

### Frontend Service
- **Service**: sanmarino-front-task-service-zp2f403l
- **Load Balancer**: Registrado con sanmarino-frontend-tg
- **Container**: frontend
- **Port**: 80

---

## 🔧 Cambios Realizados

### 1. Frontend environment.prod.ts
```typescript
apiUrl: '/api'  // Ahora usa rutas relativas (manejadas por ALB)
```

### 2. Security Groups
- **ALB SG**: Permite HTTP (80) y HTTPS (443) desde Internet
- **ECS SG**: Permite tráfico desde ALB SG en puertos 80 y 5002

### 3. Listeners Configurados
- ✅ HTTP (80): Rutas de enrutamiento configuradas
- ⏳ HTTPS (443): Pendiente (otra persona configurará certificado SSL)

---

## ✅ Próximos Pasos

### Para el Frontend:
1. **Reconstruir imagen** con nueva configuración:
   ```bash
   cd frontend
   docker build -t 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest .
   docker push 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest
   ```

2. **Forzar nuevo despliegue**:
   ```bash
   aws ecs update-service --cluster devSanmarinoZoo --service sanmarino-front-task-service-zp2f403l --force-new-deployment --region us-east-2
   ```

### Para HTTPS (Otra Persona):
1. Crear certificado SSL en AWS Certificate Manager (ACM)
2. Crear listener HTTPS (443) en el ALB con el certificado
3. Opcional: Redirigir HTTP → HTTPS

---

## 🌐 URLs de Acceso

### Después de reconstruir el frontend:
- **Frontend**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/
- **API**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api

### URLs Activas Actuales (dirección IP directa):
- **Frontend**: http://18.222.188.98
- **Backend**: http://3.145.143.253:5002

> ⚠️ **Nota**: Las URLs del ALB son estables y no cambian, a diferencia de las IPs directas de las tareas ECS.

---

## 🚨 Troubleshooting

### ALB no responde
1. Verificar que el Security Group del ALB permita tráfico en puertos 80/443
2. Verificar que las tareas ECS estén registradas en los Target Groups
3. Revisar Health Checks de los Target Groups

### Backend no alcanzable desde ALB
1. Verificar que el Security Group de ECS permita tráfico del ALB
2. Verificar que las tareas estén corriendo
3. Verificar Health Check `/health` del backend

### Frontend no alcanzable desde ALB
1. Verificar que las tareas frontend estén corriendo
2. Verificar Health Check `/` del frontend
3. Revisar logs en CloudWatch

---

## 📝 Comandos Útiles

### Ver Lovibalancer info
```bash
aws elbv2 describe-load-balancers --load-balancer-arns arn:aws:elasticloadbalancing:us-east-2:196080479890:loadbalancer/app/sanmarino-alb/c0dfbafacd4feeae --region us-east-2
```

### Ver Target Groups
```bash
aws elbv2 describe-target-groups --region us-east-2 --query 'TargetGroups[*].[TargetGroupName,HealthCheckPath,Port]' --output table
```

### Ver registros en Target Group
```bash
# Backend
aws elbv2 describe-target-health --target-group-arn arn:aws:elasticloadbalancing:us-east-2:196080479890:targetgroup/sanmarino-backend-tg/488ffbf2f71b32e5 --region us-east-2

# Frontend
aws elbv2 describe-target-health --target-group-arn arn:aws:elasticloadbalancing:us-east-2:196080479890:targetgroup/sanmarino-frontend-tg/d7e143a37b1806ba --region us-east-2
```

---

## 🎉 Conclusión

El ALB ha sido configurado exitosamente. Ahora las aplicaciones tienen URLs estables y enrutamiento inteligente. Solo queda reconstruir el frontend con la nueva configuración para que todo funcione completamente.

**Estado**: ✅ **ALB CONFIGURADO - PENDIENTE REBUILD FRONTEND**

