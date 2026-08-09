using InventarioPro.Api.Common;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Data;

/// <summary>
/// Aplica migraciones y carga datos mínimos para poder probar la API apenas levanta.
/// Es idempotente: se puede correr muchas veces sin duplicar nada.
/// </summary>
public static class DbInitializer
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DbInitializer));

        await db.Database.MigrateAsync();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // La contraseña del admin sale de configuración, nunca hardcodeada.
        // Si no está definida, no se crea el usuario y queda el aviso en el log.
        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            if (await userManager.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "Administrador"
                };

                var result = await userManager.CreateAsync(admin, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, Roles.Admin);
                    logger.LogInformation("Usuario administrador creado: {Email}", adminEmail);
                }
                else
                {
                    logger.LogError("No se pudo crear el administrador: {Errors}",
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }
        else
        {
            logger.LogWarning(
                "Seed:AdminEmail o Seed:AdminPassword sin configurar. No se creó usuario administrador.");
        }

        if (await db.Categories.AnyAsync()) return;

        logger.LogInformation("Cargando datos de ejemplo.");

        var categories = new List<Category>
        {
            new() { Name = "Bebidas",    Description = "Gaseosas, aguas, jugos e infusiones" },
            new() { Name = "Almacén",    Description = "Productos secos y no perecederos" },
            new() { Name = "Limpieza",   Description = "Artículos de limpieza e higiene" },
            new() { Name = "Panificados", Description = "Pan, facturas y repostería" }
        };

        db.Categories.AddRange(categories);

        var suppliers = new List<Supplier>
        {
            new() { Name = "Distribuidora del Centro", Email = "ventas@distcentro.test", Phone = "351 400-1000" },
            new() { Name = "Mayorista Sur",            Email = "pedidos@maysur.test",    Phone = "351 400-2000" }
        };

        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync();

        var products = new List<(string Sku, string Name, decimal Price, decimal Cost, int Stock, int Min, int CatIdx, int SupIdx)>
        {
            ("BEB-001", "Agua mineral 500ml",       1200m,   700m,  120, 30, 0, 0),
            ("BEB-002", "Gaseosa cola 2.25L",       3800m,  2400m,   45, 20, 0, 0),
            ("BEB-003", "Jugo de naranja 1L",       2100m,  1300m,   12, 15, 0, 1),
            ("ALM-001", "Fideos guiseros 500g",     1500m,   900m,   80, 25, 1, 1),
            ("ALM-002", "Arroz largo fino 1kg",     2300m,  1500m,    8, 20, 1, 1),
            ("ALM-003", "Aceite de girasol 900ml",  4200m,  2900m,   35, 15, 1, 0),
            ("LIM-001", "Detergente 750ml",         2800m,  1700m,   60, 20, 2, 0),
            ("LIM-002", "Lavandina 1L",             1600m,   950m,    0, 10, 2, 0),
            ("PAN-001", "Pan de mesa 500g",         2500m,  1600m,   25, 10, 3, 1)
        };

        foreach (var p in products)
        {
            var product = new Product
            {
                Sku = p.Sku,
                Name = p.Name,
                Price = p.Price,
                Cost = p.Cost,
                MinStock = p.Min,
                CategoryId = categories[p.CatIdx].Id,
                SupplierId = suppliers[p.SupIdx].Id
            };

            product.TryApplyDelta(p.Stock);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            if (p.Stock > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    Type = MovementType.In,
                    Quantity = p.Stock,
                    StockAfter = p.Stock,
                    Reason = "Carga inicial de inventario"
                });
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Datos de ejemplo cargados: {Categories} categorías, {Products} productos.",
            categories.Count, products.Count);
    }
}
