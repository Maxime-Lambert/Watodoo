# Stratégie de tests — Watodoo

Sept catégories de tests, chacune avec un rôle précis et un outillage dédié.
Une feature n'a pas forcément besoin des sept — `/plan` doit justifier
lesquelles s'appliquent (voir `.claude/commands/plan.md`).

## Vue d'ensemble

| Catégorie | Outil | Emplacement | Vitesse |
|---|---|---|---|
| Unitaire backend | xUnit | `Watodoo.Tests/Features/<F>/<UseCase>/*HandlerTests.cs`, `*ValidatorTests.cs` | ms |
| Unitaire frontend | Vitest + React Testing Library | `frontend/src/**/*.test.tsx` | ms |
| Intégration backend | xUnit + Testcontainers.PostgreSql | `Watodoo.Tests/Integration/<F>/` | secondes |
| Fonctionnel backend | xUnit + `WebApplicationFactory` + Testcontainers | `Watodoo.Tests/Functional/<F>/` | secondes |
| Interface (UI) | Playwright | `frontend/e2e/component/*.spec.ts` | secondes |
| QA / parcours utilisateur | Playwright (app complète) | `frontend/e2e/journeys/*.spec.ts` | secondes à minutes |
| Architecture | NetArchTest.Rules (xUnit) | `Watodoo.Tests/Architecture/` | ms |
| Mutation | Stryker.NET | config dans `Watodoo.Api/stryker-config.json` | minutes |

## Détail par catégorie

### Unitaire backend
Teste un handler, un validator ou une règle métier en isolation totale — pas
de base de données, pas de réseau, dépendances mockées si nécessaire.
**Ne couvre pas** : le câblage EF Core, le routing HTTP, la sérialisation.

### Unitaire frontend
Teste un composant ou un hook React en isolation (jsdom), sans vrai navigateur.
**Ne couvre pas** : le rendu réel dans un navigateur, le CSS, les interactions
complexes multi-composants.

### Intégration backend
Teste un handler contre un vrai PostgreSQL (Testcontainers) : vérifie que la
requête EF Core fait ce qu'on attend, que les contraintes DB sont respectées.
**Ne couvre pas** : le pipeline HTTP (middleware, auth, routing) — c'est le
rôle du test fonctionnel.

### Fonctionnel backend
Teste un endpoint de bout en bout via `WebApplicationFactory` + vrai
PostgreSQL : requête HTTP entrante → réponse HTTP sortante, middleware et
auth inclus. C'est la différence clé avec l'intégration : ici on ne appelle
pas le handler directement, on passe par le vrai pipeline ASP.NET Core.
**Ne couvre pas** : le rendu frontend, les parcours multi-écrans.

### Interface (UI)
Teste le rendu et les interactions d'une page/d'un composant isolé dans un
vrai navigateur (Playwright), pour attraper ce que jsdom ne peut pas
(CSS réel, comportement navigateur). **Ne couvre pas** : l'intégration avec
un vrai backend — utilise des mocks réseau si besoin.

### QA / parcours utilisateur
Teste un parcours utilisateur complet (plusieurs pages, plusieurs actions)
contre l'application réelle : API réelle, PostgreSQL réel, Redis réel,
frontend réel. C'est le niveau le plus proche de l'usage réel, donc le plus
lent et le plus coûteux à maintenir — réservé aux parcours critiques
(ex: inscription → connexion → action métier principale).
**Ne couvre pas** : les cas limites unitaires, qui doivent être testés plus
bas dans la pyramide (plus rapide, plus précis sur la cause d'un échec).

### Architecture
Vérifie automatiquement les règles définies dans
`docs/decisions/architecture.md` (pas de dépendance croisée entre Features,
convention de namespace, pas de couplage handler-à-handler). Empêche la
dérive architecturale silencieuse au fil des features.

### Mutation
Modifie automatiquement le code de `Watodoo.Api` (inverse une condition,
change une constante, etc.) et vérifie que la suite de tests existante
détecte chaque mutation. Mesure la qualité réelle des tests, pas seulement
leur couverture de lignes. Seuil informatif au démarrage du projet (le code
est trop jeune pour un seuil strict) — à durcir manuellement quand la base de
code métier grandit.

## Enforcement

- **Hook pre-push local** (`.githooks/pre-push`) : sous-ensemble rapide
  (unitaire + intégration + architecture backend, lint + format + unitaire
  frontend). Activer une fois par clone : `git config core.hooksPath
  .githooks`.
- **CI GitHub Actions** (`.github/workflows/ci.yml`) : suite complète (incl.
  lint + format frontend) sauf mutation, bloquante sur les PR vers `develop`
  et `main`.
- **Mutation testing** (`.github/workflows/mutation.yml`) : sur PR vers `main`
  uniquement, informatif (n'échoue pas le build).

Playwright et Stryker ne tournent jamais dans le hook local : trop lents pour
un push à chaque commit.
