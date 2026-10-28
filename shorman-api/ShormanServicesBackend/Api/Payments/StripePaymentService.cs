using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Payments;

public sealed record StripeCheckoutSessionResult(string SessionId, string CheckoutUrl, string? PaymentIntentId);

public sealed record StripePaymentSessionDetails(
    string SessionId,
    string? PaymentIntentId,
    string? PaymentMethodId,
    string? PaymentMethodType,
    string? ChargeId,
    string PaymentStatus,
    string? RawProviderStatus,
    decimal? FeeAmount,
    decimal? NetAmount,
    string? DisplayLabel,
    string? Last4,
    byte? ExpiryMonth,
    short? ExpiryYear,
    string? Country,
    string? Fingerprint,
    string? MetadataJson);

public sealed class StripePaymentService(IOptions<StripeOptions> options)
{
    private readonly StripeOptions stripeOptions = options.Value;

    public Event ConstructWebhookEvent(string payload, string signatureHeader)
    {
        EnsureWebhookConfigured();
        return EventUtility.ConstructEvent(payload, signatureHeader, stripeOptions.WebhookSecret);
    }

    public async Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(ApiOrder order, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var sessionService = new SessionService(BuildStripeClient());
        var currency = stripeOptions.Currency.ToLowerInvariant();
        var paymentType = PaymentMethodCatalog.ToStripeMethodType(order.PaymentMethod);
        var metadata = new Dictionary<string, string>
        {
            ["order_id"] = order.Id.ToString(),
            ["user_id"] = order.UserId.ToString(),
            ["payment_method"] = order.PaymentMethod
        };

        var lineItems = order.Items.Select(item => new SessionLineItemOptions
        {
            Quantity = item.Quantity,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = currency,
                UnitAmount = ToMinorUnits(item.UnitPrice),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = item.ProductName,
                    Description = item.SupermarketName
                }
            }
        }).ToList();

        lineItems.Add(new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = currency,
                UnitAmount = ToMinorUnits(order.DeliveryFee),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = "Delivery fee"
                }
            }
        });

        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = BuildReturnUrl(stripeOptions.CheckoutSuccessPath, $"payment=success&orderId={order.Id}&session_id={{CHECKOUT_SESSION_ID}}"),
            CancelUrl = BuildReturnUrl(stripeOptions.CheckoutCancelPath, $"payment=cancelled&orderId={order.Id}"),
            BillingAddressCollection = "required",
            ClientReferenceId = order.Id.ToString(),
            CustomerEmail = order.CustomerEmailSnapshot,
            Locale = "auto",
            Metadata = metadata,
            PaymentMethodTypes = [paymentType],
            PhoneNumberCollection = new SessionPhoneNumberCollectionOptions { Enabled = true },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata,
                Description = $"Shorman order #{order.Id}"
            },
            LineItems = lineItems
        }, cancellationToken: cancellationToken);

        return new StripeCheckoutSessionResult(
            session.Id,
            session.Url ?? throw new InvalidOperationException("Stripe Checkout did not return a redirect URL."),
            session.PaymentIntentId);
    }

    public async Task<StripePaymentSessionDetails> GetPaymentSessionDetailsAsync(string sessionId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var sessionService = new SessionService(BuildStripeClient());
        var session = await sessionService.GetAsync(sessionId, new SessionGetOptions
        {
            Expand = ["payment_intent.payment_method", "payment_intent.latest_charge.balance_transaction"]
        }, cancellationToken: cancellationToken);

        var paymentIntent = session.PaymentIntent;
        var paymentMethod = paymentIntent?.PaymentMethod;
        var charge = paymentIntent?.LatestCharge;
        var balanceTransaction = charge?.BalanceTransaction;

        return new StripePaymentSessionDetails(
            session.Id,
            paymentIntent?.Id ?? session.PaymentIntentId,
            paymentMethod?.Id,
            paymentMethod?.Type,
            charge?.Id,
            session.PaymentStatus ?? "unpaid",
            paymentIntent?.Status ?? session.Status,
            FromMinorUnits(balanceTransaction?.Fee),
            FromMinorUnits(balanceTransaction?.Net),
            BuildDisplayLabel(paymentMethod),
            GetLast4(paymentMethod),
            GetExpiryMonth(paymentMethod),
            GetExpiryYear(paymentMethod),
            GetCountry(paymentMethod),
            GetFingerprint(paymentMethod),
            session.Metadata?.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(session.Metadata) : null);
    }

    private StripeClient BuildStripeClient() => new(stripeOptions.SecretKey);

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }
    }

    private void EnsureWebhookConfigured()
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(stripeOptions.WebhookSecret))
        {
            throw new InvalidOperationException("Stripe webhook secret is not configured.");
        }
    }

    private string BuildReturnUrl(string path, string queryString)
    {
        var baseUri = stripeOptions.FrontendBaseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        return $"{baseUri}{normalizedPath}?{queryString}";
    }

    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static decimal? FromMinorUnits(long? amount) => amount.HasValue ? decimal.Round(amount.Value / 100m, 2) : null;

    private static string? BuildDisplayLabel(PaymentMethod? paymentMethod) => paymentMethod?.Type switch
    {
        "card" when paymentMethod.Card is not null => $"{paymentMethod.Card.Brand?.ToUpperInvariant()} ending {paymentMethod.Card.Last4}",
        "sepa_debit" when paymentMethod.SepaDebit is not null => $"SEPA ending {paymentMethod.SepaDebit.Last4}",
        "klarna" => "Klarna",
        "paypal" => "PayPal",
        _ => paymentMethod?.Type
    };

    private static string? GetLast4(PaymentMethod? paymentMethod) => paymentMethod?.Type switch
    {
        "card" => paymentMethod.Card?.Last4,
        "sepa_debit" => paymentMethod.SepaDebit?.Last4,
        _ => null
    };

    private static byte? GetExpiryMonth(PaymentMethod? paymentMethod) => paymentMethod?.Type == "card" && paymentMethod.Card?.ExpMonth is long month
        ? checked((byte)month)
        : null;

    private static short? GetExpiryYear(PaymentMethod? paymentMethod) => paymentMethod?.Type == "card" && paymentMethod.Card?.ExpYear is long year
        ? checked((short)year)
        : null;

    private static string? GetCountry(PaymentMethod? paymentMethod) => paymentMethod?.Type switch
    {
        "card" => paymentMethod.Card?.Country,
        "sepa_debit" => paymentMethod.SepaDebit?.Country,
        _ => null
    };

    private static string? GetFingerprint(PaymentMethod? paymentMethod) => paymentMethod?.Type switch
    {
        "card" => paymentMethod.Card?.Fingerprint,
        "sepa_debit" => paymentMethod.SepaDebit?.Fingerprint,
        _ => null
    };
}