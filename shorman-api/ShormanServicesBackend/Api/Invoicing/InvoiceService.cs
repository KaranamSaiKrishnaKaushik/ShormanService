using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Invoicing;

public sealed record InvoiceFile(string FileName, byte[] Content);

public interface IInvoiceService
{
    /// <summary>Creates the invoice for a delivered order if it does not exist yet (idempotent).</summary>
    Task<ApiInvoice?> EnsureInvoiceAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Returns the PDF for an order the user may access, or null when not available.</summary>
    Task<InvoiceFile?> GetInvoiceFileAsync(int orderId, int userId, bool isAdmin, CancellationToken cancellationToken);
}

public sealed class InvoiceService(
    ApiDbContext dbContext,
    IOptions<InvoiceOptions> options,
    IInvoiceStorage storage,
    ILogger<InvoiceService> logger) : IInvoiceService
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private const string Brand = "#0F7A5A";
    private const string Ink = "#17221E";
    private const string Muted = "#5B6A64";
    private const string Rule = "#D5E0DA";
    private const string Band = "#F2F7F4";
    private const string Display = "Bricolage Grotesque";
    private const string Body = "IBM Plex Sans";

    private const string LogoSvg =
        "<svg viewBox=\"0 0 64 64\" xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#E9F5EC\"/>" +
        "<path d=\"M32 8C21 8 13 16 13 27C13 41 32 57 32 57S51 41 51 27C51 16 43 8 32 8Z\" fill=\"#0F7A5A\"/>" +
        "<path d=\"M24 33C24 22 33 18 41 19C41 29 35 35 24 33Z\" fill=\"#9BE36D\"/></svg>";

    private const string PaidDotSvg =
        "<svg viewBox=\"0 0 16 16\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"8\" cy=\"8\" r=\"5\" fill=\"#9BE36D\" stroke=\"#0F7A5A\" stroke-width=\"2\"/></svg>";

    private readonly InvoiceOptions invoiceOptions = options.Value;

    static InvoiceService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

        var fontDir = Path.Combine(AppContext.BaseDirectory, "Api", "Invoicing", "Fonts");
        if (Directory.Exists(fontDir))
        {
            foreach (var file in Directory.GetFiles(fontDir, "*.ttf"))
            {
                QuestPDF.Drawing.FontManager.RegisterFontFromFile(file);
            }
        }
    }

    public async Task<ApiInvoice?> EnsureInvoiceAsync(int orderId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Invoices.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var order = await LoadOrderAsync(orderId, cancellationToken);
        if (order is null || order.DeliveredAtUtc is null)
        {
            return null;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var next = await NextSequenceAsync(cancellationToken);
            var invoice = new ApiInvoice
            {
                OrderId = order.Id,
                SequenceNumber = next,
                InvoiceNumber = FormatNumber(next),
                InvoiceDateUtc = DateTime.UtcNow,
                StoragePath = string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            var pdf = RenderPdf(order, invoice);
            invoice.StoragePath = await storage.SaveAsync($"{invoice.InvoiceNumber}.pdf", pdf, cancellationToken);

            dbContext.Invoices.Add(invoice);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return invoice;
            }
            catch (DbUpdateException exception)
            {
                dbContext.Entry(invoice).State = EntityState.Detached;
                var raced = await dbContext.Invoices.AsNoTracking().AnyAsync(x => x.OrderId == orderId, cancellationToken);
                if (raced)
                {
                    return await dbContext.Invoices.SingleAsync(x => x.OrderId == orderId, cancellationToken);
                }

                logger.LogWarning(exception, "Invoice number {Number} conflicted, retrying.", invoice.InvoiceNumber);
            }
        }

        throw new InvalidOperationException("Could not allocate an invoice number.");
    }

    public async Task<InvoiceFile?> GetInvoiceFileAsync(int orderId, int userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => new { x.UserId, x.DeliveredAtUtc })
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null || order.DeliveredAtUtc is null || (!isAdmin && order.UserId != userId))
        {
            return null;
        }

        var invoice = await EnsureInvoiceAsync(orderId, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var bytes = await storage.ReadAsync(invoice.StoragePath, cancellationToken);
        if (bytes is null)
        {
            // File went missing (e.g. storage moved): regenerate with the same number.
            var fullOrder = await LoadOrderAsync(orderId, cancellationToken);
            bytes = RenderPdf(fullOrder!, invoice);
            await storage.SaveAsync(invoice.StoragePath, bytes, cancellationToken);
        }

        return new InvoiceFile($"{invoice.InvoiceNumber}.pdf", bytes);
    }

    private Task<ApiOrder?> LoadOrderAsync(int orderId, CancellationToken cancellationToken) =>
        dbContext.Orders.AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

    private async Task<long> NextSequenceAsync(CancellationToken cancellationToken)
    {
        var max = await dbContext.Invoices.MaxAsync(x => (long?)x.SequenceNumber, cancellationToken);
        return max is null ? invoiceOptions.StartingNumber : Math.Max(max.Value + 1, invoiceOptions.StartingNumber);
    }

    private string FormatNumber(long sequence) =>
        invoiceOptions.NumberPrefix + sequence.ToString(CultureInfo.InvariantCulture).PadLeft(invoiceOptions.NumberPadding, '0');

    // Removes emoji/symbol characters the invoice font cannot render.
    private static string StripUnsupported(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var cat = System.Text.Rune.GetUnicodeCategory(rune);
            if (rune.Value <= 0x24F || cat is not (System.Globalization.UnicodeCategory.OtherSymbol
                or System.Globalization.UnicodeCategory.Surrogate or System.Globalization.UnicodeCategory.Format
                or System.Globalization.UnicodeCategory.PrivateUse or System.Globalization.UnicodeCategory.ModifierSymbol)
                && rune.Value < 0x2000)
            {
                sb.Append(rune.ToString());
            }
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static string Money(decimal value) => value.ToString("N2", De) + " €";

    private static (decimal Net, decimal Vat) SplitGross(decimal gross, decimal ratePercent)
    {
        var net = Math.Round(gross / (1 + ratePercent / 100m), 2, MidpointRounding.AwayFromZero);
        return (net, gross - net);
    }

    private static string PaymentLabel(string method) => method.ToUpperInvariant() switch
    {
        "STRIPE_CARD" => "Kreditkarte / Card",
        "STRIPE_SEPA_DEBIT" => "SEPA-Lastschrift / SEPA debit",
        "STRIPE_KLARNA" => "Klarna",
        "STRIPE_PAYPAL" => "PayPal",
        "CASH_ON_DELIVERY" => "Barzahlung bei Lieferung / Cash on delivery",
        _ => method
    };

    private byte[] RenderPdf(ApiOrder order, ApiInvoice invoice)
    {
        var o = invoiceOptions;
        var itemRate = o.ItemVatRatePercent;
        var feeRate = o.DeliveryFeeVatRatePercent;

        var itemsGross = order.Items.Sum(x => x.TotalPrice);
        var (itemsNet, itemsVat) = SplitGross(itemsGross, itemRate);
        var (feeNet, feeVat) = SplitGross(order.DeliveryFee, feeRate);
        var totalNet = itemsNet + feeNet;
        var totalVat = itemsVat + feeVat;
        var total = itemsGross + order.DeliveryFee;

        var deliveredAt = (order.DeliveredAtUtc ?? invoice.InvoiceDateUtc).ToLocalTime();
        var recipient = string.IsNullOrWhiteSpace(order.CustomerNameSnapshot) ? "Kunde / Customer" : order.CustomerNameSnapshot!;
        var senderLine = $"{o.CompanyName} · {o.Street} · {o.PostalCodeCity}";

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontFamily(Body, "Lato").FontSize(9.5f).FontColor(Ink));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Row(brand =>
                        {
                            brand.ConstantItem(36).Height(36).Svg(LogoSvg);
                            brand.ConstantItem(10);
                            brand.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Shorman").FontFamily(Display).FontSize(22).Bold().FontColor(Brand);
                                c.Item().Text("Lebensmittel-Lieferdienst · Frankfurt am Main").FontSize(8.5f).FontColor(Muted);
                            });
                        });
                        row.ConstantItem(180).AlignRight().AlignMiddle().Text(t =>
                        {
                            t.Span("Rechnung").FontFamily(Display).FontSize(22).Medium();
                            t.Span(" / Invoice").FontFamily(Display).FontSize(14).FontColor(Muted);
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Rule);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(senderLine).FontSize(7.5f).FontColor(Muted).Underline();
                            c.Item().PaddingTop(6).Text(recipient).Bold();
                            c.Item().Text($"{order.Address.Street} {order.Address.HouseNumber}");
                            c.Item().Text($"{order.Address.PostalCode} {order.Address.City}");
                            c.Item().Text(order.Address.Country);
                        });

                        row.ConstantItem(230).Background(Band).Padding(8).Column(c =>
                        {
                            Meta(c, "Rechnungsnr. / Invoice no.", invoice.InvoiceNumber);
                            Meta(c, "Rechnungsdatum / Date", invoice.InvoiceDateUtc.ToLocalTime().ToString("dd.MM.yyyy", De));
                            Meta(c, "Lieferdatum / Delivery date", deliveredAt.ToString("dd.MM.yyyy", De));
                            Meta(c, "Bestellnr. / Order no.", order.Id.ToString(CultureInfo.InvariantCulture));
                            Meta(c, "Kundennr. / Customer no.", $"K-{order.UserId}");
                        });
                    });

                    col.Item().Text("Vielen Dank für Ihre Bestellung. Wir berechnen Ihnen folgende Leistungen. / Thank you for your order. We charge you for the following.");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(26);
                            c.RelativeColumn(4);
                            c.RelativeColumn(2);
                            c.ConstantColumn(40);
                            c.ConstantColumn(62);
                            c.ConstantColumn(40);
                            c.ConstantColumn(66);
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "Pos.", "Artikel / Item", "Markt / Store", "Menge", "Einzelpreis", "MwSt", "Betrag" })
                            {
                                h.Cell().Background(Brand).Padding(4).Text(title).FontColor(Colors.White).Bold().FontSize(8.5f);
                            }
                        });

                        var pos = 1;
                        foreach (var item in order.Items)
                        {
                            Cell(table, pos++.ToString(CultureInfo.InvariantCulture));
                            Cell(table, StripUnsupported(System.Net.WebUtility.HtmlDecode(item.ProductName)));
                            Cell(table, item.SupermarketName ?? "-", badge: true);
                            Cell(table, item.Quantity.ToString(CultureInfo.InvariantCulture), right: true);
                            Cell(table, Money(item.UnitPrice), right: true);
                            Cell(table, $"{itemRate:0.##} %", right: true);
                            Cell(table, Money(item.TotalPrice), right: true);
                        }

                        if (order.DeliveryFee > 0)
                        {
                            Cell(table, pos.ToString(CultureInfo.InvariantCulture), band: true);
                            Cell(table, "Liefergebühr / Delivery fee", band: true);
                            Cell(table, "", band: true);
                            Cell(table, "1", right: true, band: true);
                            Cell(table, Money(order.DeliveryFee), right: true, band: true);
                            Cell(table, $"{feeRate:0.##} %", right: true, band: true);
                            Cell(table, Money(order.DeliveryFee), right: true, band: true);
                        }
                    });

                    col.Item().AlignRight().Width(300).Column(c =>
                    {
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn();
                                d.RelativeColumn();
                                d.RelativeColumn();
                                d.RelativeColumn();
                            });
                            foreach (var title in new[] { "Satz / Rate", "Netto / Net", "USt / VAT", "Brutto / Gross" })
                            {
                                t.Cell().BorderBottom(1).BorderColor(Rule).Padding(3).Text(title).FontSize(8).Bold().FontColor(Muted);
                            }

                            void Row(decimal rate, decimal net, decimal vat, decimal gross)
                            {
                                Cell(t, $"{rate:0.##} %");
                                Cell(t, Money(net), right: true);
                                Cell(t, Money(vat), right: true);
                                Cell(t, Money(gross), right: true);
                            }

                            Row(itemRate, itemsNet, itemsVat, itemsGross);
                            if (order.DeliveryFee > 0)
                            {
                                Row(feeRate, feeNet, feeVat, order.DeliveryFee);
                            }
                        });

                        c.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Text("Summe netto / Net total");
                            r.ConstantItem(90).AlignRight().Text(Money(totalNet));
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("zzgl. Umsatzsteuer / VAT");
                            r.ConstantItem(90).AlignRight().Text(Money(totalVat));
                        });
                        c.Item().PaddingTop(4).Background(Band).Padding(6).Row(r =>
                        {
                            r.RelativeItem().Text("Gesamtbetrag / Total").Bold().FontSize(11);
                            r.ConstantItem(90).AlignRight().Text(Money(total)).Bold().FontSize(11).FontColor(Brand);
                        });
                    });

                    var paid = order.PaymentStatus is "PAID" or "CASH_COLLECTED" or "SUCCEEDED";
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(12).AlignMiddle().Height(10).Svg(PaidDotSvg);
                        r.ConstantItem(6);
                        r.RelativeItem().Text($"{(paid ? "Bezahlt / Paid" : "Zahlungsart / Payment")}: {PaymentLabel(order.PaymentMethod)}")
                            .Medium().FontColor(Brand);
                    });

                    col.Item().Text(
                        "Alle Artikel wurden von Shorman Service im eigenen Namen eingekauft und an Sie verkauft. Die Angabe des Markts dient nur Ihrer Information. " +
                        "/ Shorman Service bought all items in its own name and sold them to you; the store is shown for your information only.")
                        .FontSize(8.5f).FontColor(Muted);

                    col.Item().Text($"Fragen zur Rechnung: {o.Email}, bitte Rechnungsnummer angeben. / Questions: {o.Email}, please quote the invoice number.")
                        .FontSize(8.5f).FontColor(Muted);
                });

                page.Footer().BorderTop(1).BorderColor(Rule).PaddingTop(6).Row(row =>
                {
                    FooterBlock(row, o.CompanyName, o.OwnerLine, o.Street, o.PostalCodeCity);
                    FooterBlock(row, "Kontakt", o.Email, o.Website);
                    FooterBlock(row, "Steuern", $"St.-Nr. {o.TaxNumber}", $"USt-IdNr. {o.VatId}", o.TaxOffice);
                    FooterBlock(row, "Bank", o.BankName, $"IBAN {o.Iban}", $"BIC {o.Bic}");
                });
            });
        }).GeneratePdf();
    }

    private static void Meta(ColumnDescriptor c, string label, string value) =>
        c.Item().PaddingVertical(1).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8).FontColor(Muted);
            r.ConstantItem(90).AlignRight().Text(value).Bold();
        });

    private static void Cell(TableDescriptor table, string text, bool right = false, bool band = false, bool badge = false)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor(Rule);
        if (band)
        {
            cell = cell.Background(Band);
        }

        var inner = cell.Padding(4);
        if (badge && text != "-")
        {
            inner.AlignLeft().Border(0.75f).BorderColor(Rule).Background(Band).PaddingHorizontal(5).PaddingVertical(1)
                .Text(text.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Brand);
            return;
        }

        (right ? inner.AlignRight() : inner).Text(text).FontSize(9);
    }

    private static void FooterBlock(RowDescriptor row, string title, params string[] lines) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(7.5f);
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                c.Item().Text(line).FontSize(7.5f).FontColor(Muted);
            }
        });
}
