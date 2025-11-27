using System;
using Npgsql;

class Program
{
    static void Main()
    {
        var connectionString = "Host=localhost;Port=5433;Username=postgres;Password=123456789;Database=sanmarinoapp_local;SSL Mode=Disable;Timeout=15;Command Timeout=30";
        
        Console.WriteLine("=== PRUEBA DE CONEXIÃ“N A BASE DE DATOS ===");
        Console.WriteLine($"Connection String: {connectionString}");
        Console.WriteLine("");
        
        try
        {
            Console.WriteLine("Intentando conectar...");
            
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                Console.WriteLine("âœ… ConexiÃ³n exitosa!");
                
                using (var cmd = new NpgsqlCommand("SELECT version();", conn))
                {
                    var version = cmd.ExecuteScalar();
                    Console.WriteLine($"ðŸ“Š VersiÃ³n PostgreSQL: {version}");
                }
                
                using (var cmd = new NpgsqlCommand("SELECT current_database();", conn))
                {
                    var db = cmd.ExecuteScalar();
                    Console.WriteLine($"ðŸ“ Base de datos actual: {db}");
                }
                
                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = ''public'';", conn))
                {
                    var count = cmd.ExecuteScalar();
                    Console.WriteLine($"ðŸ“‹ Tablas en la base de datos: {count}");
                }
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            Console.WriteLine($"âŒ Error de PostgreSQL: {ex.Message}");
            Console.WriteLine($"CÃ³digo: {ex.SqlState}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"âŒ Error: {ex.Message}");
            Console.WriteLine($"Tipo: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }
}
