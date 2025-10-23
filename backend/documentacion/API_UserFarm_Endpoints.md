# 🚀 API Endpoints para Gestión de Usuario-Granja

## 📋 **Resumen de Endpoints Disponibles**

### **UserFarmController** (`/api/userfarm`)
- Gestión completa de asociaciones usuario-granja
- Endpoints para operaciones masivas
- Verificación de acceso

### **UsersController** (`/api/users`)
- Endpoints específicos para gestión de granjas por usuario
- Integración con el CRUD de usuarios existente

---

## 🔧 **Endpoints del UserFarmController**

### **1. Obtener Granjas de un Usuario**
```http
GET /api/userfarm/user/{userId}/farms
GET /api/userfarm/me/farms
```

**Ejemplo:**
```bash
curl -X GET "https://api.example.com/api/userfarm/user/123e4567-e89b-12d3-a456-426614174000/farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Respuesta:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "userName": "Juan Pérez",
  "farms": [
    {
      "userId": "123e4567-e89b-12d3-a456-426614174000",
      "farmId": 1,
      "userName": "Juan Pérez",
      "farmName": "Granja Norte",
      "isAdmin": true,
      "isDefault": true,
      "createdAt": "2025-01-15T10:30:00Z",
      "createdByUserId": "456e7890-e89b-12d3-a456-426614174001"
    }
  ]
}
```

### **2. Obtener Usuarios de una Granja**
```http
GET /api/userfarm/farm/{farmId}/users
```

**Ejemplo:**
```bash
curl -X GET "https://api.example.com/api/userfarm/farm/1/users" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### **3. Crear Asociación Usuario-Granja**
```http
POST /api/userfarm
```

**Ejemplo:**
```bash
curl -X POST "https://api.example.com/api/userfarm" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "farmId": 2,
    "isAdmin": false,
    "isDefault": false
  }'
```

### **4. Actualizar Asociación**
```http
PUT /api/userfarm/user/{userId}/farm/{farmId}
```

**Ejemplo:**
```bash
curl -X PUT "https://api.example.com/api/userfarm/user/123e4567-e89b-12d3-a456-426614174000/farm/2" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isAdmin": true,
    "isDefault": false
  }'
```

### **5. Eliminar Asociación**
```http
DELETE /api/userfarm/user/{userId}/farm/{farmId}
```

**Ejemplo:**
```bash
curl -X DELETE "https://api.example.com/api/userfarm/user/123e4567-e89b-12d3-a456-426614174000/farm/2" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### **6. Asociar Múltiples Granjas a un Usuario**
```http
POST /api/userfarm/user/{userId}/associate-farms
```

**Ejemplo:**
```bash
curl -X POST "https://api.example.com/api/userfarm/user/123e4567-e89b-12d3-a456-426614174000/associate-farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "farmIds": [1, 2, 3],
    "isAdmin": false,
    "isDefault": false
  }'
```

### **7. Asociar Múltiples Usuarios a una Granja**
```http
POST /api/userfarm/farm/{farmId}/associate-users
```

**Ejemplo:**
```bash
curl -X POST "https://api.example.com/api/userfarm/farm/1/associate-users" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "farmId": 1,
    "userIds": ["123e4567-e89b-12d3-a456-426614174000", "456e7890-e89b-12d3-a456-426614174001"],
    "isAdmin": false
  }'
```

### **8. Reemplazar Granjas de un Usuario**
```http
PUT /api/userfarm/user/{userId}/replace-farms
```

**Ejemplo:**
```bash
curl -X PUT "https://api.example.com/api/userfarm/user/123e4567-e89b-12d3-a456-426614174000/replace-farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '[1, 3, 5]'
```

### **9. Reemplazar Usuarios de una Granja**
```http
PUT /api/userfarm/farm/{farmId}/replace-users
```

**Ejemplo:**
```bash
curl -X PUT "https://api.example.com/api/userfarm/farm/1/replace-users" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '["123e4567-e89b-12d3-a456-426614174000", "789e0123-e89b-12d3-a456-426614174002"]'
```

### **10. Verificar Acceso**
```http
GET /api/userfarm/user/{userId}/farm/{farmId}/access
```

**Ejemplo:**
```bash
curl -X GET "https://api.example.com/api/userfarm/user/123e4567-e89b-12d3-a456-426614174000/farm/1/access" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Respuesta:**
```json
{
  "hasAccess": true
}
```

### **11. Obtener Granjas Accesibles**
```http
GET /api/userfarm/user/{userId}/accessible-farms
GET /api/userfarm/me/accessible-farms
```

**Ejemplo:**
```bash
curl -X GET "https://api.example.com/api/userfarm/me/accessible-farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

