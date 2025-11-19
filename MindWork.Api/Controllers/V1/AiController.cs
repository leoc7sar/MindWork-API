using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindWork.Api.Services;

namespace MindWork.Api.Controllers.V1;

/// <summary>
/// Endpoints de “Inteligência” da MindWork:
/// gera recomendações personalizadas de bem-estar para colaboradores
/// e relatórios mensais agregados para gestores.
/// </summary>
/// <remarks>
/// A lógica de recomendação é implementada em <see cref="IAiService"/>,
/// que hoje utiliza regras de negócio baseadas nas autoavaliações.
/// Em versões futuras, este serviço pode ser substituído por um modelo ML.NET.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize] 
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Retorna recomendações personalizadas de bem-estar para o usuário autenticado.
    /// </summary>
    /// <remarks>
    /// As recomendações são geradas com base nas autoavaliações recentes
    /// (humor, estresse e carga de trabalho) do colaborador.
    ///
    /// Exemplo de chamada:
    ///
    ///     GET /api/v1/ai/recommendations/me
    ///
    /// Exemplo de resposta:
    ///
    ///     [
    ///       {
    ///         "title": "Reduzir fontes de estresse",
    ///         "description": "Percebi níveis de estresse frequentemente altos nas últimas semanas. Experimente bloquear 30 minutos no seu dia para pausas sem telas.",
    ///         "category": "stress_management"
    ///       },
    ///       {
    ///         "title": "Check-in com o gestor",
    ///         "description": "Sua carga de trabalho está acima da média. Agende uma conversa rápida com seu gestor para revisar prioridades.",
    ///         "category": "workload"
    ///       }
    ///     ]
    ///
    /// Categorias comuns:
    /// - onboarding
    /// - stress_management
    /// - workload
    /// - work_life_balance
    ///
    /// </remarks>
    /// <response code="200">
    /// Lista de recomendações geradas para o colaborador logado.
    /// Pode retornar lista vazia caso não haja dados suficientes.
    /// </response>
    /// <response code="401">Usuário não autenticado.</response>
    [HttpGet("recommendations/me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendationsForMeAsync()
    {
        var userId = GetCurrentUserId();
        var recommendations = await _aiService.GetPersonalizedRecommendationsAsync(userId);
        return Ok(recommendations);
    }

    /// <summary>
    /// Gera um relatório mensal agregado de clima emocional para a empresa/equipe.
    /// </summary>
    /// <remarks>
    /// Disponível apenas para usuários com papel <b>Manager</b>.
    ///
    /// Exemplo de chamada:
    ///
    ///     GET /api/v1/ai/monthly-report?year=2025&amp;month=3
    ///
    /// Exemplo de resposta (simplificada):
    ///
    ///     {
    ///       "year": 2025,
    ///       "month": 3,
    ///       "summary": "Em março, o humor médio ficou em 3.2, com aumento de estresse na última semana do mês.",
    ///       "averageMood": 3.2,
    ///       "averageStress": 3.9,
    ///       "averageWorkload": 4.0,
    ///       "keyFindings": [
    ///         "Aumento de estresse antes de entregas importantes.",
    ///         "Carga de trabalho acima de 4 em 60% das avaliações."
    ///       ],
    ///       "suggestedActions": [
    ///         "Reavaliar prioridades nas semanas de entrega.",
    ///         "Incentivar pausas programadas e uso de férias."
    ///       ]
    ///     }
    ///
    /// Caso não existam dados no período informado, o serviço retorna médias 0
    /// e uma mensagem explicando que não foram encontradas autoavaliações.
    ///
    /// </remarks>
    /// <param name="year">Ano do relatório (ex.: 2025).</param>
    /// <param name="month">Mês do relatório (1 a 12).</param>
    /// <response code="200">Relatório mensal gerado com sucesso.</response>
    /// <response code="400">Ano ou mês inválido.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="403">Usuário autenticado, porém sem papel de Manager.</response>
    [HttpGet("monthly-report")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyReportAsync(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        if (year <= 0 || month is < 1 or > 12)
        {
            return BadRequest(new { message = "Parâmetros de ano/mês inválidos." });
        }

        var report = await _aiService.GetMonthlyReportAsync(year, month);
        return Ok(report);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}
