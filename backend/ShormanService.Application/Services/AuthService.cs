using Microsoft.Extensions.Logging;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;

namespace ShormanService.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository userRepository, IJwtService jwtService, ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing != null)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Customer"
        };

        var created = await _userRepository.CreateAsync(user);
        _logger.LogInformation("User registered: {Email}", created.Email);

        var token = _jwtService.GenerateToken(created);
        return new AuthResponse
        {
            Token = token,
            Email = created.Email,
            FirstName = created.FirstName,
            LastName = created.LastName,
            Role = created.Role,
            UserId = created.Id
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        _logger.LogInformation("User logged in: {Email}", user.Email);

        var token = _jwtService.GenerateToken(user);
        return new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            UserId = user.Id
        };
    }
}
