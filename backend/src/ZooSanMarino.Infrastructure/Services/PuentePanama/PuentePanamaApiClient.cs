// Cliente de SOLO LECTURA contra ZooPanamaPollo. Hace login (POST /api/Login), cachea el token JWT
// y hace GET de la jerarquía. La conexión (baseUrl/credenciales) puede fijarse por request (front) o
// caer a la config del backend. Nunca escribe en el sistema origen (regla del proyecto).
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using ZooSanMarino.Application.DTOs.PuentePanama;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public sealed class PuentePanamaApiClient : IPuentePanamaApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly SemaphoreSlim _loginGate = new(1, 1);
    private string _baseUrl;
    private string _email;
    private string _password;
    private string? _token;
    private DateTime _tokenExpiraUtc = DateTime.MinValue;

    public PuentePanamaApiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var opt = PuentePanamaOptions.FromConfig(config);
        _baseUrl = NormalizarBase(opt.BaseUrl);
        _email = opt.Email;
        _password = opt.Password;
        if (_http.Timeout < TimeSpan.FromSeconds(60)) _http.Timeout = TimeSpan.FromSeconds(60);
    }

    private static string NormalizarBase(string? url) =>
        string.IsNullOrWhiteSpace(url) ? "https://italapp.italcol.com/ZooPanamaPollo/" : url.TrimEnd('/') + "/";

    public void UsarConexion(PanamaConexion? conexion)
    {
        if (conexion is null) return;
        if (!string.IsNullOrWhiteSpace(conexion.BaseUrl)) _baseUrl = NormalizarBase(conexion.BaseUrl);
        if (!string.IsNullOrWhiteSpace(conexion.Email)) _email = conexion.Email!;
        if (!string.IsNullOrWhiteSpace(conexion.Password)) _password = conexion.Password!;
        // Nueva conexión → invalidar token cacheado.
        _token = null;
        _tokenExpiraUtc = DateTime.MinValue;
    }

    // ── Login / token ─────────────────────────────────────────────────────────
    private async Task<string> EnsureTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiraUtc.AddMinutes(-1))
            return _token;

        await _loginGate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpiraUtc.AddMinutes(-1))
                return _token;

            if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_password))
                throw new InvalidOperationException(
                    "Faltan credenciales del puente (indicá usuario/contraseña en el front o en PuentePanama:Email/Password).");

            using var resp = await _http.PostAsJsonAsync(new Uri(new Uri(_baseUrl), "api/Login"),
                new { email = _email, password = _password }, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();

            var env = await resp.Content.ReadFromJsonAsync<PanamaEnvelope<PanamaLoginResult>>(JsonOpts, ct);
            var token = env?.Result?.Token;
            if (env is null || env.Error || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException($"Login en ZooPanamaPollo falló: {env?.Message ?? "sin token"}.");

            _token = token;
            _tokenExpiraUtc = env.Result!.Expiration?.ToUniversalTime() ?? DateTime.UtcNow.AddMinutes(30);
            return _token;
        }
        finally
        {
            _loginGate.Release();
        }
    }

    public async Task<ConexionResultDto> ProbarConexionAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureTokenAsync(ct);
            return new ConexionResultDto { Ok = true, Mensaje = "Conexión y login correctos.", Expiracion = _tokenExpiraUtc };
        }
        catch (Exception ex)
        {
            return new ConexionResultDto { Ok = false, Mensaje = ex.Message };
        }
    }

    // ── GET genérico (envuelto en ObjectGenericResult.result) ───────────────────
    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_baseUrl), path));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var env = await resp.Content.ReadFromJsonAsync<PanamaEnvelope<List<T>>>(JsonOpts, ct);
        if (env is null || env.Error)
            throw new InvalidOperationException($"GET {path} devolvió error: {env?.Message ?? "respuesta vacía"}.");
        return env.Result ?? new List<T>();
    }

    // ── Endpoints (solo GET) ────────────────────────────────────────────────────
    public Task<IReadOnlyList<PanamaCliente>> GetClientesAsync(CancellationToken ct = default) =>
        GetListAsync<PanamaCliente>("api/Cliente", ct);

    public Task<IReadOnlyList<PanamaGranja>> GetGranjasByClienteAsync(int idCliente, CancellationToken ct = default) =>
        GetListAsync<PanamaGranja>($"api/Granja/GetGranjaByIdCliente/{idCliente}", ct);

    public Task<IReadOnlyList<PanamaGalpon>> GetGalponesByGranjaAsync(int idGranja, CancellationToken ct = default) =>
        GetListAsync<PanamaGalpon>($"api/Galpon/GetGalponByIdGranja/{idGranja}", ct);

    public Task<IReadOnlyList<PanamaLote>> GetLotesByGalponAsync(int idGalpon, CancellationToken ct = default) =>
        GetListAsync<PanamaLote>($"api/Lote/GetLoteByIdGalpon/{idGalpon}", ct);

    public Task<IReadOnlyList<PanamaInfoProductiva>> GetInfoProductivaByLoteAsync(int idLote, CancellationToken ct = default) =>
        GetListAsync<PanamaInfoProductiva>($"api/InfoProductiva/GetInfoProductivaByIdLote/{idLote}", ct);

    public Task<IReadOnlyList<PanamaLoteReproductora>> GetLoteReproductoraByLoteAsync(int idLote, CancellationToken ct = default) =>
        GetListAsync<PanamaLoteReproductora>($"api/LoteReproductora/GetLoteReproductoraByIdLote/{idLote}", ct);

    public Task<IReadOnlyList<PanamaInfoProductivaRepro>> GetInfoProductivaReproByLoteReproAsync(int idLoteReproductora, CancellationToken ct = default) =>
        GetListAsync<PanamaInfoProductivaRepro>($"api/InfoProductivaLoteReproductora/GetInfoProductivaLoteRepByIdLoteRTeproductora/{idLoteReproductora}", ct);

    public Task<IReadOnlyList<PanamaLesion>> GetLesionesByReproAsync(int idLoteReproductora, CancellationToken ct = default) =>
        GetListAsync<PanamaLesion>($"api/Lesion/GetLesionByIdLoteReproductora/{idLoteReproductora}", ct);

    public Task<IReadOnlyList<PanamaGuiaGenetica>> GetGuiaGeneticaAsync(CancellationToken ct = default) =>
        GetListAsync<PanamaGuiaGenetica>("api/GuiaGenetica", ct);

    public Task<IReadOnlyList<PanamaListaItem>> GetListasAsync(CancellationToken ct = default) =>
        GetListAsync<PanamaListaItem>("api/Listas/GetListtoOffLine", ct);
}
