# Optimización del build y arranque local de .NET 10

Fecha: 25-sep-2026
Estado: completado

## Objetivo

Reducir el tiempo y la presión de memoria al levantar el backend local sin requerir permisos de
administrador y sin modificar el comportamiento de producción.

## Diagnóstico confirmado

- Windows resuelve primero el SDK 9.0.301 de `Program Files`; el SDK 10.0.301 está instalado en
  `%USERPROFILE%\.dotnet` y funciona correctamente.
- El arranque puro de la API tarda aproximadamente 3,55 segundos.
- Una recompilación de Infrastructure procesa 220 MB de C#, de los cuales 210,85 MB corresponden a
  862 archivos de migraciones, y puede elevar `csc.exe` por encima de 11 GB privados.
- `dev-back.ps1` usa `dotnet run` con compilación y migraciones automáticas en cada inicio.

## Cambios

1. Agregar `global.json` para declarar SDK 10.0.301 y `rollForward` a parches compatibles.
2. Actualizar `dev-back.ps1` para:
   - invocar directamente el SDK user-local;
   - restaurar solo cuando los assets estén ausentes o desactualizados;
   - compilar en un único proceso, sin compilador compartido ni analizadores en el ciclo local;
   - ejecutar directamente la DLL ya compilada, sin un segundo build ni restore;
   - omitir migraciones y el bootstrap DDL heredado por defecto, habilitándolos con `-Migrate`;
   - permitir reinicios instantáneos con `-NoBuild`.
3. Exponer `make dev-back-fast` y `make dev-back-migrate`, conservando `make dev-back` como opción
   segura que valida el build incremental.
4. Documentar los modos y la limitación del SDK instalado sin privilegios de administrador.

## Base de datos

- No hay cambios de esquema ni migraciones nuevas.
- Producción conserva su configuración y ejecución automática de migraciones.
- Solo cambia el flujo local: las migraciones se ejecutan al solicitar `make dev-back-migrate`.

## Validación

- Verificar selección del SDK 10 mediante el script.
- Validar sintaxis y comportamiento de los tres modos.
- Medir build incremental y arranque hasta que el puerto quede disponible.
- Ejecutar build backend y confirmar que no quedan procesos ni puertos abiertos.
