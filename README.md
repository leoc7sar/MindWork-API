# MindWork API

API RESTful desenvolvida em .NET 8 para a plataforma MindWork, uma solução que monitora, apoia e promove a saúde mental de colaboradores no ambiente de trabalho, dentro do tema "O Futuro do Trabalho".

A MindWork é consumida por:

- Aplicativo Mobile para colaboradores e gestores
- Módulo de IoT / IA
- Ferramentas de observabilidade e monitoramento (health checks, logs, tracing)

---

##  Links Importantes

*  **Vídeo de Demonstração : https://youtu.be/zjrxhkeTkMg**
*  **Repositório da matéria IOT: https://github.com/tcguus/MindWork-IOT**
*  **Repositório da matéria de Mobile (.NET): https://github.com/tcguus/MindWork-Mobile**

---

## Contexto da Global Solution

**Tema:** O Futuro do Trabalho  
**Desafio:** Saúde e bem-estar no trabalho  

**Ideia central:** Plataforma digital chamada MindWork – um sistema para:

- Monitorar sinais de estresse, sobrecarga e clima emocional
- Apoiar colaboradores com recomendações personalizadas de autocuidado
- Gerar indicadores anônimos para gestores tomarem decisões melhores sobre bem-estar de suas equipes

---

## Objetivos da API

1. Prover uma API RESTful em .NET que atenda às boas práticas (verbos HTTP, status codes, paginação, HATEOAS)
2. Dar suporte a:
   - Cadastro e autenticação (JWT) de colaboradores e gestores
   - Autoavaliações semanais (humor, estresse, carga de trabalho)
   - Dashboard anônimo com indicadores agregados
   - Registro de eventos de bem-estar (IoT / apps externos)
   - Recomendações de IA e relatório mensal de clima emocional
3. Atender explicitamente os requisitos da disciplina Advanced Business Development with .NET:
   - Boas práticas REST, versionamento, persistência (EF Core), observabilidade, testes, autenticação
4. Servir de backend para:
   - Projeto Mobile
   - Projeto IoT / IA

---

## Tecnologias Utilizadas

### Backend
- .NET 8 (ASP.NET Core Web API)
- C#
- Entity Framework Core 8 (SQL Server)
- JWT Bearer Authentication
- API Versioning (Microsoft.AspNetCore.Mvc.Versioning)
- Swagger / Swashbuckle

### Banco de Dados
- SQL Server (Express / Developer)
- Migrations com `dotnet ef`

### Testes
- xUnit
- EF Core InMemory para testes

### Observabilidade
- HttpLogging
- Health Checks
- Middlewares customizados:
  - RequestTracingMiddleware (CorrelationId + duração)
  - ErrorHandlingMiddleware (tratamento global de erros)

---

## Autenticação (JWT)

### Configuração no appsettings.json
```json
"Jwt": {
  "Issuer": "MindWork.Auth",
  "Audience": "MindWork.Clients",
  "SecretKey": "super-secret-key-change-in-production-123!"
}
```

### Fluxo de uso

#### 1. Registro
```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "fullName": "João Silva",
  "email": "joao.silva@empresa.com",
  "password": "SenhaForte@123",
  "role": "Collaborator"
}
```

#### 2. Login
```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "joao.silva@empresa.com",
  "password": "SenhaForte@123"
}
```

**Resposta:**
```json
{
  "token": "<jwt>",
  "fullName": "João Silva",
  "role": "Collaborator"
}
```

#### 3. Uso do token
```http
Authorization: Bearer <jwt>
```

---

## Principais Endpoints

### AuthController

- `POST /api/v1/auth/register` – Cadastro de colaborador/gestor
- `POST /api/v1/auth/login` – Login com e-mail/senha, retorna JWT

---

### UsersController

- `GET /api/v1/users/me`  
  Retorna perfil do usuário autenticado (id, nome, email, role, active)

- `GET /api/v1/users?pageNumber=&pageSize=&role=&isActive=`  
  Lista usuários com paginação + HATEOAS (apenas Manager)

- `PUT /api/v1/users/{id}/status`  
  Atualiza status (IsActive) de um usuário (apenas Manager)

---

### SelfAssessmentsController

- `POST /api/v1/selfassessments`  
  Cria autoavaliação do usuário logado

  **Exemplo de body:**
```json
  {
    "mood": 4,
    "stress": 3,
    "workload": 4,
    "notes": "Semana corrida, mas ok."
  }
```

- `GET /api/v1/selfassessments/my?pageNumber=1&pageSize=10`  
  Lista autoavaliações do usuário logado com paginação + HATEOAS

- `GET /api/v1/selfassessments/{id}`  
  Detalhe de uma autoavaliação específica do usuário

- `PUT /api/v1/selfassessments/{id}`  
  Atualiza uma autoavaliação do usuário

