namespace ShormanServicesBackend.Api.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    public string CheckoutSuccessPath { get; set; } = "/checkout";
    public string CheckoutCancelPath { get; set; } = "/checkout";
    public string Currency { get; set; } = "eur";
}