using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Options;
using ShormanServicesBackend.Api.Contracts;

namespace ShormanServicesBackend.Api.Features;

public record CheckDeliveryQuery(string PostalCode, string? City = null, string? Street = null, string? HouseNumber = null, string? Country = null) : IRequest<DeliveryCheckResult>;

public sealed class DeliveryZoneOptions
{
    public const string SectionName = "DeliveryZone";

    public string CenterLabel { get; set; } = "60596 Frankfurt am Main";
    public double CenterLatitude { get; set; } = 50.0994;
    public double CenterLongitude { get; set; } = 8.6868;
    public double RadiusKm { get; set; } = 3.0;
    public decimal DeliveryFee { get; set; } = 2.99m;
}

public interface IDeliveryGeocodingService
{
    Task<(double Latitude, double Longitude)?> GeocodeAsync(CheckDeliveryQuery request, CancellationToken cancellationToken);
}

public sealed class DeliveryGeocodingService(IHttpClientFactory httpClientFactory, ILogger<DeliveryGeocodingService> logger) : IDeliveryGeocodingService
{
    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(CheckDeliveryQuery request, CancellationToken cancellationToken)
    {
        var query = string.Join(", ",
            new[]
            {
                string.Join(' ', new[] { request.Street?.Trim(), request.HouseNumber?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x))),
                request.PostalCode.Trim(),
                request.City?.Trim(),
                request.Country?.Trim()
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient("delivery-geocoder");
        try
        {
            using var response = await client.GetAsync($"search?format=jsonv2&limit=1&countrycodes=de&q={Uri.EscapeDataString(query)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Delivery geocoding failed with status code {StatusCode} for query {Query}", response.StatusCode, query);
                return null;
            }

            var matches = await response.Content.ReadFromJsonAsync<List<NominatimSearchResult>>(cancellationToken: cancellationToken);
            var match = matches?.FirstOrDefault();
            if (match is null
                || !double.TryParse(match.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                || !double.TryParse(match.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                logger.LogInformation("Delivery geocoding returned no usable match for query {Query}", query);
                return null;
            }

            return (latitude, longitude);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Delivery geocoding timed out for query {Query}", query);
            return null;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Delivery geocoding HTTP failure for query {Query}", query);
            return null;
        }
    }

    private sealed record NominatimSearchResult(
        [property: JsonPropertyName("lat")] string Latitude,
        [property: JsonPropertyName("lon")] string Longitude);
}

public class CheckDeliveryQueryHandler(IDeliveryGeocodingService geocodingService, IOptions<DeliveryZoneOptions> options) : IRequestHandler<CheckDeliveryQuery, DeliveryCheckResult>
{
    public async Task<DeliveryCheckResult> Handle(CheckDeliveryQuery request, CancellationToken cancellationToken)
    {
        var zone = options.Value;
        var location = await geocodingService.GeocodeAsync(request, cancellationToken);
        if (location is null)
        {
            return new DeliveryCheckResult(false, "We could not verify this address. Please check the street, house number, postal code, and city.", null);
        }

        var distanceKm = CalculateDistanceKm(zone.CenterLatitude, zone.CenterLongitude, location.Value.Latitude, location.Value.Longitude);
        var roundedDistance = Math.Round(distanceKm, 1);
        var eligible = distanceKm <= zone.RadiusKm;
        return new DeliveryCheckResult(
            eligible,
            eligible
                ? $"Delivery available. This address is about {roundedDistance:0.0} km from our {zone.CenterLabel} delivery zone center."
                : $"This address is about {roundedDistance:0.0} km from our {zone.CenterLabel} delivery zone center, outside our {zone.RadiusKm:0.#} km delivery radius.",
            eligible ? zone.DeliveryFee : null);
    }

    private static double CalculateDistanceKm(double centerLatitude, double centerLongitude, double addressLatitude, double addressLongitude)
    {
        const double earthRadiusKm = 6371;

        var latDiff = DegreesToRadians(addressLatitude - centerLatitude);
        var lonDiff = DegreesToRadians(addressLongitude - centerLongitude);
        var startLat = DegreesToRadians(centerLatitude);
        var endLat = DegreesToRadians(addressLatitude);

        var haversine = Math.Sin(latDiff / 2) * Math.Sin(latDiff / 2)
            + Math.Cos(startLat) * Math.Cos(endLat) * Math.Sin(lonDiff / 2) * Math.Sin(lonDiff / 2);
        var arc = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return earthRadiusKm * arc;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
