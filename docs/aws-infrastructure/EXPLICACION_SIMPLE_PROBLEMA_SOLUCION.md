# 📝 Explicación Simple del Problema y Solución

## 🎯 Para: Equipo de Infraestructura / DevOps
### 📅 Fecha: 27 de octubre de 2025

---

## ¿Cuál es el problema?

El backend de la aplicación San Marino **NO puede conectarse a la base de datos**.

**Síntoma**: Cuando alguien intenta hacer login, aparece un error:
```
"An exception has been raised that is likely due to a transient failure."
```

---

## ¿Por qué está pasando?

Es un problema de configuración de seguridad de red en AWS (Security Groups).

- El **backend** y la **base de datos** están en la misma región (us-east-2) ✅
- La **base de datos** tiene un "firewall" (Security Group) que **NO permite** conexiones desde el backend ❌
- Necesitamos configurar el firewall para permitir la conexión ✅

---

## ¿Qué hay que hacer?

**Agregar una regla en el Security Group de la base de datos** que permita conexiones desde el Security Group del backend.

Es como abrir una puerta en un firewall para que el backend pueda "hablar" con la base de datos.

---

## 📍 Ubicaciones

### Backend (API)
- **Región**: us-east-2
- **IP**: 3.137.143.15:5002
- **Security Group**: `sg-8f1ff7fe`

### Base de Datos
- **Región**: us-east-2 (misma que el backend ✅)
- **Nombre**: sanmarinoapp
- **Endpoint**: sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com
- **Puerto**: 5432
- **Security Group**: Se obtiene de la consola RDS

---

## 🔧 Pasos a Seguir

### 1. Abrir Consola AWS
https://console.aws.amazon.com/  
Asegurarse de estar en región: **us-east-2**

### 2. Ir a RDS
Servicio RDS → Buscar base de datos `sanmarinoapp`

### 3. Ver Security Group
Click en la base de datos → Tab "Connectivity & security" → Anotar el **VPC security groups** ID

### 4. Editar Security Group
Click en el Security Group ID → "Inbound rules" → "Edit inbound rules" → "Add rule"

### 5. Configurar Regla
- **Type**: PostgreSQL
- **Port**: 5432
- **Source**: Seleccionar "Security Group" → Ingresar `sg-8f1ff7fe`
- **Description**: "Allow from San Marino Backend ECS"
- Guardar reglas

### 6. Listo ✅
Después de guardar, el backend podrá conectarse a la base de datos automáticamente.

---

## ⏱️ Tiempo Estimado

**5-10 minutos**

Es una operación rutinaria de configuración de seguridad en AWS.

---

## 🎯 Resultado Esperado

Después de hacer los cambios:
- ✅ El backend podrá conectarse a la base de datos
- ✅ Los usuarios podrán hacer login
- ✅ La aplicación funcionará normalmente
- ✅ No habrá más errores de "transient failure"

---

## 📞 Si hay problemas

Ver el documento detallado: `SOLICITUD_CONFIGURACION_RDS.md`

---

**Gracias por tu ayuda** 🙏

