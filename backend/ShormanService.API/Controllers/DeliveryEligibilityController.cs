using Microsoft.AspNetCore.Mvc;

namespace ShormanService.API.Controllers;

[ApiController]
[Route("api/delivery")]
public class DeliveryEligibilityController : ControllerBase
{
    [HttpPost("check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CheckEligibility([FromBody] DeliveryCheckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PostalCode))
            return BadRequest(new { message = "Postal code is required." });

        // German postal codes are 5 digits starting with 1-9
        var isGerman = request.PostalCode.Length == 5
            && request.PostalCode.All(char.IsDigit)
            && request.PostalCode[0] != '0';

        if (!isGerman)
            return Ok(new DeliveryCheckResponse { Eligible = false, Message = "Delivery not available for this postal code." });

        return Ok(new DeliveryCheckResponse { Eligible = true, Message = "Delivery available to your area!" });
    }
}

public class DeliveryCheckRequest
{
    public string PostalCode { get; set; } = string.Empty;
}

public class DeliveryCheckResponse
{
    public bool Eligible { get; set; }
    public string Message { get; set; } = string.Empty;
}
