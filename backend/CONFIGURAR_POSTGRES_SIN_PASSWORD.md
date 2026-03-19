# Configurar PostgreSQL para conexión local SIN contraseña (puerto 5433)

Para que la app se conecte a `localhost:5433` sin contraseña, hay que usar **trust** en `pg_hba.conf` para conexiones locales.

## 1. Ubicar el archivo pg_hba.conf

- Si usas **PostgreSQL 13** en la ruta por defecto:
  ```
  C:\Program Files\PostgreSQL\13\data\pg_hba.conf
  ```
- Si el puerto 5433 es otra instalación o versión, busca la carpeta `data` de esa instancia (o revisa el servicio de Windows “PostgreSQL” para ver su ruta).

**Abrir como administrador:** clic derecho en el Bloc de notas → “Ejecutar como administrador” → Abrir el archivo `pg_hba.conf`.

## 2. Cambiar autenticación a trust para localhost

Al final del archivo verás líneas como:

```
# IPv4 local connections:
host    all             all             127.0.0.1/32            md5
# IPv6 local connections:
host    all             all             ::1/128                 md5
```

**Cámbialas** (o añade si no están) a **trust**:

```
# IPv4 local connections:
host    all             all             127.0.0.1/32            trust
# IPv6 local connections:
host    all             all             ::1/128                 trust
```

Guarda el archivo.

## 3. Reiniciar el servicio de PostgreSQL

- Abre **Servicios** (Win + R → `services.msc`).
- Busca el servicio de PostgreSQL (por ejemplo **postgresql-x64-13** o el que use el puerto 5433).
- Clic derecho → **Reiniciar**.

## 4. Probar la conexión

En PowerShell:

```powershell
& "C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -p 5433 -U postgres -d postgres -w -c "SELECT 1 as ok;"
```

Si no pide contraseña y devuelve `ok = 1`, la conexión sin contraseña está funcionando. La app ya puede usar la cadena con `Password=;` en `appsettings.Development.json`.

## Seguridad

- **trust** solo debe usarse en desarrollo local.
- No uses trust en un servidor accesible desde la red ni en producción.
