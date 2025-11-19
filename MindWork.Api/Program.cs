using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MindWork.Api.Infrastructure.Authentication;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Middleware;
using MindWork.Api.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------
// DbContext (EF Core) - usa a connection string do appsettings.json
// ---------------------------------------------
builder.Services.AddDbContext<MindWorkDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MindWorkDatabase")));

// ---------------------------------------------
// Serviços base da API
// ---------------------------------------------
builder.Services.AddControllers();

// ---------------------------------------------
// Versionamento da API (URL segment: /api/v1/...)
// ---------------------------------------------
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    // versão vem da URL: /api/v{version}/[controller]
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Necessário para Swagger funcionar bem com versionamento
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";         // v1, v1.0, etc
    options.SubstituteApiVersionInUrl = true;
});

// ---------------------------------------------
// Logging + Tracing básico via HttpLogging
// ---------------------------------------------
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields =
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.ResponseStatusCode;
});

// ---------------------------------------------
// Health Checks
// ---------------------------------------------
builder.Services.AddHealthChecks();

// ---------------------------------------------
// Autenticação via JWT Bearer
// ---------------------------------------------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Configuração básica; os valores vêm do appsettings.json
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };
    });

// ---------------------------------------------
// CORS - necessário para o app Mobile consumir a API
// ---------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()   // em produção, ideal restringir
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------------------------
// Registro de serviços próprios (DI)
// ---------------------------------------------
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAiService, AiService>();

// ---------------------------------------------
// Swagger / OpenAPI
// ---------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MindWork API",
        Version = "v1",
        Description = "API para monitorar e promover saúde mental no trabalho (MindWork)."
    });

    // Configuração básica de segurança para enviar JWT pelo Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Ex: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var app = builder.Build();

// ---------------------------------------------
// Pipeline HTTP
// ---------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MindWork API v1");
        options.RoutePrefix = string.Empty; // Swagger em "/"
    });
}

app.UseHttpsRedirection();

// Tratamento global de erros (sempre primeiro no pipeline)
app.UseMiddleware<ErrorHandlingMiddleware>();

// Logging básico de requisições HTTP
app.UseHttpLogging();

// Tracing customizado com CorrelationId + tempo de resposta
app.UseMiddleware<RequestTracingMiddleware>();

app.UseCors("DefaultCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoints de health check
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();
