using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Payments;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;
using Stripe.Checkout;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(ApiDbContext dbContext, StripePaymentService stripePaymentService) : ControllerBase
{
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new { message = "Missing Stripe signature header." });
        }

        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = stripePaymentService.ConstructWebhookEvent(payload, signature);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Stripe.StripeException)
        {
            return BadRequest(new { message = exception.Message });
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
                await ApplyCheckoutUpdateAsync((Session)stripeEvent.Data.Object, true, cancellationToken);
                break;
            case "checkout.session.async_payment_failed":
            case "checkout.session.expired":
                await ApplyCheckoutUpdateAsync((Session)stripeEvent.Data.Object, false, cancellationToken);
                break;
        }

        return Ok();
    }

    [HttpPost("stripe/checkout/{orderId:int}/confirm")]
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin},{RoleNames.Customer}")]
    public async Task<IActionResult> ConfirmStripeCheckoutPayment(int orderId, [FromQuery] string? sessionId, CancellationToken cancellationToken)
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdText, out var userId))
        {
            return Unauthorized();
        }

        var order = await dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);

        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        if (!PaymentMethodCatalog.IsStripeManaged(order.PaymentMethod))
        {
            return BadRequest(new { message = "Only Stripe-managed payments can be confirmed." });
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = await dbContext.PaymentTransactions
                .Where(x => x.OrderId == orderId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => x.ProviderSessionRef)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { message = "Missing Stripe session id." });
        }

        try
        {
            await ApplyCheckoutUpdateAsync(new Session
            {
                Id = sessionId,
                ClientReferenceId = orderId.ToString()
            }, true, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Stripe.StripeException)
        {
            return BadRequest(new { message = exception.Message });
        }

        return Ok(new { orderId, status = "confirmed" });
    }

    private async Task ApplyCheckoutUpdateAsync(Session session, bool succeeded, CancellationToken cancellationToken)
    {
        var details = await stripePaymentService.GetPaymentSessionDetailsAsync(session.Id, cancellationToken);

        var orderIdText = session.Metadata?.GetValueOrDefault("order_id")
            ?? session.ClientReferenceId;

        if (!int.TryParse(orderIdText, out var orderId))
        {
            return;
        }

        var order = await dbContext.Orders
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            return;
        }

        var paymentTransaction = await dbContext.PaymentTransactions
            .Where(x => x.OrderId == order.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var resolvedPaymentType = PaymentMethodCatalog.FromStripeMethodType(details.PaymentMethodType) ?? order.PaymentMethod;

        if (paymentTransaction is null)
        {
            paymentTransaction = new ApiPaymentTransaction
            {
                OrderId = order.Id,
                Provider = "STRIPE",
                PaymentType = order.PaymentMethod,
                Amount = order.Total,
                Currency = "EUR",
                CreatedAtUtc = now
            };
            dbContext.PaymentTransactions.Add(paymentTransaction);
        }

        paymentTransaction.Provider = "STRIPE";
        paymentTransaction.PaymentType = resolvedPaymentType;
        paymentTransaction.ProviderRef = details.PaymentIntentId ?? details.SessionId;
        paymentTransaction.ProviderPaymentIntentRef = details.PaymentIntentId;
        paymentTransaction.ProviderSessionRef = details.SessionId;
        paymentTransaction.ProviderChargeRef = details.ChargeId;
        paymentTransaction.RawProviderStatus = details.RawProviderStatus ?? details.PaymentStatus;
        paymentTransaction.FeeAmount = details.FeeAmount;
        paymentTransaction.NetAmount = details.NetAmount;
        paymentTransaction.MetadataJson = details.MetadataJson;
        paymentTransaction.UpdatedAtUtc = now;

        if (succeeded && paymentTransaction.PaymentMethodId is null && !string.IsNullOrWhiteSpace(details.PaymentMethodId))
        {
            paymentTransaction.PaymentMethodId = await UpsertPaymentMethodAsync(order.UserId, resolvedPaymentType, details, cancellationToken);
        }

        var previousStatus = order.Status;
        var previousPaymentStatus = order.PaymentStatus;

        order.PaymentMethod = resolvedPaymentType;
        if (succeeded)
        {
            order.PaymentStatus = OrderPaymentStatuses.Paid;
            if (order.Status is OrderStatuses.Pending or OrderStatuses.Cancelled)
            {
                order.Status = OrderStatuses.AwaitingPickup;
            }
        }
        else
        {
            order.PaymentStatus = OrderPaymentStatuses.Failed;
            if (order.Status == OrderStatuses.Pending)
            {
                order.Status = OrderStatuses.Cancelled;
            }
        }

        order.UpdatedAtUtc = now;
        paymentTransaction.Status = succeeded ? PaymentTransactionStatuses.Succeeded : PaymentTransactionStatuses.Failed;

        if (order.Status != previousStatus || order.PaymentStatus != previousPaymentStatus)
        {
            dbContext.OrderStatusHistory.Add(new ApiOrderStatusHistory
            {
                OrderId = order.Id,
                Status = order.Status,
                Note = succeeded
                    ? "Stripe payment confirmed. Order moved to rider pickup queue."
                    : "Stripe payment failed or expired.",
                ChangedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int?> UpsertPaymentMethodAsync(int userId, string paymentType, StripePaymentSessionDetails details, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(details.PaymentMethodId))
        {
            return null;
        }

        var paymentMethod = await dbContext.PaymentMethods
            .SingleOrDefaultAsync(
                x => x.UserId == userId
                    && x.Provider == "STRIPE"
                    && x.ProviderPaymentMethodRef == details.PaymentMethodId,
                cancellationToken);

        if (paymentMethod is null)
        {
            paymentMethod = new ApiPaymentMethod
            {
                UserId = userId,
                Provider = "STRIPE",
                ProviderPaymentMethodRef = details.PaymentMethodId,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.PaymentMethods.Add(paymentMethod);
        }

        paymentMethod.Type = paymentType;
        paymentMethod.DisplayLabel = details.DisplayLabel;
        paymentMethod.Last4 = details.Last4;
        paymentMethod.ExpiryMonth = details.ExpiryMonth;
        paymentMethod.ExpiryYear = details.ExpiryYear;
        paymentMethod.Country = details.Country;
        paymentMethod.Fingerprint = details.Fingerprint;
        paymentMethod.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return paymentMethod.Id;
    }
}