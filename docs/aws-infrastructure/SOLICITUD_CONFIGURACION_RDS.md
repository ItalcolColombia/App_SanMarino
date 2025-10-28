# 🔐 Solicitud: Configuración de Seguridad para Conexión Backend → Base de Datos

## 📍 Ubicación de los Servicios

### Backend (API San Marino)
- **Región**: `us-east-2` (Ohio, USA)
- **Servicio**: ECS (Elastic Container Service)
- **IP Pública**: 3.137.143.15
- **Security Group**: `sg-8f1ff7fe`
- **Puerto**: 5002

### Base de Datos RDS
- **Región**: `us-east-2` (Ohio, USA) ✅ **Misma región**
- **Endpoint**: `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`
- **Database**: sanmarinoapp
- **Usuario**: postgres
- **Puerto**: 5432 (PostgreSQL)

---

## ⚠️ Problema Actual

**El backend NO puede conectarse a la base de datos** porque el Security Group (grupo de seguridad) del RDS no permite conexiones entrantes desde el Security Group del backend.

**Error que aparece**:
```
An exception has been raised that is likely due to a transient failure.
```

---

## ✅ Solución

Configurar el Security Group del RATIONAL de Datos para permitir conexiones desde el backend.

---

## 📋 Pasos para Configurar (AWS Console)

### Paso 1: Localizar el Security Group del RDS

1. Inicia sesión en AWS Console: https://console.aws.amazon.com/
2. Asegúrate de estar en la región **`us-east-2`** (Ohio, USA) - ver esquina superior derecha
3. Ve al servicio **RDS** (Reational Database Service)
4. En la lista de bases de datos, busca: `sanmarinoapp` o busca por el endpoint `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`
5. Click en la base de datos
6. Ve a la pestaña **"Connectivity & security"**
7. Busca la sección **"VPC security groups"**
8. Anota el **ID del Security Group** (formato: `sg-xxxxxxxxxxxxxxxxx`)

### Paso 2: Configurar la Regla de Entrada

1. Haz click en el **Security Group ID** que anotaste (esto te llevará a EC2 Security Groups)
2. Busca la sección **"Inbound rules"** (Reglas de entrada)
3. Click en **"Edit inbound rules"** (Editar reglas de entrada)
4. Click en **"Add rule"** (Agregar regla)
5. Configura la nueva regla:
   - **Type**: Selecciona `PostgreSQL` (o `Custom TCP`)
   - **Port range**: `5432`
   - **Source**: Selecciona `Custom` y luego ingresa el Security Group ID del backend: `sg-8f1ff7fe`
   - **Description** (opcional): `Allow connection from San Marino Backend ECS`
6. Click en **"Save rules"** (Guardar reglas)

### Paso 3: Verificar

Espera unos segundos y prueba el acceso desde el backend. Si todo está correcto, deberías poder acceder a la base de datos.

---

## 📊 Diagrama Visual

```
┌─────────────────────────────────────────────────────────────┐
│                     Región: us-east-2                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Backend ECS                           Base de Datos RDS   │
│  IP: 3.137.143.15:5002            Riche: sanmarinoapp...    │
│  Security Group: sg-8f1ff7fe      Security Group: ?         │
│                                    Puerto: 5432             │
│                                                              │
│              ❌ BLOQUEADO - Sin permiso de conexión         │
│                                                              │
│  Necesita: Regla en RDS Security Group                     │
│  Permitir: sg-8f1ff7fe → Puerto 5432                       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔍 Información Técnica para Referencia

### Security Groups Involucrados

**Backend ECS (Origen)**:
- **ID**: `sg-8f1ff7fe`
- **Tipo**: default VPC security group
- **Región**: us-east-2

**Base de Datos RDS (Destino)**:
- **ID**: Se obtiene en Paso 1 (desde la consola RDS)
- **Región**: us-east-2

### Regla Necesaria

| Campo | Valor |
|-------|-------|
| Type | PostgreSQL |
| Protocol | TCP |
| Port Range | 5432 |
| Source | sg-8f1ff7fe (Security Group del backend) |
| Description | Allow connection from San Marino Backend ECS |

---

## ✅ Verificación de Éxito

Una vez configurado correctamente, el backend podrá:

1. ✅ Conectarse a la base de datos
2. ✅ Autenticar usuarios (login)
3. ✅ Realizar consultas
4. ✅ Guardar cliqueas

**Prueba de conexión** (para verificar):
```bash
curl -X POST http://3.137.143.15:5002/api/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email": "moiesbbuga@gmail.com", "password": "Moi$3177120174", "companyId": 0}'
```

Si la configuración es correcta, debería devolver un token JWT en lugar del error "transient failure".

---

## 🆘 ¿Necesitas Ayuda?

Si tienes dudas o problemas durante la configuración:

1. Verifica que estás en la región correcta: **us-east-2**
2. Verifica que el Security Group ID del RDS sea correcto
3. Verifica que agregaste el Security Group del backend (`sg-8f1ff7fe`) y no una IP específica

---

## 📝 Nota Importante

Esta es una configuración de seguridad estándar en AWS. El Security Group actúa como un "firewall" que controla el tráfico de red entrante y saliente. La configuración solicitada permite **solo** al backend ECS conectarse al RDS en el puerto 5432, manteniendo la seguridad de la base de datos.

---

**Fecha de solicitud**: 27 de octubre de 2025  
**Urgencia**: Alta - Bloquea el funcionamiento del backend  
**Tiempo estimado**: 5-10 minutos

