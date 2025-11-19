using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;

namespace MindWork.Api.Controllers.V1;

/// <summary>
/// Endpoints de dashboard gerencial da MindWork.
/// Fornece indicadores agregados (anônimos) de humor, estresse e carga de trabalho
/// dos colaboradores para apoiar decisões de bem-estar no trabalho.
/// </summary>
/// <remarks>
/// Todos os dados retornados aqui são **anônimos**: nenhum colaborador é identificado.
/// Gestores usam essas informações para acompanhar o clima emocional geral da equipe.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = "Manager")] 
public class DashboardController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public DashboardController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retorna um resumo anônimo das autoavaliações dos colaboradores em um período (em dias).
    /// </summary>
    /// <remarks>
    /// O período é contado a partir da data atual (UTC) para trás.
    ///
    /// Exemplo de requisição:
    ///
    ///     GET /api/v1/dashboard/summary?days=30
    ///
    /// Exemplo de resposta (simplificada):
    ///
    ///     {
    ///       "periodDays": 30,
    ///       "totalAssessments": 42,
    ///       "averageMood": 3.5,
    ///       "averageStress": 3.8,
    ///       "averageWorkload": 4.1,
    ///       "moodDistribution": [
    ///         { "level": 1, "count": 2 },
    ///         { "level": 2, "count": 5 },
    ///         { "level": 3, "count": 10 },
    ///         { "level": 4, "count": 15 },
    ///         { "level": 5, "count": 10 }
    ///       ],
    ///       "stressDistribution": [
    ///         { "level": 1, "count": 1 },
    ///         { "level": 2, "count": 3 },
    ///         { "level": 3, "count": 12 },
    ///         { "level": 4, "count": 16 },
    ///         { "level": 5, "count": 10 }
    ///       ],
    ///       "workloadDistribution": [
    ///         { "level": 1, "count": 0 },
    ///         { "level": 2, "count": 4 },
    ///         { "level": 3, "count": 8 },
    ///         { "level": 4, "count": 20 },
    ///         { "level": 5, "count": 10 }
    ///       ]
    ///     }
    ///
    /// Se não houver nenhuma autoavaliação no período informado, o endpoint retorna:
    ///
    ///     {
    ///       "periodDays": 30,
    ///       "totalAssessments": 0,
    ///       "averageMood": 0,
    ///       "averageStress": 0,
    ///       "averageWorkload": 0,
    ///       "moodDistribution": [],
    ///       "stressDistribution": [],
    ///       "workloadDistribution": []
    ///     }
    ///
    /// </remarks>
    /// <param name="days">
    /// Quantidade de dias a considerar a partir de hoje (padrão 30, mínimo 1, máximo 365).
    /// </param>
    /// <response code="200">Resumo do dashboard retornado com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="403">Usuário autenticado, porém sem papel de Manager.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] int days = 30)
    {
        if (days <= 0 || days > 365)
        {
            days = 30;
        }

        var sinceDate = DateTime.UtcNow.Date.AddDays(-days);

        var query = _dbContext.SelfAssessments
            .AsNoTracking()
            .Where(sa => sa.CreatedAt >= sinceDate);

        var totalAssessments = await query.CountAsync();
        if (totalAssessments == 0)
        {
            var emptyResponse = new DashboardSummaryResponse(
                PeriodDays: days,
                TotalAssessments: 0,
                AverageMood: 0,
                AverageStress: 0,
                AverageWorkload: 0,
                MoodDistribution: new List<MoodDistributionItem>(),
                StressDistribution: new List<StressDistributionItem>(),
                WorkloadDistribution: new List<WorkloadDistributionItem>());

            return Ok(emptyResponse);
        }

        var averageMood = await query.AverageAsync(sa => (int)sa.Mood);
        var averageStress = await query.AverageAsync(sa => (int)sa.Stress);
        var averageWorkload = await query.AverageAsync(sa => (int)sa.Workload);

        var moodGroups = await query
            .GroupBy(sa => sa.Mood)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var stressGroups = await query
            .GroupBy(sa => sa.Stress)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var workloadGroups = await query
            .GroupBy(sa => sa.Workload)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var moodDistribution = moodGroups
            .Select(g => new MoodDistributionItem(
                Level: g.Level,
                Count: g.Count))
            .ToList();

        var stressDistribution = stressGroups
            .Select(g => new StressDistributionItem(
                Level: g.Level,
                Count: g.Count))
            .ToList();

        var workloadDistribution = workloadGroups
            .Select(g => new WorkloadDistributionItem(
                Level: g.Level,
                Count: g.Count))
            .ToList();

        var response = new DashboardSummaryResponse(
            PeriodDays: days,
            TotalAssessments: totalAssessments,
            AverageMood: Math.Round(averageMood, 2),
            AverageStress: Math.Round(averageStress, 2),
            AverageWorkload: Math.Round(averageWorkload, 2),
            MoodDistribution: moodDistribution,
            StressDistribution: stressDistribution,
            WorkloadDistribution: workloadDistribution);

        return Ok(response);
    }
}

/// <summary>
/// Resumo agregado das autoavaliações em um determinado período.
/// </summary>
public record DashboardSummaryResponse(
    int PeriodDays,
    int TotalAssessments,
    double AverageMood,
    double AverageStress,
    double AverageWorkload,
    List<MoodDistributionItem> MoodDistribution,
    List<StressDistributionItem> StressDistribution,
    List<WorkloadDistributionItem> WorkloadDistribution
);

/// <summary>
/// Item de distribuição de humor (contagem por nível 1–5).
/// </summary>
public record MoodDistributionItem(
    MoodLevel Level,
    int Count
);

/// <summary>
/// Item de distribuição de estresse (contagem por nível 1–5).
/// </summary>
public record StressDistributionItem(
    StressLevel Level,
    int Count
);

/// <summary>
/// Item de distribuição de carga de trabalho (contagem por nível 1–5).
/// </summary>
public record WorkloadDistributionItem(
    WorkloadLevel Level,
    int Count
);
