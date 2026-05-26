namespace ShormanServicesBackend.Api.Payments;

public static class PaymentMethodCatalog
{
    public const string StripeCard = "STRIPE_CARD";
    public const string StripeSepaDebit = "STRIPE_SEPA_DEBIT";
    public const string StripeKlarna = "STRIPE_KLARNA";
    public const string StripePaypal = "STRIPE_PAYPAL";
    public const string StripePaypalGermany = "STRIPE_PAYPAL_GERMANY";
    public const string CashOnDelivery = "CASH_ON_DELIVERY";

    public static bool IsSupported(string paymentMethod)
    {
        var normalized = Normalize(paymentMethod);
        return normalized is StripeCard or StripeSepaDebit or StripeKlarna or StripePaypal or StripePaypalGermany or CashOnDelivery;
    }

    public static bool IsStripeManaged(string paymentMethod)
    {
        var normalized = Normalize(paymentMethod);
        return normalized is StripeCard or StripeSepaDebit or StripeKlarna or StripePaypal or StripePaypalGermany;
    }

    public static bool IsPaypal(string paymentMethod)
    {
        var normalized = Normalize(paymentMethod);
        return normalized is StripePaypal or StripePaypalGermany;
    }

    public static bool IsCashOnDelivery(string paymentMethod) =>
        string.Equals(paymentMethod, CashOnDelivery, StringComparison.OrdinalIgnoreCase);

    public static string ToStripeMethodType(string paymentMethod) => paymentMethod switch
    {
        StripeCard => "card",
        StripeSepaDebit => "sepa_debit",
        StripeKlarna => "klarna",
        StripePaypal => "paypal",
        StripePaypalGermany => "paypal",
        _ => throw new InvalidOperationException($"Unsupported Stripe payment method: {paymentMethod}")
    };

    public static string? FromStripeMethodType(string? stripePaymentMethodType) => stripePaymentMethodType switch
    {
        "card" => StripeCard,
        "sepa_debit" => StripeSepaDebit,
        "klarna" => StripeKlarna,
        "paypal" => StripePaypal,
        _ => null
    };

    private static string Normalize(string paymentMethod) => paymentMethod.Trim().ToUpperInvariant();
}

public static class PaymentTransactionStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Canceled = "CANCELED";
}