using System;
using Npgsql;

class TestConnection
{
    static void Main()
    {
        var connectionString = "Host=localhost;Port=5433;Username=postgres;Password=123456789;Database=sanmarinoapp_local;SSL Mode=Disable;Timeout=15;Command Timeout=30";
        
        Console.WriteLine("=== PRUEBA DE CONEXIÓN A BASE DE DATOS ===");
        Console.WriteLine($"Connection String: {connectionString}");
        Console.WriteLine("");
        
        try
        {
            Console.WriteLine("Intentando conectar...");
            
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                Console.WriteLine("✅ Conexión exitosa!");
                
                // Verificar versión
                using (var cmd = new NpgsqlCommand("SELECT version();", conn))
                {
                    var version = cmd.ExecuteScalar();
                    Console.WriteLine($"📊 Versión PostgreSQL: {version}");
                }
                
                // Verificar base de datos actual
                using (var cmd = new NpgsqlCommand("SELECT current_database();", conn))
                {
                    var db = cmd.ExecuteScalar();
                    Console.WriteLine($"📁 Base de datos actual: {db}");
                }
                
                // Contar tablas
                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';", conn))
                {
                    var count = cmd.ExecuteScalar();
                    Console.WriteLine($"📋 Tablas en la base de datos: {count}");
                }
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            Console.WriteLine($"❌ Error de PostgreSQL: {ex.Message}");
            Console.WriteLine($"Código: {ex.SqlState}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Tipo: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }
}


