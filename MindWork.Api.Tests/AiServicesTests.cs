using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Services;
using Xunit;

namespace MindWork.Api.Tests;

public class AiServiceTests
{
    private MindWorkDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MindWorkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new MindWorkDbContext(options);
    }

    [Fact]
    public async Task GetPersonalizedRecommendationsAsync_ShouldReturnOnboarding_WhenUserHasNoAssessments()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext);
        var userId = Guid.NewGuid(); // usuário sem dados

        
        var recommendations = await service.GetPersonalizedRecommendationsAsync(userId);

        
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Category == "onboarding");
    }

    [Fact]
    public async Task GetPersonalizedRecommendationsAsync_ShouldIncludeStressManagement_WhenAverageStressIsHigh()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext);
        var userId = Guid.NewGuid();

        var assessments = new List<SelfAssessment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                Mood = MoodLevel.Neutral,
                Stress = StressLevel.High,
                Workload = WorkloadLevel.Balanced,
                Notes = "Semana puxada."
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                Mood = MoodLevel.Good,
                Stress = StressLevel.VeryHigh,
                Workload = WorkloadLevel.High,
                Notes = "Muita demanda e prazos curtos."
            }
        };

        await dbContext.SelfAssessments.AddRangeAsync(assessments);
        await dbContext.SaveChangesAsync();

        var recommendations = await service.GetPersonalizedRecommendationsAsync(userId);

        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Category == "stress_management");
    }

    [Fact]
    public async Task GetMonthlyReportAsync_ShouldReturnNoDataSummary_WhenThereAreNoAssessments()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext);
        var year = 2025;
        var month = 3;

        var report = await service.GetMonthlyReportAsync(year, month);

        Assert.Equal(year, report.Year);
        Assert.Equal(month, report.Month);
        Assert.Equal(0, report.AverageMood);
        Assert.Equal(0, report.AverageStress);
        Assert.Equal(0, report.AverageWorkload);
        Assert.Contains("Nenhum dado de autoavaliação", report.Summary);
    }
}
