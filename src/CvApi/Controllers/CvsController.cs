using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvApi;

[ApiController]
[Authorize]
[Route("api/cvs")]
public sealed class CvsController : ControllerBase
{
    private readonly CvDb _database;
    private readonly ICvAi _ai;

    public CvsController(CvDb database, ICvAi ai)
    {
        _database = database;
        _ai = ai;
    }

    private string Owner
    {
        get
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        }
    }

    [HttpPost]
    [ProducesResponseType<CvResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CvResponse>> Create(
        CvData data,
        CancellationToken cancellationToken)
    {
        InputCheck.Sanitize(data);

        var entity = new CvEntity
        {
            Owner = Owner,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _database.Cvs.Add(entity);
        await _database.SaveChangesAsync(cancellationToken);

        var routeValues = new
        {
            id = entity.Id
        };

        return CreatedAtAction(nameof(Get), routeValues, entity.Response());
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CvResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CvResponse>>> List(
        [FromQuery]
        [Range(1, int.MaxValue)]
        int page = 1,
        [FromQuery]
        [Range(1, 100)]
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var offset = (long)(page - 1) * pageSize;

        if (offset > int.MaxValue)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Číslo stránky je příliš vysoké."
            };

            return BadRequest(problem);
        }

        var items = await _database.Cvs
            .AsNoTracking()
            .Where(cv => cv.Owner == Owner)
            .OrderByDescending(cv => cv.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var responses = items
            .Select(cv => cv.Response())
            .ToList();

        return Ok(responses);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<CvResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CvResponse>> Get(
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _database.Cvs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                cv => cv.Id == id && cv.Owner == Owner,
                cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return Ok(entity.Response());
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<CvResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CvResponse>> Update(
        long id,
        CvData data,
        CancellationToken cancellationToken)
    {
        var entity = await _database.Cvs.SingleOrDefaultAsync(
            cv => cv.Id == id && cv.Owner == Owner,
            cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        InputCheck.Sanitize(data);

        entity.Data = data;
        entity.UpdatedAt = DateTime.UtcNow;
        await _database.SaveChangesAsync(cancellationToken);

        return entity.Response();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _database.Cvs.SingleOrDefaultAsync(
            cv => cv.Id == id && cv.Owner == Owner,
            cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        _database.Cvs.Remove(entity);
        await _database.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/analysis")]
    [ProducesResponseType<AnalysisResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AnalysisResult>> Analyze(
        long id,
        AnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _database.Cvs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                cv => cv.Id == id && cv.Owner == Owner,
                cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return await _ai.Analyze(
            entity.Data,
            request.JobDescription,
            cancellationToken);
    }
}
