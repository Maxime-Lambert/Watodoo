# Décisions d'architecture — Vespr

## Structure générale

### Monorepo
**Décision** : un seul repo Git contenant backend, frontend et infra.

**Raison** : projet solo avec déploiements systématiquement couplés (une feature
full-stack = un seul PR, un seul déploiement). Deux repos séparés créeraient
de la friction sans apporter de valeur à cette échelle.

**Structure** :
```
vespr/
├── src/                  ← ASP.NET Core
├── frontend/             ← React
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
src/Features/Films/
├── GetAll/
│   ├── GetAllFilmsQuery.cs
│   ├── GetAllFilmsQueryHandler.cs
│   ├── GetAllFilmsResponse.cs
│   └── GetAllFilmsEndpoint.cs
├── GetById/
│   └── ...
└── Rate/
    └── ...
src/Exceptions/
    ├── NotFoundException.cs
    └── ValidationException.cs
src/Middleware/
    └── ExceptionMiddleware.cs
```

### Namespaces
**Décision** : namespace racine `Vespr`, pas `Vespr.Api`.

**Raison** : le suffixe `.Api` est redondant pour un projet backend unique.

**Exemple** : `namespace Vespr.Features.Films.GetAll`

### CQRS sans MediatR
**Décision** : pattern Command/Query avec handlers injectés directement via DI.

**Raison** : MediatR est devenu payant au-delà de 1M de requêtes. Les handlers
directs sont plus simples à tracer en debug et sans dépendance tierce.

**Pattern** :
```csharp
// Handler
public class GetAllFilmsQueryHandler(VesprDbContext db)
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
TMDB et IGDB supportent `fr-FR` nativement.

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

**Raison** : Vespr a vocation à dépasser le marché français. L'i18n dès le MVP
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
**Décision** : OVH VPS (3,99€/mois) avec Nginx reverse proxy + Let's Encrypt.

**Raison** : Railway/Render trop chers pour un projet multi-services (40-60€/mois
estimé avec PostgreSQL + Redis + Hangfire). Le VPS offre un coût fixe et
prévisible, et une expérience ops valorisable.

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
