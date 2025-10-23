# Configuración de Base de Datos Remota

## 📋 Pasos para Configurar la Conexión Remota

### 1. **Crear archivo .env**
Crea un archivo `.env` en la raíz del proyecto con tu configuración:

```bash
# Cadena de conexión a PostgreSQL remoto
ZOO_CONN=Host=tu-servidor-remoto;Port=5432;Database=tu-base-datos;Username=tu-usuario;Password=tu-password;

# Configuración JWT
JWT_KEY=tu-clave-secreta-muy-larga-y-segura-de-al-menos-32-caracteres
JWT_ISSUER=ZooSanMarino
JWT_AUDIENCE=ZooSanMarinoAPI
JWT_DURATION=120

# Puerto de la API
PORT=5002
```

### 2. **Actualizar appsettings.json**
Reemplaza la cadena de conexión en `src/ZooSanMarino.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ZooSanMarinoContext": "Host=tu-servidor-remoto;Port=5432;Database=tu-base-datos;Username=tu-usuario;Password=tu-password;"
  }
}
```

### 3. **Aplicar Migraciones**
Una vez configurada la conexión, ejecuta:

```bash
# Desde la carpeta src/ZooSanMarino.Infrastructure
dotnet ef database update
```

### 4. **Verificar Conexión**
Puedes probar la conexión con:

```bash
# Desde la carpeta src/ZooSanMarino.API
dotnet run
```

Luego visita: `http://localhost:5002/health`

## 🔧 Formato de Cadena de Conexión PostgreSQL

```
Host=servidor;Port=5432;Database=nombre_bd;Username=usuario;Password=contraseña;
```

### Parámetros Opcionales:
- `SSL Mode=Require` - Para conexiones seguras
- `Timeout=30` - Timeout de conexión
- `Command Timeout=30` - Timeout de comandos

## ✅ Verificación

Una vez configurada la conexión, deberías poder:

1. ✅ Ejecutar migraciones sin errores
2. ✅ Iniciar la API sin errores de conexión
3. ✅ Acceder a los endpoints de la API
4. ✅ Ver la documentación en Swagger: `http://localhost:5002/swagger`

## 🚨 Notas Importantes

- **Seguridad**: Nunca subas el archivo `.env` al repositorio
- **Permisos**: Asegúrate de que el usuario de la BD tenga permisos de CREATE, ALTER, etc.
- **Red**: Verifica que el servidor remoto sea accesible desde tu máquina
- **Puerto**: Confirma que el puerto 5432 (o el que uses) esté abierto
