using System.IO;
using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ZooSanMarino.Infrastructure.Persistence.DesignTime;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZooSanMarinoContext>
{
    public ZooSanMarinoContext CreateDbContext(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory(); // suele ser la API por --startup-project
        var apiPath = Directory.Exists(Path.Combine(cwd, "wwwroot")) ? cwd
                    : Path.GetFullPath(Path.Combine(cwd, "..", "ZooSanMarino.API"));

        // Carga .env pero SIN sobrescribir si ya existe
        void LoadDotEnvIfExists(string path)
        {
            if (!File.Exists(path)) return;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line[..idx].Trim();
                var val = line[(idx + 1)..].Trim();
                if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                    val = val[1..^1];

                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, val);
            }
        }

        var envCandidates = new[]
        {
            Path.Combine(apiPath, ".env"),
            Path.Combine(apiPath, "..", ".env"),
            Path.Combine(apiPath, "..", "..", ".env"),
        };
        string? usedEnv = null;
        foreach (var p in envCandidates)
        {
            var full = Path.GetFullPath(p);
            if (File.Exists(full)) { LoadDotEnvIfExists(full); usedEnv ??= full; }
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var cfg = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Prioridad: ENV → appsettings
        var conn =
            Environment.GetEnvironmentVariable("ConnectionStrings__ZooSanMarinoContext")
            ?? Environment.GetEnvironmentVariable("ZOO_CONN")
            ?? cfg.GetConnectionString("ZooSanMarinoContext")
            ?? cfg["ConnectionStrings:ZooSanMarinoContext"];

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("No se encontró la cadena de conexión (ENV o appsettings).");

        var csb = new NpgsqlConnectionStringBuilder(conn);
        Console.WriteLine($"[EF] ENV: {environment} | .env usado: {usedEnv ?? "(ninguno)"} | Host: {csb.Host} | Port: {csb.Port}");

        var options = new DbContextOptionsBuilder<ZooSanMarinoContext>()
            .UseSnakeCaseNamingConvention()
            .UseNpgsql(conn)
            .Options;

        return new ZooSanMarinoContext(options);
    }
}
