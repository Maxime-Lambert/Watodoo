# Watodoo — Project Constitution

## Stack
- **Backend** : ASP.NET Core Web API (.NET 10), C#, PostgreSQL, EF Core, Hangfire, Redis
- **Frontend** : React 18, TypeScript, Zustand, React Query, Tailwind CSS
- **Auth** : ASP.NET Core Identity + JWT (bearer 1h) + Refresh Token rotatif (90 jours)
- **Infra** : Docker, GitHub Actions → OVH VPS

## Commandes
- Build backend : `dotnet build`
- Tests backend : `dotnet test`
- Dev frontend : `cd frontend && pnpm dev`
- Tests frontend : `cd frontend && pnpm test`
- Local complet : `docker compose up`
- Nouvelle migration : `dotnet ef migrations add <Nom> --project Watodoo.Api --startup-project Watodoo.Api`
- Appliquer migration : `dotnet ef database update --project Watodoo.Api --startup-project Watodoo.Api`

## Conventions
- Commits : `feat:` / `fix:` / `chore:` / `test:` / `docs:`
- Branches : `feature/kebab-case`, `fix/kebab-case`, `chore/kebab-case`
- Une PR = une feature = un plan dans `plans/`
- Tests écrits en même temps que le code, jamais après

## Architecture
- **Vertical Slice Architecture** — un dossier par feature, un dossier par use case
- Deux projets uniquement : `Watodoo.Api/` (code applicatif) et `Watodoo.Tests/` (xUnit + Testcontainers)
- Structure type :
  - `Watodoo.Api/Features/<Domaine>/<EntitéDomaine>.cs` (entités dans leur dossier feature)
  - `Watodoo.Api/Features/<Domaine>/<UseCase>/<Fichiers>.cs`
  - `Watodoo.Api/Shared/` pour les classes transversales entre features
  - `Watodoo.Api/Middleware/`, `Watodoo.Api/Configuration/`
- Namespace racine : `Watodoo` (ex: `namespace Watodoo.Features.Films.GetAll`)
- CQRS sans MediatR — handlers injectés directement via DI
- Validation : FluentValidation sur toutes les commandes et queries
- Erreurs métier : exceptions custom (`NotFoundException`, `ValidationException`) catchées par middleware global
- Erreurs infrastructure : exceptions standard propagées et loguées
- Voir `docs/decisions/architecture.md` pour le détail des choix

## Règles absolues
- Jamais de secrets ou clés API dans le code ou les fichiers versionnés
- `dotnet test` + `pnpm test` passent avant tout push
- Expliquer chaque nouvelle notion et chaque choix d'implémentation non évident

## Workflow
- Toute feature multi-session : créer un plan dans `plans/` avec le skill real-work
- Le plan file est la source de vérité, pas la conversation

## Routing
- Plan actif : `plans/active-plan.md`
- Décisions d'architecture : `docs/decisions/`
- Guide migrations : `docs/migrations.md`
