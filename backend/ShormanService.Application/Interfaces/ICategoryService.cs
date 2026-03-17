using ShormanService.Application.DTOs;

namespace ShormanService.Application.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync();
}
