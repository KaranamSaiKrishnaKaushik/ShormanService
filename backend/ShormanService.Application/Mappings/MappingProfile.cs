using AutoMapper;
using ShormanService.Application.DTOs;
using ShormanService.Domain.Entities;

namespace ShormanService.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Product, ProductDto>()
            .ForMember(d => d.SupermarketName, o => o.MapFrom(s => s.Supermarket != null ? s.Supermarket.Name : string.Empty))
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty));

        CreateMap<Product, ProductListDto>()
            .ForMember(d => d.SupermarketName, o => o.MapFrom(s => s.Supermarket != null ? s.Supermarket.Name : string.Empty))
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty));

        CreateMap<Category, CategoryDto>();
        CreateMap<Supermarket, SupermarketDto>();
        CreateMap<Address, AddressDto>();

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.AddressStreet, o => o.MapFrom(s => s.Address != null ? s.Address.Street : string.Empty))
            .ForMember(d => d.AddressCity, o => o.MapFrom(s => s.Address != null ? s.Address.City : string.Empty));

        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product != null ? s.Product.Name : string.Empty));
    }
}