- `DELETE /api/v1/selfassessments/{id}`  
  Remove uma autoavaliação

---

### DashboardController

- `GET /api/v1/dashboard/summary?days=30` (apenas Manager)

**Retorna (exemplo):**
```json
{
  "periodDays": 30,
  "totalAssessments": 42,
  "averageMood": 3.5,
  "averageStress": 3.8,
  "averageWorkload": 4.1,
  "moodDistribution": [
    { "level": 1, "count": 2 }
  ],
  "stressDistribution": [
    { "level": 3, "count": 10 }
  ],
  "workloadDistribution": [
    { "level": 4, "count": 8 }
  ]
}
```

---

### WellnessEventsController (IoT / Eventos de Bem-Estar)

- `POST /api/v1/wellnessevents`

  **Body exemplo:**
```json
  {
    "userId": null,
    "eventType": "break",
    "occurredAt": null,
    "source": "mobile_app",
    "value": 15,
    "metadataJson": "{ \"description\": \"Pausa de 15 minutos\" }"
  }
```

- `GET /api/v1/wellnessevents?pageNumber=1&pageSize=10&eventType=break`  
  Lista eventos com paginação + filtros (apenas Manager)

---

### AiController (IA / Recomendações)

- `GET /api/v1/ai/recommendations/me`  
  Retorna lista de recomendações personalizadas para o usuário autenticado, com base nas últimas autoavaliações

  **Exemplo de resposta:**
```json
  [
    {
      "title": "Reduzir fontes de estresse",
      "description": "Percebi níveis de estresse frequentemente altos nas últimas semanas...",
      "category": "stress_management"
    }
  ]
```

- `GET /api/v1/ai/monthly-report?year=2025&month=3` (apenas Manager)

  **Exemplo de resposta:**
```json
  {
    "year": 2025,
    "month": 3,
    "summary": "Relatório mensal automático da MindWork...",
    "averageMood": 3.2,
    "averageStress": 3.9,
    "averageWorkload": 4.0,
    "keyFindings": [ "..." ],
    "suggestedActions": [ "..." ]
  }
```

---

## Testando a API Localmente

### Pré-requisitos

- .NET SDK 8.0
- SQL Server (Express/Developer) rodando
- Connection string correta configurada em appsettings.json

### Passos

1. **Restaurar pacotes:**
```bash
   dotnet restore
```

2. **Aplicar migrations:**
```bash
   dotnet ef database update -p MindWork.Api -s MindWork.Api
```

3. **Rodar a API:**
```bash
   dotnet run --project MindWork.Api
```

4. **Acessar o Swagger:**
```
   https://localhost:5001/swagger
```

---

## Testes xUnit

**Rodar todos os testes:**
```bash
dotnet test
```

---

## Integração com o App Mobile

O app Mobile deve:

- Usar **AuthController** para:
  - Registro (`/auth/register`)
  - Login (`/auth/login`)

- Usar **SelfAssessmentsController** para:
  - Registrar autoavaliações (`POST /selfassessments`)
  - Listar histórico (`GET /selfassessments/my`)

- Usar **AiController** para:
  - Exibir recomendações (`GET /ai/recommendations/me`)

- Para gestor:
  - **DashboardController** (`GET /dashboard/summary`)
  - **AiController** (`GET /ai/monthly-report`)
  - **UsersController** (`GET /users`, `PUT /users/{id}/status`)

**Autenticação sempre via:**
```http
Authorization: Bearer <jwt>
```

---

## Integração com IoT / IA

- Dispositivos ou serviços IoT podem:
  - Enviar eventos de bem-estar para:
    - `POST /api/v1/wellnessevents`

- A disciplina de IA pode:
  - Evoluir o AiService para usar ML.NET ou outro modelo:
    - `IAiService` é a interface
    - `AiService` é a implementação atual baseada em regras
  - Consumir dados:
    - SelfAssessments
    - WellnessEvents
  - Gerar recomendações e insights mais avançados, sem mudar a assinatura dos métodos públicos

---

## MindWork.Api.http (Arquivo de Teste Rápido no Rider)

O arquivo `MindWork.Api.http` (dentro do projeto MindWork.Api) contém requisições pré-configuradas para:

- Health check
- Registro de colaborador/gestor
- Login
- Autoavaliações
- IA
- Eventos de bem-estar
- Dashboard

**Basta:**

1. Rodar a API
2. Abrir `MindWork.Api.http`
3. Clicar nos botões de execução para disparar cada requisição

---

## Nossos integrantes
- **Gustavo Camargo de Andrade**
- RM555562
- 2TDSPF
-------------------------------------------
- **Rodrigo Souza Mantovanello**
- RM555451
- 2TDSPF
-------------------------------------------
- **Leonardo Cesar Rodrigues Nascimento**
- RM558373
- 2TDSPF
