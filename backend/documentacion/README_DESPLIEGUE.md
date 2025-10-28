# 📚 Documentación de Despliegue - Backend San Marino

## 🎯 Índice de Documentación

Esta carpeta contiene toda la documentación relacionada con el despliegue del backend de San Marino en AWS.

---

## 📖 Documentos Principales

### 1. 🚀 Inicio Rápido
**[RESUMEN_DESPLIEGUE.md](./RESUMEN_DESPLIEGUE.md)**
- Resumen ejecutivo del despliegue
- Estado actual
- Referencias rápidas

### 2. ✅ Despliegue Exitoso
**[DESPLIEGUE_EXITOSO_AWS.md](./DESPLIEGUE_EXITOSO_AWS.md)**
- Documentación completa del despliegue
- Configuración de infraestructura
- Comandos útiles
- Problemas resueltos

### 3. 📝 Instrucciones Detalladas
**[INSTRUCCIONES_DESPLIEGUE.md](./INSTRUCCIONES_DESPLIEGUE.md)**
- Guía paso a paso
- Configuración de RDS
- Verificación y troubleshooting

### 4. 📋 Requisitos de Migración
**[REQUISITOS_MIGRACION_AWS_NUEVO.md](./REQUISITOS_MIGRACION_AWS_NUEVO.md)**
- Información requerida del AWS
- Componentes necesarios
- Configuración de infraestructura

### 5. 📊 Estado de Configuración
**[ESTADO_CONFIGURACION_AWS.md](./ESTADO_CONFIGURACION_AWS.md)**
- Estado actual de AWS
- Comparación con AWS anterior
- Acciones realizadas

### 6. ☑️ Checklist de Migración
**[CHECKLIST_MIGRACION_AWS.md](./CHECKLIST_MIGRACION_AWS.md)**
- Template para recopilar información
- Orden de ejecución
- Comandos útiles

---

## 🛠️ Scripts de Despliegue

Ubicación: `backend/scripts/`

### Script Principal (Recomendado)
```bash
scripts/deploy-backend-ecs.sh
```
**Uso**:
```bash
cd backend/scripts
./deploy-backend-ecs.sh
```

### Script PowerShell
```bash
scripts/deploy-new-aws.ps1
```

---

## 📁 Archivos de Configuración

### Task Definition
**Ubicación**: `backend/documentacion/ecs-taskdef-new-aws.json`

Contiene la configuración completa de la Task Definition para el nuevo AWS.

---

## 🔧 Configuración Actual

| Parámetro | Valor |
|-----------|-------|
| Account ID | 196080479890 |
| Región | us-east-2 |
| Cluster | devSanmarinoZoo |
| Service | sanmarino-back-task-service-75khncfa |
| Puerto | 5002 |
| Estado | ✅ OPERATIVO |

---

## 🚀 Inicio Rápido

### Para Desplegar el Backend

1. **Leer primero**: [RESUMEN_DESPLIEGUE.md](./RESUMEN_DESPLIEGUE.md)
2. **Ejecutar script**:
   ```bash
   cd backend/scripts
   ./deploy-backend-ecs.sh
   ```
3. **Verificar**: Revisa los logs en CloudWatch

### Para Migrar a un Nuevo AWS

1. **Leer**: [REQUISITOS_MIGRACION_AWS_NUEVO.md](./REQUISITOS_MIGRACION_AWS_NUEVO.md)
2. **Completar**: [CHECKLIST_MIGRACION_AWS.md](./CHECKLIST_MIGRACION_AWS.md)
3. **Seguir**: [INSTRUCCIONES_DESPLIEGUE.md](./INSTRUCCIONES_DESPLIEGUE.md)

---

## 📞 Referencias

- **README Principal**: `../README.md`
- **Scripts**: `../scripts/`
- **Dockerfile**: `../Dockerfile`

