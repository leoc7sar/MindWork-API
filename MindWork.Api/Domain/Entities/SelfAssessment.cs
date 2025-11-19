using MindWork.Api.Domain.Enums;

namespace MindWork.Api.Domain.Entities;

public class SelfAssessment
{
  public Guid Id { get; set; }

  /// <summary>
  /// Usuário (colaborador) que fez a autoavaliação.
  /// </summary>
  public Guid UserId { get; set; }

  public User? User { get; set; }

  /// <summary>
  /// Data/hora em que a avaliação foi registrada.
  /// </summary>
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Nível de humor do colaborador na semana.
  /// </summary>
  public MoodLevel Mood { get; set; }

  /// <summary>
  /// Nível de estresse percebido.
  /// </summary>
  public StressLevel Stress { get; set; }

  /// <summary>
  /// Percepção de carga de trabalho.
  /// </summary>
  public WorkloadLevel Workload { get; set; }

  /// <summary>
  /// Campo opcional para o colaborador descrever como está se sentindo.
  /// </summary>
  public string? Notes { get; set; }
}
