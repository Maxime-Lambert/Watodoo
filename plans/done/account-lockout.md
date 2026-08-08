# Lockout de compte après échecs de connexion répétés

`LoginCommandHandler` utilise aujourd'hui `UserManager.CheckPasswordAsync`,
qui vérifie le mot de passe mais n'incrémente jamais le compteur d'échecs ni
ne verrouille le compte — un attaquant peut bruteforcer un mot de passe sans
aucune limite (au-delà du rate limiter par IP, contournable en changeant
d'IP). Objectif : basculer sur `SignInManager.CheckPasswordSignInAsync` pour
activer le verrouillage de compte après N échecs consécutifs. Dernier item
ouvert de la Semaine 1 (`docs/roadmap.md:20`).

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary**; run the phase's
**Verification Plan** and record the result before moving on.

## Contexte technique (lu avant d'écrire ce plan)

- `Watodoo.Api/Features/Auth/Login/LoginCommandHandler.cs` : appelle
  `userManager.FindByEmailAsync` puis `userManager.CheckPasswordAsync` — ne
  touche jamais `AccessFailedCount`/`LockoutEnd`.
- `Watodoo.Api/Features/Auth/ApplicationUser.cs` : `IdentityUser<Guid>`, a
  déjà nativement les colonnes `LockoutEnd`, `LockoutEnabled`,
  `AccessFailedCount` (confirmé dans la migration existante
  `20260730124708_AddAuthIdentity.cs`) — **aucune nouvelle migration EF Core
  nécessaire**, le schéma les a déjà.
- `Program.cs` : `AddIdentityCore<ApplicationUser>(...)
  .AddEntityFrameworkStores<WatodooDbContext>().AddDefaultTokenProviders();`
  — `AddIdentityCore` (pas `AddIdentity`) n'enregistre **pas**
  `SignInManager<TUser>` par défaut, contrairement à `AddIdentity` qui est le
  setup "tout compris". Il faut ajouter `.AddSignInManager()` explicitement à
  la chaîne.
- `SignInManager<TUser>` dépend de `IHttpContextAccessor` dans son
  constructeur (même si `CheckPasswordSignInAsync` ne l'utilise pas
  directement) — **pas encore enregistré dans `Program.cs`**, il faut ajouter
  `builder.Services.AddHttpContextAccessor();`. Les autres dépendances du
  constructeur (`IUserClaimsPrincipalFactory`, `IAuthenticationSchemeProvider`,
  `IUserConfirmation`) sont déjà fournies par `AddIdentityCore` et
  `AddAuthentication(...)`, déjà appelés.
- `LockoutOptions.AllowedForNewUsers` vaut `true` par défaut dans Identity
  (sans configuration explicite) : tous les utilisateurs déjà enregistrés en
  prod ont donc déjà `LockoutEnabled = true` en base depuis leur création —
  **aucun backfill nécessaire** pour les comptes existants.
- `Watodoo.Api/Shared/Exceptions/UnauthorizedException.cs` : déjà catchée par
  `ExceptionMiddleware` → 401. Réutilisée telle quelle, pas de nouvelle
  exception.
- `frontend/src/features/auth/LoginForm.tsx:49` : affiche
  `loginMutation.error.message` tel quel, sans texte codé en dur côté
  frontend — **aucun changement frontend nécessaire** tant que le message
  d'erreur backend reste inchangé en forme.
- `Watodoo.Tests/Functional/Auth/AuthEndpointsTests.cs` +
  `FunctionalTestFixture.cs` : fixture partagée, `RateLimiting:Auth:PermitLimit=10000`
  déjà configuré (assez large pour ne pas interférer avec les tests de
  lockout, qui envoient quelques requêtes de plus par test).

## Décisions

- **Message d'erreur toujours générique** (`"Email ou mot de passe
  incorrect."`), y compris quand le compte est verrouillé — pas de message
  distinct type "compte verrouillé, réessaie dans 15 min". Raison : un email
  inexistant ne peut jamais atteindre l'état verrouillé (le handler sort tôt
  si `FindByEmailAsync` retourne `null`, avant tout appel à
  `CheckPasswordSignInAsync`), donc un message distinct pour "verrouillé"
  révélerait qu'un email correspond à un compte réel après quelques
  tentatives — fuite d'énumération de comptes. Compromis UX/sécu assumé :
  l'utilisateur légitime ne sait pas explicitement qu'il est verrouillé, mais
  voit son mot de passe "refusé" même quand il est correct — signal
  suffisant pour qu'il attende ou fasse un reset password (pas encore
  implémenté, futur item roadmap).
