using AutoMapper;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;
using ShormanService.Domain.Interfaces;

namespace ShormanService.Application.Services;

public class SupermarketService : ISupermarketService
{
    private readonly ISupermarketRepository _supermarketRepository;
    private readonly IMapper _mapper;

    public SupermarketService(ISupermarketRepository supermarketRepository, IMapper mapper)
    {
        _supermarketRepository = supermarketRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SupermarketDto>> GetAllAsync()
    {
        var supermarkets = await _supermarketRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<SupermarketDto>>(supermarkets);
    }
}
