# Décisions d'architecture — Watodoo

## Structure générale

### Monorepo
**Décision** : un seul repo Git contenant backend, frontend et infra.

**Raison** : projet solo avec déploiements systématiquement couplés (une feature
full-stack = un seul PR, un seul déploiement). Deux repos séparés créeraient
de la friction sans apporter de valeur à cette échelle.

**Structure** :
```
watodoo/
├── Watodoo.Api/          ← ASP.NET Core, tout le code applicatif
├── Watodoo.Tests/        ← xUnit + Testcontainers
├── frontend/              ← React
├── docker-compose.yml
└── .github/workflows/
```

---

## Backend

### Vertical Slice Architecture
**Décision** : une feature = un dossier = tous les fichiers qui la concernent.

**Raison** : accès immédiat à tout le code d'un use case lors d'une modification.
Clean Architecture suroptimise pour un changement d'infrastructure (changer
d'ORM, de base de données) qui n'arrivera pas sur ce projet.

**Structure type** :
```
Watodoo.Api/Features/Films/
├── Film.cs                      ← entité de domaine
├── GetAll/
│   ├── GetAllFilmsQuery.cs
│   ├── GetAllFilmsQueryHandler.cs
│   ├── GetAllFilmsResponse.cs
│   └── GetAllFilmsEndpoint.cs
├── GetById/
│   └── ...
└── Rate/
    └── ...
Watodoo.Api/Shared/
    ├── Exceptions/
    │   ├── NotFoundException.cs
    │   └── ValidationException.cs
Watodoo.Api/Middleware/
    └── ExceptionMiddleware.cs
Watodoo.Tests/Features/Films/
    └── GetAll/
        └── GetAllFilmsQueryHandlerTests.cs
```

### Namespaces
**Décision** : namespace racine `Watodoo`, pas `Watodoo.Api`.

**Raison** : le suffixe `.Api` est redondant même si le projet s'appelle
`Watodoo.Api` — le nom de projet sert à distinguer applicatif/tests, pas à
préfixer chaque namespace.

**Exemple** : `namespace Watodoo.Features.Films.GetAll`

### CQRS sans MediatR
**Décision** : pattern Command/Query avec handlers injectés directement via DI.

**Raison** : MediatR est devenu payant au-delà de 1M de requêtes. Les handlers
directs sont plus simples à tracer en debug et sans dépendance tierce.

**Pattern** :
```csharp
// Handler
public class GetAllFilmsQueryHandler(WatodooDbContext db)
{
    public async Task<List<GetAllFilmsResponse>> Handle(
        GetAllFilmsQuery query, CancellationToken ct) { ... }
}

// Endpoint
app.MapGet("/films", async (GetAllFilmsQueryHandler handler, CancellationToken ct)
    => await handler.Handle(new GetAllFilmsQuery(), ct));
```

### Validation
**Décision** : FluentValidation sur toutes les commandes et queries entrantes.

**Raison** : plus expressif que DataAnnotations, testable unitairement de manière
isolée, pas couplé aux modèles.

### Gestion des erreurs
**Décision** : exceptions custom pour les erreurs métier, middleware global pour
la conversion en réponses HTTP.

