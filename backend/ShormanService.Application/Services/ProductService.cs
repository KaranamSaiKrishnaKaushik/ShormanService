using AutoMapper;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;
using ShormanService.Domain.Interfaces;

namespace ShormanService.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public ProductService(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ProductListDto>> GetAllAsync(int? supermarketId, int? categoryId, string? search)
    {
        var products = await _productRepository.GetAllAsync(supermarketId, categoryId, search);
        return _mapper.Map<IEnumerable<ProductListDto>>(products);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        return product == null ? null : _mapper.Map<ProductDto>(product);
    }
}
