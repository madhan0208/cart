using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to DI container
builder.Services.AddSingleton<ICartService, CartService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok("Cart API is up and running!"));

app.MapGet("/cart", (ICartService cartService) => Results.Ok(cartService.GetItems()));

app.MapGet("/cart/total", (ICartService cartService) => Results.Ok(new { Total = cartService.GetTotal() }));

app.MapPost("/cart/items", (CartItemDto itemDto, ICartService cartService) =>
{
    var item = new CartItem(itemDto.ProductId, itemDto.Name, itemDto.Quantity, itemDto.Price);
    cartService.AddOrUpdateItem(item);
    return Results.Created($"/cart/items/{item.ProductId}", item);
});

app.MapPut("/cart/items/{productId:int}", (int productId, UpdateCartItemDto updateDto, ICartService cartService) =>
{
    var existing = cartService.GetItem(productId);
    if (existing is null)
        return Results.NotFound();

    cartService.UpdateQuantity(productId, updateDto.Quantity);
    return Results.Ok(cartService.GetItem(productId));
});

app.MapDelete("/cart/items/{productId:int}", (int productId, ICartService cartService) =>
{
    if (!cartService.RemoveItem(productId))
        return Results.NotFound();

    return Results.NoContent();
});

app.MapPost("/cart/clear", (ICartService cartService) =>
{
    cartService.Clear();
    return Results.NoContent();
});

app.Run();

public record CartItem(int ProductId, string Name, int Quantity, decimal Price);

public record CartItemDto(int ProductId, string Name, int Quantity, decimal Price);
public record UpdateCartItemDto(int Quantity);

public interface ICartService
{
    IReadOnlyList<CartItem> GetItems();
    CartItem? GetItem(int productId);
    void AddOrUpdateItem(CartItem item);
    bool RemoveItem(int productId);
    void UpdateQuantity(int productId, int quantity);
    void Clear();
    decimal GetTotal();
}

public class CartService : ICartService
{
    private readonly List<CartItem> _items = new();

    public IReadOnlyList<CartItem> GetItems() => _items.AsReadOnly();

    public CartItem? GetItem(int productId) => _items.FirstOrDefault(i => i.ProductId == productId);

    public void AddOrUpdateItem(CartItem item)
    {
        var existing = GetItem(item.ProductId);
        if (existing is null)
        {
            _items.Add(item);
            return;
        }

        var merged = existing with { Quantity = existing.Quantity + item.Quantity, Price = item.Price, Name = item.Name };
        _items[_items.IndexOf(existing)] = merged;
    }

    public bool RemoveItem(int productId)
    {
        var item = GetItem(productId);
        if (item is null) return false;
        return _items.Remove(item);
    }

    public void UpdateQuantity(int productId, int quantity)
    {
        var item = GetItem(productId);
        if (item is null) return;
        if (quantity <= 0)
        {
            _items.Remove(item);
            return;
        }

        _items[_items.IndexOf(item)] = item with { Quantity = quantity };
    }

    public void Clear() => _items.Clear();

    public decimal GetTotal() => _items.Sum(i => i.Price * i.Quantity);
}
