# Job Hangfire de nettoyage des refresh tokens expirés/révoqués

Les refresh tokens (`RefreshTokens` en base) ne sont jamais supprimés
aujourd'hui : à chaque rotation l'ancien token est marqué `RevokedAt` mais
reste en ligne, et les tokens expirés (90 jours, `RefreshTokenPolicy.LifetimeDays`)
restent aussi. Objectif : un job Hangfire récurrent qui supprime
périodiquement les lignes mortes (expirées ou révoquées), pour éviter
l'accumulation indéfinie en base. Item roadmap Semaine 1
(`docs/roadmap.md:17`), suivi de la review sécurité de l'auth — pas
bloquant, juste de l'hygiène de base de données.

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary**; run the phase's
**Verification Plan** and record the result before moving on.

## Contexte technique (lu avant d'écrire ce plan)

- `Watodoo.Api/Features/Auth/RefreshToken.cs` : entité avec `ExpiresAt`,
  `RevokedAt` (nullable) et `IsActive` (propriété calculée C#, pas
  traduisible en SQL par EF — `RevokedAt is null && ExpiresAt > UtcNow`).
  "Mort" = l'inverse : `RevokedAt != null || ExpiresAt <= UtcNow`.
- `Watodoo.Api/Shared/Data/WatodooDbContext.cs` : `DbSet<RefreshToken>
  RefreshTokens`, contrainte unique sur `Token`, FK cascade sur `UserId`.
- `Program.cs` : `AddHangfire` + `AddHangfireServer` déjà configurés
  (stockage PostgreSQL via `Hangfire.PostgreSql`), dashboard mappé en dev
  uniquement (`if (app.Environment.IsDevelopment())`). **Aucun job récurrent
  n'existe encore dans le code** — ce sera le premier, donc l'occasion de
  poser la convention pour les prochains (ingestion TMDB/IGDB en Semaine 2,
  déjà prévus "nightly" dans `docs/decisions/architecture.md`).
- Pas de dossier `Jobs/` existant : je suis le pattern Vertical Slice déjà en
  place (`Features/Auth/Login/`, `Refresh/`, etc.) — un dossier
  `Features/Auth/CleanupExpiredRefreshTokens/` avec la classe du job dedans,
  cohérent avec le reste de la feature Auth plutôt qu'un dossier
  transversal `Jobs/` qui casserait la convention "un dossier = un use case".
- `Watodoo.Tests/Integration/Auth/AuthDbFixture.cs` + `AuthDbCollection` déjà
  en place (Postgres réel via Testcontainers, `CreateContext()` retourne un
  `WatodooDbContext` frais) — réutilisable tel quel, pas besoin d'une
  nouvelle fixture.

## Hypothèses (à confirmer implicitement en l'absence d'objection)

- Un token révoqué est supprimé immédiatement (pas de période de grâce) :
  la révocation en cascade sur détection de réutilisation d'un token révoqué
  est explicitement hors MVP (`docs/roadmap.md:18`), donc il n'y a aucun
  besoin fonctionnel de garder les tokens révoqués pour investigation — les
  garder ne fait qu'accumuler des lignes mortes, ce que ce job doit justement
  éviter.
- Fréquence quotidienne (`Cron.Daily`), pas horaire : le volume est faible
  (un utilisateur génère au plus quelques tokens par jour) et rien n'est
  urgent à nettoyer (contrairement à un job de sécurité temps réel).
  Planifié à 3h du matin UTC, pour ne pas tomber pile sur le backup
  PostgreSQL prévu à 2h (`docs/decisions/architecture.md`, section Backup).
- Suppression en masse via `ExecuteDeleteAsync` (EF Core, traduit en un seul
  `DELETE ... WHERE` SQL) plutôt que charger les entités puis
  `RemoveRange` : pas de chargement en mémoire, plus adapté à un job de
  nettoyage qui peut potentiellement toucher beaucoup de lignes.

## Phase 1: Job de nettoyage + enregistrement récurrent

Status: Complete