**Raison** : compromis entre lisibilité (le Result pattern introduirait une
librairie supplémentaire) et contrats clairs (les exceptions custom documentent
les cas d'erreur dans le code).

**Règle** :
- Erreurs métier prévisibles → `NotFoundException`, `ConflictException`,
  `ForbiddenException` catchées par `ExceptionMiddleware`
- Erreurs infrastructure (DB down, service externe inaccessible) → exception
  standard propagée, loguée, retournée en 500

### Migrations EF Core
**Décision** : migrations manuelles, jamais automatiques au démarrage.

**Raison** : les migrations auto en prod sont dangereuses (pas de rollback facile,
risque de perte de données). Voir `docs/migrations.md` pour le guide.

---

## Authentification

### JWT + Refresh Token rotatif
**Décision** : bearer JWT (1h) + refresh token rotatif (90 jours) stocké en base.

**Raison** : le refresh token à usage unique (rotation) invalide le token précédent
à chaque renouvellement — si un token est volé, il est inutilisable après le
premier refresh légitime.

**Implémentation** :
- Table `RefreshTokens` en base : `Token`, `UserId`, `ExpiresAt`, `RevokedAt`
- À chaque refresh : nouveau bearer + nouveau refresh token, ancien révoqué
- Refresh token stocké en cookie HttpOnly (pas en localStorage)

### MVP : email/password uniquement
**Décision** : pas de social login pour le MVP.

**Raison** : complexité d'intégration OAuth non justifiée au stade MVP. À ajouter
(Google, Discord) post-lancement si la demande existe.

### Rate limiting `/auth` partitionné par IP client
**Décision** : `AddPolicy("auth", ...)` avec un `FixedWindowLimiter` par IP
(`RateLimitPartition.GetFixedWindowLimiter`), pas un compteur global unique.

**Raison** : un compteur global permet à un seul client en rafale d'épuiser le
quota et de bloquer tout le monde (DoS trivial à déclencher).

**Implémentation** : le backend n'est jamais atteignable autrement que via le
conteneur `edge` (Caddy) — en prod son port n'est pas publié sur l'hôte, donc
n'importe quel appelant direct est de facto un proxy de confiance par
construction réseau. `ForwardedHeadersMiddleware` est donc activé
(`ForwardedHeaders.XForwardedFor`) avec `KnownIPNetworks`/`KnownProxies`
vidés plutôt qu'une liste d'IP à maintenir. Chaîne réelle en prod : Client →
Cloudflare → Caddy (`edge`) → backend. Cloudflare pose déjà
`X-Forwarded-For` avec l'IP client réelle, puis Caddy y ajoute la sienne en
relayant vers le backend (append, pas overwrite) : le header reçu a donc 2
entrées, la plus à gauche étant le vrai client.

`ForwardLimit = 2` (pas `null`/illimité) : avec `KnownIPNetworks`/
`KnownProxies` vidés, le middleware ne valide plus du tout l'origine des
entrées — un `ForwardLimit` illimité laisserait un client préfixer son
propre header avec une IP forgée (`"faux-ip, vraie-ip, ip-cloudflare"`) pour
changer de partition à volonté, y compris via le chemin normal (pas besoin de
contourner Cloudflare — trouvé en revue sécurité). En ne consommant que les 2
entrées les plus à droite (celles posées par Cloudflare et Caddy, jamais par
le client), toute entrée forgée à gauche est ignorée.

**Limite connue et acceptée** : si l'IP réelle du VPS fuitait et qu'un
attaquant contactait Caddy directement en contournant Cloudflare (donc sans
passer par les 2 sauts de confiance), il pourrait forger un
`X-Forwarded-For` arbitraire et changer de partition à chaque requête,
annulant l'effet du partitionnement — pas pire que l'absence totale de
partitionnement d'avant cette décision, mais pas traité ici (mitigation
possible : restreindre Caddy aux IP publiées par Cloudflare).

### Nettoyage des refresh tokens expirés/révoqués
**Décision** : job Hangfire récurrent (`CleanupExpiredRefreshTokensJob`,
`Cron.Daily(3)`, 3h UTC) qui supprime en base les refresh tokens expirés ou
révoqués (`RevokedAt != null || ExpiresAt <= UtcNow`), via `ExecuteDeleteAsync`
(un seul `DELETE` SQL, pas de chargement en mémoire).

