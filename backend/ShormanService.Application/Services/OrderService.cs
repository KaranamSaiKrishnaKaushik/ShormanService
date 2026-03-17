using AutoMapper;
using Microsoft.Extensions.Logging;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;

namespace ShormanService.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository orderRepository, ICartRepository cartRepository,
        IAddressRepository addressRepository, IProductRepository productRepository,
        IMapper mapper, ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _cartRepository = cartRepository;
        _addressRepository = addressRepository;
        _productRepository = productRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid userId)
    {
        var orders = await _orderRepository.GetByUserIdAsync(userId);
        return _mapper.Map<IEnumerable<OrderDto>>(orders);
    }

    public async Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null || order.UserId != userId) return null;
        return _mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Cart is empty.");

        if (!cart.Items.Any())
            throw new InvalidOperationException("Cart is empty.");

        var address = await _addressRepository.GetByIdAsync(request.AddressId)
            ?? throw new KeyNotFoundException("Address not found.");

        if (address.UserId != userId)
            throw new UnauthorizedAccessException("Address does not belong to user.");

        var validPayments = new[] { "PayPal", "BankTransfer", "CashOnDelivery" };
        if (!validPayments.Contains(request.PaymentMethod))
            throw new InvalidOperationException("Invalid payment method.");

        var orderItems = new List<OrderItem>();
        decimal total = 0;

        foreach (var cartItem in cart.Items)
        {
            var product = await _productRepository.GetByIdAsync(cartItem.ProductId)
                ?? throw new KeyNotFoundException($"Product {cartItem.ProductId} not found.");
            orderItems.Add(new OrderItem
            {
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                UnitPrice = product.Price,
                Product = product
            });
            total += product.Price * cartItem.Quantity;
        }

        var order = new Order
        {
            UserId = userId,
            AddressId = request.AddressId,
            TotalAmount = total,
            PaymentMethod = request.PaymentMethod,
            Status = "Pending",
            Items = orderItems,
            Address = address
        };

        var created = await _orderRepository.CreateAsync(order);
        await _cartRepository.ClearCartAsync(cart.Id);

        _logger.LogInformation("Order created: {OrderId} for user {UserId}", created.Id, userId);
        return _mapper.Map<OrderDto>(created);
    }
}
