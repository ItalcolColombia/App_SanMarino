# Corrección de vulnerabilidad en System.Security.Cryptography.Xml

Fecha: 25-sep-2026

## Objetivo

Eliminar las alertas NU1903 asociadas a `System.Security.Cryptography.Xml 9.0.3` sin deshabilitar
la auditoría de NuGet, sin degradar .NET 10 y sin cambiar el comportamiento productivo del módulo
Gestión veterinaria ni del resto del backend.

## Enfoque

1. Obtener la cadena transitiva con `dotnet list package --include-transitive` y confirmar si el
   paquete vulnerable pertenece sólo a pruebas o también al runtime.
2. Revisar versiones resueltas y compatibilidad del paquete padre con .NET 10.
3. Aplicar la actualización mínima: preferir actualizar el paquete directo que origina la cadena;
   si no existe versión compatible, fijar explícitamente una versión corregida del transitivo.
4. Regenerar `project.assets.json` mediante restore normal, sin editar lockfiles a mano.
5. Validar con auditoría de paquetes, build completo y suite completa de tests.

## Archivos previstos

- `backend/tests/ZooSanMarino.Infrastructure.Tests/ZooSanMarino.Infrastructure.Tests.csproj` o el
  `.csproj` que el diagnóstico identifique como origen.
- Este plan y el bloque propio del `tracker_estado.md`.

## Base de datos y comportamiento

- Sin cambios de BD, migraciones, endpoints ni lógica de negocio.
- No se aplicará DDL ni se hará deploy.

## Casos de validación

- La auditoría no debe reportar `System.Security.Cryptography.Xml 9.0.3` ni NU1903 asociados.
- La solución debe compilar en .NET 10.
- Todas las pruebas existentes deben continuar verdes.
- Puertos locales de backend/frontend/runners deben quedar libres.
