using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Features;

public record ActivePricingPolicy(int? VersionId, int VersionNo, decimal XFactorPercent, decimal YFactorAmount, decimal DeliveryCharge);

public interface IPricingPolicyProvider
{
    Task<ActivePricingPolicy> GetActivePolicyAsync(CancellationToken cancellationToken);
    decimal ApplyProductPrice(decimal basePrice, ActivePricingPolicy policy);
    void InvalidateCache();
}

public sealed class PricingPolicyProvider(
    ApiDbContext dbContext,
    IOptions<DeliveryZoneOptions> deliveryOptions,
    IMemoryCache cache) : IPricingPolicyProvider
{
    private const string ActivePolicyCacheKey = "pricing-policy:active";

    public async Task<ActivePricingPolicy> GetActivePolicyAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<ActivePricingPolicy>(ActivePolicyCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var active = await dbContext.PricingPolicyVersions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new ActivePricingPolicy(x.Id, x.VersionNo, x.XFactorPercent, x.YFactorAmount, x.DeliveryCharge))
            .FirstOrDefaultAsync(cancellationToken);

        active ??= new ActivePricingPolicy(null, 0, 0m, 0m, deliveryOptions.Value.DeliveryFee);

        cache.Set(ActivePolicyCacheKey, active, TimeSpan.FromSeconds(45));
        return active;
    }

    public decimal ApplyProductPrice(decimal basePrice, ActivePricingPolicy policy)
    {
        var adjusted = (basePrice * (1m + (policy.XFactorPercent / 100m))) + policy.YFactorAmount;
        if (adjusted < 0m)
        {
            adjusted = 0m;
        }

        return Math.Round(adjusted, 2, MidpointRounding.AwayFromZero);
    }

    public void InvalidateCache() => cache.Remove(ActivePolicyCacheKey);
}

public record GetCurrentPricingPolicyQuery() : IRequest<PricingPolicyVersionDto>;
public record GetPricingPolicyHistoryQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResultDto<PricingPolicyVersionDto>>;
public record GetPricingPolicyAuditQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResultDto<PricingPolicyAuditEventDto>>;
public record ApplyPricingPolicyCommand(int UserId, ApplyPricingPolicyRequest Request) : IRequest<PricingPolicyVersionDto>;

public sealed class GetCurrentPricingPolicyQueryHandler(
    ApiDbContext dbContext,
    IOptions<DeliveryZoneOptions> deliveryOptions) : IRequestHandler<GetCurrentPricingPolicyQuery, PricingPolicyVersionDto>
{
    public async Task<PricingPolicyVersionDto> Handle(GetCurrentPricingPolicyQuery request, CancellationToken cancellationToken)
    {
        var item = await (
                from version in dbContext.PricingPolicyVersions.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on version.CreatedByUserId equals user.Id into users
                from createdBy in users.DefaultIfEmpty()
                where version.IsActive
                orderby version.VersionNo descending
                select new PricingPolicyVersionDto(
                    version.Id,
                    version.VersionNo,
                    version.XFactorPercent,
                    version.YFactorAmount,
                    version.DeliveryCharge,
                    version.IsActive,
                    version.EffectiveFromUtc.ToString("O"),
                    version.EffectiveToUtc.HasValue ? version.EffectiveToUtc.Value.ToString("O") : null,
                    version.Reason,
                    version.CreatedByUserId,
                    createdBy == null ? null : $"{createdBy.FirstName} {createdBy.LastName}".Trim(),
                    version.CreatedAtUtc.ToString("O")))
            .FirstOrDefaultAsync(cancellationToken);

        return item ?? new PricingPolicyVersionDto(
            0,
            0,
            0m,
            0m,
            deliveryOptions.Value.DeliveryFee,
            true,
            DateTime.UtcNow.ToString("O"),
            null,
            "Default fallback policy",
            0,
            "System",
            DateTime.UtcNow.ToString("O"));
    }
}

public sealed class GetPricingPolicyHistoryQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetPricingPolicyHistoryQuery, PagedResultDto<PricingPolicyVersionDto>>
{
    public async Task<PagedResultDto<PricingPolicyVersionDto>> Handle(GetPricingPolicyHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 25,
            > 200 => 200,
            _ => request.PageSize
        };

        var baseQuery = dbContext.PricingPolicyVersions.AsNoTracking();
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await (
                from version in baseQuery
                join user in dbContext.Users.AsNoTracking() on version.CreatedByUserId equals user.Id into users
                from createdBy in users.DefaultIfEmpty()
                orderby version.VersionNo descending
                select new PricingPolicyVersionDto(
                    version.Id,
                    version.VersionNo,
                    version.XFactorPercent,
                    version.YFactorAmount,
                    version.DeliveryCharge,
                    version.IsActive,
                    version.EffectiveFromUtc.ToString("O"),
                    version.EffectiveToUtc.HasValue ? version.EffectiveToUtc.Value.ToString("O") : null,
                    version.Reason,
                    version.CreatedByUserId,
                    createdBy == null ? null : $"{createdBy.FirstName} {createdBy.LastName}".Trim(),
                    version.CreatedAtUtc.ToString("O")))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<PricingPolicyVersionDto>(items, totalCount, page, pageSize);
    }
}

public sealed class GetPricingPolicyAuditQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetPricingPolicyAuditQuery, PagedResultDto<PricingPolicyAuditEventDto>>
{
    public async Task<PagedResultDto<PricingPolicyAuditEventDto>> Handle(GetPricingPolicyAuditQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 25,
            > 200 => 200,
            _ => request.PageSize
        };

        var baseQuery = dbContext.PricingPolicyAuditEvents.AsNoTracking();
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await (
                from audit in baseQuery
                join user in dbContext.Users.AsNoTracking() on audit.ChangedByUserId equals user.Id into users
                from changedBy in users.DefaultIfEmpty()
                orderby audit.ChangedAtUtc descending, audit.Id descending
                select new PricingPolicyAuditEventDto(
                    audit.Id,
                    audit.PolicyVersionId,
                    audit.ActionType,
                    audit.OldXFactorPercent,
                    audit.NewXFactorPercent,
                    audit.OldYFactorAmount,
                    audit.NewYFactorAmount,
                    audit.OldDeliveryCharge,
                    audit.NewDeliveryCharge,
                    audit.ChangedByUserId,
                    changedBy == null ? null : $"{changedBy.FirstName} {changedBy.LastName}".Trim(),
                    audit.ChangedAtUtc.ToString("O"),
                    audit.CorrelationId,
                    audit.MetadataJson))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<PricingPolicyAuditEventDto>(items, totalCount, page, pageSize);
    }
}

public sealed class ApplyPricingPolicyCommandHandler(
    ApiDbContext dbContext,
    IPricingPolicyProvider pricingPolicyProvider) : IRequestHandler<ApplyPricingPolicyCommand, PricingPolicyVersionDto>
{
    public async Task<PricingPolicyVersionDto> Handle(ApplyPricingPolicyCommand request, CancellationToken cancellationToken)
    {
        Validate(request.Request);

        var now = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var active = await dbContext.PricingPolicyVersions
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (active is not null)
        {
            active.IsActive = false;
            active.EffectiveToUtc = now;
        }

        var nextVersion = (await dbContext.PricingPolicyVersions.MaxAsync(x => (int?)x.VersionNo, cancellationToken) ?? 0) + 1;

        var created = new ApiPricingPolicyVersion
        {
            VersionNo = nextVersion,
            XFactorPercent = request.Request.XFactorPercent,
            YFactorAmount = request.Request.YFactorAmount,
            DeliveryCharge = request.Request.DeliveryCharge,
            IsActive = true,
            EffectiveFromUtc = now,
            EffectiveToUtc = null,
            Reason = string.IsNullOrWhiteSpace(request.Request.Reason) ? null : request.Request.Reason.Trim(),
            CreatedByUserId = request.UserId,
            CreatedAtUtc = now
        };

        dbContext.PricingPolicyVersions.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.PricingPolicyAuditEvents.Add(new ApiPricingPolicyAuditEvent
        {
            PolicyVersionId = created.Id,
            ActionType = active is null ? "created" : "activated",
            OldXFactorPercent = active?.XFactorPercent,
            NewXFactorPercent = created.XFactorPercent,
            OldYFactorAmount = active?.YFactorAmount,
            NewYFactorAmount = created.YFactorAmount,
            OldDeliveryCharge = active?.DeliveryCharge,
            NewDeliveryCharge = created.DeliveryCharge,
            ChangedByUserId = request.UserId,
            ChangedAtUtc = now,
            CorrelationId = correlationId,
            MetadataJson = created.Reason
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        pricingPolicyProvider.InvalidateCache();

        var changedByName = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == request.UserId)
            .Select(x => $"{x.FirstName} {x.LastName}".Trim())
            .FirstOrDefaultAsync(cancellationToken);

        return new PricingPolicyVersionDto(
            created.Id,
            created.VersionNo,
            created.XFactorPercent,
            created.YFactorAmount,
            created.DeliveryCharge,
            created.IsActive,
            created.EffectiveFromUtc.ToString("O"),
            null,
            created.Reason,
            created.CreatedByUserId,
            changedByName,
            created.CreatedAtUtc.ToString("O"));
    }

    private static void Validate(ApplyPricingPolicyRequest request)
    {
        if (request.XFactorPercent < -90m || request.XFactorPercent > 500m)
        {
            throw new InvalidOperationException("X-Factor must be between -90 and 500 percent.");
        }

        if (request.YFactorAmount < -500m || request.YFactorAmount > 500m)
        {
            throw new InvalidOperationException("Y-Factor must be between -500 and 500 EUR.");
        }

        if (request.DeliveryCharge < 0m || request.DeliveryCharge > 1000m)
        {
            throw new InvalidOperationException("Delivery charge must be between 0 and 1000 EUR.");
        }
    }
}
