using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.API;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ZooSanMarinoContext>();
        
        try
        {
            // Verificar si las tablas de producción existen
            var produccionLoteExists = await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS produccion_lote (
                    id SERIAL PRIMARY KEY,
                    lote_id INTEGER NOT NULL,
                    fecha_inicio TIMESTAMP WITH TIME ZONE NOT NULL,
                    aves_iniciales_h INTEGER NOT NULL,
                    aves_iniciales_m INTEGER NOT NULL,
                    observaciones TEXT,
                    company_id INTEGER NOT NULL,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    updated_at TIMESTAMP WITH TIME ZONE,
                    deleted_at TIMESTAMP WITH TIME ZONE,
                    CONSTRAINT uk_produccion_lote_lote_id UNIQUE (lote_id)
                );
            ") >= 0;

            // Nota (Fase 3): el bloque CREATE TABLE produccion_seguimiento se retiró.
            // Esa tabla quedó consolidada en seguimiento_diario_produccion_reproductoras
            // (entidad SeguimientoProduccion); su definición aquí era código muerto
            // (esquema distinto que nunca corría por el IF NOT EXISTS).

            Console.WriteLine("Tablas de producción creadas exitosamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creando tablas de producción: {ex.Message}");
        }
    }
}



