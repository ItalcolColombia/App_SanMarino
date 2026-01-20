# Endpoints de Gestión Empresa-País

## Base URL
`/api/CompanyPais`

## Endpoints Disponibles

### 1. Obtener todas las relaciones empresa-país
**GET** `/api/CompanyPais`

Retorna todas las combinaciones empresa-país existentes.

**Respuesta:**
```json
[
  {
    "companyId": 1,
    "companyName": "Empresa A",
    "paisId": 1,
    "paisNombre": "Colombia",
    "isDefault": false
  }
]
```

---

### 2. Obtener empresas por país
**GET** `/api/CompanyPais/pais/{paisId}/companies`

Retorna todas las empresas asignadas a un país específico.

**Parámetros:**
- `paisId` (int): ID del país

**Respuesta:**
```json
[
  {
    "id": 1,
    "name": "Empresa A",
    "identifier": "123456",
    "documentType": "NIT",
    ...
  }
]
```

---

### 3. Obtener países por empresa
**GET** `/api/CompanyPais/company/{companyId}/paises`

Retorna todos los países asignados a una empresa específica.

**Parámetros:**
- `companyId` (int): ID de la empresa

**Respuesta:**
```json
[
  {
    "paisId": 1,
    "paisNombre": "Colombia"
  }
]
```

---

### 4. Obtener combinaciones empresa-país de un usuario
**GET** `/api/CompanyPais/user/{userId}`

Retorna todas las combinaciones empresa-país asignadas a un usuario.

**Parámetros:**
- `userId` (Guid): ID del usuario

**Respuesta:**
```json
[
  {
    "companyId": 1,
    "companyName": "Empresa A",
    "paisId": 1,
    "paisNombre": "Colombia",
    "isDefault": true
  }
]
```

---

### 5. Obtener combinaciones del usuario actual
**GET** `/api/CompanyPais/user/current`

Retorna las combinaciones empresa-país del usuario autenticado.

**Respuesta:** Igual que el endpoint anterior.

---

### 6. Asignar empresa a país
**POST** `/api/CompanyPais/assign`

Asigna una empresa a un país (crea la relación empresa-país).

**Body:**
```json
{
  "companyId": 1,
  "paisId": 1
}
```

**Respuesta (201 Created):**
```json
{
  "companyId": 1,
  "companyName": "Empresa A",
  "paisId": 1,
  "paisNombre": "Colombia",
  "isDefault": false
}
```

**Errores:**
- `400 Bad Request`: Si la empresa o país no existe, o si la relación ya existe

---

### 7. Remover empresa de país
**DELETE** `/api/CompanyPais/remove`

Remueve la asignación de una empresa a un país.

**Body:**
```json
{
  "companyId": 1,
  "paisId": 1
}
```

**Respuesta:**
- `204 No Content`: Si se removió exitosamente
- `404 Not Found`: Si la relación no existe
- `400 Bad Request`: Si hay usuarios asignados a esa combinación

---

### 8. Asignar usuario a empresa-país
**POST** `/api/CompanyPais/user/assign`

Asigna un usuario a una empresa en un país específico.

**Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "companyId": 1,
  "paisId": 1,
  "isDefault": false
}
```

**Respuesta (201 Created):**
```json
{
  "companyId": 1,
  "companyName": "Empresa A",
  "paisId": 1,
  "paisNombre": "Colombia",
  "isDefault": false
}
```

**Errores:**
- `400 Bad Request`: Si la empresa no está asignada al país, si el usuario no existe, o si la relación ya existe

**Nota:** Si `isDefault` es `true`, se desmarcarán automáticamente otros defaults del usuario.

---

### 9. Remover usuario de empresa-país
**DELETE** `/api/CompanyPais/user/remove`

Remueve la asignación de un usuario a una empresa-país.

**Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "companyId": 1,
  "paisId": 1
}
```

**Respuesta:**
- `204 No Content`: Si se removió exitosamente
- `404 Not Found`: Si la relación no existe

---

### 10. Validar relación empresa-país
**POST** `/api/CompanyPais/validate`

Valida que una empresa pertenece a un país.

**Body:**
```json
{
  "companyId": 1,
  "paisId": 1
}
```

**Respuesta:**
```json
{
  "isValid": true,
  "companyId": 1,
  "paisId": 1
}
```

## Flujo de Trabajo Recomendado

### 1. Configuración Inicial
```bash
# 1. Crear países
POST /api/Pais
{
  "paisNombre": "Colombia"
}

# 2. Crear empresas
POST /api/Company
{
  "name": "Empresa A",
  ...
}

# 3. Asignar empresas a países
POST /api/CompanyPais/assign
{
  "companyId": 1,
  "paisId": 1
}
```

### 2. Asignar Usuarios
```bash
# Asignar usuario a empresa-país
POST /api/CompanyPais/user/assign
{
  "userId": "...",
  "companyId": 1,
  "paisId": 1,
  "isDefault": true
}
```

### 3. Consultas
```bash
# Ver empresas de un país
GET /api/CompanyPais/pais/1/companies

# Ver países de una empresa
GET /api/CompanyPais/company/1/paises

# Ver combinaciones de un usuario
GET /api/CompanyPais/user/{userId}
```

## Notas Importantes

1. **Validaciones:**
   - No se puede remover una relación empresa-país si hay usuarios asignados
   - La empresa debe estar asignada al país antes de asignar usuarios
   - Solo puede haber una combinación default por usuario

2. **Seguridad:**
   - Todos los endpoints requieren autenticación
   - El endpoint `/user/current` usa el usuario autenticado automáticamente

3. **Errores Comunes:**
   - Intentar asignar usuario a empresa-país que no existe: `400 Bad Request`
   - Intentar remover empresa-país con usuarios: `400 Bad Request`
   - Usuario no autenticado: `401 Unauthorized`





