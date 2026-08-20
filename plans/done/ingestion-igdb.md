# Ingestion IGDB (jeux vidéo) + enrichissement FR

Deuxième script d'ingestion de données externes (Semaine 2 de la roadmap,
`docs/roadmap.md`), après TMDB (films/séries — `feature/ingestion-tmdb`,
en pause en attendant l'approbation du compte TMDB). Peuple la base locale
avec des jeux vidéo récupérés depuis IGDB, puis ajoute une passe séparée qui
va chercher un vrai titre/synopsis français (IGDB n'en fournit pas nativement,
contrairement à TMDB) via Wikidata + Wikipédia FR.

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary**; run the phase's
**Verification Plan** and record the result before moving on.

## Décisions actées avec l'utilisateur avant ce plan

- **On repart de `develop`**, pas de `feature/ingestion-tmdb` : cette dernière
  reste en l'état (commit WIP intact, rien à perdre), en pause tant que TMDB
  n'a pas répondu sur l'approbation du compte.
- **Schéma `Game` symétrique à `Film`/`TvShow`** (`TitleFr`/`TitleEn`,
  `SynopsisFr`/`SynopsisEn`) malgré l'absence de champs par langue côté IGDB —
  pour la cohérence inter-catégories (moteur de suggestion et UI n'ont pas de
  cas particulier à gérer). Contrairement à Film/TvShow où les deux langues
  viennent de TMDB directement, ici `TitleFr`/`SynopsisFr` sont peuplés dans
  un second temps par une passe dédiée (Phase 3), `TitleEn`/`SynopsisEn` par
  l'ingestion IGDB (Phase 2).
- **Source du FR : Wikidata + Wikipédia FR**, pas Steam (rejeté explicitement :
  couverture insuffisante — jeux console, jeux anciens, jeux avec client
  propriétaire hors Steam) ni traduction automatique (l'utilisateur veut une
  vraie source, pas une traduction générée). Wikidata expose une propriété
  dédiée « IGDB game ID » (`P5794`) qui permet un matching par ID fiable
  (comme l'aurait fait un App ID Steam) tout en gardant la couverture large de
  Wikipédia (tous types de jeux/plateformes, pas seulement Steam) — meilleure
  option trouvée que Wikipédia seule (qui n'aurait matché que par titre, donc
  plus de faux-matchs).

## Hypothèses techniques à vérifier en implémentant (documentées, non bloquantes)

- **Noms de champs Apicalypse exacts côté IGDB** (ex. `total_rating_count`
  pour trier par popularité approximative) : à confirmer contre
  https://api-docs.igdb.com/#game au moment d'écrire `IgdbClient`. Si le champ
  choisi n'existe pas/se comporte différemment, ajuster sans repasser par une
  nouvelle question — impact limité au tri du seed, pas à la structure du plan.
- **Limite de `titles=` par requête sur l'API MediaWiki Wikipédia FR**
  (`action=query&prop=extracts`) : supposée 20 dans ce plan (limite connue
  pour les requêtes anonymes). À confirmer/ajuster si l'API répond une erreur
  de type "too many titles" pendant les tests d'intégration.
- **Couverture Wikidata partielle par construction** : tous les jeux IGDB
  n'ont pas de correspondance Wikidata, et tous les items correspondants
  n'ont pas forcément de lien vers Wikipédia FR. Accepté comme limite du MVP
  (voir edge cases Phase 3) — un jeu sans correspondance garde `TitleFr =
  TitleEn` / `SynopsisFr = SynopsisEn` (repli IGDB), jamais de champ vide.
  **Mise à jour (invalidée puis re-vérifiée, voir Phase 3/4)** : l'hypothèse
  initiale reposait sur un matching par la propriété `P5794` ("IGDB game
  ID"), quasi vide en pratique (2/300 sur un échantillon réel, tous deux
  inexploitables) — remplacé par un matching par titre + garde-fous
  (instance-of + année de sortie), qui donne 295/300 matches exploités sur
  le même échantillon. La couverture partielle reste une limite acceptée en
  théorie, mais s'est avérée quasi totale (98%) en pratique avec le design
  final.

## Contexte technique (lu avant d'écrire ce plan)

- `docs/decisions/architecture.md` (section "APIs externes (ingestion)") :
  décision déjà prise que les données externes sont stockées en base, jamais
  d'appel à la volée depuis une requête utilisateur (rate limits IGDB : 4
  req/s, incompatible avec plusieurs utilisateurs simultanés). Table des
  sources par catégorie déjà présente (IGDB pour jeux vidéo). La ligne
  "Stratégie multilingue" affirme que TMDB **et IGDB** supportent le fr-FR
  nativement — **c'est faux pour IGDB** (pas de champs par langue), corrigé
  dans ce plan (Phase 4, mise à jour de `architecture.md`).
- `Watodoo.Api/Features/Films/` et `Watodoo.Api/Features/Series/` (branche
  `feature/ingestion-tmdb`, non mergée mais lue comme référence de pattern) :
  entité, `IngestFromTmdb/{Mapper,Job,Endpoint}`, `<Domaine>Endpoints.cs`,
  tests unitaire/intégration/fonctionnel associés. Repris à l'identique pour
  la structure `Games`/`IngestFromIgdb`, avec les adaptations IGDB détaillées
  ci-dessous.
- `Watodoo.Api/Shared/ExternalApis/Tmdb/` (même branche) : `ITmdbClient`
  typed `HttpClient` avec bearer token statique posé une fois dans
  `Program.cs` (`AddStandardResilienceHandler()`), `TmdbDiscoverQuery`/
  `TmdbDiscoverResponseDto<T>` génériques. **Ne peut pas être repris tel
  quel pour IGDB** : IGDB utilise OAuth2 client-credentials (token qui
  expire, à rafraîchir), une requête POST avec un corps en langage
  Apicalypse (pas de query string GET), et une pagination par
  `offset`/`limit` (jusqu'à 500 par requête) plutôt que par numéro de page.
- `Watodoo.Api/Shared/Security/AdminKeyEndpointFilter.cs` + `IngestionOptions`
  (branche `feature/ingestion-tmdb`, **pas encore sur `develop`**) :
  `Ingestion:AdminKey` est volontairement générique (pas `Tmdb:AdminKey`),
  pensé pour être réutilisé par les futures catégories. Comme cette branche
  part de `develop` (TMDB non mergée), ces deux fichiers n'existent pas
  encore ici — recréés à l'identique en Phase 1 (pas une divergence de
  design, juste un effet de l'ordre de merge ; à dédupliquer au moment où
  TMDB mergera, pas un problème à résoudre maintenant).
- `Watodoo.Api/Features/Auth/CleanupExpiredRefreshTokens/CleanupExpiredRefreshTokensJob.cs`
  et `Program.cs` (`develop`, seul job Hangfire mergé à ce jour) : pattern de
  job (`AddScoped`, `RunAsync(CancellationToken ct = default)`,
  `RecurringJob.AddOrUpdate<T>`), planifié `Cron.Daily(3)` UTC. Les nouveaux
  jobs de ce plan viennent après : `refresh-games-from-igdb` à 4h,
  `enrich-games-french-localization` à 5h (dépend des jeux déjà en base).
- `Watodoo.Api/Configuration/JwtOptions.cs` + bloc `AddOptions<JwtOptions>()`
  dans `Program.cs` (`develop`) : pattern de binding d'options avec
  `.Validate(...)` + `.ValidateOnStart()`, repris pour `IgdbOptions`.
- `Watodoo.Api/Shared/Data/WatodooDbContext.cs` (`develop`) :
  `IdentityUserContext`, un seul `DbSet<RefreshToken>` aujourd'hui, index
  unique + config dans `OnModelCreating`. `DbSet<Game>` ajouté ici en Phase 2,
  index unique sur `IgdbId`.
- `Watodoo.Tests/Architecture/VerticalSliceRulesTests.cs` : règles génériques
  (pas de dépendance croisée entre `Features.*`, `Shared` — sauf le
  `DbContext` — ne dépend jamais de `Features`). S'appliquent automatiquement
  aux nouveaux namespaces `Features.Games.*` et
  `Shared.ExternalApis.{Igdb,Wikidata,Wikipedia}`, aucun nouveau test requis.
- Pas de librairie de mock (Moq/NSubstitute) dans `Watodoo.Tests.csproj` —
  fakes écrits à la main, comme `FakeTmdbClient`.

## Phase 1 : Client IGDB (OAuth2 + rate limiting) + configuration + secrets

Status: Complete

- [x] `Watodoo.Api/Configuration/IngestionOptions.cs` : `AdminKey` (requis, ≥
      16 caractères) — recréé à l'identique de la branche `feature/ingestion-tmdb`
      (absent de `develop`, voir Contexte technique).
- [x] `Watodoo.Api/Shared/Security/AdminKeyEndpointFilter.cs` : comparaison
      par hash SHA-256 + `CryptographicOperations.FixedTimeEquals`, `404` (pas
      `401`/`403`) si absente/incorrecte — recréé à l'identique.
- [x] `Watodoo.Api/Configuration/IgdbOptions.cs` : `ClientId`, `ClientSecret`
      (requis), `SeedMaxItems` (défaut 5000 — avec `limit=500`/requête,
      10 requêtes max pour un seed complet), `NightlyLookbackDays` (défaut 3,
      même sémantique que `TmdbOptions`).
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IIgdbTokenProvider.cs` :
      `Task<string> GetAccessTokenAsync(CancellationToken ct)`.
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IgdbAccessTokenDto.cs` : DTO de la
      réponse `POST https://id.twitch.tv/oauth2/token` (`access_token`,
      `expires_in`, `token_type`).
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IgdbTokenProvider.cs` : typed
      `HttpClient` pointé sur `https://id.twitch.tv/`. Cache le token en
      mémoire (champ privé + `SemaphoreSlim(1,1)` pour éviter un rafraîchissement
      concurrent) avec une marge de sécurité de 5 minutes avant l'expiration
      réelle (`expires_in`). Expose aussi
      `Task InvalidateAsync(CancellationToken ct)` pour forcer un
      rafraîchissement (utilisé par `IgdbClient` sur un 401, voir edge cases).
      Enregistré en `AddSingleton` (le token est partagé entre toutes les
      requêtes IGDB, pas par scope de requête HTTP entrante).
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IgdbRateLimitingHandler.cs` :
      `DelegatingHandler` qui garantit un intervalle minimum de 260ms entre
      deux requêtes sortantes vers IGDB (marge sous la limite documentée de
      4 req/s), via un verrou statique partagé + `Task.Delay`. Inséré dans le
      pipeline du typed `HttpClient` IGDB (pas dans celui du token provider,
      qui appelle Twitch, une API différente sans cette limite).
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IgdbGameDto.cs` : `Id`, `Name`,
      `Summary?`, `FirstReleaseDate?` (unix seconds, `long?`), `TotalRating?`
      (`double?`, échelle IGDB 0-100 — **différente de l'échelle TMDB 0-10**,
      volontairement non normalisée dans ce plan, voir edge cases),
      `TotalRatingCount?` (`int?`), `Genres` (`List<int>`, ids bruts — pas
      `genres.name`, pour rester cohérent avec `Film.GenreIds`/
      `TvShow.GenreIds`), `Cover` (objet imbriqué avec `ImageId?`).
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IgdbQuery.cs` : `record` avec
      `Offset`, `Limit` (défaut 500), `ReleaseDateFrom?`/`ReleaseDateTo?`
      (`DateOnly?`, mêmes rôles que `TmdbDiscoverQuery`), `SortBy` (défaut
      `"total_rating_count desc"` — voir hypothèse technique ci-dessus).
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IIgdbClient.cs` :
      `Task<List<IgdbGameDto>> DiscoverGamesAsync(IgdbQuery query, CancellationToken ct)`.
- [x] `Watodoo.Api/Shared/ExternalApis/Igdb/IgdbClient.cs` : typed
      `HttpClient` pointé sur `https://api.igdb.com/v4/`. Construit le corps
      Apicalypse (`fields ...; sort ...; limit N; offset N; where ...;`),
      pose `Client-ID` (`IgdbOptions.ClientId`) et `Authorization: Bearer
      <token>` (via `IIgdbTokenProvider.GetAccessTokenAsync`) **par requête**
      (pas une seule fois dans `Program.cs` comme TMDB — le token change au
      fil du temps). Sur un `401` de la réponse IGDB : appelle
      `InvalidateAsync` puis retente une fois avec un nouveau token avant de
      laisser l'exception remonter.
- [x] Program.cs : `AddOptions<IgdbOptions>().BindConfiguration("Igdb")` +
      `.Validate(...)` (ClientId/ClientSecret non vides, SeedMaxItems > 0,
      NightlyLookbackDays > 0) + `.ValidateOnStart()`.
      `AddHttpClient<IIgdbTokenProvider, IgdbTokenProvider>(...)`,
      `AddTransient<IgdbRateLimitingHandler>()`,
      `AddHttpClient<IIgdbClient, IgdbClient>(...).AddHttpMessageHandler<IgdbRateLimitingHandler>().AddStandardResilienceHandler()`.
- [x] `Watodoo.Api/appsettings.json` : section `"Igdb": { "SeedMaxItems": 5000, "NightlyLookbackDays": 3 }`.
- [ ] `dotnet user-secrets set "Igdb:ClientId" "<placeholder-local>"` et
      `"Igdb:ClientSecret" "<placeholder-local>"` en local (valeurs réelles à
      créer par l'utilisateur, voir Deployment Plan Phase 4).

### Verification Plan
- `dotnet build` : succès, aucune référence cassée.
- Pas de test dédié à cette phase seule (le client est testé indirectement en
  Phase 2 via `FakeIgdbClient` dans les jobs) — cohérent avec `TmdbClient`,
  jamais testé unitairement pour lui-même dans le plan TMDB.

### Phase Summary
`dotnet build` : succès (0 warning, 0 erreur), après ajout du package
`Microsoft.Extensions.Http.Resilience` (repris tel quel de la branche TMDB,
absent de `develop`). Suite existante (16 tests unitaire + architecture,
`Watodoo.Tests`) verte, aucune régression.

Écarts par rapport au texte initial de la phase :
- `IngestionOptions`/`AdminKeyEndpointFilter` recréés ici (anticipé dans le
  plan avant l'implémentation, voir Contexte technique) — pas un écart, une
  clarification faite avant d'écrire le code.
- Item "`dotnet user-secrets set` en local" volontairement non fait : pas de
  vraies valeurs `Igdb:ClientId`/`ClientSecret` disponibles dans cet
  environnement. `ValidateOnStart()` sur `IgdbOptions` fera donc échouer
  `dotnet run` tant que ces secrets ne sont pas positionnés — attendu, sans
  impact sur `dotnet build`/`dotnet test` (les tests fonctionnels de la
  Phase 2 fourniront leurs propres valeurs de test via
  `ConfigureAppConfiguration`, comme `SeedFilmsFromTmdbTestFixture` côté
  TMDB). Reporté à la vérification manuelle de la Phase 4, quand
  l'utilisateur aura créé une vraie app Twitch/IGDB.

## Phase 2 : Feature Games — ingestion IGDB (entité, migration, mapper, job, endpoint, tests)

Status: Complete

- [x] `Watodoo.Api/Features/Games/Game.cs` : `Id` (Guid), `IgdbId` (int,
      requis), `TitleFr`/`TitleEn` (string, requis — les deux initialisés à
      la même valeur `Name` IGDB par cette phase, `TitleFr` sera écrasé par
      la Phase 3 si un match Wikidata est trouvé), `SynopsisFr?`/
      `SynopsisEn?` (même logique de repli), `CoverImageId?`, `ReleaseDate?`
      (`DateOnly?`), `Rating` (`double`, 0 par défaut), `RatingCount` (`int`),
      `GenreIds` (`int[]`), `WikidataQid?` (string, nul tant que Phase 3 n'a
      pas tourné), `FrenchEnrichedAt?` (`DateTimeOffset?`, nul = pas encore
      traité par la Phase 3), `CreatedAt`/`UpdatedAt`.
- [x] `Watodoo.Api/Shared/Data/WatodooDbContext.cs` : `DbSet<Game> Games`,
      config `OnModelCreating` (clé primaire `Id`, index unique sur `IgdbId`).
- [x] Migration EF Core `AddGames`
      (`dotnet ef migrations add AddGames --project Watodoo.Api --startup-project Watodoo.Api`).
- [x] `Watodoo.Api/Features/Games/IngestFromIgdb/GameMapper.cs` :
      `ToNewEntity(IgdbGameDto dto)` et `ApplyTo(Game existing, IgdbGameDto dto)`
      (ne touche jamais `Id`/`IgdbId`/`CreatedAt`/`WikidataQid`/
      `FrenchEnrichedAt` — ces deux derniers champs appartiennent à la Phase 3,
      un re-seed IGDB ne doit pas effacer un enrichissement FR déjà fait).
      `FirstReleaseDate` unix → `DateOnly?` (null si absent). `Summary`
      vide/absent → `SynopsisFr`/`SynopsisEn` null. `TotalRating`/
      `TotalRatingCount` absents → `Rating = 0`/`RatingCount = 0`.
- [x] `Watodoo.Api/Features/Games/IngestFromIgdb/IngestGamesFromIgdbJob.cs` :
      `SeedAsync(int maxItems, CancellationToken ct = default)` (pagine par
      `offset`/`limit=500` jusqu'à `maxItems` ou page vide, triée par
      `IgdbQuery.SortBy`) et `RefreshNightlyAsync(CancellationToken ct = default)`
      (fenêtre `first_release_date` entre aujourd'hui − `NightlyLookbackDays`
      et aujourd'hui, plafond de sécurité `NightlySafetyPageCap` comme
      `IngestFilmsFromTmdbJob`). Upsert par lot sur `IgdbId` (même pattern
      `ToDictionaryAsync` + add/update que `IngestFilmsFromTmdbJob`).
- [x] `Watodoo.Api/Features/Games/IngestFromIgdb/SeedGamesFromIgdbEndpoint.cs` :
      `POST /igdb?maxItems=N` (défaut `IgdbOptions.SeedMaxItems`, borné
      `[1, 50000]` — cohérent avec un `limit=500`/requête, soit jusqu'à 100
      requêtes), protégé par `AdminKeyEndpointFilter` (réutilisé tel quel).
- [x] `Watodoo.Api/Features/Games/GamesEndpoints.cs` : `MapGamesEndpoints`,
      groupe `/internal/ingestion/games`.
- [x] Program.cs : `AddScoped<IngestGamesFromIgdbJob>()`, `app.MapGamesEndpoints()`.
- [x] `Watodoo.Tests/Features/Games/IngestFromIgdb/GameMapperTests.cs` :
      mapping nominal, `FirstReleaseDate` absent → `ReleaseDate` null,
      `Summary` vide/absent → synopsis null, `ApplyTo` préserve
      `Id`/`IgdbId`/`CreatedAt`/`WikidataQid`/`FrenchEnrichedAt` et met à jour
      le reste.
- [x] `Watodoo.Tests/Integration/Games/FakeIgdbClient.cs` : implémente
      `IIgdbClient`, pages configurables par `(Offset, Limit)`, capture les
      requêtes reçues (comme `FakeTmdbClient`).
- [x] `Watodoo.Tests/Integration/Games/GamesDbFixture.cs` : Testcontainers
      Postgres, `[CollectionDefinition(nameof(GamesDbCollection))]` (même
      pattern que `FilmsDbFixture`).
- [x] `Watodoo.Tests/Integration/Games/IngestGamesFromIgdbJobTests.cs` :
      insertion nouveaux jeux, upsert (pas de doublon, `CreatedAt` préservé),
      arrêt sur page vide, respect de `maxItems`, fenêtre nightly correcte.
- [x] `Watodoo.Tests/Functional/Games/SeedGamesFromIgdbTestFixture.cs` :
      `WebApplicationFactory` + Testcontainers, remplace `IIgdbClient` par le
      fake en DI (même pattern que `SeedFilmsFromTmdbTestFixture` — le
      `HangfireServer` réel du host peut exécuter le job pendant le test,
      donc jamais de vrai `IIgdbClient`/token provider en test).
- [x] `Watodoo.Tests/Functional/Games/SeedGamesFromIgdbEndpointTests.cs` :
      404 sans clé, 404 mauvaise clé, 202 avec bonne clé, 400 hors bornes.

### Verification Plan
- `dotnet build` + `dotnet test --filter "FullyQualifiedName~Games"` : succès.
- `dotnet ef database update --project Watodoo.Api --startup-project Watodoo.Api`
  contre le Postgres local (`docker compose up -d`) : migration `AddGames`
  appliquée sans erreur, table `Games` avec index unique sur `IgdbId`.

### Phase Summary
Code complet et conforme au plan : entité `Game`, migration `AddGames`
(index unique `IgdbId` confirmé dans le fichier de migration généré),
mapper, job (`SeedAsync`/`RefreshNightlyAsync`), endpoint admin, câblage
Program.cs. `dotnet build` : succès (0 warning, 0 erreur).

`dotnet test --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Functional"` :
23/23 verts (16 préexistants + 7 `GameMapperTests`), aucune régression.

**Mise à jour après retour de Docker (WSL2, intégration Docker Desktop
activée par l'utilisateur)** :
- `dotnet ef database update` appliqué contre le Postgres local
  (`docker compose up -d`) : table `Games` créée, index unique `IgdbId`
  confirmé en base (pas seulement dans le fichier de migration généré).
- Suite complète (`dotnet test`, 68 tests, toutes catégories) verte, stable
  sur 2 exécutions consécutives — après 2 corrections trouvées pendant cette
  vérification réelle :
  1. **Régression sur les fixtures fonctionnelles préexistantes**
     (`FunctionalTestFixture`, `AuthRateLimitingTestFixture`) : le
     `ValidateOnStart()` ajouté en Phase 1 sur `IgdbOptions`/
     `IngestionOptions` faisait échouer au démarrage tous les tests
     fonctionnels Auth déjà en place, qui ne fournissaient pas ces clés de
     config de test. Corrigé en ajoutant les mêmes valeurs de test que
     `SeedGamesFromIgdbTestFixture` aux deux fixtures existantes.
  2. **Flakiness Hangfire.PostgreSql pré-existante rendue systématique** :
     même cause que documentée dans le plan TMDB (course statique dans
     `PostgreSqlDistributedLock` entre plusieurs `WebApplicationFactory<Program>`
     concurrents, chacun avec un vrai `HangfireServer` jamais désactivé en
     test) — le passage de 2 à 3 fixtures fonctionnelles concurrentes
     (ajout de `SeedGamesFromIgdbTestFixture`) l'a rendue quasi
     systématique. Corrigé en ajoutant `Watodoo.Tests/AssemblyInfo.cs`
     (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`),
     absent de `develop` puisque ce correctif n'existe que sur la branche
     TMDB non mergée — recréé ici pour la même raison que
     `AdminKeyEndpointFilter` en Phase 1.

Un vrai bug de test spécifique à cette phase a aussi été trouvé et corrigé :
`SeedAsync_does_not_overwrite_french_enrichment_already_done` comparait une
valeur `DateTimeOffset.UtcNow` en mémoire à la valeur relue depuis Postgres,
qui tronque à la microseconde (6 décimales) contre 7 côté .NET — faux
négatif de précision, pas un bug applicatif. Corrigé en comparant contre la
valeur déjà relue depuis la base.

## Phase 3 : Enrichissement FR (Wikidata + Wikipédia)

Status: Complete

**Révision majeure post-vérification réelle** (voir Phase 4) : la version
initiale de cette phase matchait par la propriété Wikidata `P5794` ("IGDB
game ID"). Sur un échantillon réel de 300 jeux IGDB populaires, seuls 2
avaient cette propriété renseignée, et les deux étaient inexploitables (une
erreur de saisie Wikidata pointant vers un jeu différent ; un item sans
contenu réel). `P5794` abandonné, remplacé par un matching **par titre**
avec garde-fous (instance-of "video game" + concordance d'année de sortie
±1 an). Résultat mesuré après ce changement, mêmes 300 jeux : **295/300
matches exploités**. Le détail ci-dessous décrit directement la version
finale (titre + garde-fous) ; l'historique complet de la décision est dans
`docs/decisions/architecture.md` ("Enrichissement FR jeux vidéo").

- [x] `Watodoo.Api/Shared/ExternalApis/Wikidata/WikidataMatchDto.cs` :
      `QId` (string), `FrTitle?` (label FR Wikidata), `FrWikipediaTitle?`
      (titre de l'article FR si un sitelink existe).
- [x] `Watodoo.Api/Shared/ExternalApis/Wikidata/IWikidataClient.cs` :
      `Task<WikidataMatchDto?> FindByTitleAsync(string title, DateOnly? releaseDate, CancellationToken ct)`
      — un jeu à la fois (pas de requête groupée possible côté API de
      recherche Wikidata, contrairement à l'ancienne approche par lot de
      `P5794`).
- [x] `Watodoo.Api/Shared/ExternalApis/Wikidata/WikidataClient.cs` : typed
      `HttpClient` pointé sur `https://www.wikidata.org/`, en-tête
      `User-Agent: Watodoo/1.0 (+https://watodoo.app)` (exigé par la
      politique d'accès Wikimedia). Deux appels par jeu :
      `action=wbsearchentities` (recherche par titre EN IGDB, jusqu'à 5
      candidats) puis `action=wbgetentities` (labels/claims/sitelinks des
      candidats en un seul appel groupé). Parcourt les candidats dans
      l'ordre de pertinence renvoyé par la recherche, retient le premier qui
      passe les deux garde-fous : `P31` contient `Q7889` ("video game"), et
      si une année de sortie est disponible des deux côtés (`P577`
      Wikidata / `ReleaseDate` IGDB), elle concorde à ±1 an près (marge pour
      les écarts de date de sortie régionaux autour du nouvel an). Retourne
      `null` si aucun candidat ne passe — jamais de match forcé.
- [x] `Watodoo.Api/Shared/ExternalApis/Wikidata/WikidataSearchResponseDto.cs` /
      `WikidataEntitiesResponseDto.cs` : DTOs calibrés sur les vraies
      réponses de l'API Wikidata (vérifiées par appels `curl` réels pendant
      l'implémentation, pas seulement supposées).
- [x] `Watodoo.Api/Shared/ExternalApis/Wikipedia/IWikipediaClient.cs` :
      `Task<Dictionary<string, string>> GetExtractsAsync(IReadOnlyCollection<string> titles, CancellationToken ct)`
      — retour indexé par titre exact, absent = extrait introuvable.
      Inchangé par la révision (toujours utilisé pour le contenu, seul le
      matching côté Wikidata a changé).
- [x] `Watodoo.Api/Shared/ExternalApis/Wikipedia/WikipediaClient.cs` : typed
      `HttpClient` pointé sur `https://fr.wikipedia.org/w/api.php`, même
      en-tête `User-Agent`. `action=query&prop=extracts&exintro=1&explaintext=1&redirects=1&formatversion=2&format=json&titles=...`,
      titres joints par `|`, découpés en lots de 20. Suit les redirections
      (`redirects=1`) — un titre de sitelink Wikidata peut être une
      redirection Wikipédia.
- [x] `Watodoo.Api/Features/Games/Game.cs` : déjà porteur de `WikidataQid?`/
      `FrenchEnrichedAt?` depuis la Phase 2 — rien à ajouter ici.
- [x] `Watodoo.Api/Features/Games/EnrichFrenchLocalization/EnrichGamesFrenchLocalizationJob.cs` :
      `RunAsync(int batchSize, CancellationToken ct = default)`. Sélectionne
      jusqu'à `batchSize` jeux où `FrenchEnrichedAt == null`, triés par
      `CreatedAt` croissant (FIFO). Pour chaque jeu du lot : appelle
      `IWikidataClient.FindByTitleAsync(game.TitleEn, game.ReleaseDate, ct)`
      (un appel par jeu, pas de lot groupé côté Wikidata avec cette
      approche) ; si match avec `FrTitle`, `TitleFr = FrTitle` ; si match
      avec sitelink FR, appelle `IWikipediaClient.GetExtractsAsync` pour ce
      titre et applique `SynopsisFr` si trouvé ; `WikidataQid` renseigné si
      match. **Dans tous les cas** (match ou non), `FrenchEnrichedAt =
      DateTimeOffset.UtcNow` — évite de retraiter indéfiniment les jeux sans
      correspondance (limite acceptée, voir edge cases). Un seul
      `SaveChangesAsync` en fin de lot.
- [x] `Watodoo.Api/Features/Games/EnrichFrenchLocalization/EnrichGamesFrenchLocalizationEndpoint.cs` :
      `POST /internal/ingestion/games/enrich-french?batchSize=N` (défaut
      configurable, borné `[1, 1000]`), protégé par `AdminKeyEndpointFilter`
      — déclenchement manuel pour vérifier la Phase 3 sans attendre le cron
      nightly (câblé en Phase 4).
- [x] `Watodoo.Api/Features/Games/GamesEndpoints.cs` : ajoute le mapping du
      nouvel endpoint dans le même groupe `/internal/ingestion/games`.
- [x] `Watodoo.Api/appsettings.json` : `"Games": { "FrenchEnrichmentBatchSize": 200 }`.
- [x] Program.cs : DI des nouveaux clients (`AddHttpClient<IWikidataClient, WikidataClient>(...)`,
      `AddHttpClient<IWikipediaClient, WikipediaClient>(...)`, tous deux avec
      `.AddStandardResilienceHandler()` — pas de rate limiting dédié, ni
      Wikidata ni Wikipédia n'imposent de limite comparable à IGDB),
      `AddScoped<EnrichGamesFrenchLocalizationJob>()`.
- [x] `Watodoo.Tests/Features/Games/EnrichFrenchLocalization/` : pas de
      mapper pur séparé ici (la logique d'application du match est directement
      dans le job, trop couplée à l'accès DB pour un test unitaire isolé
      pertinent) — couverte en intégration ci-dessous.
- [x] `Watodoo.Tests/Integration/Games/FakeWikidataClient.cs` (match
      configurable par titre exact, comme `FakeTmdbClient`) et
      `FakeWikipediaClient.cs` : implémentent les deux interfaces.
- [x] `Watodoo.Tests/Integration/Games/EnrichGamesFrenchLocalizationJobTests.cs` :
      jeu avec match complet (Wikidata + extrait) → `TitleFr`/`SynopsisFr`/
      `WikidataQid` mis à jour et `FrenchEnrichedAt` renseigné (contenu EN
      jamais touché) ; match sans sitelink FR → `TitleFr` mis à jour mais pas
      `SynopsisFr` ; jeu sans match Wikidata → `TitleFr`/`SynopsisFr`
      inchangés (repli IGDB) mais `FrenchEnrichedAt` quand même renseigné
      (pas de retraitement futur) ; jeu déjà enrichi (`FrenchEnrichedAt` non
      nul) → exclu du lot ; respect de `batchSize`. **Écart, inchangé par la
      révision** : la logique des deux garde-fous (instance-of "video game",
      concordance d'année) vit dans `WikidataClient` lui-même, pas dans le
      job — `FakeWikidataClient` la court-circuite entièrement (match
      configuré = toujours accepté). Non testable au niveau du job pour la
      même raison que documentée plus haut ; validée à la place par
      vérification manuelle réelle contre l'API Wikidata (voir Phase 4 —
      cas "Doom" confirmant que le garde-fou année rejette bien un
      faux-match entre deux jeux de la même franchise).
- [x] `Watodoo.Tests/Functional/Games/EnrichGamesFrenchLocalizationEndpointTests.cs` :
      404 sans clé / mauvaise clé, 202 avec bonne clé (fakes branchés dans la
      fixture fonctionnelle existante de la Phase 2, étendue pour remplacer
      aussi `IWikidataClient`/`IWikipediaClient`).

### Verification Plan
- `dotnet build` + `dotnet test --filter "FullyQualifiedName~Games"` : succès
  (inclut désormais les tests de la Phase 2 et de la Phase 3).

### Phase Summary
Code complet : clients `WikidataClient` (SPARQL, matching par `P5794`) et
`WikipediaClient` (MediaWiki `action=query&prop=extracts`), job
`EnrichGamesFrenchLocalizationJob`, endpoint admin `enrich-french`, câblage
Program.cs/appsettings.json. `dotnet build` : succès (0 warning, 0 erreur).

Précision technique non détaillée dans le texte initial du plan :
`WikipediaClient` utilise `&formatversion=2` sur la requête MediaWiki — le
format par défaut (v1) représente `missing` comme un attribut vide plutôt
qu'un booléen explicite et indexe `pages` par pageid plutôt qu'en liste,
ambigu à désérialiser proprement. `formatversion=2` donne une réponse sans
ambiguïté (`missing: bool`, `pages` en liste) ; choix fait en écrivant le
code, sans impact sur le reste du plan.

`dotnet test --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Functional"` :
23/23 verts, aucune régression (les nouveaux tests de cette phase sont tous
Integration/Functional, donc hors de ce filtre — voir ci-dessous).

**Mise à jour après retour de Docker** : `EnrichGamesFrenchLocalizationJobTests`
(Integration) et `EnrichGamesFrenchLocalizationEndpointTests` (Functional)
exécutés, suite complète 68/68 verte et stable sur 2 exécutions — voir
Phase Summary de la Phase 2 pour les 2 corrections transverses (config de
test manquante, flakiness Hangfire) qui ont débloqué toute la suite
fonctionnelle, pas seulement celle de cette phase.

Un vrai bug de test spécifique à cette phase trouvé et corrigé pendant cette
vérification : `Already_enriched_game_is_excluded_from_the_batch` échouait
parce que `EnrichGamesFrenchLocalizationJob.RunAsync` traite toute la table
`Games` (pas de filtre par `IgdbId`, contrairement aux tests de
`IngestGamesFromIgdbJobTests`), et `GamesDbFixture` partage un seul Postgres
sans réinitialisation entre tests — des jeux non enrichis laissés par
`Respects_batch_size` (même classe, même collection) polluaient le lot
sélectionné par ce test. Corrigé en ajoutant un nettoyage explicite
(`Games.ExecuteDeleteAsync()`) en tête de chaque test de cette classe, pour
un état déterministe indépendant de l'ordre d'exécution.

Écart de couverture assumé et documenté plus haut : les garde-fous internes
à `WikidataClient` ne sont de toute façon pas testables au niveau du job
(voir note dans la checklist ci-dessus) — aucun test automatisé prévu pour
eux, cohérent avec la convention existante de ne jamais tester
`TmdbClient`/`IgdbClient` unitairement pour eux-mêmes.

**Révision post-vérification réelle (Phase 4)** : matching `P5794` remplacé
par un matching par titre + garde-fous (voir texte en tête de cette phase et
`docs/decisions/architecture.md`). Fichiers modifiés :
`WikidataMatchDto.cs` (retrait du champ `IgdbId`), `IWikidataClient.cs`
(`FindByTitleAsync` remplace `FindByIgdbIdsAsync`), `WikidataClient.cs`
(réécrit entièrement — recherche + détails via l'API action Wikidata plutôt
que SPARQL), 2 nouveaux DTOs (`WikidataSearchResponseDto`,
`WikidataEntitiesResponseDto`, calibrés sur de vraies réponses testées par
`curl` pendant l'implémentation), `EnrichGamesFrenchLocalizationJob.cs`
(boucle par jeu au lieu d'un appel groupé), `FakeWikidataClient.cs` et
`EnrichGamesFrenchLocalizationJobTests.cs` (adaptés à la nouvelle
interface). Program.cs : `BaseAddress` d'`IWikidataClient` changée de
`query.wikidata.org` vers `www.wikidata.org`.

`dotnet build` + `dotnet test` (suite complète) après révision : succès,
68/68 verts, aucune régression.

**Vérification manuelle réelle après révision** (300 jeux, vrais
identifiants IGDB, voir Phase 4 pour le détail) : **295/300 matches
exploités** (contre 2/300 avec `P5794`, tous deux inexploitables). Qualité
vérifiée sur échantillon aléatoire : titres/synopsis authentiquement
français, y compris des détails typographiques corrects ("Dragon Age :
Inquisition", espace avant `:`). Garde-fou année confirmé fonctionnel sur un
cas réel de collision de titre (voir Phase 4, cas "Doom").

## Phase 4 : Cron nightly + secrets prod + vérification réelle

Status: Complete

- [x] Program.cs : `RecurringJob.AddOrUpdate<IngestGamesFromIgdbJob>("refresh-games-from-igdb", job => job.RefreshNightlyAsync(), Cron.Daily(4))`
      et `RecurringJob.AddOrUpdate<EnrichGamesFrenchLocalizationJob>("enrich-games-french-localization", job => job.RunAsync(batchSize: 200), Cron.Daily(5))`
      — après le nettoyage des refresh tokens (3h), l'enrichissement après le
      refresh des jeux (dépend des jeux déjà en base). Le seed en gros volume
      n'est jamais planifié (déclenché à la demande uniquement, comme TMDB).
- [x] `.env.prod.example` : ajoute `IGDB_CLIENT_ID`/`IGDB_CLIENT_SECRET` avec
      commentaire (créer une app sur https://dev.twitch.tv/console/apps,
      catégorie "Application Backend Service" pour le flux client-credentials,
      redirect URL peu importe puisqu'inutilisé par ce flux). **Ajoute aussi
      `INGESTION_ADMIN_KEY`** (absent de `develop`, même raison qu'en
      Phase 1 : cette branche part de `develop`, pas de TMDB — ce secret
      n'existe donc pas encore ici non plus).
- [x] `docker-compose.prod.yml` : `Igdb__ClientId: ${IGDB_CLIENT_ID}`,
      `Igdb__ClientSecret: ${IGDB_CLIENT_SECRET}`, `Ingestion__AdminKey: ${INGESTION_ADMIN_KEY}`
      sur le service `backend`.
- [x] `.github/workflows/ci.yml` (job deploy) : ajoute
      `IGDB_CLIENT_ID`/`IGDB_CLIENT_SECRET`/`INGESTION_ADMIN_KEY` aux
      `secrets.*` lus, à `envs:`, et au bloc `cat > .env` — même chaîne que
      `TMDB_BEARER_TOKEN`/`INGESTION_ADMIN_KEY` côté branche TMDB (ce dernier
      devra être dédupliqué au merge des deux branches, comme
      `AdminKeyEndpointFilter`/`IngestionOptions` en Phase 1).
- [x] `docs/decisions/architecture.md` : corrige "Stratégie multilingue"
      (IGDB ne supporte pas fr-FR nativement, contrairement à ce qui était
      écrit). Ajoute une section "Ingestion IGDB : OAuth2 client-credentials +
      rate limiting" (décision + raison, sur le modèle de la section TMDB
      équivalente une fois mergée) et une section "Enrichissement FR via
      Wikidata + Wikipédia" documentant le choix (rejeté : Steam pour la
      couverture, traduction automatique pour l'authenticité) et la limite
      acceptée (couverture partielle, pas de retraitement automatique des
      jeux sans correspondance). Note explicite que ce pattern
      d'enrichissement est probablement réutilisable pour Jikan/MangaDex
      (même absence de contenu FR natif attendue) **sans l'abstraire
      maintenant** — à généraliser seulement si une 2e catégorie en a
      effectivement besoin.
- [x] `docs/roadmap.md` : coche "Scripts d'ingestion IGDB (jeux vidéo)".
- [x] `dotnet ef database update` appliqué et vérifié contre un vrai Postgres
      local (si pas déjà fait en Phase 2).
- [x] Suite complète `dotnet test` verte, stable sur 2 exécutions
      consécutives minimum.

### Verification Plan
- `dotnet build` + `dotnet test` (suite complète) : succès.
- Vérification manuelle en local (nécessite un vrai Client ID/Secret IGDB) :
  `docker compose up -d`, `dotnet user-secrets set "Igdb:ClientId" "<vrai>"`
  et `"Igdb:ClientSecret" "<vrai>"`, `dotnet run --project Watodoo.Api`, puis :
  - `curl -X POST "http://localhost:5xxx/internal/ingestion/games/igdb?maxItems=500" -H "X-Admin-Key: <clé>"`
    → `202` + `jobId` ; observer l'exécution sur `/hangfire` puis
    `psql -h localhost -U watodoo -d watodoo -c 'SELECT COUNT(*) FROM "Games";'`
    — proche de 500.
  - `curl -X POST "http://localhost:5xxx/internal/ingestion/games/enrich-french?batchSize=50" -H "X-Admin-Key: <clé>"`
    → `202` ; puis
    `psql ... -c 'SELECT COUNT(*) FROM "Games" WHERE "FrenchEnrichedAt" IS NOT NULL;'`
    — proche de 50 ; vérifier manuellement quelques lignes où `WikidataQid`
    n'est pas nul pour confirmer que `TitleFr` diffère bien de `TitleEn`
    pour au moins quelques jeux connus.
  - Vérifier la protection : mauvaise clé → `404` sur les deux endpoints.

### Phase Summary
Cron nightly câblé (`refresh-games-from-igdb` 4h UTC, `enrich-games-french-localization`
5h UTC), chaîne de secrets prod complète (`.env.prod.example`,
`docker-compose.prod.yml`, `ci.yml`) — y compris `INGESTION_ADMIN_KEY`,
absent de `develop` pour la même raison que `AdminKeyEndpointFilter` en
Phase 1 (branche partie avant TMDB). `docs/decisions/architecture.md`
corrigé (l'affirmation "IGDB supporte le fr-FR nativement" était fausse) et
complété par les 2 sections attendues. `docs/roadmap.md` : case IGDB cochée.

**Mise à jour après retour de Docker** :
- `dotnet ef database update` appliqué (table `Games` créée, index unique
  confirmé).
- Suite complète (`dotnet test`, 68 tests, toutes catégories) verte, stable
  sur 2 exécutions consécutives après 3 corrections trouvées pendant cette
  vérification (voir Phase Summary des Phases 2-3 pour le détail) : une
  régression sur les fixtures fonctionnelles Auth préexistantes (config de
  test `Igdb`/`Ingestion` manquante), une flakiness Hangfire pré-existante
  rendue systématique par ce plan, et un vrai bug de pollution
  inter-tests dans `EnrichGamesFrenchLocalizationJobTests`.

**Mise à jour après obtention des identifiants Twitch/IGDB par l'utilisateur** :
vérification manuelle réelle exécutée en local (`dotnet run`, vrai Client
ID/Secret) :
- `POST /internal/ingestion/games/igdb?maxItems=20` → `202` + `jobId`, job
  exécuté par le vrai `HangfireServer`, **20 vrais jeux IGDB insérés** en
  base (vérifié via `psql` — titres/notes/dates réels, triés par
  `total_rating_count desc` comme attendu : Zelda BOTW, God of War, RDR2...).
- `POST /internal/ingestion/games/enrich-french?batchSize=20` → `202`,
  vrais appels HTTP sortants vers `query.wikidata.org` et
  `fr.wikipedia.org` confirmés dans les logs (200, ~700ms/~230ms) : **1/20
  jeux avec correspondance Wikidata** (`The Witcher 3: Wild Hunt`,
  `WikidataQid = Q55532`).
- Protection admin confirmée en conditions réelles : `404` sans clé et avec
  mauvaise clé, sur les deux endpoints.

**Problème réel trouvé, pas un bug de code** : le seul match Wikidata
obtenu est incorrect — `SynopsisFr` récupéré parle du jeu d'arcade *1942*
(Capcom, 1984), pas de *The Witcher 3*. Vérifié directement contre l'API
Wikidata (`SELECT ?item WHERE { ?item wdt:P5794 "1942" }` puis
`Special:EntityData/Q55532.json`) : Q55532 a bien `P5794 = "1942"` en
données réelles — **erreur de saisie côté Wikidata elle-même** (un
contributeur a vraisemblablement confondu le titre littéral "1942" avec
l'ID numérique IGDB 1942, qui désigne en réalité *The Witcher 3* — pure
coïncidence numérique).

**Mesure sur un échantillon élargi (300 jeux, décision utilisateur)** :
seed de 300 jeux IGDB réels (les plus populaires), enrichissement FR sur
l'ensemble avec le design `P5794` d'origine → **2/300 matches, les deux
inexploitables** (le cas Witcher 3/1942 ci-dessus, plus un item Wikidata
"fantôme" — label littéralement `"78"`, aucun contenu réel — matché sur
*Dragon Age II*). Vérifié directement contre Wikidata que ce n'est pas un
bug de requête : ni *Zelda: Breath of the Wild* ni *GTA V* n'ont **aucun**
item avec `P5794` renseigné, malgré leur notoriété. Conclusion : `P5794`
trop peu utilisée sur Wikidata pour servir de mécanisme de matching
primaire.

**Décision utilisateur** : basculer sur un matching par titre avec
garde-fous plutôt qu'accepter la faible couverture ou mettre l'enrichissement
de côté (voir Phase 3, révision complète du design). Après implémentation
du nouveau `WikidataClient` (recherche par titre + vérification instance-of
+ année de sortie), **même échantillon de 300 jeux ré-enrichi : 295/300
matches exploités**. Qualité vérifiée sur échantillon aléatoire de 15 jeux
matchés (titres/synopsis authentiquement français, y compris des détails
typographiques FR corrects). Les 5 jeux sans correspondance examinés
individuellement : le garde-fou année a correctement empêché un faux-match
dans au moins un cas identifié (*Doom* — IGDB désigne le reboot 2016, le
meilleur résultat de recherche Wikidata pour "Doom" est l'original 1993,
rejeté par l'écart d'année plutôt que silencieusement mal assigné).

`dotnet build` + `dotnet test` (suite complète) après la révision : 68/68
verts, aucune régression.

Phase et plan marqués `Complete` : code, migrations, tests automatisés,
câblage, et vérification manuelle réelle (identifiants IGDB réels, 300 jeux,
qualité des matchs FR confirmée) sont tous faits et fonctionnent comme
attendu. Reste uniquement les 2 étapes de déploiement prod ci-dessous, côté
utilisateur.

## Edge cases couverts (récapitulatif transverse)

- IGDB : jeu sans `first_release_date`/`summary`/`cover`/`total_rating` →
  champs nullables, jamais de valeur inventée (0/null explicites).
- IGDB : jeton OAuth2 expiré en cours de job long → un seul retry après
  invalidation forcée du token, pas de boucle infinie.
- IGDB : rate limit 4 req/s → throttling actif y compris pendant un seed de
  plusieurs dizaines de requêtes.
- Wikidata : plusieurs candidats de recherche pour un même titre (ex.
  franchise avec plusieurs épisodes) → premier candidat qui passe les deux
  garde-fous (instance-of + année), jamais le premier résultat brut de
  recherche pris aveuglément.
- Wikidata/Wikipédia : pas de correspondance (aucun candidat ne passe les
  garde-fous, ou pas de sitelink FR, ou extrait introuvable) → repli sur les
  valeurs IGDB, jamais de champ vide, `FrenchEnrichedAt` quand même
  renseigné (accepté : pas de retraitement automatique futur — limite
  documentée, cohérente avec le style des "Limites connues et acceptées"
  déjà présentes dans `architecture.md`).
- Re-seed IGDB après enrichissement FR déjà fait → `GameMapper.ApplyTo` ne
  touche jamais `WikidataQid`/`FrenchEnrichedAt`/`TitleFr`/`SynopsisFr`,
  seul `TitleEn`/`SynopsisEn`/notes/genres sont rafraîchis.
- **Résolu par la révision de la Phase 3 (voir plus haut)** : le design
  initial (matching par `P5794`) faisait confiance à une seule valeur
  d'identifiant externe sans vérification croisée — une valeur `P5794`
  erronée sur Wikidata (trouvée en vérification réelle, cas "1942" au lieu
  de *The Witcher 3*) produisait un faux-match confiant, sans que le code
  puisse le détecter. Le nouveau design (titre + garde-fou année) élimine
  structurellement cette classe de risque : un faux-match nécessiterait
  maintenant à la fois un titre suffisamment proche **et** une année de
  sortie coïncidant à ±1 an — confirmé sur le cas réel "Doom" (rejette
  correctement l'original 1993 quand IGDB désigne le reboot 2016).

## Justification des catégories de tests omises

- **Interface (UI)** : aucun nouvel écran frontend dans ce plan (endpoints
  internes admin uniquement, jamais exposés au frontend) — rien à tester.
- **QA / parcours utilisateur** : aucun nouveau parcours utilisateur, même
  raison.
- **Architecture** : héritée automatiquement de
  `VerticalSliceRulesTests` (règles génériques déjà en place, aucun nouveau
  test spécifique nécessaire pour que les nouveaux namespaces s'y
  conforment).
- **Mutation** : suite Stryker globale existante, seuil informatif au
  démarrage du projet — aucune config additionnelle par feature.

## Final Recap
Feature complète et vérifiée de bout en bout en conditions réelles : client
IGDB (`Shared/ExternalApis/Igdb/`, OAuth2 client-credentials + rate limiting
4 req/s), feature `Games` (entité, mapper, job `SeedAsync`/
`RefreshNightlyAsync`, endpoint de déclenchement manuel protégé),
enrichissement FR séparé (`Shared/ExternalApis/{Wikidata,Wikipedia}/`, job
`EnrichGamesFrenchLocalizationJob`, endpoint manuel), nightly cron (4h/5h
UTC), chaîne de secrets prod, décisions documentées dans
`docs/decisions/architecture.md` (dont la correction d'une affirmation
fausse sur le support fr-FR d'IGDB, et l'historique complet de la révision
du design d'enrichissement). `docs/roadmap.md` mis à jour.

Suite de tests complète (68 tests : unitaire, intégration Testcontainers,
fonctionnel `WebApplicationFactory`, architecture) verte de façon stable sur
plusieurs exécutions consécutives. Migration `AddGames` appliquée et
vérifiée contre un vrai Postgres. Trois bugs de tests trouvés et corrigés
pendant la vérification (détail Phases 2-3) : régression sur les fixtures
fonctionnelles Auth existantes (config de test manquante), flakiness
Hangfire pré-existante rendue systématique par ce plan, pollution
inter-tests dans `EnrichGamesFrenchLocalizationJobTests`.

**Vérification manuelle réelle complète** (identifiants Twitch/IGDB réels,
obtenus par l'utilisateur en cours de session) : seed de 300 jeux IGDB
populaires réellement inséré en base (titres/notes/dates réels, tri par
popularité confirmé), protection admin confirmée (`404` sans/mauvaise clé).
L'enrichissement FR a révélé en conditions réelles que le design initial
(matching par `P5794`) ne couvrait quasiment rien (2/300, tous deux
inexploitables) — **le plan a été révisé pendant cette vérification** (voir
Phase 3) vers un matching par titre + garde-fous (instance-of "video game" +
concordance d'année de sortie), qui donne **295/300 matches exploités** sur
le même échantillon, qualité confirmée manuellement. C'est le genre de
défaut qu'aucun test automatisé (fakes) n'aurait pu révéler — seule la
vérification contre les vraies API l'a fait apparaître.

**Reste à faire avant un déploiement prod effectif de cette feature**
(aucune ne dépend plus de l'agent) : créer les secrets GitHub Actions
(`IGDB_CLIENT_ID`/`IGDB_CLIENT_SECRET`/`INGESTION_ADMIN_KEY` — les
identifiants Twitch existent déjà, testés en local) et déployer normalement
— voir Deployment Plan ci-dessous.

## Deployment Plan
**Avant tout déploiement prod utilisant ce plan**, dans l'ordre :
1. ✅ **Déjà fait** — Créer une application sur https://dev.twitch.tv/console/apps
   (catégorie "Application Backend Service", flux client-credentials, l'URL
   de redirection n'est pas utilisée par ce flux). Client ID/Secret créés et
   testés en local avec succès pendant la vérification de la Phase 4.
2. ✅ **Déjà fait** — Générer une clé admin : `openssl rand -base64 32`
   (testée en local, voir Phase 4).
3. Créer les 3 secrets GitHub Actions sur le repo (Settings → Secrets and
   variables → Actions) : `IGDB_CLIENT_ID`, `IGDB_CLIENT_SECRET` (étape 1),
   `INGESTION_ADMIN_KEY` (étape 2 — probablement déjà créé si
   `feature/ingestion-tmdb` a été déployée avant cette branche ; sinon à
   créer ici).
4. Vérifier en local d'abord (Docker requis) : `docker compose up -d`,
   `dotnet user-secrets set "Igdb:ClientId" "<id>" --project Watodoo.Api`,
   `dotnet user-secrets set "Igdb:ClientSecret" "<secret>" --project Watodoo.Api`,
   `dotnet user-secrets set "Ingestion:AdminKey" "<clé>" --project Watodoo.Api`,
   `dotnet ef database update --project Watodoo.Api --startup-project Watodoo.Api`,
   puis `dotnet test` (suite complète) doit passer.
5. Vérification manuelle de bout en bout en local (voir Verification Plan de
   la Phase 4) : seed d'un petit volume, puis enrichissement FR, confirmer
   qu'au moins quelques jeux ont un `WikidataQid` non nul et un `TitleFr`
   différent de `TitleEn`.
6. Déployer normalement (merge vers `main`, en passant par `develop`) — le
   workflow CI écrit désormais aussi `IGDB_CLIENT_ID`/`IGDB_CLIENT_SECRET`/
   `INGESTION_ADMIN_KEY` dans le `.env` du VPS.
7. Une fois en prod, déclencher le premier seed manuellement :
   `curl -X POST "https://<domaine>/internal/ingestion/games/igdb?maxItems=5000" -H "X-Admin-Key: <clé>"`,
   attendre qu'il se termine (observer `/hangfire`), puis
   `curl -X POST "https://<domaine>/internal/ingestion/games/enrich-french?batchSize=1000" -H "X-Admin-Key: <clé>"`
   plusieurs fois si besoin (le batch est plafonné) — le nightly (4h/5h UTC)
   prendra le relai automatiquement ensuite pour les deux.

## Revue code/architecture (`/pr`)

`code-reviewer` (sécurité/RGPD) et `architecture-reviewer` (Vertical Slice)
lancés en parallèle sur le diff complet. Aucun point RGPD (aucune donnée
personnelle utilisateur dans cette feature), architecture globalement
conforme (aucun point 🔴).

**Corrigé** :
- 🔴 (sécurité) `IgdbTokenProvider` passait `client_id`/`client_secret` en
  query string vers Twitch OAuth2. Vérifié empiriquement (logs réels de la
  Phase 4) que le logging HttpClientFactory par défaut redacte déjà la
  query string (`?*`), donc pas de fuite réelle constatée — mais corrigé
  quand même vers un corps `FormUrlEncodedContent` (recommandation RFC 6749
  §2.3.1, ne dépend plus du comportement de redaction du logging pour rester
  sûr). Re-testé en conditions réelles après correction : `POST
  .../oauth2/token` toujours `200`, seed IGDB toujours fonctionnel.
- 🟠 `Games:FrenchEnrichmentBatchSize` dupliqué (clé + défaut 200) entre
  l'endpoint et le câblage du cron. Extrait dans `GamesOptions`
  (`Configuration/GamesOptions.cs`), validée au démarrage comme les autres
  options, injectée via `IOptions<GamesOptions>` aux deux endroits.
- 🟠 Commentaire de `IgdbTokenProvider` affirmait à tort un enregistrement
  Singleton (`AddHttpClient<TClient, TImplementation>` enregistre en
  réalité Transient) — le cache du token n'aurait donc pas été partagé
  entre deux jobs comme voulu. Corrigé en rendant les champs de cache
  `static` (même pattern que `IgdbRateLimitingHandler`), commentaire
  réécrit pour refléter la réalité.

**Non corrigé, différé (🟡, non bloquants selon le reviewer lui-même)** :
index filtré sur `Games.FrenchEnrichedAt` (utile seulement à volume
important, pas encore le cas) ; `SaveChangesAsync` unique en fin de lot
dans `EnrichGamesFrenchLocalizationJob` plutôt que par sous-lot (fenêtre de
perte en cas d'échec partiel, atténuée par `FrenchEnrichedAt` qui garantit
un rattrapage automatique au run suivant).

`dotnet build` + `dotnet test` (suite complète) après corrections : 68/68
verts, aucune régression.
   prendra le relai automatiquement ensuite pour les deux.
