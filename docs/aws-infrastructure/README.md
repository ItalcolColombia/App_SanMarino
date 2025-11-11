# 📚 Documentación AWS Infrastructure - San Marino

Este directorio contiene toda la documentación relacionada con la infraestructura de AWS para el proyecto San Marino.

## 📋 Categorías

### 🔧 Configuración y Despliegue
- `CONFIGURACION_SEGURA.md` - Configuración segura del entorno
- `GUIA_CONFIGURACION_SEGURA.md` - Guía de configuración segura
- `GUIA_DESPLIEGUE_PRODUCCION.md` - Guía de despliegue a producción
- `DESPLIEGUE_COMPLETO_SAN_MARINO.md` - Resumen del despliegue completo

### 🔌 RDS (Base de Datos)
- `ACTUALIZAR_RDS_SECURITY_GROUP.md` - Actualizar Security Group de RDS
- `ACTUALIZAR_SECURITY_GROUP_RDS_US_EAST_2.md` - Configuración en us-east-2
- `CONFIGURAR_RDS_POR_COMANDO.md` - Configuración por comandos AWS CLI
- `CONFIGURAR_RDS_SECURITY_GROUP_US_EAST_1.md` - Configuración en us-east-1
- `PROBLEMA_CONECTIVIDAD_RDS.md` - Problemas de conectividad con RDS
- `RESUMEN_CONFIGURACION_BD_NUEVA.md` - Resumen configuración BD nueva
- `SOLUCION_RDS_CONEXION.md` - Solución para conexión RDS

### ⚖️ ALB (Application Load Balancer)
- `ALB_RESUMEN_DESPLIEGUE.md` - Resumen del despliegue ALB
- `CONFIGURACION_FRONTEND_BACKEND_ALB.md` - Configuración Frontend-Backend vía ALB
- `frontend/CONFIGURACION_ALB.md` - Configuración específica del ALB

### 🚀 Despliegue ECS
- `frontend/DESPLIEGUE_ECS.md` - Despliegue Frontend en ECS
- `frontend/DESPLIEGUE_EXITOSO.md` - Despliegue exitoso del frontend
- `RESUMEN_BACKEND_US_EAST_2.md` - Resumen backend en us-east-2
- `ESTADO_FINAL_BACKEND_US_EAST_1.md` - Estado final backend en us-east-1

### 🌍 Migración Entre Regiones
- `INSTRUCCIONES_MOVER_A_US_EAST_1.md` - Instrucciones para mover a us-east-1
- `RESUMEN_MIGRACION_US_EAST_1.md` - Resumen de migración a us-east-1

### 🛠️ Comandos AWS CLI
- `RESUMEN_COMANDOS_AWS_CLI.md` - Resumen de comandos AWS CLI

### 📞 Acceso y URLs
- `ACCESO_BACKEND.md` - URLs de acceso al backend

### 📨 Solicitudes de Infraestructura
- `EXPLICACION_SIMPLE_PROBLEMA_SOLUCION.md` - Explicación simple de problema/solución
- `SOLICITUD_CONFIGURACION_RDS.md` - Solicitud para configurar RDS Security Group
- `SOLICITUD_CONFIGURAR_ALB_TARGET_GROUP.md` - Solicitud para configurar ALB Target Group

---

## 🎯 Archivos Prioritarios

### Para Resolver Problemas Actuales

1. **`SOLICITUD_CONFIGURACION_RDS.md`** (URGENTE)
   - Configurar Security Group del RDS para permitir conexiones desde el backend
   - Bloquea el funcionamiento del backend

2. **`SOLICITUD_CONFIGURAR_ALB_TARGET_GROUP.md`** (MEDIO)
   - Registrar el backend en el Target Group del ALB
   - Necesario para que el frontend acceda al backend vía ALB

### Para Desarrollo

- `CONFIGURACION_FRONTEND_BACKEND_ALB.md` - Entender cómo funciona la configuración ALB
- `ACCESO_BACKEND.md` - URLs para acceder al backend
- `RESUMEN_COMANDOS_AWS_CLI.md` - Comandos útiles para AWS

---

## 📝 Notas

- Esta documentación está organizada por temas relacionados con infraestructura AWS
- Todos los archivos relacionados con AWS ECS, RDS, ALB, Security Groups, etc. están aquí
- La documentación del proyecto (APIs, funcionalidades) está en `docs/project-documentation/`

---

**Última actualización**: Octubre 2025  
**Región principal**: us-east-2 (Ohio)