- [x] `Watodoo.Api/Features/Auth/CleanupExpiredRefreshTokens/CleanupExpiredRefreshTokensJob.cs`
      (nouveau) : classe `CleanupExpiredRefreshTokensJob(WatodooDbContext db,
      ILogger<CleanupExpiredRefreshTokensJob> logger)`, méthode
      `Task<int> RunAsync()` qui fait
      `db.RefreshTokens.Where(rt => rt.RevokedAt != null || rt.ExpiresAt <= DateTimeOffset.UtcNow).ExecuteDeleteAsync()`
      et logue le nombre de lignes supprimées ; retourne le compte (facilite
      le test d'intégration, évite de parser les logs)
- [x] `Program.cs` : `builder.Services.AddScoped<CleanupExpiredRefreshTokensJob>();`
      à côté des autres handlers Auth déjà enregistrés
- [x] `Program.cs` : après le bloc `MapHangfireDashboard` (dev uniquement),
      enregistrer le job récurrent de façon inconditionnelle (tous
      environnements, y compris prod) :
      `RecurringJob.AddOrUpdate<CleanupExpiredRefreshTokensJob>("cleanup-expired-refresh-tokens", job => job.RunAsync(), Cron.Daily(3));`
- [x] `docs/decisions/architecture.md` : ajouter un court paragraphe sous
      "Authentification" documentant la décision (critère de suppression,
      fréquence, absence de période de grâce) — pose aussi la convention de
      structure pour les futurs jobs Hangfire (dossier
      `Features/<Domaine>/<NomJob>/`, `ExecuteDeleteAsync`/bulk plutôt que
      charger en mémoire quand pertinent)
- [x] `docs/roadmap.md:17` : cocher l'item une fois vérifié

### Verification Plan
- `dotnet build` → 0 erreur, 0 warning
- Relecture visuelle : le job est bien enregistré après `AddHangfireServer`
  et en dehors du bloc `IsDevelopment()`

### Phase Summary
`dotnet build` propre (0 warning, 0 erreur). Job enregistré après le bloc
`MapHangfireDashboard`, avant la migration auto en prod — inconditionnel,
tourne aussi en local et pendant les tests fonctionnels (WebApplicationFactory
démarre tout `Program.cs`), sans effet de bord observé (idempotent via
`AddOrUpdate`).

## Phase 2: Tests d'intégration

Status: Complete

Catégories concernées et justification (voir `docs/testing-strategy.md`) :
- **Intégration backend** : seule catégorie pertinente — le job exécute une
  requête EF Core (`ExecuteDeleteAsync`) contre un vrai PostgreSQL, sans
  passer par le pipeline HTTP (pas d'endpoint, pas de middleware, pas
  d'auth) : c'est exactement la définition "vérifie que la requête EF Core
  fait ce qu'on attend, que les contraintes DB sont respectées".
- **Unitaire** : la logique est une seule requête LINQ-to-SQL, pas de règle
  métier isolable sans base de données à mocker inutilement.
- **Fonctionnel** : pas d'endpoint HTTP concerné par ce changement.
- **Interface / QA e2e** : aucun changement frontend, aucun parcours
  utilisateur.
- **Architecture** : la convention de dossier suit déjà les règles
  existantes (`Features/<Domaine>/<UseCase>/`), pas de nouvelle règle à
  ajouter aux tests NetArchTest.
- **Mutation** : couvert passivement par Stryker, pas de configuration
  dédiée nécessaire.

Réutilise `AuthDbFixture`/`AuthDbCollection` existants
(`Watodoo.Tests/Integration/Auth/`), pas de nouvelle fixture.

- [x] `Watodoo.Tests/Integration/Auth/CleanupExpiredRefreshTokensJobTests.cs`
      (nouveau fichier, `[Collection(nameof(AuthDbCollection))]`)
- [x] Test : un token expiré (non révoqué) est supprimé par le job
- [x] Test : un token révoqué mais pas encore expiré est supprimé par le job
      (le cœur de la feature : la révocation seule suffit, pas besoin
      d'attendre l'expiration)
- [x] Test : un token actif (ni expiré ni révoqué) survit au job — sans ce
      test, un bug dans le prédicat (ex: `&&` au lieu de `||`) supprimerait
      silencieusement des sessions valides d'utilisateurs
- [x] Test edge case : le job ne lève pas d'exception quel que soit l'état de
      la table (reformulé depuis "table vide → retourne 0", voir Phase
      Summary)

### Verification Plan
- `dotnet test --filter FullyQualifiedName~CleanupExpiredRefreshTokensJobTests` → tous verts
- `dotnet test` (suite complète) → tous verts, aucune régression

### Phase Summary
Écart avec le plan initial, trouvé à l'exécution : `AuthDbFixture` partage un
seul conteneur Postgres réel entre **tous** les tests de `AuthDbCollection`
(pas de rollback/troncature entre tests, même pattern que
`RefreshTokenRotationTests` déjà existant). Comme le job supprime *toutes*
les lignes mortes de la table (pas seulement celles créées par un test), les
assertions initiales sur `deletedCount` exact (`Assert.Equal(1, ...)` /
`Assert.Equal(0, ...)`) étaient fragiles : `Keeps_active_tokens` a échoué en
suite complète (`deletedCount == 1` au lieu de `0`) à cause d'un token révoqué
laissé par `RefreshTokenRotationTests` (autre classe, même fixture partagée),
balayé incidemment par mon job. Corrigé : chaque test vérifie uniquement la
présence/absence de **son propre** token par valeur (`AnyAsync(rt => rt.Token
== "...")`), plus aucune assertion sur le compte total. Le test "table vide"
a été reformulé en "ne lève pas d'exception quel que soit l'état de la
table", pour la même raison — on ne peut pas garantir une table vide dans une
fixture partagée. `dotnet test` (suite complète, 37 tests) : vert, relancé
deux fois pour confirmer la stabilité.

## Phase 3: Revue (code-reviewer + architecture-reviewer)

Status: Complete

- [x] `code-reviewer` (sécurité/RGPD) : aucun point trouvé — le prédicat de
      suppression est confirmé comme le complément logique exact de
      `IsActive` (De Morgan), donc aucune session active ne peut être
      supprimée ; pas d'injection SQL (`ExecuteDeleteAsync` via LINQ) ; pas
      de PII dans les logs ; job jugé positif du point de vue RGPD
      (minimisation des données, art. 5(1)(c)/(e)).
- [x] `architecture-reviewer` : 🟠 important trouvé — `RunAsync()` n'acceptait
      pas de `CancellationToken`, incohérent avec tous les autres handlers
      Auth (`LoginCommandHandler`, `RefreshCommandHandler`, etc.), risque de
      se reproduire sur les jobs d'ingestion Semaine 2 où l'annulation
      coopérative sera plus critique. Corrigé : `RunAsync(CancellationToken
      ct = default)`, propagé à `ExecuteDeleteAsync(ct)` — Hangfire injecte
      le vrai token d'arrêt du serveur à l'exécution indépendamment de la
      valeur capturée dans l'expression `RecurringJob.AddOrUpdate`. Convention
      de structure (`Features/<Domaine>/<NomJob>/`, DI scoped, opérations
      bulk) jugée bien choisie pour les futurs jobs. Point 🟡 mineur
      (`CreateUser` dupliqué entre 2 classes de tests d'intégration) laissé
      tel quel — l'architecture-reviewer lui-même le juge "à planifier pour
      la prochaine PR qui ajoute des tests dans cette collection", pas
      urgent à 2 occurrences.

### Verification Plan
- `dotnet build` → 0 warning, 0 erreur
- `dotnet test` (suite complète) → tous verts

### Phase Summary
`dotnet build` propre. Suite complète : 37/37 verts après application du
correctif `CancellationToken`.

## Final Recap

Premier job Hangfire récurrent du projet
(`CleanupExpiredRefreshTokensJob`, `Features/Auth/CleanupExpiredRefreshTokens/`),
planifié quotidiennement à 3h UTC (`Cron.Daily(3)`, décalé du backup Postgres
de 2h). Supprime en une requête (`ExecuteDeleteAsync`, avec `CancellationToken`
propagé) les refresh tokens révoqués ou expirés, sans période de grâce (la
révocation en cascade sur détection de réutilisation est hors MVP, donc rien
ne justifie de garder les tokens révoqués). Décision et convention pour les
futurs jobs Hangfire (structure de dossier, préférence pour les opérations
bulk) documentées dans `docs/decisions/architecture.md`, jugées bien choisies
en revue architecture. Item roadmap Semaine 1 coché (`docs/roadmap.md:17`).
4 tests d'intégration (Postgres réel via `AuthDbFixture` existant) — un
ajustement notable trouvé en cours de route : la fixture partagée entre tests
interdit d'asserter un compte de lignes supprimées exact, seulement la
présence/absence de tokens spécifiques (voir Phase 2 Summary). Revues
sécurité/RGPD (aucun point) et architecture (1 point important corrigé —
`CancellationToken` manquant) effectuées avant la PR. Suite complète :
37/37 verts, stable sur plusieurs runs. Aucun changement frontend.

## Deployment Plan

1. Commit + PR `feat/cleanup-refresh-tokens-job` (depuis `develop`) → `develop`
2. CI GitHub Actions doit passer (build + tests complets)
3. Lors de la prochaine PR `develop` → `main`, ce changement partira en prod
   avec le reste — pas de migration DB, pas d'étape de déploiement spécifique
4. Vérification en prod après déploiement : dashboard Hangfire (accessible en
   dev uniquement aujourd'hui — en prod, vérifier via les logs applicatifs
   l'entrée `Nettoyage refresh tokens : N ligne(s) supprimée(s)` après le
   premier passage à 3h UTC, ou interroger directement la table
   `RefreshTokens` pour confirmer l'absence de lignes mortes anciennes)
