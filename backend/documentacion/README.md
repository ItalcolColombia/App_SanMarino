# 📚 Documentación del Proyecto ZooSanMarino Backend

Bienvenido a la documentación completa del sistema backend de ZooSanMarino. Este directorio contiene toda la documentación técnica, guías de implementación y referencias del API.

## 📋 Índice de Documentación

### 🏗️ Arquitectura y Diseño
- [**Arquitectura Hexagonal**](arquitectura-hexagonal.md) - Descripción del patrón arquitectónico implementado
- [**Estructura del Proyecto**](estructura-proyecto.md) - Organización de carpetas y componentes

### 🔧 Funcionalidades Implementadas
- [**Sistema de Liquidación Técnica**](liquidacion-tecnica.md) - Cálculos automáticos para lotes de levante
- [**Importación de Excel**](excel-import.md) - Sistema de carga masiva de datos
- [**CRUD Producción Avícola**](produccion-avicola-crud.md) - Gestión de datos de producción
- [**Sistema de Traslados de Aves**](frontend-traslados-aves.md) - Gestión completa de inventario y movimientos

### 📡 API Reference
- [**Endpoints de Liquidación Técnica**](api-liquidacion-tecnica.md) - Documentación completa del API
- [**Endpoints de Excel Import**](api-excel-import.md) - Documentación de importación
- [**Endpoints de Traslados de Aves**](ejemplos-api-traslados.md) - Ejemplos prácticos de inventario y movimientos
- [**Autenticación y Seguridad**](autenticacion.md) - Sistema JWT y permisos

### 🛠️ Desarrollo y Despliegue
- [**Guía de Instalación**](instalacion.md) - Configuración del entorno de desarrollo
- [**Base de Datos**](base-datos.md) - Configuración y migraciones
- [**Configuración de Entorno**](configuracion-entorno.md) - Variables y settings

### 📊 Casos de Uso
- [**Flujo de Liquidación Técnica**](flujo-liquidacion.md) - Proceso completo paso a paso
- [**Importación Masiva de Datos**](flujo-importacion.md) - Proceso de carga de Excel

## 🚀 Inicio Rápido

1. **Configuración Inicial**
   ```bash
   # Clonar el repositorio
   git clone [repository-url]
   cd backend
   
   # Configurar base de datos
   cp .env.example .env
   # Editar .env con tu configuración
   
   # Ejecutar migraciones
   dotnet ef database update
   ```

2. **Ejecutar la API**
   ```bash
   cd src/ZooSanMarino.API
   dotnet run
   ```

3. **Acceder a Swagger**
   - Abrir: `http://localhost:5002/swagger`
   - Probar endpoints disponibles

## 🏛️ Arquitectura del Sistema

El proyecto sigue los principios de **Arquitectura Hexagonal (Ports and Adapters)** con las siguientes capas:

- **🎯 Domain**: Entidades de negocio y lógica central
- **📋 Application**: Casos de uso y DTOs
- **🔌 Infrastructure**: Implementaciones concretas (BD, servicios externos)
- **🌐 API**: Controladores y endpoints REST

## 📈 Funcionalidades Principales

### ✅ Sistema de Liquidación Técnica
- Cálculos automáticos de métricas de lotes
- Comparación con guías genéticas
- Reportes de cierre de levante

### ✅ Importación de Excel
- Carga masiva de datos de producción
- Mapeo inteligente de columnas
- Validación y manejo de errores

### ✅ Sistema de Traslados de Aves
- Gestión completa de inventarios
- Movimientos entre lotes y granjas
- Trazabilidad y historial completo
- Validaciones automáticas

### ✅ Gestión Multi-tenant
- Seguridad por compañía
- Aislamiento de datos
- Autenticación JWT

## 🤝 Contribución

Para contribuir al proyecto:

1. Lee la [Guía de Arquitectura](arquitectura-hexagonal.md)
2. Sigue los patrones establecidos
3. Documenta nuevas funcionalidades
4. Ejecuta las pruebas antes de hacer commit

## 📞 Soporte

Para dudas técnicas o reportar problemas:
- Revisar la documentación específica de cada módulo
- Consultar los logs de la aplicación
- Verificar la configuración de entorno

---

**Última actualización**: Octubre 2024  
**Versión del sistema**: 1.0.0
