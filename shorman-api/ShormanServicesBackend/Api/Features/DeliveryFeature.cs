using MediatR;
using ShormanServicesBackend.Api.Contracts;

namespace ShormanServicesBackend.Api.Features;

public record CheckDeliveryQuery(string PostalCode) : IRequest<DeliveryCheckResult>;

public class CheckDeliveryQueryHandler : IRequestHandler<CheckDeliveryQuery, DeliveryCheckResult>
{
    private static readonly HashSet<string> EligiblePostalCodes =
    [
        "10115", "10117", "10119", "10178", "10179", "12043", "12045", "13353"
    ];

    public Task<DeliveryCheckResult> Handle(CheckDeliveryQuery request, CancellationToken cancellationToken)
    {
        var postalCode = request.PostalCode.Trim();
        var eligible = EligiblePostalCodes.Contains(postalCode);
        var result = new DeliveryCheckResult(
            eligible,
            eligible ? "Delivery available to your area!" : "Sorry, we do not deliver to this postal code yet.",
            eligible ? 2.99m : null);

        return Task.FromResult(result);
    }
}