**Raison** : sans ce job, les tokens révoqués (à chaque rotation, cf.
"JWT + Refresh Token rotatif" ci-dessus) et expirés (90 jours) s'accumulent
indéfiniment en base. Suppression immédiate à la révocation, sans période de
grâce : la révocation en cascade sur détection de réutilisation d'un token
révoqué est explicitement hors MVP, donc rien ne justifie de garder les
tokens révoqués pour investigation. Fréquence quotidienne (volume faible,
rien d'urgent), 3h pour ne pas tomber sur le backup PostgreSQL de 2h.

**Convention posée pour les futurs jobs Hangfire** (premier job récurrent du
projet — les prochains seront l'ingestion nightly de la Semaine 2) : classe
du job dans `Features/<Domaine>/<NomJob>/`, comme un use case normal (pas de
dossier `Jobs/` transversal), enregistrée en DI (`AddScoped`) et planifiée via
`RecurringJob.AddOrUpdate<T>(...)` dans `Program.cs`. Privilégier
`ExecuteDeleteAsync`/`ExecuteUpdateAsync` (bulk, un seul aller-retour SQL)
plutôt que charger les entités en mémoire quand le job peut toucher beaucoup
de lignes.

### Lockout de compte après échecs de connexion répétés
**Décision** : `LoginCommandHandler` utilise `SignInManager.CheckPasswordSignInAsync(user,
password, lockoutOnFailure: true)` plutôt que `UserManager.CheckPasswordAsync` — verrouille le
compte après `Lockout:MaxFailedAccessAttempts` échecs consécutifs (défaut 5),
pour `Lockout:DurationMinutes` (défaut 15).

**Raison** : `CheckPasswordAsync` seul ne compte jamais les échecs ni ne
verrouille — un attaquant peut bruteforcer un mot de passe sans limite au-delà
du rate limiter par IP (contournable en changeant d'IP, cf. décision
ci-dessus). `AddIdentityCore` (utilisé ici, pas `AddIdentity`) n'enregistre
pas `SignInManager` par défaut : ajouté explicitement via `.AddSignInManager()`,
avec `IHttpContextAccessor` (dépendance de son constructeur) enregistré via
`AddHttpContextAccessor()`.

**Message d'erreur toujours générique**, y compris compte verrouillé (pas de
message distinct type "réessaie dans 15 min") : un email inexistant ne peut
jamais atteindre l'état verrouillé (le handler sort tôt si l'utilisateur
n'existe pas, avant tout appel à `CheckPasswordSignInAsync`), donc un message
distinct pour "verrouillé" révélerait qu'un email correspond à un compte réel
après quelques tentatives — fuite d'énumération de comptes. Compromis assumé :
l'utilisateur légitime ne sait pas explicitement qu'il est verrouillé, juste
que son mot de passe est "refusé" même quand il est correct.

**Comptes déjà existants en prod** : `LockoutOptions.AllowedForNewUsers` vaut
`true` par défaut dans Identity (sans configuration explicite) — tous les
comptes déjà enregistrés ont donc déjà `LockoutEnabled = true` en base depuis
leur création, aucun backfill nécessaire.

**Limite connue et acceptée** : un attaquant qui connaît l'email d'une
victime (l'email est aussi l'identifiant de login) peut la verrouiller à
volonté en envoyant `MaxFailedAccessAttempts` mots de passe faux, y compris
en changeant d'IP pour contourner le rate limiter par IP — DoS ciblé contre
un utilisateur légitime. Compromis inhérent à tout mécanisme de lockout par
compte (même trade-off chez Auth0/Firebase), pas spécifique à cette
implémentation ; non traité ici (mitigations possibles : CAPTCHA progressif,
rate limit par compte cible en plus du rate limit par IP, notification email
à l'utilisateur en cas de verrouillage — nécessite un provider d'email, déjà
une dépendance non résolue pour la vérification d'email à l'inscription).
Item roadmap ajouté pour ce durcissement futur.

---

## APIs externes (ingestion)

### Ingestion locale + jobs de synchronisation
**Décision** : les données des APIs externes sont stockées dans PostgreSQL.
Les requêtes utilisateur frappent la base locale, pas les APIs externes.

**Raison** :
- Les APIs ont des rate limits incompatibles avec des appels à la volée
  (IGDB : 4 req/s, incompatible avec plusieurs utilisateurs simultanés)
- L'algorithme de suggestion doit comparer des milliers d'œuvres — impossible
  sur des APIs paginées en temps réel
- Les données changent lentement (refresh nocturne largement suffisant)

**Implémentation** :
- Job Hangfire d'ingestion initiale (one-shot, lancé manuellement)
- Jobs Hangfire récurrents (nightly) pour les nouveautés et mises à jour
- Redis : cache des résultats de suggestions calculés, pas des données brutes

### Sources par catégorie
| Catégorie | API | Notes |
|-----------|-----|-------|
| Films / Séries | TMDB | `language=fr-FR`, fallback `en-US` |
| Jeux vidéo | IGDB (via Twitch) | Tags genre natifs |
| Anime | Jikan (MAL non-officiel) | Pas de clé requise |
| Manga / Manwha | MangaDex | API ouverte |
| Livres | Google Books API | Tier gratuit généreux |

### Stratégie multilingue
**Décision** : français par défaut, fallback anglais. Deux champs en base :
`title_fr`, `title_en` (idem pour `synopsis_fr`, `synopsis_en`).

**Raison** : bien plus simple à gérer dès le schéma initial que de migrer après.
TMDB supporte `fr-FR` nativement (`language=fr-FR`/`en-US` sur les mêmes
endpoints). **IGDB, en revanche, ne fournit aucun contenu par langue** (pas de
`name_fr`/`summary_fr` — un seul `name`/`summary`, en anglais dans l'immense
majorité des jeux) : voir "Ingestion IGDB" et "Enrichissement FR jeux vidéo"
ci-dessous pour la façon dont `Game.TitleFr`/`SynopsisFr` sont malgré tout
peuplés. Ancienne version de cette décision affirmait à tort que "TMDB et IGDB
supportent le fr-FR nativement" — corrigé après vérification en implémentant
l'ingestion IGDB (`plans/active-plan.md`).

### Ingestion IGDB : OAuth2 client-credentials + rate limiting

**Décision** : `IgdbClient` (`Shared/ExternalApis/Igdb/`) obtient un token
d'accès via le flux OAuth2 client-credentials de Twitch (`POST
https://id.twitch.tv/oauth2/token`), caché en mémoire et rafraîchi
automatiquement par `IgdbTokenProvider` (marge de sécurité de 5 minutes avant
expiration théorique, verrou pour éviter un rafraîchissement concurrent). Les
requêtes vers `https://api.igdb.com/v4/` sont des `POST` avec un corps en
langage Apicalypse (`fields ...; sort ...; limit ...; offset ...; where ...;`),
pagination par `offset`/`limit` (jusqu'à 500 résultats par requête) plutôt que
par numéro de page comme TMDB.

**Raison** : IGDB n'expose pas de bearer token statique comme TMDB — c'est un
vrai flux OAuth2 dont le token expire (théoriquement ~60 jours, mais non
garanti). Un `Client-ID` + `Authorization: Bearer <token>` doivent être posés
par requête (pas une seule fois au démarrage) puisque le token peut changer en
cours de vie du process.

**Rate limiting** : `IgdbRateLimitingHandler` (`DelegatingHandler` inséré dans
le pipeline du `HttpClient` IGDB) garantit un intervalle minimum de 260ms entre
deux requêtes sortantes, sous la limite documentée de 4 req/s. Sur un `401` de
réponse, `IgdbClient` invalide le token en cache et retente une fois avant de
laisser l'exception remonter (token révoqué avant expiration théorique).

### Enrichissement FR jeux vidéo (Wikidata + Wikipédia)

**Décision** : une passe séparée (`Features/Games/EnrichFrenchLocalization/`,
job `EnrichGamesFrenchLocalizationJob`, planifié nightly après le refresh IGDB)
va chercher un titre et un synopsis français réels pour chaque jeu, via
Wikidata puis Wikipédia FR — jamais de traduction automatique du contenu
anglais IGDB. Matching **par titre** (`WikidataClient.FindByTitleAsync`,
recherche `action=wbsearchentities` sur le nom IGDB, jusqu'à 5 candidats),
avec deux garde-fous avant d'accepter un candidat : il doit être une instance
de "video game" (`P31 = Q7889`), et si l'un des deux côtés a une date de
sortie, l'année doit concorder à ±1 an près (`P577` côté Wikidata contre
`Game.ReleaseDate` côté IGDB) — sinon le prochain candidat est essayé. Le
sitelink `fr.wikipedia.org` du candidat retenu donne le titre exact de
l'article à interroger ensuite (`WikipediaClient`, extrait d'introduction via
`action=query&prop=extracts&formatversion=2`). Chaque jeu porte
`WikidataQid`/`FrenchEnrichedAt` (`Features/Games/Game.cs`) :
`FrenchEnrichedAt` marque qu'une tentative a eu lieu (match trouvé ou non),
pour ne jamais retraiter indéfiniment un jeu sans correspondance.

**Historique de la décision** : la première version de ce design matchait par
la propriété Wikidata dédiée "IGDB game ID" (`P5794`), pas par titre — choix
initial motivé par la fiabilité d'un matching par ID (éviter les faux-matchs
d'une recherche floue). **Abandonné après vérification réelle** : sur un
échantillon de 300 jeux IGDB parmi les plus populaires (Zelda BOTW, GTA V, God
of War...), seuls 2 avaient une valeur `P5794` renseignée sur Wikidata, et les
deux étaient inexploitables (un item dont le `P5794` était une pure erreur de
saisie pointant vers un jeu totalement différent ; un item "fantôme" sans
label ni contenu réel). La propriété `P5794` s'est révélée trop peu utilisée
sur Wikidata pour servir de mécanisme de matching primaire, y compris pour des
jeux très connus. Remplacée par la recherche par titre + les deux garde-fous
ci-dessus, qui reproduisent la fiabilité recherchée par le matching par ID
sans dépendre de cette propriété quasi-vide.

**Raison du choix Wikidata + Wikipédia (toujours valable)** : Steam (fiche
boutique localisée, matching fiable par App ID) a été écarté — couverture
insuffisante pour l'objectif du projet (jeux console, jeux anciens, jeux avec
client propriétaire hors Steam). Une traduction automatique du contenu anglais
aurait donné une couverture universelle mais pas un "vrai" titre/synopsis
français, ce qui était le critère explicite retenu.

