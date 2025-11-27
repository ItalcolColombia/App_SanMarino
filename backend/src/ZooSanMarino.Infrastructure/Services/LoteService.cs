// file: src/ZooSanMarino.Infrastructure/Services/LoteService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.DTOs;           // LoteDto, Create/Update
using ZooSanMarino.Application.DTOs.Lotes;     // LoteDetailDto, LoteSearchRequest, TrasladoLoteRequestDto, TrasladoLoteResponseDto, HistorialTrasladoLoteDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using AppInterfaces = ZooSanMarino.Application.Interfaces;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

using ZooSanMarino.Domain.Entities;
using HistorialTrasladoLote = ZooSanMarino.Domain.Entities.HistorialTrasladoLote;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services
{
    public class LoteService : AppInterfaces.ILoteService
    {
        private readonly ZooSanMarinoContext _ctx;
        private readonly AppInterfaces.ICurrentUser _current;

        public LoteService(ZooSanMarinoContext ctx, AppInterfaces.ICurrentUser current)
        {
            _ctx = ctx;
            _current = current;
        }

        // ======================================================
        // LISTADO SIMPLE CON INFORMACIÓN COMPLETA DE RELACIONES
        // ======================================================
        public async Task<IEnumerable<LoteDetailDto>> GetAllAsync()
        {
            var q = _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .OrderBy(l => l.LoteId);

            return await ProjectToDetail(q).ToListAsync();
        }

        // ======================================================
        // BÚSQUEDA / LISTADO AVANZADO (paginado)
        // ======================================================
        public async Task<CommonDtos.PagedResult<LoteDetailDto>> SearchAsync(LoteSearchRequest req)
        {
            // saneo mínimo
            var page = req.Page <= 0 ? 1 : req.Page;
            var pageSize = req.PageSize <= 0 ? 50 : req.PageSize;

            var q = _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.CompanyId == _current.CompanyId);

            if (req.SoloActivos)
                q = q.Where(l => l.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim().ToLower();
                q = q.Where(l =>
                    (l.LoteId.HasValue && l.LoteId.Value.ToString().Contains(term)) ||
                    EF.Functions.Like(l.LoteNombre!.ToLower(), $"%{term}%"));
            }

            if (req.GranjaId.HasValue) q = q.Where(l => l.GranjaId == req.GranjaId.Value);
            if (!string.IsNullOrWhiteSpace(req.NucleoId)) q = q.Where(l => l.NucleoId == req.NucleoId);
            if (!string.IsNullOrWhiteSpace(req.GalponId)) q = q.Where(l => l.GalponId == req.GalponId);

            if (req.FechaDesde.HasValue) q = q.Where(l => l.FechaEncaset >= req.FechaDesde!.Value);
            if (req.FechaHasta.HasValue) q = q.Where(l => l.FechaEncaset <= req.FechaHasta!.Value);

            if (!string.IsNullOrWhiteSpace(req.TipoLinea)) q = q.Where(l => l.TipoLinea == req.TipoLinea);
            if (!string.IsNullOrWhiteSpace(req.Raza)) q = q.Where(l => l.Raza == req.Raza);
            if (!string.IsNullOrWhiteSpace(req.Tecnico)) q = q.Where(l => l.Tecnico == req.Tecnico);

            q = ApplyOrder(q, req.SortBy, req.SortDesc);

            var total = await q.LongCountAsync();
            var items = await ProjectToDetail(q)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new CommonDtos.PagedResult<LoteDetailDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            };
        }

        // ======================================================
        // GET DETALLE POR ID (tenant-safe)
        // ======================================================
        public async Task<LoteDetailDto?> GetByIdAsync(int loteId)
        {
            var q = _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.CompanyId == _current.CompanyId &&
                            l.LoteId == loteId &&
                            l.DeletedAt == null);

            return await ProjectToDetail(q).SingleOrDefaultAsync();
        }

        // ======================================================
        // CREATE (valida pertenencia y relaciones)
        // ======================================================
        public async Task<LoteDetailDto> CreateAsync(CreateLoteDto dto)
        {
            // La base de datos generará automáticamente el loteId
            // No necesitamos generar IDs manualmente

            await EnsureFarmExists(dto.GranjaId);

            string? nucleoId = string.IsNullOrWhiteSpace(dto.NucleoId) ? null : dto.NucleoId.Trim();
            string? galponId = string.IsNullOrWhiteSpace(dto.GalponId) ? null : dto.GalponId.Trim();

            // Si viene Galpón, validamos pertenencia y, si falta, derivamos NucleoId del galpón
            if (!string.IsNullOrWhiteSpace(galponId))
            {
                var g = await _ctx.Galpones
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.GalponId == galponId &&
                        x.CompanyId == _current.CompanyId);

                if (g is null)
                    throw new InvalidOperationException("Galpón no existe o no pertenece a la compañía.");

                if (g.GranjaId != dto.GranjaId)
                    throw new InvalidOperationException("Galpón no pertenece a la granja indicada.");

                if (!string.IsNullOrWhiteSpace(nucleoId) &&
                    !string.Equals(g.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Galpón no pertenece al núcleo indicado.");

                nucleoId ??= g.NucleoId;
            }

            // Si viene Núcleo, validar que existe en la granja
            if (!string.IsNullOrWhiteSpace(nucleoId))
            {
                var n = await _ctx.Nucleos
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.NucleoId == nucleoId &&
                        x.GranjaId == dto.GranjaId
                    // Si Nucleo tiene CompanyId, añadir filtro CompanyId == _current.CompanyId
                    );

                if (n is null)
                    throw new InvalidOperationException("Núcleo no existe en la granja (o no pertenece a la compañía).");
            }

            var ent = new Lote
            {
                // LoteId será generado automáticamente por la base de datos
                LoteNombre = (dto.LoteNombre ?? string.Empty).Trim(),
                GranjaId = dto.GranjaId,
                NucleoId = nucleoId,
                GalponId = galponId,

                Regional = dto.Regional,
                // Fechas en UTC para consistencia
                FechaEncaset = dto.FechaEncaset?.ToUniversalTime(),

                HembrasL = dto.HembrasL,
                MachosL = dto.MachosL,

                // ← tipos decimales: asignación directa
                PesoInicialH = dto.PesoInicialH,
                PesoInicialM = dto.PesoInicialM,
                UnifH = dto.UnifH,
                UnifM = dto.UnifM,

                MortCajaH = dto.MortCajaH,
                MortCajaM = dto.MortCajaM,

                Raza = dto.Raza,
                AnoTablaGenetica = dto.AnoTablaGenetica,
                Linea = dto.Linea,
                TipoLinea = dto.TipoLinea,
                CodigoGuiaGenetica = dto.CodigoGuiaGenetica,
                LineaGeneticaId = dto.LineaGeneticaId,  // ← NUEVO: ID de la línea genética
                Tecnico = dto.Tecnico,

                Mixtas = dto.Mixtas,
                PesoMixto = dto.PesoMixto,
                AvesEncasetadas = dto.AvesEncasetadas,
                EdadInicial = dto.EdadInicial,
                LoteErp = dto.LoteErp,  // ← NUEVO: Código ERP del lote
                LotePadreId = dto.LotePadreId,  // ← NUEVO: ID del lote padre

                CompanyId = _current.CompanyId,
                CreatedByUserId = _current.UserId,
                CreatedAt = DateTime.UtcNow
            };

            // Validar que el lote padre existe y pertenece a la misma compañía
            if (dto.LotePadreId.HasValue)
            {
                var lotePadre = await _ctx.Lotes
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.LoteId == dto.LotePadreId.Value &&
                        x.CompanyId == _current.CompanyId &&
                        x.DeletedAt == null);
                
                if (lotePadre is null)
                    throw new InvalidOperationException("El lote padre no existe o no pertenece a la compañía.");
                
                // Validar que el lote padre no tenga un padre (evitar jerarquías de más de 2 niveles)
                if (lotePadre.LotePadreId.HasValue)
                    throw new InvalidOperationException("El lote seleccionado como padre ya tiene un lote padre asignado. No se permiten jerarquías de más de 2 niveles.");
            }

            _ctx.Lotes.Add(ent);
            await _ctx.SaveChangesAsync();

            var result = await GetByIdAsync(ent.LoteId ?? 0);
            return result ?? throw new InvalidOperationException("No fue posible leer el lote recién creado.");
        }

        // ======================================================
        // UPDATE (tenant-safe + validaciones de relaciones)
        // ======================================================
        public async Task<LoteDetailDto?> UpdateAsync(UpdateLoteDto dto)
        {
            var ent = await _ctx.Lotes
                .SingleOrDefaultAsync(x =>
                    x.LoteId == dto.LoteId &&
                    x.CompanyId == _current.CompanyId &&
                    x.DeletedAt == null);

            if (ent is null) return null;

            await EnsureFarmExists(dto.GranjaId);

            string? nucleoId = string.IsNullOrWhiteSpace(dto.NucleoId) ? null : dto.NucleoId.Trim();
            string? galponId = string.IsNullOrWhiteSpace(dto.GalponId) ? null : dto.GalponId.Trim();

            if (!string.IsNullOrWhiteSpace(galponId))
            {
                var g = await _ctx.Galpones
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.GalponId == galponId &&
                        x.CompanyId == _current.CompanyId);

                if (g is null)
                    throw new InvalidOperationException("Galpón no existe o no pertenece a la compañía.");

                if (g.GranjaId != dto.GranjaId)
                    throw new InvalidOperationException("Galpón no pertenece a la granja indicada.");

                if (!string.IsNullOrWhiteSpace(nucleoId) &&
                    !string.Equals(g.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Galpón no pertenece al núcleo indicado.");
            }

            if (!string.IsNullOrWhiteSpace(nucleoId))
            {
                var n = await _ctx.Nucleos
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.NucleoId == nucleoId &&
                        x.GranjaId == dto.GranjaId
                    // Si Nucleo tiene CompanyId, añadir filtro CompanyId == _current.CompanyId
                    );

                if (n is null)
                    throw new InvalidOperationException("Núcleo no existe en la granja (o no pertenece a la compañía).");
            }

            // Mutación (fechas en UTC y decimales directos)
            ent.LoteNombre = (dto.LoteNombre ?? string.Empty).Trim();
            ent.GranjaId = dto.GranjaId;
            ent.NucleoId = nucleoId ?? ent.NucleoId;
            ent.GalponId = galponId ?? ent.GalponId;
            ent.Regional = dto.Regional;
            ent.FechaEncaset = dto.FechaEncaset?.ToUniversalTime();

            ent.HembrasL = dto.HembrasL;
            ent.MachosL = dto.MachosL;

            ent.PesoInicialH = dto.PesoInicialH;
            ent.PesoInicialM = dto.PesoInicialM;
            ent.UnifH = dto.UnifH;
            ent.UnifM = dto.UnifM;

            ent.MortCajaH = dto.MortCajaH;
            ent.MortCajaM = dto.MortCajaM;

            ent.Raza = dto.Raza;
            ent.AnoTablaGenetica = dto.AnoTablaGenetica;
            ent.Linea = dto.Linea;
            ent.TipoLinea = dto.TipoLinea;
            ent.CodigoGuiaGenetica = dto.CodigoGuiaGenetica;
            ent.LineaGeneticaId = dto.LineaGeneticaId;  // ← NUEVO: ID de la línea genética
            ent.Tecnico = dto.Tecnico;

            ent.Mixtas = dto.Mixtas;
            ent.PesoMixto = dto.PesoMixto;
            ent.AvesEncasetadas = dto.AvesEncasetadas;
            ent.EdadInicial = dto.EdadInicial;
            ent.LoteErp = dto.LoteErp;  // ← NUEVO: Código ERP del lote
            ent.LotePadreId = dto.LotePadreId;  // ← NUEVO: ID del lote padre

            // Validar que el lote padre existe y pertenece a la misma compañía
            if (dto.LotePadreId.HasValue)
            {
                var lotePadre = await _ctx.Lotes
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.LoteId == dto.LotePadreId.Value &&
                        x.CompanyId == _current.CompanyId &&
                        x.DeletedAt == null);
                
                if (lotePadre is null)
                    throw new InvalidOperationException("El lote padre no existe o no pertenece a la compañía.");
                
                // Evitar referencias circulares
                if (dto.LotePadreId.Value == dto.LoteId)
                    throw new InvalidOperationException("Un lote no puede ser su propio padre.");
                
                // Validar que el lote padre no tenga un padre (evitar jerarquías de más de 2 niveles)
                if (lotePadre.LotePadreId.HasValue)
                    throw new InvalidOperationException("El lote seleccionado como padre ya tiene un lote padre asignado. No se permiten jerarquías de más de 2 niveles.");
                
                // Validar que no se cree un ciclo: verificar que el lote padre no sea descendiente del lote actual
                // Solo validar si estamos actualizando (no creando)
                if (dto.LoteId > 0)
                {
                    var esDescendiente = await EsDescendienteAsync(dto.LoteId, dto.LotePadreId.Value);
                    if (esDescendiente)
                        throw new InvalidOperationException("No se puede asignar un lote hijo como padre. Esto crearía una referencia circular.");
                }
            }

            ent.UpdatedByUserId = _current.UserId;
            ent.UpdatedAt = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return await GetByIdAsync(ent.LoteId ?? 0);
        }

        // ======================================================
        // DELETE (soft) y HARD DELETE
        // ======================================================
        public async Task<bool> DeleteAsync(int loteId)
        {
            var ent = await _ctx.Lotes
                .SingleOrDefaultAsync(x => x.LoteId == loteId && x.CompanyId == _current.CompanyId);
            if (ent is null || ent.DeletedAt != null) return false;

            ent.DeletedAt = DateTime.UtcNow;
            ent.UpdatedByUserId = _current.UserId;
            ent.UpdatedAt = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HardDeleteAsync(int loteId)
        {
            var ent = await _ctx.Lotes
                .SingleOrDefaultAsync(x => x.LoteId == loteId && x.CompanyId == _current.CompanyId);
            if (ent is null) return false;

            _ctx.Lotes.Remove(ent);
            await _ctx.SaveChangesAsync();
            return true;
        }

        // ======================================================
        // Helpers
        // ======================================================
        private async Task EnsureFarmExists(int granjaId)
        {
            var exists = await _ctx.Farms
                .AsNoTracking()
                .AnyAsync(f => f.Id == granjaId && f.CompanyId == _current.CompanyId);
            if (!exists) throw new InvalidOperationException("Granja no existe o no pertenece a la compañía.");
        }

        // Proyección consistente a LoteDetailDto con Lite DTOs
        private static IQueryable<LoteDetailDto> ProjectToDetail(IQueryable<Lote> q)
        {
            return q
                .Include(l => l.Farm)
                .Include(l => l.Nucleo)
                .Include(l => l.Galpon)
                .Select(l => new LoteDetailDto(
                    l.LoteId ?? 0,
                    l.LoteNombre,
                    l.GranjaId,
                    l.NucleoId,
                    l.GalponId,
                    l.Regional,
                    l.FechaEncaset,
                    l.HembrasL,
                    l.MachosL,
                    l.PesoInicialH,
                    l.PesoInicialM,
                    l.UnifH,
                    l.UnifM,
                    l.MortCajaH,
                    l.MortCajaM,
                    l.Raza,
                    l.AnoTablaGenetica,
                    l.Linea,
                    l.TipoLinea,
                    l.CodigoGuiaGenetica,
                    l.LineaGeneticaId,  // ← NUEVO: ID de la línea genética
                    l.Tecnico,
                    l.Mixtas,
                    l.PesoMixto,
                    l.AvesEncasetadas,
                    l.EdadInicial,
                    l.LoteErp,  // ← NUEVO: Código ERP del lote
                    l.EstadoTraslado,  // ← Estado de traslado
                    l.LotePadreId,  // ← NUEVO: ID del lote padre
                    l.CompanyId,
                    l.CreatedByUserId,
                    l.CreatedAt,
                    l.UpdatedByUserId,
                    l.UpdatedAt,
                    new FarmLiteDto(
                        l.Farm.Id,
                        l.Farm.Name,
                        l.Farm.RegionalId,
                        l.Farm.DepartamentoId,
                        l.Farm.MunicipioId
                    ),
                    l.Nucleo == null
                        ? null
                        : new NucleoLiteDto(
                            l.Nucleo.NucleoId,
                            l.Nucleo.NucleoNombre,
                            l.Nucleo.GranjaId
                        ),
                    l.Galpon == null
                        ? null
                        : new GalponLiteDto(
                            l.Galpon.GalponId,
                            l.Galpon.GalponNombre,
                            l.Galpon.NucleoId,
                            l.Galpon.GranjaId
                        )
                ));
        }

        private static IQueryable<Lote> ApplyOrder(IQueryable<Lote> q, string? sortBy, bool desc)
        {
            Expression<Func<Lote, object>> key = (sortBy ?? string.Empty).ToLower() switch
            {
                "lote_nombre" => l => l.LoteNombre ?? string.Empty,
                "lote_id" => l => l.LoteId ?? 0,
                "fecha_encaset" => l => l.FechaEncaset ?? DateTime.MinValue,
                _ => l => l.FechaEncaset ?? DateTime.MinValue
            };
            return desc ? q.OrderByDescending(key) : q.OrderBy(key);
        }


        /// <summary>
        /// Resumen de mortalidad + saldos (levante).
        /// Reglas solicitadas:
        ///  - Sumas acumuladas = Σ(mortalidad hembra) y Σ(mortalidad macho) de SeguimientoLoteLevante por LoteId.
        ///  - SaldoHembras = (HembrasL - MortCajaH) - MortalidadAcumHembras
        ///  - SaldoMachos  = (MachosL  - MortCajaM) - MortalidadAcumMachos
        ///  - Clampea a cero si queda negativo.
        ///  - Tenant-safe (CompanyId) y exige que el lote no esté eliminado.
        /// </summary>
        public async Task<LoteMortalidadResumenDto?> GetMortalidadResumenAsync(int loteId)
        {
            // 1) Carga del lote (tenant-safe)
            var lote = await _ctx.Lotes
                .AsNoTracking()
                .SingleOrDefaultAsync(l =>
                    l.LoteId == loteId &&
                    l.CompanyId == _current.CompanyId &&
                    l.DeletedAt == null);

            if (lote is null) return null;

            // 2) Sumas de mortalidad (una sola consulta agrupada)
            var mort = await _ctx.SeguimientoLoteLevante
                .AsNoTracking()
                .Where(s => s.LoteId == loteId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    H = (int?)g.Sum(x => x.MortalidadHembras) ?? 0,
                    M = (int?)g.Sum(x => x.MortalidadMachos) ?? 0
                })
                .SingleOrDefaultAsync();

            int mortH = mort?.H ?? 0;
            int mortM = mort?.M ?? 0;

            // 3) Bases y mortandad en caja (si tu entidad las trae)
            int baseH = lote.HembrasL ?? 0;
            int baseM = lote.MachosL ?? 0;
            int mortCajaH = lote.MortCajaH ?? 0;
            int mortCajaM = lote.MortCajaM ?? 0;

            // 4) Saldos solicitados (solo restando mortalidad)
            int saldoH = Math.Max(0, baseH - mortCajaH - mortH);
            int saldoM = Math.Max(0, baseM - mortCajaM - mortM);

            return new LoteMortalidadResumenDto
            {
                LoteId = loteId.ToString(),
                MortalidadAcumHembras = mortH,
                MortalidadAcumMachos = mortM,
                HembrasIniciales = baseH,
                MachosIniciales = baseM,
                MortCajaHembras = mortCajaH,
                MortCajaMachos = mortCajaM,
                SaldoHembras = saldoH,
                SaldoMachos = saldoM
            };
        }

        // ======================================================
        // TRASLADO DE LOTE A OTRA GRANJA
        // ======================================================
        public async Task<TrasladoLoteResponseDto> TrasladarLoteAsync(TrasladoLoteRequestDto dto)
        {
            // 1. Validar y obtener el lote original
            var loteOriginal = await _ctx.Lotes
                .Include(l => l.Farm)
                .SingleOrDefaultAsync(x =>
                    x.LoteId == dto.LoteId &&
                    x.CompanyId == _current.CompanyId &&
                    x.DeletedAt == null);

            if (loteOriginal == null)
            {
                throw new InvalidOperationException($"No se encontró el lote con ID {dto.LoteId} o no pertenece a la compañía actual.");
            }

            // 2. Validar que no sea el mismo lote (misma granja)
            if (loteOriginal.GranjaId == dto.GranjaDestinoId)
            {
                throw new InvalidOperationException("No se puede trasladar un lote a la misma granja.");
            }

            // 3. Validar que el lote no esté ya trasladado
            if (loteOriginal.EstadoTraslado == "trasladado")
            {
                throw new InvalidOperationException("Este lote ya ha sido trasladado anteriormente.");
            }

            // 4. Validar que la granja destino existe y pertenece a la compañía
            var granjaDestino = await _ctx.Farms
                .AsNoTracking()
                .SingleOrDefaultAsync(f =>
                    f.Id == dto.GranjaDestinoId &&
                    f.CompanyId == _current.CompanyId);

            if (granjaDestino == null)
            {
                throw new InvalidOperationException($"La granja destino con ID {dto.GranjaDestinoId} no existe o no pertenece a la compañía actual.");
            }

            // 5. Validar núcleo destino si se proporciona
            if (!string.IsNullOrWhiteSpace(dto.NucleoDestinoId))
            {
                var nucleoDestino = await _ctx.Nucleos
                    .AsNoTracking()
                    .SingleOrDefaultAsync(n =>
                        n.NucleoId == dto.NucleoDestinoId &&
                        n.GranjaId == dto.GranjaDestinoId);

                if (nucleoDestino == null)
                {
                    throw new InvalidOperationException($"El núcleo destino con ID {dto.NucleoDestinoId} no existe en la granja destino.");
                }
            }

            // 6. Validar galpón destino si se proporciona
            if (!string.IsNullOrWhiteSpace(dto.GalponDestinoId))
            {
                var galponDestino = await _ctx.Galpones
                    .AsNoTracking()
                    .SingleOrDefaultAsync(g =>
                        g.GalponId == dto.GalponDestinoId &&
                        g.CompanyId == _current.CompanyId);

                if (galponDestino == null)
                {
                    throw new InvalidOperationException($"El galpón destino con ID {dto.GalponDestinoId} no existe o no pertenece a la compañía actual.");
                }

                if (galponDestino.GranjaId != dto.GranjaDestinoId)
                {
                    throw new InvalidOperationException("El galpón destino no pertenece a la granja destino.");
                }

                if (!string.IsNullOrWhiteSpace(dto.NucleoDestinoId) &&
                    !string.Equals(galponDestino.NucleoId, dto.NucleoDestinoId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("El galpón destino no pertenece al núcleo destino indicado.");
                }
            }

            // 7. Actualizar el lote original con estado "trasladado"
            loteOriginal.EstadoTraslado = "trasladado";
            loteOriginal.UpdatedByUserId = _current.UserId;
            loteOriginal.UpdatedAt = DateTime.UtcNow;

            // 8. Crear nuevo lote en la granja destino con estado "en_transferencia"
            var nuevoLote = new Lote
            {
                CompanyId = _current.CompanyId,
                LoteNombre = loteOriginal.LoteNombre,
                GranjaId = dto.GranjaDestinoId,
                NucleoId = dto.NucleoDestinoId ?? null,
                GalponId = dto.GalponDestinoId ?? null,
                Regional = loteOriginal.Regional,
                FechaEncaset = loteOriginal.FechaEncaset,
                HembrasL = loteOriginal.HembrasL,
                MachosL = loteOriginal.MachosL,
                PesoInicialH = loteOriginal.PesoInicialH,
                PesoInicialM = loteOriginal.PesoInicialM,
                UnifH = loteOriginal.UnifH,
                UnifM = loteOriginal.UnifM,
                MortCajaH = loteOriginal.MortCajaH,
                MortCajaM = loteOriginal.MortCajaM,
                Raza = loteOriginal.Raza,
                AnoTablaGenetica = loteOriginal.AnoTablaGenetica,
                Linea = loteOriginal.Linea,
                TipoLinea = loteOriginal.TipoLinea,
                CodigoGuiaGenetica = loteOriginal.CodigoGuiaGenetica,
                LineaGeneticaId = loteOriginal.LineaGeneticaId,
                Tecnico = loteOriginal.Tecnico,
                Mixtas = loteOriginal.Mixtas,
                PesoMixto = loteOriginal.PesoMixto,
                AvesEncasetadas = loteOriginal.AvesEncasetadas,
                EdadInicial = loteOriginal.EdadInicial,
                LoteErp = loteOriginal.LoteErp,
                EstadoTraslado = "en_transferencia",
                CreatedByUserId = _current.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Lotes.Add(nuevoLote);
            await _ctx.SaveChangesAsync();

            // 9. Registrar en el historial de traslados
            var historial = new HistorialTrasladoLote
            {
                LoteOriginalId = loteOriginal.LoteId ?? 0,
                LoteNuevoId = nuevoLote.LoteId ?? 0,
                GranjaOrigenId = loteOriginal.GranjaId,
                GranjaDestinoId = dto.GranjaDestinoId,
                NucleoDestinoId = dto.NucleoDestinoId,
                GalponDestinoId = dto.GalponDestinoId,
                Observaciones = dto.Observaciones,
                CompanyId = _current.CompanyId,
                CreatedByUserId = _current.UserId,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.HistorialTrasladoLote.Add(historial);
            await _ctx.SaveChangesAsync();

            // 10. Obtener información de las granjas para la respuesta
            var granjaOrigenNombre = loteOriginal.Farm?.Name ?? "N/A";

            return new TrasladoLoteResponseDto
            {
                Success = true,
                Message = $"Lote trasladado exitosamente de '{granjaOrigenNombre}' a '{granjaDestino.Name}'.",
                LoteOriginalId = loteOriginal.LoteId,
                LoteNuevoId = nuevoLote.LoteId,
                LoteNombre = loteOriginal.LoteNombre,
                GranjaOrigen = granjaOrigenNombre,
                GranjaDestino = granjaDestino.Name
            };
        }

        // ======================================================
        // HISTORIAL DE TRASLADOS
        // ======================================================
        public async Task<IEnumerable<HistorialTrasladoLoteDto>> GetHistorialTrasladosAsync(int loteId)
        {
            var historiales = await _ctx.HistorialTrasladoLote
                .AsNoTracking()
                .Where(h => 
                    (h.LoteOriginalId == loteId || h.LoteNuevoId == loteId) &&
                    h.CompanyId == _current.CompanyId)
                .OrderByDescending(h => h.CreatedAt)
                .Include(h => h.GranjaOrigen)
                .Include(h => h.GranjaDestino)
                .ToListAsync();

            var result = new List<HistorialTrasladoLoteDto>();

            foreach (var h in historiales)
            {
                // Obtener nombres de núcleo y galpón si existen
                string? nucleoNombre = null;
                if (!string.IsNullOrWhiteSpace(h.NucleoDestinoId))
                {
                    var nucleo = await _ctx.Nucleos
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.NucleoId == h.NucleoDestinoId);
                    nucleoNombre = nucleo?.NucleoNombre;
                }

                string? galponNombre = null;
                if (!string.IsNullOrWhiteSpace(h.GalponDestinoId))
                {
                    var galpon = await _ctx.Galpones
                        .AsNoTracking()
                        .FirstOrDefaultAsync(g => g.GalponId == h.GalponDestinoId);
                    galponNombre = galpon?.GalponNombre;
                }

                // Obtener nombre del usuario (CreatedByUserId es int, pero User.Id es Guid)
                // Por ahora, no podemos hacer la relación directa, así que usamos un valor por defecto
                // TODO: Si se necesita el nombre del usuario, se podría crear una tabla de mapeo o cambiar el sistema
                var nombreUsuario = $"Usuario ID: {h.CreatedByUserId}";

                result.Add(new HistorialTrasladoLoteDto(
                    h.Id,
                    h.LoteOriginalId,
                    h.LoteNuevoId,
                    h.GranjaOrigenId,
                    h.GranjaOrigen?.Name ?? "N/A",
                    h.GranjaDestinoId,
                    h.GranjaDestino?.Name ?? "N/A",
                    h.NucleoDestinoId,
                    nucleoNombre,
                    h.GalponDestinoId,
                    galponNombre,
                    h.Observaciones,
                    h.CreatedByUserId,
                    nombreUsuario,
                    h.CreatedAt
                ));
            }

            return result;
        }

        // Los métodos de generación manual de IDs han sido removidos
        // La base de datos ahora genera automáticamente los IDs

        /// <summary>
        /// Verifica si un lote es descendiente de otro (para evitar ciclos)
        /// </summary>
        private async Task<bool> EsDescendienteAsync(int loteIdActual, int loteIdPadre)
        {
            // Obtener todos los hijos del lote actual
            var hijos = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LotePadreId == loteIdActual &&
                           l.CompanyId == _current.CompanyId &&
                           l.DeletedAt == null)
                .Select(l => l.LoteId)
                .ToListAsync();

            // Si el lote padre está en la lista de hijos, es un descendiente
            if (hijos.Contains(loteIdPadre))
                return true;

            // Verificar recursivamente en los hijos
            foreach (var hijoId in hijos.Where(h => h.HasValue).Select(h => h!.Value))
            {
                var esDescendiente = await EsDescendienteAsync(hijoId, loteIdPadre);
                if (esDescendiente)
                    return true;
            }

            return false;
        }
    }

}
