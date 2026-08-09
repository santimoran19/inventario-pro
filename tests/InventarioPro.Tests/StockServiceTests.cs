using FluentAssertions;
using InventarioPro.Api.Common;
using InventarioPro.Api.Data;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Domain.Enums;
using InventarioPro.Api.Dtos;
using InventarioPro.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InventarioPro.Tests;

public class StockServiceTests
{
    /// <summary>
    /// Base en memoria con nombre único por test, para que no se pisen entre sí.
    /// </summary>
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"stock-tests-{Guid.NewGuid()}")
            // InMemory ignora las transacciones; sin esto el warning se vuelve excepción.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(AppDbContext Db, Product Product)> SeedAsync(int initialStock)
    {
        var db = CreateDb();

        var category = new Category { Name = "Test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Sku = "TEST-001",
            Name = "Producto de prueba",
            Price = 100m,
            Cost = 60m,
            MinStock = 5,
            CategoryId = category.Id
        };

        product.TryApplyDelta(initialStock);

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return (db, product);
    }

    private static StockService CreateService(AppDbContext db)
        => new(db, NullLogger<StockService>.Instance);

    [Fact]
    public async Task Entrada_suma_al_stock_y_registra_el_movimiento()
    {
        var (db, product) = await SeedAsync(10);
        var service = CreateService(db);

        var result = await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.In, 15, "Compra a proveedor", "FC-0001"));

        result.StockAfter.Should().Be(25);

        var stored = await db.Products.FindAsync(product.Id);
        stored!.Stock.Should().Be(25);

        var movements = await db.StockMovements.ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].Type.Should().Be(MovementType.In);
        movements[0].Reference.Should().Be("FC-0001");
    }

    [Fact]
    public async Task Salida_descuenta_del_stock()
    {
        var (db, product) = await SeedAsync(10);
        var service = CreateService(db);

        var result = await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.Out, 4, "Venta", null));

        result.StockAfter.Should().Be(6);
    }

    [Fact]
    public async Task Salida_mayor_al_stock_disponible_se_rechaza()
    {
        var (db, product) = await SeedAsync(3);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.Out, 10, "Venta imposible", null));

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Stock insuficiente*");

        // El stock no se tocó.
        var stored = await db.Products.FindAsync(product.Id);
        stored!.Stock.Should().Be(3);
    }

    [Fact]
    public async Task El_stock_nunca_queda_negativo()
    {
        var (db, product) = await SeedAsync(0);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.Out, 1, null, null));

        await act.Should().ThrowAsync<ConflictException>();

        var stored = await db.Products.FindAsync(product.Id);
        stored!.Stock.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Ajuste_fija_el_stock_al_valor_contado()
    {
        var (db, product) = await SeedAsync(50);
        var service = CreateService(db);

        // Conteo físico: había 42, no 50.
        var result = await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.Adjustment, 42, "Conteo físico de fin de mes", null));

        result.StockAfter.Should().Be(42);
        // La cantidad registrada es la diferencia absoluta.
        result.Quantity.Should().Be(8);
    }

    [Fact]
    public async Task Producto_inexistente_devuelve_not_found()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new StockMovementCreateDto(
            999, MovementType.In, 5, null, null));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Producto_inactivo_no_admite_movimientos()
    {
        var (db, product) = await SeedAsync(10);

        product.IsActive = false;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.In, 5, null, null));

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactivo*");
    }

    [Fact]
    public async Task Cantidad_negativa_se_rechaza()
    {
        var (db, product) = await SeedAsync(10);
        var service = CreateService(db);

        var act = async () => await service.RegisterAsync(new StockMovementCreateDto(
            product.Id, MovementType.In, -5, null, null));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task El_historial_de_movimientos_reconstruye_el_stock_actual()
    {
        var (db, product) = await SeedAsync(0);
        var service = CreateService(db);

        await service.RegisterAsync(new StockMovementCreateDto(product.Id, MovementType.In, 100, null, null));
        await service.RegisterAsync(new StockMovementCreateDto(product.Id, MovementType.Out, 30, null, null));
        await service.RegisterAsync(new StockMovementCreateDto(product.Id, MovementType.Out, 20, null, null));

        var stored = await db.Products.FindAsync(product.Id);
        stored!.Stock.Should().Be(50);

        // El último movimiento debe reflejar el mismo stock que el producto.
        var last = await db.StockMovements
            .OrderByDescending(m => m.Id)
            .FirstAsync();

        last.StockAfter.Should().Be(stored.Stock);
    }
}
