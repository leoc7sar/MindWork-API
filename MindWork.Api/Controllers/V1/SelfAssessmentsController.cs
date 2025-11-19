using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Models.Responses;

namespace MindWork.Api.Controllers.V1;

/// <summary>
/// Endpoints de autoavaliação de bem-estar (humor, estresse, carga de trabalho)
/// realizados pelos colaboradores na plataforma MindWork.
/// </summary>
/// <remarks>
/// Os dados retornados aqui são sempre referentes ao usuário autenticado,
/// garantindo privacidade. Gestores acessam os dados de forma agregada
/// por meio do Dashboard.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize] // exige JWT válido
public class SelfAssessmentsController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public SelfAssessmentsController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Cria uma nova autoavaliação para o usuário autenticado.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    ///
    ///     POST /api/v1/selfassessments
    ///     {
    ///       "mood": 4,
    ///       "stress": 3,
    ///       "workload": 4,
    ///       "notes": "Semana corrida, mas controlada."
    ///     }
    ///
    /// Os campos são números inteiros normalmente em uma escala de 1 a 5:
    /// - mood: 1 (muito ruim) a 5 (muito bom)
    /// - stress: 1 (baixíssimo) a 5 (altíssimo)
    /// - workload: 1 (muito leve) a 5 (muito pesado)
    ///
    /// </remarks>
    /// <param name="request">Dados da autoavaliação.</param>
    /// <response code="201">Autoavaliação criada com sucesso.</response>
    /// <response code="400">Requisição inválida.</response>
    /// <response code="401">Usuário não autenticado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SelfAssessmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SelfAssessmentResponse>> CreateAsync([FromBody] CreateSelfAssessmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        var entity = new SelfAssessment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Mood = (MoodLevel)request.Mood,
            Stress = (StressLevel)request.Stress,
            Workload = (WorkloadLevel)request.Workload,
            Notes = request.Notes
        };

        _dbContext.SelfAssessments.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = new SelfAssessmentResponse(
            entity.Id,
            entity.CreatedAt,
            (int)entity.Mood,
            (int)entity.Stress,
            (int)entity.Workload,
            entity.Notes
        );

        return CreatedAtAction(nameof(GetByIdAsync), new { id = entity.Id }, response);
    }

    /// <summary>
    /// Lista as autoavaliações do usuário autenticado com paginação e links HATEOAS.
    /// </summary>
    /// <remarks>
    /// Exemplo de chamada:
    ///
    ///     GET /api/v1/selfassessments/my?pageNumber=1&amp;pageSize=5
    ///
    /// Exemplo de resposta (simplificada):
    ///
    ///     {
    ///       "items": [
    ///         { "id": "...", "createdAt": "...", "mood": 4, "stress": 3, "workload": 4, "notes": "..." }
    ///       ],
    ///       "pageNumber": 1,
    ///       "pageSize": 5,
    ///       "totalCount": 12,
    ///       "links": [
    ///         { "href": "/api/v1/selfassessments/my?pageNumber=1&amp;pageSize=5", "rel": "self", "method": "GET" },
    ///         { "href": "/api/v1/selfassessments/my?pageNumber=2&amp;pageSize=5", "rel": "next", "method": "GET" }
    ///       ]
    ///     }
    ///
    /// </remarks>
    /// <param name="pageNumber">Número da página (1 em diante).</param>
    /// <param name="pageSize">Quantidade de itens por página.</param>
    /// <response code="200">Lista paginada retornada com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResponse<SelfAssessmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<SelfAssessmentResponse>>> GetMyAsync(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

        var query = _dbContext.SelfAssessments
            .Where(sa => sa.UserId == userId)
            .OrderByDescending(sa => sa.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(sa => new SelfAssessmentResponse(
                sa.Id,
                sa.CreatedAt,
                (int)sa.Mood,
                (int)sa.Stress,
                (int)sa.Workload,
                sa.Notes
            ))
            .ToListAsync();

        // Usa o construtor do PagedResponse que já existe no seu projeto
        var response = new PagedResponse<SelfAssessmentResponse>(
            items,
            pageNumber,
            pageSize,
            totalCount
        );

        // Os Links geralmente já são uma List<Link> criada no próprio PagedResponse.
        // Aqui só adicionamos os links HATEOAS.
        response.Links.Add(new Link(
            Url.ActionLink(nameof(GetMyAsync), values: new { pageNumber, pageSize })!,
            "self",
            "GET"));

        if (pageNumber * pageSize < totalCount)
        {
            response.Links.Add(new Link(
                Url.ActionLink(nameof(GetMyAsync), values: new { pageNumber = pageNumber + 1, pageSize })!,
                "next",
                "GET"));
        }

        if (pageNumber > 1)
        {
            response.Links.Add(new Link(
                Url.ActionLink(nameof(GetMyAsync), values: new { pageNumber = pageNumber - 1, pageSize })!,
                "previous",
                "GET"));
        }

        return Ok(response);
    }

    /// <summary>
    /// Obtém os detalhes de uma autoavaliação específica do usuário autenticado.
    /// </summary>
    /// <param name="id">Identificador da autoavaliação.</param>
    /// <response code="200">Autoavaliação encontrada.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="404">Autoavaliação não encontrada ou não pertence ao usuário.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SelfAssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SelfAssessmentResponse>> GetByIdAsync(Guid id)
    {
        var userId = GetCurrentUserId();

        var sa = await _dbContext.SelfAssessments
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (sa is null)
            return NotFound(new { message = "Autoavaliação não encontrada." });

        var response = new SelfAssessmentResponse(
            sa.Id,
            sa.CreatedAt,
            (int)sa.Mood,
            (int)sa.Stress,
            (int)sa.Workload,
            sa.Notes
        );

        return Ok(response);
    }

    /// <summary>
    /// Atualiza uma autoavaliação existente do usuário autenticado.
    /// </summary>
    /// <remarks>
    /// Apenas autoavaliações do próprio usuário podem ser alteradas.
    /// </remarks>
    /// <param name="id">Id da autoavaliação.</param>
    /// <param name="request">Dados a serem atualizados.</param>
    /// <response code="204">Atualizada com sucesso.</response>
    /// <response code="400">Requisição inválida.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="404">Autoavaliação não encontrada ou não pertence ao usuário.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateSelfAssessmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        var sa = await _dbContext.SelfAssessments
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (sa is null)
            return NotFound(new { message = "Autoavaliação não encontrada." });

        sa.Mood = (MoodLevel)request.Mood;
        sa.Stress = (StressLevel)request.Stress;
        sa.Workload = (WorkloadLevel)request.Workload;
        sa.Notes = request.Notes;

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Remove uma autoavaliação do usuário autenticado.
    /// </summary>
    /// <param name="id">Id da autoavaliação.</param>
    /// <response code="204">Removida com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="404">Autoavaliação não encontrada ou não pertence ao usuário.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        var userId = GetCurrentUserId();

        var sa = await _dbContext.SelfAssessments
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (sa is null)
            return NotFound(new { message = "Autoavaliação não encontrada." });

        _dbContext.SelfAssessments.Remove(sa);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}

/// <summary>
/// Dados para criação de uma autoavaliação.
/// </summary>
public record CreateSelfAssessmentRequest(
    int Mood,
    int Stress,
    int Workload,
    string? Notes
);

/// <summary>
/// Dados para atualização de uma autoavaliação existente.
/// </summary>
public record UpdateSelfAssessmentRequest(
    int Mood,
    int Stress,
    int Workload,
    string? Notes
);

/// <summary>
/// Resposta padrão de uma autoavaliação.
/// </summary>
public record SelfAssessmentResponse(
    Guid Id,
    DateTime CreatedAt,
    int Mood,
    int Stress,
    int Workload,
    string? Notes
);
