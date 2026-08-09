using InventarioPro.Api.Common;
using InventarioPro.Api.Data;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
[Produces("application/json")]
public class SuppliersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SupplierDto>>> GetAll(CancellationToken ct)
    {
        var items = await db.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id, s.Name, s.Email, s.Phone, s.Address,
                s.Products.Count(p => !p.IsDeleted)))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken ct)
    {
        var item = await db.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierDto(
                s.Id, s.Name, s.Email, s.Phone, s.Address,
                s.Products.Count(p => !p.IsDeleted)))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Proveedor", id);

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierDto>> Create(
        [FromBody] SupplierCreateDto dto, CancellationToken ct)
    {
        var supplier = new Supplier
        {
            Name = dto.Name.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim()
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id },
            new SupplierDto(supplier.Id, supplier.Name, supplier.Email,
                supplier.Phone, supplier.Address, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> Update(
        int id, [FromBody] SupplierCreateDto dto, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Proveedor", id);

        supplier.Name = dto.Name.Trim();
        supplier.Email = dto.Email?.Trim();
        supplier.Phone = dto.Phone?.Trim();
        supplier.Address = dto.Address?.Trim();

        await db.SaveChangesAsync(ct);

        var count = await db.Products.CountAsync(p => p.SupplierId == id, ct);
        return Ok(new SupplierDto(supplier.Id, supplier.Name, supplier.Email,
            supplier.Phone, supplier.Address, count));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Proveedor", id);

        // Los productos quedan sin proveedor (FK SetNull), no se bloquea la baja.
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
