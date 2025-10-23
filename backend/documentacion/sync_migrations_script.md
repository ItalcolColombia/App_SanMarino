# Script para Sincronizar Migraciones

## Problema
La base de datos tiene tablas que no están reflejadas en las migraciones de Entity Framework, causando conflictos al intentar aplicar nuevas migraciones.

## Solución

### Opción 1: Crear la tabla manualmente y sincronizar

1. **Ejecutar el script SQL**:
   ```sql
   -- Ejecutar el contenido de create_produccion_avicola_raw_table.sql en tu base de datos
   ```

2. **Marcar la migración como aplicada sin ejecutarla**:
   ```bash
   dotnet ef database update --startup-project ../ZooSanMarino.API --script
   ```

3. **O usar el comando de sincronización**:
   ```bash
   dotnet ef migrations script --startup-project ../ZooSanMarino.API --output migration_script.sql
   ```

### Opción 2: Resetear las migraciones (CUIDADO: Pérdida de datos)

1. **Eliminar todas las migraciones**:
   ```bash
   dotnet ef migrations remove --startup-project ../ZooSanMarino.API
   # Repetir hasta eliminar todas
   ```

2. **Crear migración inicial**:
   ```bash
   dotnet ef migrations add InitialCreate --startup-project ../ZooSanMarino.API
   ```

3. **Aplicar migración**:
   ```bash
   dotnet ef database update --startup-project ../ZooSanMarino.API
   ```

### Opción 3: Forzar la aplicación (Recomendado)

1. **Crear la tabla manualmente** usando el script SQL
2. **Marcar la migración como aplicada**:
   ```bash
   dotnet ef database update --startup-project ../ZooSanMarino.API --force
   ```

## Recomendación
Usar la **Opción 1** es la más segura ya que no afecta las tablas existentes.
