# 🔒 Actualizar Security Group RDS - us-east-2

## ⚠️ El backend regresó a us-east-2

### Nueva Configuración

- **Backend IP**: 3.137.143.15 (pública)
- **Backend IP Privada**: 172.31.40.91
- **RDS**: sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com
- **Región**: us-east-2

---

## 🔧 Configurar Security Group del RDS

### Opción 1: Usar Security Group de ECS (Recomendado)

El backend usa el Security Group: `sg-8f1ff7fe` ✅

**Pasos**:
1. AWS Console → EC2 → Security Groups → **us-east-2**
2. Busca el Security Group del RDS `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`
3. **Edit inbound rules** → **Add rule**:
   - **Type**: PostgreSQL
   - **Port**: 5432
   - **Source**: `sg-8f1ff7fe`
   - **Description**: Allow ECS San Marino Backend
4. **Save rules**

### Opción 2: Usar IP Privada (Temporal)

Si prefieres usar IP específica:

1. **Edit inbound rules** → **Add rule**:
   - **Type**: PostgreSQL
   - **Port**: 5432
   - **Source**: 172.31.40.91/32
   - **Description**: Backend San Marino IP Privada
2. **Save rules**

---

## ✅ Verificación

Después de configurar:

```bash
curl -X POST http://3.137.143.15:5002/api/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email": "moiesbbuga@gmail.com", "password": "Moi$3177120174", "companyId": 0}'
```

**Debería devolver**: Un token JWT (no error "transient failure").

---

**Backend desplegado** ✅ - Falta configurar Security Group del RDS en **us-east-2**.

