using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Models.Responses;

namespace MindWork.Api.Controllers.V1;

/// <summary>
/// Endpoints responsáveis pelos eventos de bem-estar (Wellness Events).
/// Podem representar pausas, uso de recursos de bem-estar, dados vindos de IoT, etc.
/// </summary>
/// <remarks>
/// Esses eventos podem ser enviados tanto pelo app mobile quanto por
/// integrações externas (ex.: dispositivos IoT, wearables, scripts de monitoramento).
/// Os dados podem ser usados posteriormente em dashboards e relatórios de IA.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize] 
public class WellnessEventsController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public WellnessEventsController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Cria um novo evento de bem-estar.
    /// </summary>
    /// <remarks>
    /// Pode ser chamado pelo app mobile ou por integrações IoT/externas.
    ///
    /// Se o campo <c>userId</c> não for informado, o endpoint tentará vincular o
    /// evento ao usuário autenticado (claim <c>sub</c> do JWT).
    ///
    /// Exemplo de requisição:
    ///
    ///     POST /api/v1/wellnessevents
    ///     {
    ///       "userId": null,
    ///       "eventType": "break",
    ///       "occurredAt": "2025-03-10T14:30:00Z",
    ///       "source": "mobile_app",
    ///       "value": 15,
    ///       "metadataJson": "{ \"description\": \"Pausa de 15 minutos\" }"
    ///     }
    ///
    /// Exemplo de resposta:
    ///
    ///     {
    ///       "id": "c8c0bb9e-6a3a-4f4e-9c9a-123456789abc",
    ///       "userId": "f1b9a9b6-9c8f-4e9e-8a0d-123456789abc",
    ///       "eventType": "break",
    ///       "occurredAt": "2025-03-10T14:30:00Z",
    ///       "source": "mobile_app",
    ///       "value": 15,
    ///       "metadataJson": "{ \"description\": \"Pausa de 15 minutos\" }"
    ///     }
    ///
    /// </remarks>
    /// <param name="request">Dados do evento de bem-estar.</param>
    /// <response code="201">Evento criado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Usuário não autenticado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WellnessEventItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WellnessEventItemResponse>> Create(
        [FromBody] CreateWellnessEventRequest request)
    {
        Guid? userId = request.UserId;

        if (userId == null)
        {
            userId = GetCurrentUserId();
        }

        var entity = new WellnessEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = request.EventType,
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "unknown" : request.Source,
            Value = request.Value,
            MetadataJson = request.MetadataJson
        };

        await _dbContext.WellnessEvents.AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        var response = new WellnessEventItemResponse(
            Id: entity.Id,
            UserId: entity.UserId,
            EventType: entity.EventType,
            OccurredAt: entity.OccurredAt,
            Source: entity.Source,
            Value: entity.Value,
            MetadataJson: entity.MetadataJson
        );

        return CreatedAtAction(
            nameof(Get),
            new { version = "1", pageNumber = 1, pageSize = 10 },
            response);
    }

    /// <summary>
    /// Lista eventos de bem-estar com paginação e filtros opcionais.
    /// </summary>
    /// <remarks>
    /// Apenas usuários com papel <b>Manager</b> possuem acesso por padrão.
    ///
    /// Parâmetros de filtro disponíveis:
    ///
    /// - <b>userId</b>: filtra por usuário específico (opcional)  
    /// - <b>eventType</b>: tipo do evento (ex.: break, meditation, step_count)  
    /// - <b>source</b>: origem do evento (ex.: mobile_app, wearable, script)  
    /// - <b>occurredFrom</b> / <b>occurredTo</b>: intervalo de datas (UTC)  
    ///
    /// Exemplo de requisição:
    ///
    ///     GET /api/v1/wellnessevents?pageNumber=1&amp;pageSize=10&amp;eventType=break&amp;source=wearable
    ///
    /// Exemplo de resposta (simplificada):
    ///
    ///     {
    ///       "items": [
    ///         {
    ///           "id": "c8c0bb9e-6a3a-4f4e-9c9a-123456789abc",
    ///           "userId": "f1b9a9b6-9c8f-4e9e-8a0d-123456789abc",
    ///           "eventType": "break",
    ///           "occurredAt": "2025-03-10T14:30:00Z",
    ///           "source": "wearable",
    ///           "value": 10,
    ///           "metadataJson": "{ \"steps\": 1200 }"
    ///         }
    ///       ],
    ///       "pageNumber": 1,
    ///       "pageSize": 10,
    ///       "totalCount": 5,
    ///       "hasNext": false,
    ///       "hasPrevious": false,
    ///       "links": [
    ///         { "href": "/api/v1/wellnessevents?pageNumber=1&amp;pageSize=10", "rel": "self", "method": "GET" }
    ///       ]
    ///     }
    ///
    /// </remarks>
    /// <param name="pageNumber">Número da página (1 em diante).</param>
    /// <param name="pageSize">Quantidade de registros por página (1–50, padrão 10).</param>
    /// <param name="userId">Filtro opcional por usuário.</param>
    /// <param name="eventType">Filtro opcional por tipo de evento.</param>
    /// <param name="source">Filtro opcional por origem.</param>
    /// <param name="occurredFrom">Data/hora mínima (UTC) do evento.</param>
    /// <param name="occurredTo">Data/hora máxima (UTC) do evento.</param>
    /// <response code="200">Lista paginada de eventos retornada com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="403">Usuário autenticado, porém sem papel de Manager.</response>
    [HttpGet]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(PagedResponse<WellnessEventItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<WellnessEventItemResponse>>> Get(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? source = null,
        [FromQuery] DateTime? occurredFrom = null,
        [FromQuery] DateTime? occurredTo = null)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 50) pageSize = 10;

        var query = _dbContext.WellnessEvents
            .AsNoTracking()
            .AsQueryable();

        if (userId != null)
        {
            query = query.Where(e => e.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(e => e.Source == source);
        }

        if (occurredFrom != null)
        {
            query = query.Where(e => e.OccurredAt >= occurredFrom);
        }

        if (occurredTo != null)
        {
            query = query.Where(e => e.OccurredAt <= occurredTo);
        }

        query = query.OrderByDescending(e => e.OccurredAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var itemResponses = items.Select(e => new WellnessEventItemResponse(
            Id: e.Id,
            UserId: e.UserId,
            EventType: e.EventType,
            OccurredAt: e.OccurredAt,
            Source: e.Source,
            Value: e.Value,
            MetadataJson: e.MetadataJson
        ));

        var pagedResponse = new PagedResponse<WellnessEventItemResponse>(
            items: itemResponses,
            pageNumber: pageNumber,
            pageSize: pageSize,
            totalCount: totalCount);

        var selfUrl = Url.ActionLink(
            action: nameof(Get),
            controller: "WellnessEvents",
            values: new
            {
                version = "1",
                pageNumber,
                pageSize,
                userId,
                eventType,
                source,
                occurredFrom,
                occurredTo
            });

        if (selfUrl != null)
        {
            pagedResponse.AddLink(selfUrl, "self", "GET");
        }

        if (pagedResponse.HasNext)
        {
            var nextUrl = Url.ActionLink(
                action: nameof(Get),
                controller: "WellnessEvents",
                values: new
                {
                    version = "1",
                    pageNumber = pageNumber + 1,
                    pageSize,
                    userId,
                    eventType,
                    source,
                    occurredFrom,
                    occurredTo
                });

            if (nextUrl != null)
            {
                pagedResponse.AddLink(nextUrl, "next", "GET");
            }
        }

        if (pagedResponse.HasPrevious)
        {
            var prevUrl = Url.ActionLink(
                action: nameof(Get),
                controller: "WellnessEvents",
                values: new
                {
                    version = "1",
                    pageNumber = pageNumber - 1,
                    pageSize,
                    userId,
                    eventType,
                    source,
                    occurredFrom,
                    occurredTo
                });

            if (prevUrl != null)
            {
                pagedResponse.AddLink(prevUrl, "previous", "GET");
            }
        }

        return Ok(pagedResponse);
    }

    /// <summary>
    /// Recupera o Id do usuário autenticado a partir do JWT.
    /// Usa o claim "sub" (JwtRegisteredClaimNames.Sub) definido em JwtTokenGenerator.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}

/// <summary>
/// Dados de entrada para criação de um evento de bem-estar.
/// </summary>
/// <param name="UserId">
/// Id do usuário associado ao evento (opcional). Se não informado,
/// o usuário autenticado será usado, se possível.
/// </param>
/// <param name="EventType">Tipo do evento (ex.: break, meditation, steps).</param>
/// <param name="OccurredAt">Data e hora em que o evento ocorreu (UTC). Se nulo, usa agora.</param>
/// <param name="Source">Origem do evento (ex.: mobile_app, wearable, script).</param>
/// <param name="Value">Valor numérico associado (ex.: minutos, passos, pontos).</param>
/// <param name="MetadataJson">JSON livre com metadados adicionais.</param>
public record CreateWellnessEventRequest(
    Guid? UserId,
    string EventType,
    DateTime? OccurredAt,
    string? Source,
    double? Value,
    string? MetadataJson
);

/// <summary>
/// Representa um evento de bem-estar retornado pela API.
/// </summary>
public record WellnessEventItemResponse(
    Guid Id,
    Guid? UserId,
    string EventType,
    DateTime OccurredAt,
    string Source,
    double? Value,
    string? MetadataJson
);
