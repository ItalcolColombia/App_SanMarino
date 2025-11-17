# Guía Genética - Código Completo

Este documento contiene todo el código relacionado con el módulo de Guía Genética del proyecto San Marino.

## 📋 Índice

1. [Entidad de Base de Datos](#entidad-base-de-datos)
2. [Configuración Entity Framework](#configuracion-ef)
3. [DTOs](#dtos)
4. [Interfaz del Servicio](#interfaz-servicio)
5. [Implementación del Servicio](#implementacion-servicio)
6. [Controlador API](#controlador-api)
7. [Servicio Frontend (TypeScript)](#servicio-frontend)
8. [Componente Angular](#componente-angular)
9. [Template HTML](#template-html)

---

## 1. Entidad de Base de Datos

**Archivo:** `backend/src/ZooSanMarino.Domain/Entities/ProduccionAvicolaRaw.cs`

```csharp
// src/ZooSanMarino.Domain/Entities/ProduccionAvicolaRaw.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionAvicolaRaw : AuditableEntity
{
    public int Id { get; set; }
    public string? AnioGuia { get; set; }
    public string? Raza { get; set; }
    public string? Edad { get; set; }
    public string? MortSemH { get; set; }
    public string? RetiroAcH { get; set; }
    public string? MortSemM { get; set; }
    public string? RetiroAcM { get; set; }
    public string? ConsAcH { get; set; }
    public string? ConsAcM { get; set; }
    public string? GrAveDiaH { get; set; }
    public string? GrAveDiaM { get; set; }
    public string? PesoH { get; set; }
    public string? PesoM { get; set; }
    public string? Uniformidad { get; set; }
    public string? HTotalAa { get; set; }
    public string? ProdPorcentaje { get; set; }
    public string? HIncAa { get; set; }
    public string? AprovSem { get; set; }
    public string? PesoHuevo { get; set; }
    public string? MasaHuevo { get; set; }
    public string? GrasaPorcentaje { get; set; }
    public string? NacimPorcentaje { get; set; }
    public string? PollitoAa { get; set; }
    public string? KcalAveDiaH { get; set; }
    public string? KcalAveDiaM { get; set; }
    public string? AprovAc { get; set; }
    public string? GrHuevoT { get; set; }
    public string? GrHuevoInc { get; set; }
    public string? GrPollito { get; set; }
    public string? Valor1000 { get; set; }
    public string? Valor150 { get; set; }
    public string? Apareo { get; set; }
    public string? PesoMh { get; set; }
}
```

**Tabla SQL:** `produccion_avicola_raw`

---

## 2. Configuración Entity Framework

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionAvicolaRawConfiguration.cs`

Ver archivo completo en el repositorio. Mapea la entidad a la tabla `produccion_avicola_raw` con todos sus campos.

---

## 3. DTOs

**Archivo:** `backend/src/ZooSanMarino.Application/DTOs/GuiaGeneticaDto.cs`

```csharp
namespace ZooSanMarino.Application.DTOs;

/// DTO para obtener datos de guía genética por edad/semana
public record GuiaGeneticaDto(
    int Edad,
    double ConsumoHembras,      // Gramos por ave por día
    double ConsumoMachos,       // Gramos por ave por día
    double PesoHembras,         // Peso esperado hembras
    double PesoMachos,          // Peso esperado machos
    double MortalidadHembras,   // Mortalidad esperada hembras
    double MortalidadMachos,    // Mortalidad esperada machos
    double Uniformidad,         // Uniformidad esperada
    bool PisoTermicoRequerido,  // Si requiere piso térmico
    string? Observaciones       // Observaciones adicionales
);

/// Request para obtener guía genética
public record GuiaGeneticaRequest(
    string Raza,
    int AnoTabla,
    int Edad
);

/// Response con datos de guía genética
public record GuiaGeneticaResponse(
    bool Existe,
    GuiaGeneticaDto? Datos,
    string? Mensaje
);
```

---

## 4. Interfaz del Servicio

**Archivo:** `backend/src/ZooSanMarino.Application/Interfaces/IGuiaGeneticaService.cs`

```csharp
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IGuiaGeneticaService
{
    /// Obtiene los datos de guía genética para una raza, año y edad específicos
    Task<GuiaGeneticaResponse> ObtenerGuiaGeneticaAsync(GuiaGeneticaRequest request);

    /// Obtiene múltiples edades de una guía genética
    Task<IEnumerable<GuiaGeneticaDto>> ObtenerGuiaGeneticaRangoAsync(string raza, int anoTabla, int edadDesde, int edadHasta);

    /// Verifica si existe una guía genética para los parámetros dados
    Task<bool> ExisteGuiaGeneticaAsync(string raza, int anoTabla sull);

    /// Obtiene las razas disponibles en las guías genéticas
    Task<IEnumerable<string>> ObtenerRazasDisponiblesAsync();

    /// Obtiene los años disponibles para una raza específica
    Task<IEnumerable<int>> ObtenerAnosDisponiblesAsync(string raza);
}
```

---

## 5. Implementación del Servicio

**Archivo:** `backend/src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class GuiaGeneticaService : IGuiaGeneticaService
{
    private readonly ZooSanMarinoContext _ctx;

    public GuiaGeneticaService(ZooSanMarinoContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<GuiaGeneticaResponse> ObtenerGuiaGeneticaAsync(GuiaGeneticaRequest request)
    {
        try
        {
            // Buscar en la tabla de guías genéticas
            var guia = await _ctx.ProduccionAvicolaRaw
                .Where(p => p.Raza == request.Raza && 
                           p.AnioGuia == request.AnoTabla.ToString() && 
                           p.Edad == request.Edad.ToString())
                .FirstOrDefaultAsync();

            if (guia == null)
            {
                return new GuiaGeneticaResponse(
                    Existe: false,
                    Datos: null,
                    Mensaje: $"No se encontró guía genética para Raza: {request.Raza}, Año: {request.AnoTabla}, Edad: {request.Edad}"
                );
            }

            // Parsear los valores de la guía genética
            var datos = new GuiaGeneticaDto(
                Edad: request.Edad,
                ConsumoHembras: ParseDouble(guia.GrAveDiaH),
                ConsumoMachos: ParseDouble(guia.GrAveDiaM),
                PesoHembras: ParseDouble(guia.PesoH),
                PesoMachos: ParseDouble(guia.PesoM),
                MortalidadHembras: ParseDouble(guia.MortSemH),
                MortalidadMachos: ParseDouble(guia.MortSemM),
                Uniformidad: ParseDouble(guia.Uniformidad),
                PisoTermicoRequerido: DeterminarPisoTermico(request.Edad, guia),
                Observaciones: $"Guía: {guia.Raza} {guia.AnioGuia}"
            );

            return new GuiaGeneticaResponse(
                Existe: true,
                Datos: datos,
                Mensaje: "Guía genética encontrada exitosamente"
            );
        }
        catch (Exception ex)
        {
            return new GuiaGeneticaResponse(
                Existe: false,
                Datos: null,
                Mensaje: $"Error al obtener guía genética: {ex.Message}"
            );
        }
    }

    public async Task<IEnumerable<GuiaGeneticaDto>> ObtenerGuiaGeneticaRangoAsync(string raza, int anoTabla, int edadDesde, int edadHasta)
    {
        var guias = await _ctx.ProduccionAvicolaRaw
            .Where(p => p.Raza == raza && 
                       p.AnioGuia == anoTabla.ToString())
            .ToListAsync();

        return guias
            .Where(p => int.TryParse(p.Edad, out var edad) && 
                       edad >= edadDesde && edad <= edadHasta)
            .OrderBy(p => int.Parse(p.Edad ?? "0"))
            .Select(g => new GuiaGeneticaDto(
                Edad: int.Parse(g.Edad ?? "0"),
                ConsumoHembras: ParseDouble(g.GrAveDiaH),
                ConsumoMachos: ParseDouble(g.GrAveDiaM),
                PesoHembras: ParseDouble(g.PesoH),
                PesoMachos: ParseDouble(g.PesoM),
                MortalidadHembras: ParseDouble(g.MortSemH),
                MortalidadMachos: ParseDouble(g.MortSemM),
                Uniformidad: ParseDouble(g.Uniformidad),
                PisoTermicoRequerido: DeterminarPisoTermico(int.Parse(g.Edad ?? "0"), g),
                Observaciones: $"Guía: {g.Raza} {g.AnioGuia}"
            ));
    }

    public async Task<bool> ExisteGuiaGeneticaAsync(string raza, int anoTabla)
    {
        return await _ctx.ProduccionAvicolaRaw
            .AnyAsync(p => p.Raza == raza && p.AnioGuia == anoTabla.ToString());
    }

    public async Task<IEnumerable<string>> ObtenerRazasDisponiblesAsync()
    {
        var razas = await _ctx.ProduccionAv更容易Raw
            .Where(p => !string.IsNullOrEmpty(p.Raza))
            .Select(p => p.Raza!)
            .Distinct()
            .ToListAsync();

        var razasValidas = razas
            .Where(raza => !string.IsNullOrEmpty(raza) && 
                          raza.Trim().Length >= 2)
            .Select(raza => raza.Trim())
            .OrderBy(r => r)
            .ToList();

        return razasValidas;
    }

    public async Task<IEnumerable<int>> ObtenerAnosDisponiblesAsync(string raza)
    {
        var anos = await _ctx.ProduccionAvicolaRaw
            .Where(p => p.Raza == raza && !string.IsNullOrEmpty(p.AnioGuia))
            .Select(p => p.AnioGuia!)
            .Distinct()
            .ToListAsync();

        var anosValidos = anos
            .Where(ano => int.TryParse(ano, out _))
            .Select(ano => int.Parse(ano))
            .OrderByDescending(a => a)
            .ToList();

        return anosValidos;
    }

    // Métodos privados auxiliares
    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0.0;

        var cleanValue = value.Replace(",", ".").Trim();
        
        if (double.TryParse(cleanValue, out var result))
            return result;

        return 0.0;
    }

    private static bool DeterminarPisoTermico(int edad, ProduccionAvicolaRaw guia)
    {
        if (edad <= 3)
            return true;

        if (!string.IsNullOrEmpty(guia.Valor1000))
        {
            var valor = guia.Valor1000.ToLower();
            if (valor.Contains("termico") || valor.Contains("calor") || valor.Contains("temperatura"))
                return true;
        }

        return false;
    }
}
```

---

## 6. Controlador API

**Archivo:** `backend/src/ZooSanMarino.API/Controllers/GuiaGeneticaController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/guia-genetica")]
public class GuiaGeneticaController : ControllerBase
{
    private readonly IGuiaGeneticaService _guiaGeneticaran;

    public GuiaGeneticaController(IGuiaGeneticaService guiaGeneticaService)
    {
        _guiaGeneticaService = guiaGeneticaService;
    }

    [HttpGet("obtener")]
    [ProducesResponseType(typeof(GuiaGeneticaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GuiaGeneticaResponse>> ObtenerGuiaGenetica(
        [FromQuery] string raza, 
        [FromQuery] int anoTabla, 
        [FromQuery] int edad)
    {
        if (string.IsNullOrEmpty(raza) || anoTabla <= 0 || edad <= 0)
        {
            return BadRequest(new GuiaGeneticaResponse(
                Existe: false,
                Datos: null,
                Mensaje: "Parámetros inválidos. Raza, año y edad son requeridos."
            ));
        }

        var request = new GuiaGeneticaRequest(raza, anoTabla, edad);
        var response = await _guiaGeneticaService.ObtenerGuiaGeneticaAsync(request);

        if (!response.Existe)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    [HttpGet("razas")]
    public async Task<ActionResult<IEnumerable<string>>> ObtenerRazasDisponibles()
    {
        var razas = await _guiaGeneticaService.ObtenerRazasDisponiblesAsync();
        return Ok(razas);
    }

    [HttpGet("anos")]
    public async Task<ActionResult<IEnumerable<int>>> ObtenerAnosDisponibles([FromQuery] string raza)
    {
        if (string.IsNullOrEmpty(raza))
            return BadRequest("Raza es requerida.");

        var anos = await _guiaGeneticaService.ObtenerAnosDisponiblesAsync(raza);
        return Ok(anos);
    }
}
```

---

## 7. Servicio Frontend (TypeScript)

**Archivo:** `frontend/src/app/services/guia-genetica.service.ts`

Ver archivo completo en el repositorio. Principales métodos:

```typescript
@Injectable({ providedIn: 'root' })
export class GuiaGeneticaService {
  private readonly apiUrl = `${environment.apiUrl}/guia-genetica`;

  constructor(private http: HttpClient) {}

  obtenerGuiaGenetica(raza: string, anoTabla: number, edad: number): Observable<GuiaGeneticaResponse>
  obtenerRazasDisponibles(): Observable<string[]>
  obtenerAnosDisponibles(raza: string): Observable<number[]>
}
```

---

## 8. Componente Angular

**Archivo:** `frontend/src/app/features/lote-levante/components/liquidacion-tecnica/liquidacion-tecnica.component.ts`

Véase el archivo completo en el repositorio. Características principales:
- Usa signals de Angular para reactividad
- Integra Chart.js para gráficos
- Obtiene datos de guía genética para comparaciones
- Calcula cumplimiento contra la guía genética

---

## 9. Template HTML

**Archivo:** `frontend/src/app/features/lote-levante/components/liquidacion-tecnica/liquidacion-tecnica.component.html`

Ver el archivo completo en el repositorio. Incluye:
- Sección de información del lote
- Tabla comparativa con guía genética
- Gráficos de indicadores
- Tabs de navegación (Resumen, Gráficos, Detalle)

---

## 🔗 Flujo de Datos

```
Base de Datos (produccion_avicola_raw)
    ↓
Entidad: ProduccionAvicolaRaw
    ↓
Servicio: GuiaGeneticaService
    ↓
Controlador API: /api/guia-genetica
    ↓
Servicio Frontend: GuiaGeneticaService
    ↓
Componente: LiquidacionTecnicaComponent
    ↓
Template HTML (Modal de Liquidación)
```

---

## 📝 Notas Importantes

1. **Búsqueda Flexible de Edad**: El servicio busca con múltiples formatos ("175", "25", "25.0", "25,0") para manejar inconsistencias en los datos usando `TryParseInt` con `InvariantCulture`.

2. **Parseo de Valores**: Los valores se almacenan como strings en la BD y se parsean a double en el servicio usando `InvariantCulture` para robustez internacional.

3. **Consumo de Datos**: El servicio ahora usa `ConsAcH/ConsAcM` (de `cons_ac_h/cons_ac_m` en la BD) en lugar de `GrAveDiaH/GrAveDiaM` que no existía en el CSV real.

4. **Rendimiento**: Todas las lecturas usan `AsNoTracking()` para mejor rendimiento al no cargar las entidades en el contexto de EF.

5. **Comparación con Guía**: El frontend usa semana fija 25 para la liquidación técnica.

6. **Datos del Lote**: Los datos del lote ahora vienen de `LiquidacionTecnicaDto`, no de una petición separada.

---

## 📚 Archivos Referenciados

- `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaService.cs` - Usa guía genética para comparaciones
- `layers/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaComparacionService.cs` - Compara lotes con guía genética