- **Pas de test sur l'expiration réelle du verrou** (attendre que
  `LockoutEnd` soit dépassé) : ce comportement est entièrement interne à
  ASP.NET Core Identity (déjà testé par le framework), pas du code à nous —
  on teste seulement notre câblage (`CheckPasswordSignInAsync` appelé avec
  `lockoutOnFailure: true`, le résultat correctement traduit en 401).
- `Lockout:MaxFailedAccessAttempts` (défaut 5) et `Lockout:DurationMinutes`
  (défaut 15) configurables via `appsettings`/variables d'env, même
  convention que `RateLimiting:Auth:*` déjà en place — pas de nouvelle
  fixture de test dédiée nécessaire : le lockout est scopé par utilisateur
  (pas global comme le rate limiter ou le job de cleanup), donc plusieurs
  tests avec des emails uniques n'interfèrent jamais entre eux même sur la
  fixture partagée existante.

## Phase 1: Activer le lockout Identity

Status: Complete

- [x] `Program.cs` : ajouter `builder.Services.AddHttpContextAccessor();`
- [x] `Program.cs` : dans le bloc `AddIdentityCore<ApplicationUser>(options => {...})`,
      ajouter `options.Lockout.MaxFailedAccessAttempts =
      builder.Configuration.GetValue("Lockout:MaxFailedAccessAttempts", 5);`
      et `options.Lockout.DefaultLockoutTimeSpan =
      TimeSpan.FromMinutes(builder.Configuration.GetValue("Lockout:DurationMinutes", 15));`
- [x] `Program.cs` : ajouter `.AddSignInManager()` à la chaîne après
      `.AddDefaultTokenProviders()`
- [x] `Watodoo.Api/Features/Auth/Login/LoginCommandHandler.cs` : injecter
      `SignInManager<ApplicationUser> signInManager`, remplacer
      `userManager.CheckPasswordAsync(user, command.Password)` par
      `(await signInManager.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true)).Succeeded`
      — garder `FindByEmailAsync` inchangé (nécessaire, `CheckPasswordSignInAsync`
      exige un `user` non-null)
- [x] `docs/decisions/architecture.md` : ajouter un paragraphe sous
      "Authentification" documentant la décision (seuil, durée, message
      générique et sa raison anti-énumération)
- [x] `docs/roadmap.md:20` : cocher l'item une fois vérifié

### Verification Plan
- `dotnet build` → 0 erreur, 0 warning
- Relecture visuelle : `AddHttpContextAccessor()` et `.AddSignInManager()`
  bien présents, `LoginCommandHandler` ne référence plus `CheckPasswordAsync`

### Phase Summary
`dotnet build` propre (0 warning, 0 erreur). Suite complète existante
(37 tests, incluant `AuthEndpointsTests`) toujours verte après le changement
de `LoginCommandHandler` — aucune régression sur les logins avec bon mot de
passe du premier coup.

## Phase 2: Tests fonctionnels

Status: Complete

Catégories concernées et justification (voir `docs/testing-strategy.md`) :
- **Fonctionnel backend** : seule catégorie pertinente — `SignInManager`
  n'est correctement résolu (toutes ses dépendances DI) que dans un host
  complet ; le comportement à vérifier traverse tout le pipeline
  (endpoint → handler → Identity → Postgres réel) exactement comme
  `AuthEndpointsTests.cs` existant.