**Résultat vérifié en conditions réelles** (300 jeux, vrais identifiants
IGDB, `plans/active-plan.md`) : **295/300 jeux avec un titre/synopsis français
authentique** trouvé et appliqué, qualité confirmée sur échantillon (ex.
"Dragon Age : Inquisition" — espace insécable avant le `:`, typographie
française correcte, synopsis entièrement en français et factuellement exact).
Les 5 jeux sans correspondance sont des cas où le garde-fou année a
correctement rejeté un faux-match probable (ex. "Doom" IGDB = le reboot 2016,
mais le meilleur résultat de recherche Wikidata pour "Doom" est l'original de
1993 — rejeté par l'écart d'année plutôt que silencieusement mal assigné).

**Limite acceptée** : couverture non garantie à 100% par construction (jeu
absent de Wikidata, sans sitelink FR, ou dont le seul candidat de recherche ne
passe pas les garde-fous) — un jeu sans correspondance garde le contenu
anglais IGDB en repli, jamais de champ vide, jamais retraité automatiquement
ensuite.

**Pattern probablement réutilisable** pour les futures catégories sans contenu
FR natif (Jikan/anime, MangaDex/manga — à confirmer au moment de les
implémenter), mais volontairement **pas abstrait maintenant** : à généraliser
seulement si une deuxième catégorie en a effectivement besoin.

