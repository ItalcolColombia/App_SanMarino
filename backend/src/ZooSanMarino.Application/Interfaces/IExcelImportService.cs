// src/ZooSanMarino.Application/Interfaces/IExcelImportService.cs
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IExcelImportService
{
    Task<ExcelImportResultDto> ImportProduccionAvicolaFromExcelAsync(IFormFile file);
    Task<List<ProduccionAvicolaRawDto>> ValidateExcelDataAsync(IFormFile file);
}
