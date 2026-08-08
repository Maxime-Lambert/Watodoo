# Partitionner le rate limiter /auth par IP client

Le rate limiter `/auth` (`Program.cs`) utilise aujourd'hui un unique
`FixedWindowLimiter` global : tous les clients partagent le même compteur, donc
un seul client en rafale (volontaire ou bug) épuise le quota et bloque tout le
monde (DoS trivial à déclencher). Objectif : une fenêtre glissante par IP
cliente, pour qu'un client bruyant n'affecte que lui-même. Item roadmap
Semaine 1 marqué "à corriger avant prod" (`docs/roadmap.md:19`).

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary**; run the phase's
**Verification Plan** and record the result before moving on.

## Contexte technique (lu avant d'écrire ce plan)

- `Program.cs:90-99` : `AddRateLimiter` avec `AddFixedWindowLimiter("auth", ...)`
  — pas de partitionnement, une seule fenêtre pour tout le monde.
- Chaîne réseau en prod (voir `docs/decisions/architecture.md`, section
  Hébergement) : **Client → Cloudflare (proxy orange) → Caddy (`edge`) →
  `backend:8080`**, uniquement sur le réseau Docker interne (le port du
  conteneur `backend` n'est pas publié dans `docker-compose.prod.yml`). Donc
  `HttpContext.Connection.RemoteIpAddress` vu par le backend est aujourd'hui
  l'IP interne Docker du conteneur `edge`, jamais l'IP du vrai client — un
  partitionnement naïf sur `RemoteIpAddress` grouperait donc tout le monde
  dans une seule partition (aucune amélioration par rapport à l'existant).
- Cloudflare pose déjà un header `X-Forwarded-For: <ip-client-réelle>` en
  contactant Caddy. Caddy (`reverse_proxy backend:8080` dans `Caddyfile`)
  **ajoute** sa propre IP source (celle de Cloudflare) à ce header plutôt que
  de l'écraser — le backend reçoit donc `X-Forwarded-For: <ip-client>,
  <ip-edge-cloudflare>` (2 entrées, la plus à gauche étant le vrai client).
- En local (`docker-compose.yml`), il n'y a ni Caddy ni Cloudflare : le
  frontend (Vite) appelle le backend directement, `RemoteIpAddress` est déjà
  la vraie IP, et aucun `X-Forwarded-For` n'est présent — le comportement doit
  rester correct dans ce cas aussi (pas de dépendance à un environnement précis).

## Hypothèses (à confirmer implicitement en l'absence d'objection)

- On fait confiance à `X-Forwarded-For` sans liste blanche de proxys connus
  (`KnownProxies`/`KnownNetworks` vidées), car le backend n'est *jamais*
  atteignable autrement que via `edge` en prod (pas de port publié) — donc
  n'importe quel appelant direct du backend est de facto un proxy de confiance
  par construction réseau, pas parce qu'on fait confiance à l'en-tête en soi.
  Limite connue et acceptée (hors scope) : si l'IP réelle du VPS fuitait et
  qu'un attaquant contactait Caddy directement en contournant Cloudflare, il
  pourrait forger un `X-Forwarded-For` arbitraire et se donner une IP
  partition différente à chaque requête, annulant l'effet du partitionnement
  (mais pas pire que l'absence totale de partitionnement actuelle).
- `ForwardLimit` mis à `null` (illimité) plutôt qu'à une valeur fixe comme 2 :
  remonte jusqu'à l'IP la plus à gauche quelle que soit la longueur de la
  chaîne, plus robuste si un intermédiaire est ajouté/retiré plus tard.

## Phase 1: Rate limiter partitionné par IP + confiance forwarded headers

Status: Complete

- [x] `Watodoo.Api/Program.cs` : ajouter `using Microsoft.AspNetCore.HttpOverrides;`
      et `using System.Threading.RateLimiting;`
- [x] `Program.cs` : après `builder.Services.AddCors(...)`, ajouter
      `builder.Services.Configure<ForwardedHeadersOptions>(...)` —
      `ForwardedHeaders = ForwardedHeaders.XForwardedFor`, `KnownIPNetworks` et
      `KnownProxies` vidés (`.Clear()`), `ForwardLimit = null` — avec le
      commentaire expliquant le choix (voir contexte ci-dessus, pas évident)
- [x] `Program.cs` : remplacer `options.AddFixedWindowLimiter("auth", ...)` par
      `options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(...))`,
      clé de partition = `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"`,
      mêmes valeurs de config (`RateLimiting:Auth:PermitLimit`/`WindowSeconds`,
      `QueueLimit = 0`) que l'actuel `AddFixedWindowLimiter`
- [x] `Program.cs` : ajouter `app.UseForwardedHeaders();` en tout premier
      middleware après `var app = builder.Build();` (avant
      `UseMiddleware<ExceptionMiddleware>()`) — doit s'exécuter avant tout code
      qui lit `RemoteIpAddress`, y compris le rate limiter et les logs
- [x] `docs/decisions/architecture.md` : ajouter un paragraphe court sous
      "Authentification" documentant la décision (partitionnement par IP +
      confiance de la chaîne Cloudflare→Caddy→backend via forwarded headers,
      sans liste de proxys connus)
- [x] `docs/roadmap.md:19` : cocher l'item une fois vérifié

### Verification Plan
- `dotnet build` → 0 erreur, 0 warning (le projet a `WarningsAsErrors=Nullable`)
- Relecture visuelle de `Program.cs` : `UseForwardedHeaders()` bien avant
  `UseRateLimiter()` dans le pipeline

### Phase Summary
`.NET 10` a déprécié `ForwardedHeadersOptions.KnownNetworks` au profit de
`KnownIPNetworks` (warning `ASPDEPR005`) — corrigé, non anticipé dans le plan
initial mais sans impact sur la logique. `dotnet build` propre (0 warning,
0 erreur). Reste à valider par les tests fonctionnels de la Phase 2.

## Phase 2: Tests fonctionnels

Status: Complete

Catégories concernées et justification (voir `docs/testing-strategy.md`) :
- **Fonctionnel backend** : seule catégorie pertinente — le comportement à
  vérifier est un middleware (rate limiting + forwarded headers) branché sur
  le vrai pipeline ASP.NET Core, invisible à un test unitaire ou d'intégration
  qui appellerait un handler directement.
- **Unitaire** : rien à isoler — la logique est de la configuration
  déclarative dans `Program.cs`, pas une classe/méthode testable en isolation.
- **Intégration backend** : pas de nouvelle requête EF Core/contrainte DB.
- **Interface / QA e2e** : aucun changement frontend, aucun nouveau parcours
  utilisateur.
- **Architecture** : aucune nouvelle règle structurelle à faire respecter.
- **Mutation** : couvert passivement par la suite Stryker existante sur
  `Watodoo.Api`, pas de configuration dédiée nécessaire.

La fixture partagée `FunctionalTestFixture` force déjà
`RateLimiting:Auth:PermitLimit=10000` (pour ne pas gêner les autres tests
`/auth`) : les tests de rate limiting ont besoin d'une limite basse, donc leur
propre `WebApplicationFactory` + conteneur Postgres dédiés plutôt que la
fixture partagée.

- [x] `Watodoo.Tests/Functional/Auth/AuthRateLimitingTestFixture.cs` +
      `AuthRateLimitingTests.cs` (nouveaux fichiers, mêmes noms/rôles que
      `FunctionalTestFixture.cs`/`AuthEndpointsTests.cs` mais fixture dédiée —
      `PostgreSqlContainer` + `WebApplicationFactory<Program>` propres avec
      `RateLimiting:Auth:PermitLimit=3`, `WindowSeconds=60`)
- [x] Test : une IP qui dépasse la limite (`X-Forwarded-For` fixe sur toutes
      les requêtes) reçoit `429` sur la requête en trop, les précédentes non
      (comportement de base, remplace l'ancien global par un cas explicite par IP)
- [x] Test : deux IPs différentes (deux valeurs `X-Forwarded-For` distinctes)
      ont chacune leur propre quota — l'IP A épuisée en `429` n'empêche pas
      l'IP B de recevoir une réponse normale (le cœur de la feature : preuve
      du partitionnement)
- [x] Test edge case : `X-Forwarded-For` à 2 entrées (`"<ip-client>,
      <ip-edge>"`, simulant la chaîne Cloudflare→Caddy réelle) — deux clients
      avec la même IP "edge" (2ème entrée) mais des IP client (1ère entrée)
      différentes ne partagent pas leur quota → prouve qu'on ne se limite pas
      par erreur au dernier saut de la chaîne
- [x] Test edge case : aucune requête n'échoue avec une erreur 500 quand
      `X-Forwarded-For` est absent (cas local dev / connexion directe) — la
      requête doit simplement être limitée sur la `RemoteIpAddress` réelle du
      client de test

### Verification Plan
- `dotnet test --filter FullyQualifiedName~AuthRateLimitingTests` → tous verts
- `dotnet test` (suite complète) → tous verts, aucune régression sur
  `AuthEndpointsTests` existants

### Phase Summary
`dotnet test --filter FullyQualifiedName~AuthRateLimitingTests` : 4/4 verts.
`dotnet test` (suite complète, 32 tests) : vert au 2ème run — le 1er run avait
2 échecs (`RefreshTokenRotationTests`) dus à un flake Testcontainers
(`ResourceReaperException: Initialization has been cancelled`, contention au
démarrage de plusieurs conteneurs Docker en parallèle dans cet environnement),
sans rapport avec le changement — confirmé par le fait qu'ils passent seuls et
en re-run complet.

## Phase 3: Revue (code-reviewer + architecture-reviewer)

Status: Complete

- [x] `code-reviewer` (sécurité/RGPD) : 🔴 bloquant trouvé — `ForwardLimit =
      null` combiné à `KnownIPNetworks`/`KnownProxies` vidés désactive toute
      validation de proxy (`checkKnownIps = false`), donc un client peut
      préfixer son propre `X-Forwarded-For` avec une IP forgée pour changer
      de partition de rate limiting à volonté, **y compris via le chemin
      normal à travers Cloudflare** (pas besoin de contourner Cloudflare,
      contrairement à la limite déjà documentée). Corrigé : `ForwardLimit =
      2` (exactement les 2 sauts de confiance Cloudflare + Caddy) — seules
      les entrées posées par l'infra sont consommées, toute entrée forgée à
      gauche par le client est ignorée. `Program.cs` et
      `docs/decisions/architecture.md` mis à jour en conséquence. Nouveau
      test `Forged_leftmost_entry_does_not_bypass_the_real_client_partition`
      ajouté pour couvrir ce cas.
- [x] `architecture-reviewer` : 🟠 important trouvé — le test
      `Missing_forwarded_header_does_not_error_and_still_gets_rate_limited`
      n'envoyait qu'une seule requête et ne vérifiait donc jamais le vrai
      `429`. Corrigé : 4 requêtes, la 4ème doit renvoyer `429` (même forme
      que les autres tests du fichier). Points 🟡 mineurs (duplication de la
      fixture de test, lecture de config dans la policy, duplication du
      commentaire avec `architecture.md`) laissés tels quels — non urgents à
      2 fixtures / sans impact fonctionnel / duplication volontaire pour la
      lisibilité inline, conforme à la règle CLAUDE.md d'expliquer les choix
      non évidents à l'endroit où ils sont pris.

### Verification Plan
- `dotnet build` → 0 warning, 0 erreur
- `dotnet test --filter FullyQualifiedName~AuthRateLimitingTests` → tous verts
- `dotnet test` (suite complète) → tous verts

### Phase Summary
`dotnet build` propre. `AuthRateLimitingTests` : 5/5 verts (nouveau test
anti-spoofing inclus). Suite complète : 33/33 verts.

## Final Recap

Le rate limiter `/auth` est passé d'un `FixedWindowLimiter` global à une
`AddPolicy("auth", ...)` partitionnée par IP cliente
(`RateLimitPartition.GetFixedWindowLimiter`). Pour que la partition reflète le
vrai client (et pas l'IP interne Docker du conteneur `edge`, ni l'IP de sortie
Cloudflare), `ForwardedHeadersMiddleware` a été activé avec
`KnownIPNetworks`/`KnownProxies` vidés et `ForwardLimit = 2` (exactement les 2
sauts de confiance Cloudflare + Caddy — pas `null`/illimité, qui aurait permis
à un client de forger son propre `X-Forwarded-For` pour changer de partition
à volonté, trouvé en revue sécurité), en s'appuyant sur le fait que le backend
n'est jamais joignable autrement qu'au travers de `edge` en prod. Décision
documentée dans `docs/decisions/architecture.md` (section Authentification).
Item roadmap Semaine 1 coché (`docs/roadmap.md:19`). 5 tests fonctionnels
ajoutés couvrant le cas nominal, le partitionnement à deux IPs, la chaîne à 2
sauts façon Cloudflare→Caddy, la résistance à une IP forgée préfixée par le
client, et l'absence de `X-Forwarded-For`. Suite complète (33 tests) verte.
Aucun changement frontend.

## Deployment Plan

1. Commit + PR `fix/rate-limiter-auth-par-ip` (depuis `develop`) → `develop`
2. CI GitHub Actions doit passer (build + tests complets, incl. les 4
   nouveaux tests fonctionnels)
3. Lors de la prochaine PR `develop` → `main`, ce changement partira en prod
   avec le reste — pas de migration DB, pas d'étape de déploiement spécifique
4. Vérification en prod après déploiement : quelques requêtes `POST
   /auth/login` avec des identifiants invalides depuis la même IP doivent
   renvoyer `429` après le quota (`RateLimiting:Auth:PermitLimit`, 10 par
   défaut) sans bloquer un autre client — contrairement au comportement
   global actuel où tout le monde partage le même compteur
