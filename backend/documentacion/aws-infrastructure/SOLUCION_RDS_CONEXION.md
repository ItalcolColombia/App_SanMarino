# ✅ Solución: Configurar Conexión Backend a RDS

## 🎯 Información Exacta Necesaria

### Backend en us-east-1
- **IP Pública**: 44.203.245.250
- **IP Privada**: 172.31.1.63
- **Security Group ECS**: `sg-0c6a91db2ba4b872f`
- **Puerto**: 5002

### RDS en us-east-1
- **Endpoint**: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- **Puerto**: 5432
- **Database**: sanmarinoappdev223
- **Usuario**: repropesa01

---

## 🔧 Solución (Dos Opciones)

### ⭐ Opción 1: Usar Security Group (Recomendado)

**Ventaja**: Funciona aunque cambie la IP del backend.

1. Ve a AWS Console → EC2 → Security Groups → **us-east-1**
2. Busca el Security Group del RDS (el asociado a `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`)
3. Selecciona el Security Group
4. **Inbound rules** → **Edit inbound rules**
5. **Add rule**:
   - **Type**: PostgreSQL
   - **Port**: 5432
   - **Source**: Custom → Selecciona: `sg-0c6a91db2ba4b872f`
   - **Description**: ECS San Marino Backend
6. **Save rules**

### Opción 2: Usar IP Privada (Temporal)

**Nota**: Esta IP puede cambiar si la tarea se reinicia.

1. Ve a AWS Console → EC2 → Security Groups → **us-east-1**
2. Busca el Security Group del RDS
3. **Add rule**:
   - **Type**: PostgreSQL
   - **Port**: 5432
   - **Source**: 172.31.1.63/32
   - **Description**: Backend San Marino IP Privada

---

## ✅ Verificación

Después de configurar el Security Group:

```bash
# Probar login
curl -X POST http://44.203.245.250:5002/api/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email": "moiesbbuga@gmail.com", "password": "Moi$3177120174", "companyId": 0}'
```

**Debería devolver un token JWT** (no el error de "transient failure").

---

## 🔍 Cómo Encontrar el Security Group del RDS

Si no estás seguro de cuál es el Security Group:

1. Ve a **RDS Console** → **Databases** → **us-east-1**
2. Busca: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
3. Ve a **Connectivity & security**
4. Anota el **VPC security groups**
5. Ve a EC2 → Security Groups y busca ese ID

---

## 📝 Nota Importante

**Recomendación**: Usa la **Opción 1** (Security Group), no la IP, porque:
- Las IPs de ECS cambian cuando se reinicia el servicio
- El Security Group es permanente
- Mejor práctica de seguridad

---

**Una vez configurado el Security Group, el backend se conectará automáticamente a la base de datos.** 🎉