---

## Frontend

### React Query + Zustand
**Décision** : deux outils aux rôles distincts, pas interchangeables.

**Règle** :
- **React Query** : tout ce qui vient du serveur (listes, profil, bibliothèque).
  Gère le cache, refetch, états loading/error.
- **Zustand** : état UI local uniquement (filtres sélectionnés, état des drawers,
  thème). Ne persiste pas côté serveur.

### CSS : Tailwind
**Décision** : Tailwind CSS. Shadcn/ui non inclus dans le MVP.

**Raison** : Shadcn peut être ajouté à tout moment si besoin de composants
complexes. Partir sans évite une dépendance non nécessaire au MVP.

### Internationalisation
**Décision** : `i18next` + `react-i18next`. Français par défaut, anglais disponible.

**Raison** : Watodoo a vocation à dépasser le marché français. L'i18n dès le MVP
évite une migration douloureuse plus tard.

---

## Infrastructure

### Environnements
**Décision** : deux environnements uniquement — local et prod.

**Raison** : pas de budget pour un environnement de staging dédié. La CI/CD
(GitHub Actions) joue le rôle de validation intermédiaire : build + tests
automatiques avant chaque déploiement en prod.

### Docker
**Décision** : un seul `docker-compose.yml` pour le développement local
(app + PostgreSQL + Redis). Un `docker-compose.prod.yml` pour la prod sur OVH VPS.