---

## 👤 **Endpoints del UsersController**

### **1. Obtener Granjas de un Usuario**
```http
GET /api/users/{userId}/farms
```

**Ejemplo:**
```bash
curl -X GET "https://api.example.com/api/users/123e4567-e89b-12d3-a456-426614174000/farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### **2. Asociar Granjas a un Usuario**
```http
POST /api/users/{userId}/farms
```

**Ejemplo:**
```bash
curl -X POST "https://api.example.com/api/users/123e4567-e89b-12d3-a456-426614174000/farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "farmIds": [1, 2, 3],
    "isAdmin": false,
    "isDefault": false
  }'
```

### **3. Reemplazar Granjas de un Usuario**
```http
PUT /api/users/{userId}/farms
```

**Ejemplo:**
```bash
curl -X PUT "https://api.example.com/api/users/123e4567-e89b-12d3-a456-426614174000/farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '[1, 3, 5]'
```

### **4. Eliminar Asociación Específica**
```http
DELETE /api/users/{userId}/farms/{farmId}
```

**Ejemplo:**
```bash
curl -X DELETE "https://api.example.com/api/users/123e4567-e89b-12d3-a456-426614174000/farms/2" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### **5. Actualizar Permisos**
```http
PATCH /api/users/{userId}/farms/{farmId}
```

**Ejemplo:**
```bash
curl -X PATCH "https://api.example.com/api/users/123e4567-e89b-12d3-a456-426614174000/farms/1" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isAdmin": true,
    "isDefault": true
  }'
```

### **6. Verificar Acceso**
```http
GET /api/users/{userId}/farms/{farmId}/access
```

### **7. Obtener Granjas Accesibles**
```http
GET /api/users/{userId}/accessible-farms
```

---

## 🔄 **Flujos de Trabajo Comunes**

### **Crear Usuario con Granjas**
```bash
# 1. Crear usuario
curl -X POST "https://api.example.com/api/users" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "surName": "Pérez",
    "firstName": "Juan",
    "cedula": "12345678",
    "telefono": "3001234567",
    "ubicacion": "Bogotá",
    "email": "juan@example.com",
    "password": "password123",
    "companyIds": [1],
    "roleIds": [1],
    "farmIds": [1, 2]
  }'

# 2. Verificar granjas asignadas
curl -X GET "https://api.example.com/api/users/{userId}/farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### **Asignar Granjas a Usuario Existente**
```bash
# 1. Asociar múltiples granjas
curl -X POST "https://api.example.com/api/users/{userId}/farms" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "{userId}",
    "farmIds": [1, 2, 3],
    "isAdmin": false,
    "isDefault": false
  }'

# 2. Marcar una como default
curl -X PATCH "https://api.example.com/api/users/{userId}/farms/1" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isDefault": true
  }'
```

### **Gestionar Permisos de Granja**
```bash
# 1. Otorgar permisos de admin
curl -X PATCH "https://api.example.com/api/users/{userId}/farms/{farmId}" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isAdmin": true
  }'

# 2. Verificar acceso
curl -X GET "https://api.example.com/api/users/{userId}/farms/{farmId}/access" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

---

## 📊 **Códigos de Respuesta**

| Código | Descripción |
|--------|-------------|
| `200 OK` | Operación exitosa |
| `201 Created` | Recurso creado exitosamente |
| `204 No Content` | Operación exitosa sin contenido |
| `400 Bad Request` | Error en la solicitud |
| `401 Unauthorized` | No autenticado |
| `404 Not Found` | Recurso no encontrado |

---

## 🔐 **Autenticación**

Todos los endpoints requieren autenticación JWT. Incluye el token en el header:

```http
Authorization: Bearer YOUR_JWT_TOKEN
```

---

## ⚠️ **Notas Importantes**

1. **Auditoría**: Todas las operaciones registran quién las realizó
2. **Validación**: Se valida que usuarios y granjas existan
3. **Unicidad**: No se permiten asociaciones duplicadas
4. **Cascada**: Al eliminar usuario/granja se eliminan las asociaciones
5. **Default**: Solo una granja puede ser default por usuario
6. **Acceso**: Se verifica acceso directo + acceso por compañía