- **Intégration backend** : volontairement omise — un test direct sur
  `WatodooDbContext` vérifierait juste que les colonnes Identity changent en
  base, ce que le test fonctionnel couvre déjà de façon plus représentative
  (via le vrai endpoint HTTP), doublon évité.
- **Unitaire** : rien à isoler — pas de règle métier à nous, juste un appel à
  l'API Identity.
- **Interface / QA e2e** : aucun changement frontend (message d'erreur déjà
  affiché génériquement, voir Contexte technique).
- **Architecture** : aucune nouvelle règle structurelle.
- **Mutation** : couvert passivement par Stryker.

Réutilise `FunctionalTestFixture`/`FunctionalTestCollection` existants — le
lockout est scopé par utilisateur, pas besoin de fixture dédiée avec un seuil
bas (contrairement au rate limiter/cleanup qui sont globaux).

- [x] `Watodoo.Tests/Functional/Auth/AccountLockoutTests.cs` (nouveau
      fichier, `[Collection(nameof(FunctionalTestCollection))]`, même pattern
      que `AuthEndpointsTests.cs` : `UniqueEmail()`, `CreateClient()`)
- [x] Test : après `MaxFailedAccessAttempts` (5, valeur par défaut non
      surchargée) tentatives avec un mauvais mot de passe, une tentative
      suivante **avec le bon mot de passe** échoue quand même (401) — le
      cœur de la feature : le compte est verrouillé même pour des
      identifiants corrects
- [x] Test : avant d'atteindre le seuil (`MaxFailedAccessAttempts - 1` échecs),
      une connexion avec le bon mot de passe réussit normalement (pas de
      verrouillage prématuré)
- [x] Test edge case : une connexion réussie entre deux séries d'échecs
      réinitialise le compteur — `MaxFailedAccessAttempts - 1` échecs, puis 1
      succès, puis `MaxFailedAccessAttempts - 1` échecs à nouveau ne doivent
      *pas* verrouiller le compte (comportement Identity par défaut, mais
      c'est notre câblage — `lockoutOnFailure: true` — qui doit le laisser
      s'exprimer correctement)

### Verification Plan
- `dotnet test --filter FullyQualifiedName~AccountLockoutTests` → tous verts
- `dotnet test` (suite complète) → tous verts, aucune régression sur
  `AuthEndpointsTests` existants

### Phase Summary
`dotnet test --filter FullyQualifiedName~AccountLockoutTests` : 3/3 verts.
Suite complète (40 tests) : verte, relancée deux fois pour confirmer la
stabilité (le lockout étant scopé par utilisateur avec emails uniques par
test, pas d'interférence observée avec la fixture partagée, contrairement
au rate limiter/job de cleanup rencontrés précédemment).

## Phase 3: Revue (code-reviewer + architecture-reviewer)

Status: Complete

- [x] `code-reviewer` (sécurité/RGPD) : aucun point bloquant. 🟠 important
      trouvé — un attaquant connaissant l'email d'une victime peut la
      verrouiller à volonté en changeant d'IP (contourne le rate limiter par
      IP), DoS ciblé. Jugé par le reviewer comme un compromis inhérent à tout
      mécanisme de lockout par compte (même trade-off chez Auth0/Firebase),
      pas un défaut de cette implémentation — "pas bloquant pour cette PR
      tant qu'il est délibérément accepté". Documenté comme risque accepté
      dans `docs/decisions/architecture.md` + nouvel item roadmap Semaine 1
      pour le durcissement futur (CAPTCHA progressif, rate limit par compte
      cible, notification email). 🟡 mineurs laissés tels quels : canal de
      timing user-inexistant/mauvais-mot-de-passe (déjà largement éclipsé par
      le 409 d'énumération existant sur `/auth/register`, non spécifique à
      cette PR) et duplication de la constante de seuil dans le test.
