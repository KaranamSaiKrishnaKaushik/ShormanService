using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;

namespace ShormanService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;
    private readonly ILogger<AddressesController> _logger;

    public AddressesController(IAddressService addressService, ILogger<AddressesController> logger)
    {
        _addressService = addressService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var id))
            throw new UnauthorizedAccessException("Invalid user token.");
        return id;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAddresses()
    {
        var addresses = await _addressService.GetAddressesAsync(GetUserId());
        return Ok(addresses);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
    {
        var address = await _addressService.CreateAddressAsync(GetUserId(), request);
        return CreatedAtAction(nameof(GetAddresses), address);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request)
    {
        try
        {
            var address = await _addressService.UpdateAddressAsync(GetUserId(), id, request);
            return Ok(address);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        try
        {
            await _addressService.DeleteAddressAsync(GetUserId(), id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/set-default")]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        try
        {
            var address = await _addressService.SetDefaultAsync(GetUserId(), id);
            return Ok(address);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
