using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;

namespace MindWork.Api.Services;

/// <summary>
/// Define a interface para o serviço de IA da MindWork.
/// A disciplina de IoT/IA pode evoluir essa implementação
/// para usar modelos de ML/IA generativa no lugar das regras simples.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Gera recomendações personalizadas de bem-estar para um usuário,
    /// com base em suas autoavaliações e eventos recentes.
    /// </summary>
    Task<List<AiRecommendation>> GetPersonalizedRecommendationsAsync(Guid userId);

    /// <summary>
    /// Gera um relatório mensal do clima emocional da empresa
    /// (ou de um grupo de usuários, no futuro).
    /// </summary>
    Task<MonthlyReportResult> GetMonthlyReportAsync(int year, int month);
}

/// <summary>
/// Implementação inicial e simplificada do serviço de IA.
/// Atualmente usa regras baseadas em dados históricos,
/// mas pode ser substituída/expandida por ML/IA generativa na disciplina de IoT/IA.
/// </summary>
public class AiService : IAiService
{
    private readonly MindWorkDbContext _dbContext;

    public AiService(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AiRecommendation>> GetPersonalizedRecommendationsAsync(Guid userId)
    {
        var sinceDate = DateTime.UtcNow.Date.AddDays(-28);

        var assessments = await _dbContext.SelfAssessments
            .AsNoTracking()
            .Where(sa => sa.UserId == userId && sa.CreatedAt >= sinceDate)
            .OrderByDescending(sa => sa.CreatedAt)
            .ToListAsync();

        var recommendations = new List<AiRecommendation>();

        if (!assessments.Any())
        {
            recommendations.Add(new AiRecommendation(
                Title: "Comece registrando suas semanas",
                Description: "Ainda não encontrei autoavaliações recentes. Tente registrar como você se sente uma vez por semana para que eu possa sugerir ações mais personalizadas.",
                Category: "onboarding"));

            return recommendations;
        }

        var averageMood = assessments.Average(a => (int)a.Mood);
        var averageStress = assessments.Average(a => (int)a.Stress);
        var averageWorkload = assessments.Average(a => (int)a.Workload);
        
        if (averageStress >= (int)StressLevel.High)
        {
            recommendations.Add(new AiRecommendation(
                Title: "Reduzir fontes de estresse",
                Description: "Percebi níveis de estresse frequentemente altos nas últimas semanas. Que tal alinhar prioridades com seu gestor, negociar prazos ou fracionar tarefas grandes em passos menores?",
                Category: "stress_management"));
        }

        if (averageWorkload >= (int)WorkloadLevel.High)
        {
            recommendations.Add(new AiRecommendation(
                Title: "Rever carga de trabalho",
                Description: "Sua percepção de carga está acima do ideal. Tente bloquear blocos de foco no calendário, registrar interrupções e conversar com o time sobre redistribuição de demandas.",
                Category: "workload"));
        }

        if (averageMood <= (int)MoodLevel.Bad)
        {
            recommendations.Add(new AiRecommendation(
                Title: "Cuidar do seu bem-estar emocional",
                Description: "Seu humor médio tem ficado mais baixo recentemente. Considere incluir pausas conscientes, momentos de lazer após o expediente e, se necessário, buscar apoio profissional.",
                Category: "emotional_health"));
        }

        if (!recommendations.Any())
        {
            recommendations.Add(new AiRecommendation(
                Title: "Continue mantendo bons hábitos",
                Description: "Seu equilíbrio entre humor, carga e estresse está estável. Mantenha pequenas pausas, rotina de sono regular e limites saudáveis entre trabalho e vida pessoal.",
                Category: "maintenance"));
        }

        return recommendations;
    }

    public async Task<MonthlyReportResult> GetMonthlyReportAsync(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var assessments = await _dbContext.SelfAssessments
            .AsNoTracking()
            .Where(sa => sa.CreatedAt >= startDate && sa.CreatedAt < endDate)
            .ToListAsync();

        if (!assessments.Any())
        {
            return new MonthlyReportResult(
                Year: year,
                Month: month,
                Summary: "Nenhum dado de autoavaliação foi encontrado para este período.",
                AverageMood: 0,
                AverageStress: 0,
                AverageWorkload: 0,
                KeyFindings: new List<string>
                {
                    "Sem dados suficientes para avaliar o clima emocional.",
                },
                SuggestedActions: new List<string>
                {
                    "Incentive a equipe a registrar autoavaliações semanais.",
                    "Inclua a MindWork nas rotinas de one-on-one e reuniões de time."
                });
        }

        var averageMood = assessments.Average(a => (int)a.Mood);
        var averageStress = assessments.Average(a => (int)a.Stress);
        var averageWorkload = assessments.Average(a => (int)a.Workload);

        var findings = new List<string>();
        var actions = new List<string>();

        findings.Add($"Média de humor no período: {averageMood:0.0} (escala 1–5).");
        findings.Add($"Média de estresse no período: {averageStress:0.0} (escala 1–5).");
        findings.Add($"Média de carga de trabalho no período: {averageWorkload:0.0} (escala 1–5).");

        if (averageStress >= (int)StressLevel.High)
        {
            actions.Add("Rever metas e prazos junto à equipe, priorizando tarefas críticas.");
            actions.Add("Incentivar pausas regulares e evitar horas extras recorrentes.");
        }

        if (averageWorkload >= (int)WorkloadLevel.High)
        {
            actions.Add("Avaliar redistribuição de demandas entre os membros do time.");
            actions.Add("Alinhar expectativas com outras áreas sobre capacidade do time.");
        }

        if (averageMood <= (int)MoodLevel.Bad)
        {
            actions.Add("Promover conversas abertas sobre bem-estar emocional.");
            actions.Add("Divulgar canais de apoio psicológico da empresa, se houver.");
        }

        if (!actions.Any())
        {
            actions.Add("Manter práticas atuais de gestão, reforçando boas iniciativas já em andamento.");
            actions.Add("Explorar novas ações de reconhecimento e celebração de conquistas.");
        }

        var summary =
            "Relatório mensal automático da MindWork. " +
            "Baseado nas autoavaliações registradas, este resumo destaca o clima emocional geral da equipe " +
            "e sugere ações práticas para apoiar o bem-estar no trabalho.";

        return new MonthlyReportResult(
            Year: year,
            Month: month,
            Summary: summary,
            AverageMood: Math.Round(averageMood, 2),
            AverageStress: Math.Round(averageStress, 2),
            AverageWorkload: Math.Round(averageWorkload, 2),
            KeyFindings: findings,
            SuggestedActions: actions);
    }
}



public record AiRecommendation(
    string Title,
    string Description,
    string Category 
);

public record MonthlyReportResult(
    int Year,
    int Month,
    string Summary,
    double AverageMood,
    double AverageStress,
    double AverageWorkload,
    List<string> KeyFindings,
    List<string> SuggestedActions
);
