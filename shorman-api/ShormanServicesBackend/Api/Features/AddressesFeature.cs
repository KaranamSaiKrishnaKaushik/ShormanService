using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Features;

public record GetAddressesQuery(int UserId) : IRequest<IReadOnlyCollection<AddressDto>>;
public record AddAddressCommand(int UserId, CreateAddressRequest Request) : IRequest<AddressDto>;
public record UpdateAddressCommand(int UserId, int AddressId, UpdateAddressRequest Request) : IRequest<AddressDto?>;
public record DeleteAddressCommand(int UserId, int AddressId) : IRequest<bool>;
public record SetDefaultAddressCommand(int UserId, int AddressId) : IRequest<AddressDto?>;

public class GetAddressesQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetAddressesQuery, IReadOnlyCollection<AddressDto>>
{
    public async Task<IReadOnlyCollection<AddressDto>> Handle(GetAddressesQuery request, CancellationToken cancellationToken)
    {
        var addresses = await dbContext.Addresses
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return addresses.Select(Map).ToList();
    }

    internal static AddressDto Map(ApiAddress address) =>
        new(address.Id, address.UserId, address.Label, address.Street, address.HouseNumber, address.PostalCode, address.City, address.Country, address.IsDefault);
}

public class AddAddressCommandHandler(ApiDbContext dbContext) : IRequestHandler<AddAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(AddAddressCommand request, CancellationToken cancellationToken)
    {
        if (request.Request.IsDefault)
        {
            await UnsetDefaultAddressesAsync(request.UserId, cancellationToken);
        }

        var address = new ApiAddress
        {
            UserId = request.UserId,
            Label = request.Request.Label?.Trim(),
            Street = request.Request.Street.Trim(),
            HouseNumber = request.Request.HouseNumber.Trim(),
            PostalCode = request.Request.PostalCode.Trim(),
            City = request.Request.City.Trim(),
            Country = request.Request.Country.Trim(),
            IsDefault = request.Request.IsDefault
        };

        dbContext.Addresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        return GetAddressesQueryHandler.Map(address);
    }

    private Task UnsetDefaultAddressesAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Addresses
            .Where(x => x.UserId == userId && x.IsDefault)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
}

public class UpdateAddressCommandHandler(ApiDbContext dbContext) : IRequestHandler<UpdateAddressCommand, AddressDto?>
{
    public async Task<AddressDto?> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == request.UserId, cancellationToken);
        if (address is null)
        {
            return null;
        }

        if (request.Request.IsDefault)
        {
            await dbContext.Addresses
                .Where(x => x.UserId == request.UserId && x.Id != address.Id && x.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
        }

        address.Label = request.Request.Label?.Trim();
        address.Street = request.Request.Street.Trim();
        address.HouseNumber = request.Request.HouseNumber.Trim();
        address.PostalCode = request.Request.PostalCode.Trim();
        address.City = request.Request.City.Trim();
        address.Country = request.Request.Country.Trim();
        address.IsDefault = request.Request.IsDefault;

        await dbContext.SaveChangesAsync(cancellationToken);
        return GetAddressesQueryHandler.Map(address);
    }
}

public class DeleteAddressCommandHandler(ApiDbContext dbContext) : IRequestHandler<DeleteAddressCommand, bool>
{
    public async Task<bool> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == request.UserId, cancellationToken);
        if (address is null)
        {
            return false;
        }

        var wasDefault = address.IsDefault;
        dbContext.Addresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (wasDefault)
        {
            var nextAddress = await dbContext.Addresses.Where(x => x.UserId == request.UserId).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            if (nextAddress is not null)
            {
                nextAddress.IsDefault = true;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return true;
    }
}

public class SetDefaultAddressCommandHandler(ApiDbContext dbContext) : IRequestHandler<SetDefaultAddressCommand, AddressDto?>
{
    public async Task<AddressDto?> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == request.UserId, cancellationToken);
        if (address is null)
        {
            return null;
        }

        await dbContext.Addresses
            .Where(x => x.UserId == request.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);

        address.IsDefault = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return GetAddressesQueryHandler.Map(address);
    }
}
