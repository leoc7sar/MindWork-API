using System.IdentityModel.Tokens.Jwt;
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
/// Endpoints de gerenciamento de usuários da MindWork.
/// Permite que o próprio usuário veja seu perfil e que gestores listem/gerenciem usuários.
/// </summary>
/// <remarks>
/// - <b>/users/me</b>: dados do usuário autenticado (colaborador ou gestor).  
/// - <b>/users</b>: listagem paginada para gestores, com filtros por papel e status.  
/// - <b>/users/{id}/status</b>: ativar/desativar usuários (gestores).  
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public UsersController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retorna os dados básicos do usuário autenticado.
    /// </summary>
    /// <remarks>
    /// Exemplo de chamada:
    ///
    ///     GET /api/v1/users/me
    ///
    /// Exemplo de resposta:
    ///
    ///     {
    ///       "id": "f1b9a9b6-9c8f-4e9e-8a0d-123456789abc",
    ///       "fullName": "João da Silva",
    ///       "email": "joao.silva@empresa.com",
    ///       "role": "Collaborator",
    ///       "createdAt": "2025-03-10T14:32:00Z",
    ///       "isActive": true
    ///     }
    ///
    /// </remarks>
    /// <response code="200">Perfil do usuário retornado com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="404">Usuário não encontrado (por exemplo, removido do sistema).</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetMe()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound();
        }

        var response = new UserProfileResponse(
            Id: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            IsActive: user.IsActive
        );

        return Ok(response);
    }

    /// <summary>
    /// Lista usuários com paginação e filtros (apenas gestores).
    /// </summary>
    /// <remarks>
    /// Apenas usuários com papel <b>Manager</b> podem acessar este endpoint.
    ///
    /// Parâmetros de consulta:
    ///
    /// - <b>pageNumber</b>: número da página (iniciando em 1)  
    /// - <b>pageSize</b>: tamanho da página (1 a 50, padrão 10)  
    /// - <b>role</b>: filtra por papel (Collaborator ou Manager)  
    /// - <b>isActive</b>: filtra por usuários ativos/inativos  
    ///
    /// Exemplo de chamada:
    ///
    ///     GET /api/v1/users?pageNumber=1&amp;pageSize=10&amp;role=Collaborator&amp;isActive=true
    ///
    /// Exemplo de resposta (simplificada):
    ///
    ///     {
    ///       "items": [
    ///         {
    ///           "id": "f1b9a9b6-9c8f-4e9e-8a0d-123456789abc",
    ///           "fullName": "João da Silva",
    ///           "email": "joao.silva@empresa.com",
    ///           "role": "Collaborator",
    ///           "isActive": true
    ///         }
    ///       ],
    ///       "pageNumber": 1,
    ///       "pageSize": 10,
    ///       "totalCount": 24,
    ///       "hasNext": true,
    ///       "hasPrevious": false,
    ///       "links": [
    ///         { "href": "/api/v1/users?pageNumber=1&amp;pageSize=10", "rel": "self", "method": "GET" },
    ///         { "href": "/api/v1/users?pageNumber=2&amp;pageSize=10", "rel": "next", "method": "GET" }
    ///       ]
    ///     }
    ///
    /// </remarks>
    /// <param name="pageNumber">Número da página (1 em diante).</param>
    /// <param name="pageSize">Quantidade de registros por página (1–50, padrão 10).</param>
    /// <param name="role">Papel do usuário (Collaborator ou Manager).</param>
    /// <param name="isActive">Filtrar por usuários ativos ou inativos.</param>
    /// <response code="200">Lista paginada de usuários retornada com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="403">Usuário autenticado, porém sem papel de Manager.</response>
    [HttpGet]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(typeof(PagedResponse<UserListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<UserListItemResponse>>> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 50) pageSize = 10;

        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(role) &&
            Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            query = query.Where(u => u.Role == parsedRole);
        }

        if (isActive != null)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        query = query.OrderBy(u => u.FullName);

        var totalCount = await query.CountAsync();

        var users = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = users.Select(u => new UserListItemResponse(
            Id: u.Id,
            FullName: u.FullName,
            Email: u.Email,
            Role: u.Role.ToString(),
            IsActive: u.IsActive
        ));

        var pagedResponse = new PagedResponse<UserListItemResponse>(
            items: items,
            pageNumber: pageNumber,
            pageSize: pageSize,
            totalCount: totalCount);

        var selfUrl = Url.ActionLink(
            action: nameof(GetUsers),
            controller: "Users",
            values: new { version = "1", pageNumber, pageSize, role, isActive });

        if (selfUrl != null)
        {
            pagedResponse.AddLink(selfUrl, "self", "GET");
        }

        if (pagedResponse.HasNext)
        {
            var nextUrl = Url.ActionLink(
                action: nameof(GetUsers),
                controller: "Users",
                values: new { version = "1", pageNumber = pageNumber + 1, pageSize, role, isActive });

            if (nextUrl != null)
            {
                pagedResponse.AddLink(nextUrl, "next", "GET");
            }
        }

        if (pagedResponse.HasPrevious)
        {
            var prevUrl = Url.ActionLink(
                action: nameof(GetUsers),
                controller: "Users",
                values: new { version = "1", pageNumber = pageNumber - 1, pageSize, role, isActive });

            if (prevUrl != null)
            {
                pagedResponse.AddLink(prevUrl, "previous", "GET");
            }
        }

        return Ok(pagedResponse);
    }

    /// <summary>
    /// Ativa ou desativa um usuário (apenas gestores).
    /// </summary>
    /// <remarks>
    /// Exemplo de chamada:
    ///
    ///     PUT /api/v1/users/7f1d4af4-2af5-4f30-bf6a-123456789abc/status
    ///     {
    ///       "isActive": false
    ///     }
    ///
    /// Isso permite, por exemplo, bloquear o acesso de um colaborador
    /// sem excluir seus dados históricos de autoavaliação.
    ///
    /// </remarks>
    /// <param name="id">Identificador do usuário a ser atualizado.</param>
    /// <param name="request">Objeto contendo o novo status (ativo/inativo).</param>
    /// <response code="204">Status do usuário atualizado com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    /// <response code="403">Usuário autenticado, porém sem papel de Manager.</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

/// <summary>
/// Informações de perfil básico do usuário autenticado.
/// </summary>
public record UserProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt,
    bool IsActive
);

/// <summary>
/// Item de listagem de usuários para o dashboard/gestores.
/// </summary>
public record UserListItemResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive
);

/// <summary>
/// Requisição para ativar ou desativar um usuário.
/// </summary>
public record UpdateUserStatusRequest(
    bool IsActive
);
