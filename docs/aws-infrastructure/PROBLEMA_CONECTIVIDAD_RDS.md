# ⚠️ Problema de Conectividad con RDS

## Actual error

```
Failed to connect to 10.4.6.6:5432
System.TimeoutException: The operation has timed out.
```

## Problema raíz

### Configuración actual

| Componente | Región | Estado |
|-----------|--------|--------|
| **Backend ECS** | us-east-2 | ✅ Activo |
| **RDS** | us-east-1 | ❌ No accesible |
| **Frontend ECS** | us-east-2 | ✅ Activo |

**Problema**: El backend (us-east-2) no puede conectarse al RDS (us-east-1) porque están en regiones diferentes.

### Detalles

El endpoint `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com` se resuelve a una IP privada (10.4.6.6) que pertenece a la VPC de us-east-1, pero el backend está en la VPC de us-east-2. Estas VPCs no tienen conectividad entre sí.

## Opciones

Tres opciones:

### Opción 1: Mover el RDS a us-east-2 (recomendado si es posible)

1. Crear una snapshot del RDS en us-east-1
2. Copiar la snapshot a us-east-2
3. Restaurar el RDS en us-east-2
4. Actualizar el endpoint en la configuración

**Pros**:
- Todo en la misma región
- Mejor latencia
- Costos de transferencia menores

**Contras**:
- Requiere tiempo de migración
- Downtime temporal

### Opción 2: Configurar VPC Peering o Transit Gateway

Conectar las VPCs de us-east-1 y us-east-2 mediante:
- VPC Peering (cross-region)
- AWS Transit Gateway

**Pros**:
- Sin mover datos
- Solución rápida

**Contras**:
- Complejidad adicional
- Mantenimiento
- Costos de transferencia entre regiones

### Opción 3: Mover Backend a us-east-1

Mover los servicios ECS a us-east-1.

**Pros**:
- Todo en una región
- Sin migración de datos

**Contras**:
- Reconfiguración
- Posible impacto en ALB

## Acción inmediata

Verificar si el RDS puede estar en us-east-2:

1. Confirma si es viable usar us-east-2:
   ```bash
   # Verificar si existe el RDS en us-east-2
   aws rds describe-db-instances --region us-east-2 --query 'DBInstances[*].[DBInstanceIdentifier,Engine,EngineVersion]' --output table
   ```

2. Si no, define si aplica VPC Peering o mover el backend.

## Nota

Las regiones us-east-1 y us-east-2 son diferentes AWS regions; no hay conectividad directa entre VPCs en distintas regiones.

---

**Estado actual**: ❌ Backend no puede conectarse a RDS por incompatibilidad de regiones  
**Acción requerida**: Decisión sobre la opción a implementar

