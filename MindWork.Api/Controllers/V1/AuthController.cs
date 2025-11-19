using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Authentication;
using MindWork.Api.Infrastructure.Persistence;

namespace MindWork.Api.Controllers.V1;

/// <summary>
/// Endpoints de autenticação e gestão de credenciais da MindWork.
/// Permite registro de novos usuários (colaboradores/gestores) e login via JWT.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(MindWorkDbContext dbContext, IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    /// <summary>
    /// Registra um novo usuário (colaborador ou gestor) na plataforma MindWork.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    ///
    ///     POST /api/v1/auth/register
    ///     {
    ///       "fullName": "João da Silva",
    ///       "email": "joao.silva@empresa.com",
    ///       "password": "SenhaForte@123",
    ///       "role": "Collaborator"
    ///     }
    ///
    /// Roles válidos:
    /// - Collaborator
    /// - Manager
    ///
    /// </remarks>
    /// <param name="request">Dados do usuário a ser cadastrado.</param>
    /// <response code="201">Usuário criado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="409">Já existe um usuário com este e-mail.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email == request.Email);

        if (emailExists)
        {
            return Conflict(new { message = "Já existe um usuário cadastrado com este e-mail." });
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return BadRequest(new { message = "Role inválida. Use 'Collaborator' ou 'Manager'." });
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var response = new RegisterResponse(
            Id: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt
        );

        return CreatedAtAction(nameof(Register), new { id = user.Id }, response);
    }

    /// <summary>
    /// Realiza login na plataforma e retorna um token JWT.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    ///
    ///     POST /api/v1/auth/login
    ///     {
    ///       "email": "joao.silva@empresa.com",
    ///       "password": "SenhaForte@123"
    ///     }
    ///
    /// Exemplo de resposta:
    ///
    ///     {
    ///       "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///       "fullName": "João da Silva",
    ///       "role": "Collaborator"
    ///     }
    ///
    /// O token deve ser enviado em chamadas autenticadas via:
    ///
    ///     Authorization: Bearer {token}
    ///
    /// </remarks>
    /// <param name="request">Credenciais de login.</param>
    /// <response code="200">Login bem-sucedido, JWT retornado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="401">Credenciais incorretas ou usuário inativo.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null)
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Usuário inativo. Entre em contato com o gestor." });
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!passwordValid)
        {
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        var response = new LoginResponse(
            Token: token,
            FullName: user.FullName,
            Role: user.Role.ToString()
        );

        return Ok(response);
    }
}

/// <summary>
/// Dados de entrada para registro de um novo usuário.
/// </summary>
public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string Role
);

/// <summary>
/// Resposta ao registrar um novo usuário.
/// </summary>
public record RegisterResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt
);

/// <summary>
/// Dados de entrada para login.
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Resposta de login contendo o token JWT e informações básicas do usuário.
/// </summary>
public record LoginResponse(
    string Token,
    string FullName,
    string Role
);
