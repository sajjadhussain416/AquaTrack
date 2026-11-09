# AquaTrack – Day 1 Setup

Goal of Day 1: solution structure, all database tables, login roles, tenant filter, and the database created on your machine.

## What is in this zip

```
src/
  AquaTrack.Domain/          Entities + enums (all tables from SRS section 5)
  AquaTrack.Application/     ICurrentUserService, Roles, PakistanTime
  AquaTrack.Infrastructure/  AppDbContext (tenant filter, soft delete, audit), Identity, DbSeeder
  AquaTrack.Web/             Program.cs, CurrentUserService, appsettings.json, csproj
```

## Requirements on your laptop

- .NET 8 SDK (`dotnet --version` shows 8.x)
- SQL Server (Express or Developer) + SSMS
- EF Core tool: `dotnet tool install --global dotnet-ef --version 8.0.11`

## Step 1 – Create the solution in your cloned repo

Run inside the `AquaTrack` repo folder (the one you cloned from GitHub):

```bash
dotnet new sln -n AquaTrack

# Web project from the MVC template (gives Views, wwwroot, HomeController). NO --auth flag.
dotnet new mvc -n AquaTrack.Web -o src/AquaTrack.Web
```

## Step 2 – Copy the Day 1 files

Copy the `src` folder from this zip over your repo's `src` folder. When asked, **replace** these Web files:
`AquaTrack.Web.csproj`, `Program.cs`, `appsettings.json`.

Delete the template's `src/AquaTrack.Web/Models/ErrorViewModel.cs` only if it conflicts (normally it does not).

## Step 3 – Add projects to the solution

```bash
dotnet sln add src/AquaTrack.Domain/AquaTrack.Domain.csproj
dotnet sln add src/AquaTrack.Application/AquaTrack.Application.csproj
dotnet sln add src/AquaTrack.Infrastructure/AquaTrack.Infrastructure.csproj
dotnet sln add src/AquaTrack.Web/AquaTrack.Web.csproj

dotnet build
```

Build must show **0 errors**. If not, send me the error text.

## Step 4 – Super Admin login (kept out of Git)

```bash
cd src/AquaTrack.Web
dotnet user-secrets set "SuperAdmin:Phone" "03001234567"
dotnet user-secrets set "SuperAdmin:Password" "ChangeMe@2026"
dotnet user-secrets set "SuperAdmin:FullName" "Attique"
cd ../..
```

Use your own phone number and a strong password. These are stored outside the repo.

If your SQL Server is not the default instance, change `ConnectionStrings:DefaultConnection` in `appsettings.json`, e.g. `Server=.\\SQLEXPRESS;...`.

## Step 5 – Create the first migration

```bash
dotnet ef migrations add InitialCreate -p src/AquaTrack.Infrastructure -s src/AquaTrack.Web -o Persistence/Migrations
```

## Step 6 – Run

```bash
dotnet run --project src/AquaTrack.Web
```

On first run the app will automatically:
1. Create database `AquaTrackDb` and all tables
2. Create the 4 roles: SuperAdmin, PlantOwner, PlantManager, DeliveryBoy
3. Create your Super Admin login
4. Insert 2 plans: Basic (PKR 1,000) and Pro (PKR 2,000)

## Step 7 – Check in SSMS

Open `AquaTrackDb` and confirm you see tables like `Plants`, `Customers`, `Deliveries`, `Bills`, `Payments`, `AspNetUsers`, `AspNetRoles`.

## Step 8 – Commit

```bash
git checkout -b feature/day1-foundation
git add .
git commit -m "feat: domain model, DbContext with tenant filter, identity and seeding"
git push -u origin feature/day1-foundation
```

Then merge to `main` on GitHub (Pull Request).

## How the tenant safety works (important)

- Every plant table inherits `TenantEntity` and has `PlantId`.
- `AppDbContext` adds a filter to every query: `PlantId == logged-in user's PlantId`. A plant owner can never read another plant's data, even by mistake in a query.
- On save, new rows get the user's `PlantId` automatically, and writing another plant's row throws an error.
- `Remove()` never deletes; it sets `IsDeleted = true`.
- Edits/deletes on Deliveries, Bills, Payments, Deposits and Bottle movements are written to `AuditLogs`.

## Day 2 (next)

Login page (mobile number + password), logout, role-based layout and menus, Super Admin: create plant + plant owner.
