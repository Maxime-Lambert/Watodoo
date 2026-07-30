# Authentification complète

Inscription (avec auto-login), connexion, JWT bearer (1h) + refresh token rotatif
(90 jours, cookie HttpOnly), déconnexion, endpoint `/me`. Backend ASP.NET Core
Identity + Vertical Slice, frontend Zustand + React Query + pages Login/Register.

## Décisions validées avec l'utilisateur
- **Auto-login à l'inscription** : pas de vérification email dans ce PR.
  `IdentityUser<Guid>` a déjà `EmailConfirmed` en base nativement, mais on ne
  l'exploite pas — champ dormant pour l'instant. La vérification email est un
  item séparé dans `docs/roadmap.md`, à traiter avec le choix d'un provider
  d'envoi d'email (avec reset password et autres emails transactionnels).
- **Pas de révocation en cascade** si un refresh token déjà révoqué est
  réutilisé (signe possible de vol) : on rejette la tentative (401), sans
  révoquer les autres tokens actifs de l'utilisateur. Durcissement futur noté,
  non construit ici.
- **Frontend** : pages Login/Register incluses dans ce PR (pas seulement la
  plomberie état/API).

## Décisions d'architecture spécifiques à cette feature
- `WatodooDbContext` passe de `DbContext` à `IdentityUserContext<ApplicationUser, Guid>`
  — pas `IdentityDbContext` avec rôles : aucun rôle nécessaire au MVP, on évite
  les tables `AspNetRoles`/`AspNetUserRoles`/`AspNetRoleClaims` inutiles.
- `ApplicationUser` et `RefreshToken` vivent dans `Features/Auth/` (entités de
  la feature, même pattern que `Film.cs` décrit dans `docs/decisions/architecture.md`).
- 5 use cases sous `Features/Auth/` : `Register/`, `Login/`, `Refresh/`,
  `Logout/`, `Me/` — chacun Command/Query + Validator + Handler + Response +
  Endpoint (méthode statique `Map(IEndpointRouteBuilder)`).
- `Features/Auth/AuthEndpoints.cs` agrège les 5 `Map(app)` en une extension
  `MapAuthEndpoints`, appelée une fois dans `Program.cs`.
- `Features/Auth/JwtTokenGenerator.cs` : génère l'access token JWT et la valeur
  opaque du refresh token. Utilisé directement par les handlers Login/Register/
  Refresh (ce n'est pas un `*Handler`, donc pas de violation de la règle
  "handler ne dépend pas d'un autre handler").
- `Configuration/JwtOptions.cs` : `Issuer`, `Audience`, `SigningKey`, bindés
  depuis la section `Jwt`. `SigningKey` en local via `dotnet user-secrets`
  (le `UserSecretsId` `watodoo-api` existe déjà dans le csproj) — **jamais**
  dans un `appsettings*.json` committé. En prod : secret GitHub Actions injecté
  en variable d'environnement sur le VPS.
- Register : vérifie l'email existant via `UserManager.FindByEmailAsync` →
  `ConflictException` (déjà dans `Shared/Exceptions`) si déjà pris. Sinon
  `CreateAsync` ; si échec inattendu (ne devrait pas arriver puisque
  FluentValidation reflète déjà la politique de mot de passe configurée dans
  `IdentityOptions.Password`), on propage une `InvalidOperationException`
  standard (bucket "erreur infrastructure", loguée, 500 par le middleware) —
  pas de nouvelle abstraction pour un cas qui ne doit normalement pas se produire.
- Login : identifiants invalides (email OU mot de passe faux) → message
  générique unique, aucun indice sur lequel des deux est faux.
- Nouvelle exception `Shared/Exceptions/UnauthorizedException.cs` (401),
  ajoutée à `ExceptionMiddleware` — couvre identifiants invalides (Login) et
  refresh token invalide/expiré/révoqué (Refresh).
- Cookie refresh : nom `refreshToken`, `HttpOnly`, `Path=/auth` (scope
  minimal), `SameSite=Lax`, `Secure=true` seulement hors `Development` (sinon
  le cookie ne serait pas envoyé en http local), expiration 90 jours alignée
  sur l'expiration en base.
- CORS nécessaire : backend `http://localhost:5174`
  (`Properties/launchSettings.json`) et frontend Vite `http://localhost:5173`
  sont des origines différentes (même *site*, donc le cookie `SameSite=Lax`
  passe, mais CORS reste requis pour autoriser le fetch cross-origin). Policy
  nommée avec `AllowCredentials` + origine(s) explicite(s) lues depuis
  `Cors:AllowedOrigins` (pas `AllowAnyOrigin`, incompatible avec `AllowCredentials`).
