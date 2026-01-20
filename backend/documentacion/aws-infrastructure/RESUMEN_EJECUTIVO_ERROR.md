# Resumen Ejecutivo: Error de Conectividad Backend-RDS

## 🎯 Problema en Una Línea
El backend en ECS (us-east-2) no puede conectarse al RDS (us-east-1) porque falta una regla de seguridad.

## 🔍 Diagnóstico Rápido

**Backend:**
- Security Group: `sg-8f1ff7fe` (us-east-2)
- ✅ Puede salir (reglas de salida correctas)

**RDS:**
- Endpoint: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- ❌ No permite entrada desde el backend

## ✅ Solución (2 Pasos)

### Paso 1: Obtener Security Group de RDS
1. Consola AWS → RDS → Región **us-east-1**
2. Databases → Instancia `reproductoras-pesadas...`
3. Pestaña "Connectivity & security" → Anotar Security Group ID

### Paso 2: Ejecutar este comando
```bash
aws ec2 authorize-security-group-ingress \
  --group-id <RDS_SECURITY_GROUP_ID> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-1
```

**Nota:** También verificar que RDS sea "Publicly accessible" (regiones diferentes).

## 📋 Verificación

```bash
# 1. Verificar regla
aws ec2 describe-security-groups \
  --group-ids <RDS_SECURITY_GROUP_ID> \
  --region us-east-1 \
  --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' \
  --output table

# 2. Reiniciar backend
aws ecs update-service \
  --cluster devSanmarinoZoo \
  --service sanmarino-back-task-service-75khncfa \
  --force-new-deployment \
  --region us-east-2
```

## 📄 Documentación Completa
Ver: `REPORTE_ERROR_CONECTIVIDAD_RDS.md` para pasos detallados.

