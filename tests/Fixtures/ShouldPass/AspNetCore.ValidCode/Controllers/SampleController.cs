using AspNetCore.ValidCode.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.ValidCode.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SampleController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly IRandomService _randomService;

    public SampleController(IDataService dataService, IRandomService randomService)
    {
        _dataService = dataService;
        _randomService = randomService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        // GOOD: No ConfigureAwait needed
        var data = await _dataService.GetDataAsync();
        return Ok(data);
    }

    [HttpGet("random")]
    public IActionResult GetRandom()
    {
        // GOOD: Random usage is acceptable
        var number = _randomService.GetRandomNumber(1, 100);
        return Ok(new { Value = number });
    }

    [HttpGet("id")]
    public IActionResult GenerateId()
    {
        var id = _randomService.GenerateId();
        return Ok(new { Id = id });
    }
}
