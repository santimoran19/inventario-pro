using InventarioPro.Api.Common;
using InventarioPro.Api.Data;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
[Produces("application/json")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll(CancellationToken ct)
    {
        var items = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Products.Count(p => !p.IsDeleted)))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken ct)
    {
        var item = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Description, c.Products.Count(p => !p.IsDeleted)))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Categoría", id);

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CategoryCreateDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();

        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException($"Ya existe una categoría llamada '{name}'.");

        var category = new Category { Name = name, Description = dto.Description?.Trim() };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = category.Id },
            new CategoryDto(category.Id, category.Name, category.Description, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Update(
        int id, [FromBody] CategoryCreateDto dto, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Categoría", id);

        var name = dto.Name.Trim();

        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException($"Ya existe otra categoría llamada '{name}'.");

        category.Name = name;
        category.Description = dto.Description?.Trim();

        await db.SaveChangesAsync(ct);

        var count = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        return Ok(new CategoryDto(category.Id, category.Name, category.Description, count));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Categoría", id);

        // No se permite dejar productos huérfanos de categoría.
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new ConflictException(
                "No se puede eliminar una categoría que todavía tiene productos asociados.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
