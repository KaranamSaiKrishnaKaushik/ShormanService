using ShormanService.Domain.Entities;

namespace ShormanService.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
}
