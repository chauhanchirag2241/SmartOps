# SmartOps Module Template

Use this checklist when adding a new feature module (one controller = one module).

## Folder layout

```
SmartOps.Api/Modules/{Feature}/
  Controllers/{Feature}Controller.cs

SmartOps.Application/Modules/{Feature}/
  {Feature}Dtos.cs                    # All request/response DTOs + mapping extensions
  I{Feature}Service.cs                # Only if service layer is needed

SmartOps.Domain/Modules/{Feature}/
  {Feature}Entities.cs                # Entity types (one file per aggregate when practical)
  {Feature}ListModel.cs               # List/grid projection
  {Feature}Enums.cs                   # Module-specific enums (optional)
  I{Feature}Repository.cs             # Repository contract

SmartOps.Domain/Common/Enums/
  TableFilters.cs                     # Add {Feature}Filter here for list screens

SmartOps.Infrastructure/Modules/{Feature}/
  {Feature}Repository.cs              # Dapper repository implementation
  {Feature}Service.cs                 # Only if service layer is needed
```

## Rules

| Item | Convention |
|------|------------|
| Api controller | Always under `Controllers/` |
| DTOs | Single `{Feature}Dtos.cs` at Application module root |
| List filters | `Domain/Common/Enums/TableFilters.cs` |
| Cross-cutting enums | `Domain/Common/Enums/AuthorizationEnums.cs` |
| Repositories | Always `Infrastructure/Modules/{Feature}/` |
| Simple CRUD | Controller → Repository |
| Complex flows | Controller → Service → Repository |

## DI registration

Register in `Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<I{Feature}Repository, {Feature}Repository>();
// services.AddScoped<I{Feature}Service, {Feature}Service>(); // if applicable
```

## Reference module

See **Student** as the canonical example:

- [`StudentsController.cs`](../src/SmartOps.Api/Modules/Student/Controllers/StudentsController.cs)
- [`StudentDtos.cs`](../src/SmartOps.Application/Modules/Student/StudentDtos.cs)
- [`IStudentRepository.cs`](../src/SmartOps.Domain/Modules/Student/IStudentRepository.cs)
- [`StudentRepository.cs`](../src/SmartOps.Infrastructure/Modules/Student/StudentRepository.cs)

## Solution projects (4 layers)

```
SmartOps.Api → SmartOps.Application, SmartOps.Infrastructure
SmartOps.Infrastructure → SmartOps.Application, SmartOps.Domain
SmartOps.Application → SmartOps.Domain
SmartOps.Domain → (constants, enums, Result<T> in Domain/Common)
```
