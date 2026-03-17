using Microsoft.AspNetCore.Mvc;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;

namespace ShormanService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupermarketsController : ControllerBase
{
    private readonly ISupermarketService _supermarketService;

    public SupermarketsController(ISupermarketService supermarketService) => _supermarketService = supermarketService;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SupermarketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var supermarkets = await _supermarketService.GetAllAsync();
        return Ok(supermarkets);
    }
}
