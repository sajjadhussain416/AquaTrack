# AquaTrack

RO water plant delivery management system (multi-tenant SaaS) for Pakistan.
Tracks customers, 19 L bottles, daily deliveries, monthly bills, payments and delivery staff.
UI in English and Urdu.

## Tech stack

- ASP.NET Core MVC (.NET 8), C#
- SQL Server + Entity Framework Core 8
- ASP.NET Core Identity (roles: SuperAdmin, PlantOwner, PlantManager, DeliveryBoy)
- Clean Architecture: Domain, Application, Infrastructure, Web

## Solution layout

```
src/
  AquaTrack.Domain/          Entities, enums
  AquaTrack.Application/     Interfaces, services, business rules
  AquaTrack.Infrastructure/  EF Core DbContext, Identity, seeding
  AquaTrack.Web/             MVC controllers and views
docs/                        SRS and setup notes
```

## Run locally

1. Install .NET 8 SDK, SQL Server, and `dotnet tool install --global dotnet-ef --version 8.0.11`
2. Set the Super Admin login (stored outside Git):
   ```
   cd src/AquaTrack.Web
   dotnet user-secrets set "SuperAdmin:Phone" "03XXXXXXXXX"
   dotnet user-secrets set "SuperAdmin:Password" "<strong password>"
   ```
3. Check `ConnectionStrings:DefaultConnection` in `src/AquaTrack.Web/appsettings.json`
4. `dotnet run --project src/AquaTrack.Web` (creates the database on first run)

## Requirements

The SRS lives at https://claude.ai/artifact/98c852oR1Gd8AvA2dWq3Mz and is copied into `docs/SRS.md` with each version.

## Git conventions

- Branches: `feature/<name>`, `fix/<name>`
- Commits: `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`