### Hébergement
**Décision** : OVH VPS (3,99€/mois) avec Caddy comme reverse proxy + HTTPS
automatique (Let's Encrypt intégré, pas de certbot séparé à gérer).

**Raison** : Railway/Render trop chers pour un projet multi-services (40-60€/mois
estimé avec PostgreSQL + Redis + Hangfire). Le VPS offre un coût fixe et
prévisible, et une expérience ops valorisable.

**Architecture des conteneurs prod** (`docker-compose.prod.yml`) : un seul
conteneur "edge" (`Dockerfile.edge`) fait à la fois reverse-proxy HTTPS et sert
les fichiers statiques du frontend buildé — pas de conteneur nginx/caddy séparé
pour le frontend, pour rester léger sur un VPS aux ressources limitées. Le
frontend appelle l'API en chemin relatif (`/api/*`), proxyfié en interne vers
`backend:8080` (avec `strip_prefix /api`, les routes backend n'ayant pas ce
préfixe) — même origine, donc pas de CORS nécessaire pour le flux normal en
prod. Postgres/Redis ne sont pas exposés sur l'hôte en prod (réseau Docker
interne uniquement), contrairement au `docker-compose.yml` local.

**DNS et Cloudflare** : `watodoo.app` est proxifié par Cloudflare (nuage
orange) — le DNS pointe vers des IPs Cloudflare, pas directement vers le VPS.
Conséquence : le challenge Let's Encrypt HTTP-01 par défaut de Caddy (port 80)
n'est pas fiable derrière ce proxy. `Dockerfile.edge` build donc un binaire
Caddy custom (via `xcaddy` + plugin `caddy-dns/cloudflare`) et `Caddyfile`
utilise un challenge DNS-01 (`tls { dns cloudflare {env.CLOUDFLARE_API_TOKEN} }`),
qui prouve la possession du domaine via un enregistrement TXT plutôt que par le
trafic HTTP entrant — fonctionne indépendamment du proxy Cloudflare. Nécessite
un token API Cloudflare scopé `Zone:DNS:Edit` sur la zone uniquement (secret
`CLOUDFLARE_API_TOKEN`).

### Migrations en production
**Décision** : les migrations EF Core s'appliquent automatiquement au démarrage
du conteneur backend (`Database.Migrate()`, gated à `IsProduction()`), pas de
job CI séparé.

**Raison** : évite une étape de déploiement manuelle supplémentaire, acceptable
pour une instance unique à faible trafic. À revoir si l'app scale un jour à
plusieurs instances (migrations concurrentes = risque de conflit).

### Backup PostgreSQL
**Stratégie** :
```bash
# Cron 2h du matin chaque nuit
pg_dump vespr_prod | gzip > /backups/vespr_$(date +%Y%m%d).sql.gz
find /backups -name "*.sql.gz" -mtime +30 -delete
```
Backups envoyés sur OVH Object Storage (~0,01€/Go) pour résilience externe.

### Variables d'environnement
- **Local** : fichier `.env` (gitignored)
- **Prod** : secrets GitHub Actions injectés dans le VPS au déploiement
- Jamais de secrets dans le code ou dans un fichier versionné
