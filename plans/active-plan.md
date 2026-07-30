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
Status: Not started

- [ ] `Features/Auth/JwtTokenGenerator.cs` : `GenerateAccessToken(ApplicationUser)`,
      `GenerateRefreshTokenValue()`
- [ ] `Features/Auth/Register/` : `RegisterCommand`, `RegisterCommandValidator`
      (email format, password non vide + longueur min 8), `RegisterCommandHandler`
      (vérifie email existant → `ConflictException`, sinon `CreateAsync` + émission
      immédiate JWT + refresh token + pose du cookie), `RegisterResponse`,
      `RegisterEndpoint` (`POST /auth/register`, 201)
- [ ] `Features/Auth/Login/` : `LoginCommand`, `LoginCommandValidator`,
      `LoginCommandHandler` (`UserManager.CheckPasswordAsync`, échec →
      `UnauthorizedException` générique), `LoginResponse`, `LoginEndpoint`
      (`POST /auth/login`, 200)
- [ ] `Features/Auth/Refresh/` : `RefreshCommandHandler` (lit le cookie, vérifie
      non révoqué/non expiré en base → `UnauthorizedException` sinon, révoque
      l'ancien, crée un nouveau refresh token + nouveau JWT, repose le cookie),
      `RefreshResponse`, `RefreshEndpoint` (`POST /auth/refresh`, 200)
- [ ] `Features/Auth/Logout/` : `LogoutCommandHandler` (lit le cookie si présent,
      idempotent si absent, révoque en base, efface le cookie),
      `LogoutEndpoint` (`POST /auth/logout`, 204)
- [ ] `Features/Auth/Me/` : `GetMeQuery`, `GetMeQueryHandler` (lit
      `ClaimsPrincipal`), `GetMeResponse` (id, email), `GetMeEndpoint`
      (`GET /auth/me`, `[Authorize]`/`RequireAuthorization()`, 200/401)
- [ ] `Features/Auth/AuthEndpoints.cs` : agrège les 5 `Map(app)`
- [ ] `Program.cs` : `AddIdentityCore<ApplicationUser>` + `AddSignInManager` +
      `AddEntityFrameworkStores<WatodooDbContext>` + `AddDefaultTokenProviders`,
      politique de mot de passe MVP, `Configure<JwtOptions>`, `AddAuthentication`
      + `AddJwtBearer`, `AddAuthorization`, `AddCors` (policy nommée,
      `Cors:AllowedOrigins` depuis config), pipeline dans l'ordre documenté,
      `app.MapAuthEndpoints()`
- [ ] `appsettings.Development.json` : ajouter `Cors:AllowedOrigins` =
      `["http://localhost:5173"]`

### Verification Plan
- `dotnet build` → succès
- `docker compose up -d postgres redis && dotnet ef database update --project Watodoo.Api --startup-project Watodoo.Api && dotnet run --project Watodoo.Api`
  puis manuellement (`curl`) : register → 201 + `Set-Cookie: refreshToken`,
  login → 200, `/auth/me` sans header → 401, avec `Authorization: Bearer <token>` → 200

### Phase Summary
_(à écrire une fois la phase terminée)_

## Phase 3: Tests backend (unitaire, intégration, fonctionnel)
Status: Not started

- [ ] `Watodoo.Tests/Features/Auth/Register/RegisterCommandValidatorTests.cs`
      (email invalide, mot de passe vide/trop court)
- [ ] `Watodoo.Tests/Features/Auth/Login/LoginCommandValidatorTests.cs`
- [ ] `Watodoo.Tests/Integration/Auth/RefreshTokenRotationTests.cs`
      (Testcontainers.PostgreSql direct sur `WatodooDbContext` : rotation
      révoque l'ancien + crée le nouveau, contrainte unique sur `Token` respectée)
- [ ] Étendre `Watodoo.Tests/Functional/` avec `Watodoo.Tests/Functional/Auth/AuthEndpointsTests.cs`
      couvrant : register → 201 + cookie ; email déjà pris → 409 ; login valide
      → 200 + cookie ; login invalide → 401 générique ; `/auth/me` sans token
      → 401 ; `/auth/me` avec token → 200 ; refresh fait tourner le cookie et
      l'ancien refresh token devient inutilisable (deuxième refresh avec
      l'ancien → 401) ; logout révoque puis refresh suivant → 401 ; logout
      sans cookie → 204 sans exception
- [ ] Vérifier que `Watodoo.Tests/Architecture/VerticalSliceRulesTests.cs`
      passe sans modification sur `Features.Auth`

### Verification Plan
- `dotnet test --filter "FullyQualifiedName~Auth"` → tous verts
- `dotnet test` (suite complète) → tous verts, y compris
  `VerticalSliceRulesTests`

### Phase Summary
_(à écrire une fois la phase terminée)_

## Phase 4: Frontend — plomberie (store, client HTTP, hooks)
Status: Not started

- [ ] `frontend/src/features/auth/store.ts` : Zustand `{ accessToken, user,
      setAuth, clear }`, **pas de `persist`** (l'access token ne doit pas
      survivre en storage, uniquement en mémoire)
- [ ] `frontend/src/features/auth/api.ts` : wrapper fetch `credentials:'include'`,
      fonctions `register`, `login`, `refresh`, `logout`, `getMe`
- [ ] `frontend/src/features/auth/hooks.ts` : `useRegister`, `useLogin`,
      `useLogout` (mutations React Query, mettent à jour le store au succès),
      `useMe` (query)
- [ ] Réhydratation au chargement de l'app : appel silencieux à `refresh` dans
      `main.tsx` ou un composant racine, avant le premier rendu authentifié
- [ ] `frontend/src/features/auth/store.test.ts` (Vitest) : setAuth/clear,
      dérivé `isAuthenticated`

### Verification Plan
- `cd frontend && pnpm test` → tous verts
- `cd frontend && pnpm build` → succès (typecheck inclus)

### Phase Summary
_(à écrire une fois la phase terminée)_

## Phase 5: Frontend — pages Login/Register & tests interface/QA
Status: Not started

- [ ] `frontend/src/features/auth/RegisterForm.tsx` (email, password, submit,
      erreurs de validation affichées, bouton désactivé pendant la requête)
- [ ] `frontend/src/features/auth/LoginForm.tsx` (idem)
- [ ] Câblage minimal dans `App.tsx` ou un routeur simple pour accéder aux
      deux pages (pas de librairie de routing si aucune n'est déjà choisie —
      toggle d'état local suffit pour ce PR)
- [ ] `frontend/e2e/component/auth-forms.spec.ts` (Playwright, réseau mocké) :
      validation email/password, état disabled pendant soumission
- [ ] `frontend/e2e/journeys/auth.spec.ts` (Playwright, app réelle) : register
      → état connecté visible → logout → login à nouveau

### Verification Plan
- `cd frontend && pnpm test:e2e` → tous verts
- `cd frontend && pnpm test:e2e:journeys` (avec `docker compose up -d` +
  backend + frontend lancés) → tous verts

### Phase Summary
_(à écrire une fois la phase terminée)_

## Phase 6: Vérification finale & documentation
Status: Not started

- [ ] `dotnet test` (suite complète) + `cd frontend && pnpm test` +
      `pnpm test:e2e` → tous verts
- [ ] Relire le diff complet pour cohérence Vertical Slice
      (agent `architecture-reviewer`)
- [ ] Relire le diff complet pour sécurité/RGPD (agent `code-reviewer`) —
      feature touchant auth et données utilisateur
- [ ] Cocher "Authentification complète" dans `docs/roadmap.md` (Semaine 1)
- [ ] Déplacer ce fichier vers `plans/done/authentification-complete.md`
      une fois mergé

### Verification Plan
- `dotnet test && cd frontend && pnpm test && pnpm build` → tous verts, aucune
  régression sur `/health` ni les tests existants

### Phase Summary
_(à écrire une fois la phase terminée)_

## Final Recap
_(à écrire une fois toutes les phases terminées)_

## Deployment Plan
_(à écrire une fois toutes les phases terminées — pas de déploiement auto VPS
existant à ce stade, cf. `docs/roadmap.md` Semaine 1 ; migration à appliquer
manuellement en prod via `dotnet ef database update` selon `docs/migrations.md`
une fois ce guide écrit)_