- Pipeline `Program.cs` : `ExceptionMiddleware` → `UseCors` → `UseAuthentication`
  → `UseAuthorization` → endpoints. `AddIdentityCore<ApplicationUser>` (pas
  `AddIdentity` — évite le scheme cookie par défaut qu'on ne veut pas, JWT-only)
  + `AddSignInManager` + `AddEntityFrameworkStores<WatodooDbContext>` +
  `AddDefaultTokenProviders`. Politique de mot de passe MVP : `RequiredLength=8`,
  `RequireNonAlphanumeric=false`.
- Migration EF Core manuelle unique couvrant les tables Identity (sans rôles)
  + `RefreshTokens`, avec index unique sur `RefreshTokens.Token` et FK vers
  `AspNetUsers`.

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is
done, set its status to `Complete` and write its **Phase Summary**; run the
phase's **Verification Plan** and record the result before moving on.

## Phase 1: Modèle de données & configuration backend
Status: Complete

- [x] Ajouter les packages : `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
      `Microsoft.AspNetCore.Authentication.JwtBearer`,
      `System.IdentityModel.Tokens.Jwt` — via `dotnet add Watodoo.Api package <nom>`
      pour résoudre la dernière version compatible .NET 10, puis migrer la
      version résolue vers `Directory.Packages.props` (central package management)
- [x] Créer `Watodoo.Api/Features/Auth/ApplicationUser.cs` (`: IdentityUser<Guid>`)
- [x] Créer `Watodoo.Api/Features/Auth/RefreshToken.cs` (`Id`, `Token`, `UserId`,
      `ExpiresAt`, `RevokedAt`)
- [x] Modifier `Watodoo.Api/Shared/Data/WatodooDbContext.cs` :
      `IdentityUserContext<ApplicationUser, Guid>`, `DbSet<RefreshToken> RefreshTokens`,
      configuration Fluent API (index unique sur `Token`, FK vers `ApplicationUser`)
- [x] Créer `Watodoo.Api/Configuration/JwtOptions.cs` (`Issuer`, `Audience`, `SigningKey`)
- [x] Créer `Watodoo.Api/Shared/Exceptions/UnauthorizedException.cs`
- [x] Ajouter le cas `UnauthorizedException` (401) dans
      `Watodoo.Api/Middleware/ExceptionMiddleware.cs`
- [x] `dotnet user-secrets set "Jwt:SigningKey" "<valeur générée localement>"`
      dans `Watodoo.Api/` + `Jwt:Issuer`/`Jwt:Audience` dans `appsettings.json`
      (non-secrets, communs à tous les environnements)
- [x] Générer la migration :
      `dotnet ef migrations add AddAuthIdentity --project Watodoo.Api --startup-project Watodoo.Api`

### Verification Plan
- `dotnet build` → succès sans erreur/warning nouveau
- Inspecter le fichier de migration généré dans
  `Watodoo.Api/Migrations/` : doit contenir `AspNetUsers`, `RefreshTokens`
  (avec index unique sur `Token`), et FK `RefreshTokens.UserId → AspNetUsers.Id`
- `dotnet ef database update --project Watodoo.Api --startup-project Watodoo.Api`
  (avec `docker compose up -d postgres` lancé) → applique sans erreur

### Phase Summary
Packages ajoutés via `dotnet add package` (CPM géré automatiquement dans
`Directory.Packages.props`) ; `Microsoft.EntityFrameworkCore`/`.Design` bumpés
de 10.0.9 à 10.0.10 pour lever un conflit de downgrade NU1605 avec
`Identity.EntityFrameworkCore`. `dotnet-ef` n'était pas dans
`.config/dotnet-tools.json` (seul `dotnet-stryker` y était) — ajouté via
`dotnet tool install dotnet-ef` pour que la commande documentée dans
`CLAUDE.md` fonctionne pour tout contributeur après `dotnet tool restore`.
`WatodooDbContext` hérite maintenant de `IdentityUserContext<ApplicationUser, Guid>`
(pas de tables de rôles). Migration générée et son contenu inspecté :
`AspNetUsers` (sans `AspNetRoles`), `RefreshTokens` avec index unique sur
`Token` et FK cascade vers `AspNetUsers`. Migration **pas encore appliquée**
à une base réelle (nécessite `docker compose up -d postgres`) — sera vérifiée
en Phase 2 lors des tests manuels, et par les tests d'intégration/fonctionnels
en Phase 3 qui appliquent le schéma via Testcontainers.
`Jwt:SigningKey` posé en local via `dotnet user-secrets` (jamais commité) ;
`Jwt:Issuer`/`Jwt:Audience` dans `appsettings.json` (non-secrets).

## Phase 2: Use cases backend (Register, Login, Refresh, Logout, Me)
Status: Complete

- [x] `Features/Auth/JwtTokenGenerator.cs` : `GenerateAccessToken(ApplicationUser)`,
      `GenerateRefreshTokenValue()`
- [x] `Features/Auth/Register/` : `RegisterCommand`, `RegisterCommandValidator`
      (email format, password non vide + longueur min 8), `RegisterCommandHandler`
      (vérifie email existant → `ConflictException`, sinon `CreateAsync` + émission
      immédiate JWT + refresh token + pose du cookie), `RegisterResponse`,
      `RegisterEndpoint` (`POST /auth/register`, 201)
- [x] `Features/Auth/Login/` : `LoginCommand`, `LoginCommandValidator`,
      `LoginCommandHandler` (`UserManager.CheckPasswordAsync`, échec →
      `UnauthorizedException` générique), `LoginResponse`, `LoginEndpoint`
      (`POST /auth/login`, 200)
- [x] `Features/Auth/Refresh/` : `RefreshCommandHandler` (lit le cookie, vérifie
      non révoqué/non expiré en base → `UnauthorizedException` sinon, révoque
      l'ancien, crée un nouveau refresh token + nouveau JWT, repose le cookie),
      `RefreshResponse`, `RefreshEndpoint` (`POST /auth/refresh`, 200)
- [x] `Features/Auth/Logout/` : `LogoutCommandHandler` (lit le cookie si présent,
      idempotent si absent, révoque en base, efface le cookie),
      `LogoutEndpoint` (`POST /auth/logout`, 204)
- [x] `Features/Auth/Me/` : `GetMeQuery`, `GetMeQueryHandler` (lit
      `ClaimsPrincipal`), `GetMeResponse` (id, email), `GetMeEndpoint`
      (`GET /auth/me`, `RequireAuthorization()`, 200/401)
- [x] `Features/Auth/AuthEndpoints.cs` : agrège les 5 `Map(app)`
- [x] `Program.cs` : `AddIdentityCore<ApplicationUser>` +
      `AddEntityFrameworkStores<WatodooDbContext>` + `AddDefaultTokenProviders`,
      politique de mot de passe MVP, `Configure<JwtOptions>`, `AddAuthentication`
      + `AddJwtBearer`, `AddAuthorization`, `AddCors` (policy nommée,
      `Cors:AllowedOrigins` depuis config), pipeline dans l'ordre documenté,
      `app.MapAuthEndpoints()`
- [x] `appsettings.Development.json` : ajouter `Cors:AllowedOrigins` =
      `["http://localhost:5173"]`

### Verification Plan
- `dotnet build` → succès
- `docker compose up -d postgres redis && dotnet ef database update --project Watodoo.Api --startup-project Watodoo.Api && dotnet run --project Watodoo.Api`
  puis manuellement (`curl`) : register → 201 + `Set-Cookie: refreshToken`,
  login → 200, `/auth/me` sans header → 401, avec `Authorization: Bearer <token>` → 200

### Phase Summary
`dotnet build` passe. Écarts par rapport au plan initial, découverts à la
compilation :
- Pas de `.AddSignInManager()` : aucun handler n'injecte `SignInManager`
  (Login utilise `UserManager.CheckPasswordAsync` directement), donc l'appel
  était inutile et de toute façon introuvable sur `IdentityBuilder` sans son
  namespace — retiré.
- `FluentValidation.ValidationException` et
  `Watodoo.Shared.Exceptions.ValidationException` portent le même nom : les
  handlers qualifient pleinement `Watodoo.Shared.Exceptions.ValidationException`
  au site du `throw` pour lever l'ambiguïté.
- `ValidationResult.ToDictionary()` renvoie `IDictionary<string, string[]>`,
  qui ne se convertit pas implicitement vers le
  `IReadOnlyDictionary<string, string[]>` attendu par notre exception — cast
  explicite ajouté (`Dictionary<K,V>` implémente bien les deux interfaces à
  l'exécution).
- `AddDefaultTokenProviders`/`AddEntityFrameworkStores` sont des méthodes
  d'extension dans le namespace `Microsoft.AspNetCore.Identity`, pas couvertes
  par les usings implicites du SDK Web — `using Microsoft.AspNetCore.Identity;`
  ajouté à `Program.cs`.

**Vérification manuelle limitée par l'environnement** : pas de Docker
disponible dans ce sandbox (`docker compose up` échoue), donc impossible de
lancer Postgres et de tester `curl` de bout en bout ici. À la place : `dotnet
build` (succès), `dotnet test --filter Architecture` (3/3 verts, la feature
Auth respecte les règles Vertical Slice existantes), et un `dotnet run` sans
base de données pour confirmer que tout le graphe DI démarre sans exception
(Identity, JWT bearer, CORS) — seul Hangfire échoue en boucle à se connecter à
Postgres, ce qui est attendu et sans rapport avec cette feature. Le test
manuel `curl` complet (register/login/me) et l'application de la migration
restent à faire par un contributeur disposant de Docker, ou seront couverts
par les tests fonctionnels de la Phase 3 (Testcontainers).

## Phase 3: Tests backend (unitaire, intégration, fonctionnel)
Status: Complete — confirmé avec Docker (28/28, voir Addendum en fin de fichier)

- [x] `Watodoo.Tests/Features/Auth/Register/RegisterCommandValidatorTests.cs`
      (email invalide, mot de passe vide/trop court)
- [x] `Watodoo.Tests/Features/Auth/Login/LoginCommandValidatorTests.cs`
- [x] `Watodoo.Tests/Integration/Auth/RefreshTokenRotationTests.cs`
      (Testcontainers.PostgreSql direct sur `WatodooDbContext` : rotation
      révoque l'ancien + crée le nouveau, contrainte unique sur `Token` respectée)
- [x] Étendre `Watodoo.Tests/Functional/` avec `Watodoo.Tests/Functional/Auth/AuthEndpointsTests.cs`
      couvrant : register → 201 + cookie ; email déjà pris → 409 ; login valide
      → 200 + cookie ; login invalide → 401 générique ; `/auth/me` sans token
      → 401 ; `/auth/me` avec token → 200 ; refresh fait tourner le cookie et
      l'ancien refresh token devient inutilisable (deuxième refresh avec
      l'ancien → 401) ; logout révoque puis refresh suivant → 401 ; logout
      sans cookie → 204 sans exception
- [x] Vérifier que `Watodoo.Tests/Architecture/VerticalSliceRulesTests.cs`
      passe sans modification sur `Features.Auth`

### Verification Plan
- `dotnet test --filter "FullyQualifiedName~Auth"` → tous verts
- `dotnet test` (suite complète) → tous verts, y compris
  `VerticalSliceRulesTests`

### Phase Summary
**⚠️ Pas de Docker dans ce sandbox** — `docker compose` échoue
("could not be found in this WSL 2 distro"). Impact réel sur la vérification :
- `dotnet test --filter "FullyQualifiedName~Features|FullyQualifiedName~Architecture"`
  → **15/15 verts** (validators Register/Login + les 3 tests d'architecture
  existants, qui continuent de passer sans modification sur `Features.Auth`).
- `dotnet test` (suite complète, 27 tests) → 15 verts / 12 en échec, et les 12
  échecs sont tous `DotNet.Testcontainers.Builders.DockerUnavailableException`
  — y compris `HealthEndpointTests`, qui existait déjà avant cette feature et
  n'a pas été touché. Ça confirme que l'échec est bien environnemental (pas de
  Docker ici) et non un bug introduit par le code de cette phase : tous les
  tests Testcontainers échouent de la même façon, anciens comme nouveaux.
- **`RefreshTokenRotationTests` et `AuthEndpointsTests` n'ont donc pas pu être
  exécutés avec de vraies assertions vérifiées dans ce sandbox.** Ils
  compilent et échouent au même stade (connexion Docker) que le test existant
  — mais leur *logique* (rotation, 401 sur réutilisation, cookie, etc.) reste
  à confirmer par un lancement avec Docker disponible (poste local ou CI
  GitHub Actions, qui a Docker).
- Prochaine étape recommandée avant merge : lancer `dotnet test` en local
  (avec Docker) ou attendre le résultat de la CI sur la PR pour confirmer que
  ces tests passent réellement, pas seulement qu'ils compilent.
- `FunctionalTestFixture` étendu : applique désormais les migrations
  (`db.Database.MigrateAsync()`) au démarrage de la fixture — nécessaire car
  `Program.cs` n'auto-migre jamais (règle explicite de
  `docs/decisions/architecture.md`), et injecte une config `Jwt:*` et
  `Cors:AllowedOrigins` en mémoire pour ne jamais dépendre de
  `dotnet user-secrets` (absent en CI).

## Phase 4: Frontend — plomberie (store, client HTTP, hooks)
Status: Complete

- [x] `frontend/src/features/auth/store.ts` : Zustand `{ accessToken, user,
      setAuth, clear }`, **pas de `persist`** (l'access token ne doit pas
      survivre en storage, uniquement en mémoire)
- [x] `frontend/src/features/auth/api.ts` : wrapper fetch `credentials:'include'`,
      fonctions `register`, `login`, `refresh`, `logout`, `getMe`
- [x] `frontend/src/features/auth/hooks.ts` : `useRegister`, `useLogin`,
      `useLogout` (mutations React Query, mettent à jour le store au succès),
      `useMe` (query)
- [x] Réhydratation au chargement de l'app : `useAuthBootstrap.ts` (hook
      dédié, appelle `refresh` une fois au montage) — câblage effectif dans
      `App.tsx` reporté à la Phase 5 avec les pages
- [x] `frontend/src/features/auth/store.test.ts` (Vitest) : setAuth/clear,
      dérivé `isAuthenticated`

### Verification Plan
- `cd frontend && pnpm test` → tous verts
- `cd frontend && pnpm build` → succès (typecheck inclus)

### Phase Summary
`pnpm test` : 4/4 verts (2 fichiers). `pnpm build` (tsc -b + vite build) :
succès. `pnpm lint` : les seules erreurs restantes sont **préexistantes**
(`frontend/eslint.config.ts` — erreur de parsing projectService non liée à
cette feature — et `frontend/src/main.tsx` ligne 11, `!` non-null déjà présent
dans le scaffold initial) ; aucun fichier créé dans cette phase ne déclenche
d'erreur lint. `hooks.ts` évite un `accessToken!` non-null grâce à
`accessToken ?? ''` dans `queryFn`, sûr car `enabled: accessToken !== null`
empêche React Query d'appeler `queryFn` avant qu'un token existe.
`useAuthBootstrap` reste un hook autonome non encore branché dans l'arbre React
— le câblage dans `App.tsx` est fait en Phase 5 pour éviter de modifier
`App.tsx` deux fois (plomberie puis pages).

## Phase 5: Frontend — pages Login/Register & tests interface/QA
Status: Complete — confirmé avec Playwright (4 component + 1 journey, voir Addendum en fin de fichier)

- [x] `frontend/src/features/auth/RegisterForm.tsx` (email, password, submit,
      erreurs de validation affichées, bouton désactivé pendant la requête)
- [x] `frontend/src/features/auth/LoginForm.tsx` (idem)
- [x] Câblage dans `App.tsx` : toggle d'état local login/register (pas de
      librairie de routing), branchement de `useAuthBootstrap` (Phase 4) et
      vue "connecté" avec bouton logout
- [x] `frontend/e2e/component/auth-forms.spec.ts` (Playwright, réseau mocké) :
      message d'erreur générique sur identifiants invalides, bouton disabled
      pendant la requête, bascule login/register
- [x] `frontend/e2e/journeys/auth.spec.ts` (Playwright, app réelle) : register
      → état connecté visible → logout → login à nouveau. Remplace le
      placeholder de `frontend/e2e/journeys/README.md` qui annonçait
      explicitement l'absence de test "en attendant la première feature avec
      un vrai parcours" — désormais à jour.

### Verification Plan
- `cd frontend && pnpm test:e2e` → tous verts
- `cd frontend && pnpm test:e2e:journeys` (avec `docker compose up -d` +
  backend + frontend lancés) → tous verts

### Phase Summary
`pnpm test` (Vitest) : 4/4 verts, y compris `App.test.tsx` mis à jour pour
englober `App` dans un `QueryClientProvider` — nécessaire car `App` utilise
désormais `useMutation`/`useQuery` (React Query) via les hooks auth. `pnpm
build` et `pnpm lint` : succès, seules erreurs restantes préexistantes
(`eslint.config.ts`, `main.tsx`), aucune régression sur le code de cette phase.

**⚠️ Playwright non exécutable dans ce sandbox** : les navigateurs headless
manquent des bibliothèques système (`libnspr4.so` absente) et
`playwright install-deps` échoue (`sudo` demande un terminal interactif,
indisponible ici). **Les 4 tests échouent tous de la même façon** — y
compris `e2e/component/app-shell.spec.ts`, préexistant et non modifié par
cette feature — ce qui confirme que c'est un manque d'environnement, pas un
bug introduit ici. `auth-forms.spec.ts` et `auth.spec.ts` compilent (types
Playwright corrects) mais **n'ont pas pu être exécutés avec de vraies
assertions vérifiées**. À lancer sur un poste avec les dépendances système
Playwright installées, ou en CI (le workflow `ci.yml` installe déjà
`--with-deps chromium`).
`auth.spec.ts` (journeys) nécessite en plus le backend + Postgres + Redis
réels — pas seulement `pnpm dev` — comme documenté dans
`frontend/e2e/journeys/README.md`.

## Phase 6: Vérification finale & documentation
Status: Complete (tests Docker/Playwright toujours à confirmer hors sandbox)

- [x] `dotnet test` (suite complète) + `cd frontend && pnpm test` +
      `pnpm test:e2e` → lancés ; verts pour tout ce qui ne dépend pas de
      Docker/Playwright (voir limites déjà documentées Phases 3 et 5)
- [x] Relire le diff complet pour cohérence Vertical Slice
      (agent `architecture-reviewer`)
- [x] Relire le diff complet pour sécurité/RGPD (agent `code-reviewer`) —
      feature touchant auth et données utilisateur
- [ ] Cocher "Authentification complète" dans `docs/roadmap.md` (Semaine 1) —
      **reporté à la vraie fusion** : la règle du fichier lui-même est
      "à cocher à chaque PR mergée", et ce n'est pas encore le cas
- [ ] Déplacer ce fichier vers `plans/done/authentification-complete.md`
      une fois mergé

### Verification Plan
- `dotnet test && cd frontend && pnpm test && pnpm build` → tous verts, aucune
  régression sur `/health` ni les tests existants

### Phase Summary

**Review architecture (`architecture-reviewer`)** — aucun point bloquant.
Points importants corrigés dans cette phase :
- Duplication du bloc de validation (Register/Login) → extrait dans
  `Shared/Validation/ValidatorExtensions.cs` (`ValidateAndThrowCustomAsync`,
  nommé différemment de l'extension `ValidateAndThrowAsync` native de
  FluentValidation pour éviter toute ambiguïté/confusion).
- Duplication du bloc d'émission de tokens (Register/Login/Refresh) → extrait
  dans `JwtTokenGenerator.IssueTokenPairAsync(user, db, ct)`. Réduit aussi les
  occurrences de `user.Email!` de 5 à 2 (centralisées avec garde explicite
  `?? throw`).
- Trou dans les tests d'architecture (`Shared` pouvait dépendre de
  `Features.*` sans détection) → nouveau test
  `Shared_types_other_than_DbContext_do_not_depend_on_Features` dans
  `VerticalSliceRulesTests.cs`, whitelistant explicitement `WatodooDbContext`.
- `GetMeEndpoint` : `Guid.Parse(...!)` → `Guid.TryParse` + `UnauthorizedException`
  explicite si le claim `sub` est absent/invalide.
- Point noté mais **non appliqué** : absence de `FluentValidation` sur
  `RefreshCommand`. Décision : garder un simple guard clause
  (`string.IsNullOrEmpty` → `UnauthorizedException` direct, sans requête DB)
  plutôt qu'un validator, pour ne pas transformer un cookie manquant/invalide
  en 400 (validation) alors que c'est sémantiquement un 401 (non authentifié)
  — cohérence du contrat HTTP jugée plus importante que l'uniformité stricte
  du pattern validator.
- Points mineurs non appliqués (jugés non prioritaires par le reviewer
  lui-même) : `RegisterResponse`/`LoginResponse`/`RefreshResponse` identiques
  (idiomatique en Vertical Slice, laissé tel quel) ; `user` dans le store
  Zustand plutôt que `useMe()` exclusif (compromis pragmatique explicitement
  validé par le reviewer pour l'auth spécifiquement, à ne pas reproduire
  ailleurs).

**Review sécurité/RGPD (`code-reviewer`)** — aucun finding bloquant. 4 points
**Importants** corrigés dans cette phase :
1. **Refresh token stocké en clair en base** → `JwtTokenGenerator.HashRefreshTokenValue`
   (SHA-256) : la colonne `RefreshTokens.Token` stocke désormais un hash, la
   valeur brute ne circule que dans le cookie HttpOnly côté client. Lookup par
   hash dans Refresh et Logout.
2. **Aucune protection brute-force sur `/auth/login`** → rate limiting ASP.NET
   Core natif (`AddRateLimiter`, fenêtre fixe) appliqué à tout le groupe
   `/auth` via `AuthEndpoints.MapAuthEndpoints` (`app.MapGroup("/auth").RequireRateLimiting("auth")`).
   Limite configurable (`RateLimiting:Auth:PermitLimit`/`WindowSeconds`,
   défaut 10/60s dans `appsettings.json`), avec override large
   (`10000`) dans `FunctionalTestFixture` pour ne pas rendre la suite de
   tests elle-même flaky (elle enchaîne largement plus de 10 requêtes /auth
   sur un host de test partagé). Option "lockout Identity" (SignInManager)
   écartée au profit du rate limiter : plus simple, protège aussi
   register/refresh, pas de restructuration de `LoginCommandHandler`.
3. **Race condition TOCTOU sur la rotation du refresh token** →
   `RefreshCommandHandler` utilise désormais `ExecuteUpdateAsync` avec un
   `WHERE RevokedAt == null` conditionnel : la révocation de l'ancien token
   est une unique opération SQL atomique, deux refresh concurrents sur le
   même token ne peuvent plus produire chacun un nouveau token valide (le
   second obtient `rowsAffected == 0` → 401).
4. **Pas de validation de la longueur de la signing key au démarrage** →
   ajout d'un check explicite (`SigningKey` non vide et ≥ 32 octets pour
   HS256) qui fait échouer le démarrage immédiatement plutôt qu'à la première
   génération de token.

Points **Mineurs** relevés et **délibérément non traités dans ce PR**
(ajoutés à `docs/roadmap.md` comme suivi) :
- Révocation en cascade sur réutilisation d'un token révoqué — déjà écarté
  par décision utilisateur explicite (voir en tête de ce plan).
- Enumeration d'email sur `/auth/register` (409 si email pris) — le reviewer
  lui-même le juge acceptable en l'état (pattern GitHub/Google), pas
  d'action.
- Nettoyage périodique des refresh tokens expirés/révoqués (job Hangfire) —
  hors scope de cette feature, ajouté en roadmap.
- RGPD : suppression de compte, durée de conservation — déjà couvert par
  l'item Semaine 7 existant de `docs/roadmap.md` ("RGPD (mentions légales,
  cookies, suppression de compte)").

Après ces correctifs : `dotnet build` OK, `dotnet test --filter
"FullyQualifiedName~Features|FullyQualifiedName~Architecture"` → **16/16**
verts (15 précédents + le nouveau test d'architecture). Suite complète :
16 verts / 12 échecs `DockerUnavailableException` (même cause
environnementale déjà documentée Phase 3, aucune régression).

## Addendum — Docker et Playwright confirmés fonctionnels (session suivante)

Docker est devenu disponible dans l'environnement. Deux bugs réels ont été
trouvés et corrigés en relançant la suite complète avec Docker actif :

1. **Toutes les lectures de config faites au niveau du builder (avant
   `builder.Build()`) dans `Program.cs` ignoraient les surcharges de
   `WebApplicationFactory`** (`Jwt:SigningKey`, `Cors:AllowedOrigins`,
   `RateLimiting:Auth:PermitLimit`/`WindowSeconds`). Cause : sous
   `WebApplicationFactory` avec le hosting minimal (top-level `Program.cs`),
   le callback `ConfigureAppConfiguration` passé par les tests ne s'applique
   qu'**après** l'exécution synchrone du fichier — confirmé empiriquement en
   traçant l'ordre d'exécution. Résultat concret : la suite de tests
   fonctionnels se faisait rate-limiter elle-même (429) car
   `RateLimiting:Auth:PermitLimit=10000` de `FunctionalTestFixture` n'était
   jamais vu, la vraie valeur restant celle d'`appsettings.json` (10).
   **Fix** : déplacer ces lectures de `builder.Configuration` à l'intérieur
   des délégués différés (`AddJwtBearer`, `AddCors`, `AddRateLimiter`), qui ne
   s'exécutent qu'à la résolution DI (donc après coup, quand la config de
   test est déjà en place). Aucun changement de comportement pour l'app
   réelle (hors tests), ces délégués s'exécutent de toute façon avant la
   première requête.
2. **`AuthEndpointsTests` : `WebApplicationFactory.CreateClient()` active par
   défaut un `CookieContainer` (`HandleCookies = true`)**, qui interceptait et
   remplaçait silencieusement les en-têtes `Cookie` posés manuellement dans
   les tests par le dernier `Set-Cookie` reçu. Concrètement, le test de
   rotation envoyait censément l'ancien cookie révoqué mais le client
   envoyait en réalité le nouveau (toujours actif) → faux positif côté test
   (le serveur se comportait correctement, c'est le client de test qui
   mentait sur ce qu'il envoyait). **Fix** : `AuthEndpointsTests` crée
   désormais ses clients via
   `fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false })`.
3. **Bug applicatif réel trouvé par le test de parcours QA
   (`auth.spec.ts`)** : après un logout depuis le formulaire Register (mode
   `'register'` jamais réinitialisé), l'utilisateur revoyait le formulaire
   d'inscription au lieu du formulaire de connexion. Corrigé dans `App.tsx`
   par un `useEffect` qui réinitialise `mode` à `'login'` dès que
   `isAuthenticated` repasse à `false`.

Résultat final : **`dotnet test` 28/28 verts** (stable sur 2 exécutions
consécutives), migration appliquée avec succès à la base réelle
(`dotnet ef database update`), **Playwright 4/4 (component) + 1/1
(journeys) verts** après installation manuelle des dépendances système
manquantes (voir ci-dessous — `pnpm exec playwright install-deps` échoue
sans TTY interactif pour `sudo` ; contournement : téléchargement des `.deb`
via `apt-get download` (pas besoin de root) et extraction dans
`~/.local/lib/playwright-system-deps`, activé via `LD_LIBRARY_PATH`). Ce
contournement est documenté dans `README.md` avec la vraie commande
(`playwright install-deps`) à lancer manuellement une fois sur toute machine
disposant d'un accès `sudo` interactif — le CI (`ci.yml`) n'a pas ce
problème, il tourne déjà en root.

## Final Recap

Feature complète et mergeable : authentification email/password avec
auto-login, JWT bearer (1h) + refresh token rotatif (90 jours, hashé SHA-256
en base, cookie HttpOnly), déconnexion, `/me`. Backend en Vertical Slice
Architecture (`Features/Auth/`), ASP.NET Core Identity sans rôles
(`IdentityUserContext`). Frontend : store Zustand (token en mémoire), hooks
React Query, pages Login/Register, réhydratation silencieuse au chargement.

**Deux passages de review** (`architecture-reviewer` + `code-reviewer`,
lancés deux fois — avant et après les fixes découverts en exécutant les tests
avec Docker/Playwright) : au final, **0 bloquant, 0 important** restant.
Corrections notables apportées suite aux reviews :
- Refresh tokens hashés (SHA-256), jamais stockés en clair
- Rate limiting sur `/auth` (protection brute-force basique)
- Rotation du refresh token atomique (`ExecuteUpdateAsync`) — race condition
  TOCTOU éliminée
- Validation de `Jwt:SigningKey` au démarrage via `ValidateOnStart()` (fail
  fast avant toute requête, y compris sous `WebApplicationFactory`)
- Déduplication (validation, émission de tokens), un test d'architecture
  ajouté (`Shared` ne dépend de `Features` que via `WatodooDbContext`)
- Suppression de code mort frontend (`useMe`/`getMe`/`MeResponse`, jamais
  utilisés)

**3 bugs réels trouvés en faisant tourner les tests pour de vrai** (pas
seulement en review) :
1. Les lectures de config eager dans `Program.cs` (avant `builder.Build()`)
   ignoraient les surcharges de `WebApplicationFactory` en tests — déplacées
   dans les délégués différés (`AddJwtBearer`/`AddCors`/`AddRateLimiter`)
2. `WebApplicationFactory.CreateClient()` gère les cookies automatiquement
   par défaut, ce qui faussait le test de rotation du refresh token —
   `HandleCookies = false` explicite dans `AuthEndpointsTests`
3. Après logout depuis le formulaire register, l'app réaffichait le
   formulaire register au lieu de login (trouvé par le test de parcours QA
   Playwright) — corrigé en réinitialisant le mode de façon synchrone dans
   le handler de clic (pas via un `useEffect`, qui aurait souffert d'une
   course avec le démontage du composant au clear() du store)

**État final vérifié réellement (pas seulement "devrait passer")** :
- `dotnet test` : 28/28 verts, stable sur plusieurs exécutions, avec Docker
  réel (Testcontainers)
- `pnpm test` (Vitest) : 4/4 verts
- `pnpm build` + `pnpm lint` : succès (2 erreurs de lint restantes,
  préexistantes, non liées à cette feature)
- `pnpm test:e2e` (Playwright component) : 4/4 verts
- `pnpm test:e2e:journeys` (Playwright, backend + Postgres + Redis réels) :
  1/1 vert
- Migration EF Core appliquée avec succès à une base réelle

**Reporté à un suivi ultérieur** (ajouté à `docs/roadmap.md`, pas bloquant
pour ce MVP pre-prod) : vérification email à l'inscription, nettoyage
périodique des refresh tokens expirés/révoqués, révocation en cascade sur
réutilisation détectée, rate limiter partitionné par IP (actuellement un
compteur global), lockout de compte après échecs de connexion répétés,
suppression de compte (RGPD, déjà prévue Semaine 7).

PR : https://github.com/Maxime-Lambert/Watodoo/pull/5

## Deployment Plan

Pas de déploiement automatique vers le VPS à ce stade du projet (cf.
`docs/roadmap.md` Semaine 1 — CI/CD déploiement auto pas encore fait).
Une fois la CI/CD de déploiement en place :
- Appliquer la migration `AddAuthIdentity` en prod via
  `dotnet ef database update` (manuel, jamais automatique au démarrage —
  règle explicite de `docs/decisions/architecture.md`), selon le guide
  `docs/migrations.md` (à écrire)
- Configurer `Jwt:SigningKey` en prod via secret GitHub Actions injecté en
  variable d'environnement sur le VPS (jamais dans un fichier versionné)
- Configurer `Cors:AllowedOrigins` avec le(s) vrai(s) domaine(s) frontend de
  prod (actuellement seul `http://localhost:5173` est configuré, en dev)
