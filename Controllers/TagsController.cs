using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly AppDbContext _context;
    public TagsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tag>>> GetTags()
    {
        return await _context.Tags.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tag>> GetTag(Guid id)
    {
        var tag = await _context.Tags.FindAsync(id);
        if(tag is null)
            return NotFound();
        
        return tag;
    }

    [HttpPost]
    public async Task<ActionResult<Tag>> PostTag(Tag tag)
    {
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTag), new { id = tag.Id }, tag);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        var tag = await _context.Tags.FindAsync(id);
        if(tag is null)
            return NotFound();
        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}