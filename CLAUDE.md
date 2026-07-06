# Vespr — Project Constitution

## Stack
- **Backend** : ASP.NET Core Web API (.NET 8), C#, PostgreSQL, EF Core, Hangfire, Redis
- **Frontend** : React 18, TypeScript, Zustand, React Query, Tailwind CSS
- **Auth** : ASP.NET Core Identity + JWT (bearer 1h) + Refresh Token rotatif (90 jours)
- **Infra** : Docker, GitHub Actions → OVH VPS

## Commandes
- Build backend : `dotnet build`
- Tests backend : `dotnet test`
- Dev frontend : `cd frontend && npm run dev`
- Tests frontend : `cd frontend && npm test`
- Local complet : `docker compose up`
- Nouvelle migration : `dotnet ef migrations add <Nom> --project src/Vespr.Infrastructure --startup-project src/Vespr.Api`
- Appliquer migration : `dotnet ef database update --project src/Vespr.Infrastructure --startup-project src/Vespr.Api`

## Conventions
- Commits : `feat:` / `fix:` / `chore:` / `test:` / `docs:`
- Branches : `feature/kebab-case`, `fix/kebab-case`, `chore/kebab-case`
- Une PR = une feature = un plan dans `plans/`
- Tests écrits en même temps que le code, jamais après

## Architecture
- **Vertical Slice Architecture** — un dossier par feature, un dossier par use case
- Structure type : `src/Features/Films/GetAll/GetAllFilmsQuery.cs`
- Namespace racine : `Vespr` (ex: `namespace Vespr.Features.Films.GetAll`)
- CQRS sans MediatR — handlers injectés directement via DI
- Validation : FluentValidation sur toutes les commandes et queries
- Erreurs métier : exceptions custom (`NotFoundException`, `ValidationException`) catchées par middleware global
- Erreurs infrastructure : exceptions standard propagées et loguées
- Voir `docs/decisions/architecture.md` pour le détail des choix

## Règles absolues
- Jamais de secrets ou clés API dans le code ou les fichiers versionnés
- `dotnet test` + `npm test` passent avant tout push
- Expliquer chaque nouvelle notion et chaque choix d'implémentation non évident

## Workflow
- Toute feature multi-session : créer un plan dans `plans/` avec le skill real-work
- Le plan file est la source de vérité, pas la conversation

## Routing
- Plan actif : `plans/active-plan.md`
- Décisions d'architecture : `docs/decisions/`
- Guide migrations : `docs/migrations.md`