- [x] `architecture-reviewer` : aucun point bloquant/important. 🟡 mineur
      corrigé — `RegisterUser` (helper de test) n'assertait pas le succès de
      l'inscription, risque de faux positif silencieux sur le test principal
      si l'inscription échouait un jour silencieusement (les 401 attendus
      seraient alors dus à "utilisateur inexistant", pas à un vrai
      verrouillage). Corrigé avec `response.EnsureSuccessStatusCode()`. 🟡
      mineur laissé tel quel : `UniqueEmail()` dupliqué entre
      `AccountLockoutTests` et `AuthEndpointsTests` — déjà le cas avant cette
      PR, pas une régression introduite ici. Choix de réutiliser la fixture
      partagée (pas de fixture dédiée à seuil bas, contrairement au rate
      limiter/cleanup) confirmé sûr : le lockout est scopé par utilisateur
      via `AccessFailedCount`, emails uniques par test, aucune interférence
      possible même avec `AuthEndpointsTests` dans la même collection.

### Verification Plan
- `dotnet build` → 0 warning, 0 erreur
- `dotnet test` (suite complète) → tous verts

### Phase Summary
`dotnet build` propre. Suite complète : 40/40 verts après le correctif
`EnsureSuccessStatusCode()`.

## Final Recap

`LoginCommandHandler` bascule de `UserManager.CheckPasswordAsync` (ne compte
jamais les échecs) vers `SignInManager.CheckPasswordSignInAsync(user,
password, lockoutOnFailure: true)`, activant le verrouillage de compte après
`Lockout:MaxFailedAccessAttempts` échecs (défaut 5) pendant
`Lockout:DurationMinutes` (défaut 15). `AddIdentityCore` n'enregistrant pas
`SignInManager` par défaut (contrairement à `AddIdentity`), `.AddSignInManager()`
et sa dépendance `AddHttpContextAccessor()` ont été ajoutés. Aucune migration
EF Core nécessaire (colonnes de lockout déjà présentes dans le schéma
Identity), aucun backfill pour les comptes déjà en prod
(`LockoutOptions.AllowedForNewUsers` vaut `true` par défaut). Message d'erreur
toujours générique, y compris en cas de verrouillage, pour éviter une fuite
d'énumération de comptes. Aucun changement frontend nécessaire. Décision et
un risque accepté (DoS par lockout via changement d'IP, trouvé en revue
sécurité, item roadmap ajouté pour durcissement futur) documentés dans
`docs/decisions/architecture.md`. Dernier item ouvert de la Semaine 1 coché
(`docs/roadmap.md:20`) — **Semaine 1 (Fondations) désormais entièrement
close**, hors les deux items explicitement différés (révocation en cascade,
durcissement anti-DoS du lockout) et la vérification email (bloquée sur le
choix d'un provider). 3 tests fonctionnels (seuil atteint, seuil non atteint,
reset du compteur après succès). Revues sécurité/RGPD (1 point important,
documenté comme risque accepté) et architecture (1 point mineur corrigé)
effectuées avant la PR. Suite complète : 40/40 verts, stable. Aucun
changement frontend.

## Deployment Plan

1. Commit + PR `feat/account-lockout` (depuis `develop`) → `develop`
2. CI GitHub Actions doit passer (build + tests complets)
3. Lors de la prochaine PR `develop` → `main`, ce changement partira en prod
   avec le reste — pas de migration DB, pas d'étape de déploiement spécifique
4. Vérification en prod après déploiement : tenter 5 connexions avec un
   mauvais mot de passe sur un compte de test, puis une 6e avec le bon mot de
   passe → doit renvoyer 401 (verrouillé), pas 200 ; attendre 15 min ou
   attendre le déverrouillage naturel avant de re-tester le login normal
