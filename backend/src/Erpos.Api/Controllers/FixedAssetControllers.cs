using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

[ApiController, Authorize, Route("api/finance/assets")]
public class FixedAssetsController(FixedAssetService assets) : ControllerBase
{
    [HttpGet("categories")] public Task<List<AssetCategoryDto>> Categories(CancellationToken ct) => assets.CategoriesAsync(ct);
    [HttpPost("categories")] public Task<AssetCategoryDto> CreateCategory(SaveAssetCategoryRequest req, CancellationToken ct) => assets.SaveCategoryAsync(null, req, ct);
    [HttpPut("categories/{id:guid}")] public Task<AssetCategoryDto> UpdateCategory(Guid id, SaveAssetCategoryRequest req, CancellationToken ct) => assets.SaveCategoryAsync(id, req, ct);
    [HttpGet("depreciation")] public Task<List<DepreciationRunDto>> Runs(CancellationToken ct) => assets.RunsAsync(ct);
    [HttpPost("depreciation")] public Task<DepreciationRunDto> Run(RunDepreciationRequest req, CancellationToken ct) => assets.RunAsync(req, ct);
    [HttpGet("schedule")] public Task<AssetScheduleDto> Schedule([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) => assets.ScheduleAsync(from, to, ct);

    [HttpGet] public Task<List<AssetListItem>> List([FromQuery] AssetStatus? status, [FromQuery] Guid? categoryId, CancellationToken ct) => assets.ListAsync(status, categoryId, ct);
    [HttpGet("{id:guid}")] public Task<AssetDto> Get(Guid id, CancellationToken ct) => assets.GetAsync(id, ct);
    [HttpPost] public Task<AssetDto> Register(RegisterAssetRequest req, CancellationToken ct) => assets.RegisterAsync(req, ct);
    [HttpPut("{id:guid}")] public Task<AssetDto> Update(Guid id, UpdateAssetRequest req, CancellationToken ct) => assets.UpdateAsync(id, req, ct);
    [HttpPost("{id:guid}/dispose")] public Task<AssetDto> Dispose(Guid id, DisposeAssetRequest req, CancellationToken ct) => assets.DisposeAsync(id, req, ct);
}
