# CI/CD — Déploiement automatique GitHub Actions → OVH VPS (Caddy)

Dockeriser le backend et le frontend, écrire la config prod (`docker-compose.prod.yml`
+ `Caddyfile`), et ajouter un job de déploiement dans `ci.yml` qui déploie
automatiquement sur le VPS OVH à chaque merge sur `main`. Couvre les items roadmap
Semaine 1 : "CI/CD GitHub Actions → déploiement automatique" et "Structure de base
du Caddyfile".

Branche : `chore/ci-cd-deploiement-vps` (depuis `develop`).

## Décisions d'architecture prises pour cette feature

- **Un seul conteneur "edge"** (`Dockerfile.edge` à la racine) fait à la fois
  reverse-proxy HTTPS (Caddy, cert Let's Encrypt automatique) et sert les fichiers
  statiques du frontend buildé — pas de conteneur nginx/caddy séparé pour le
  frontend. Évite un hop réseau inutile sur un VPS à ressources limitées (3,99€/mois).
- **Frontend appelle l'API en chemin relatif** (`VITE_API_URL=/api` baké au build),
  proxyfié par Caddy vers `backend:8080` en interne — même origine, donc pas besoin
  de CORS en prod pour le flux normal (on garde `Cors:AllowedOrigins` renseigné en
  défense en profondeur, pas comme mécanisme principal).
- **Migrations EF Core appliquées automatiquement au démarrage** du conteneur
  backend en environnement `Production` (`app.Database.Migrate()`), pas de job CI
  séparé. Acceptable pour une instance unique low-traffic ; à revoir si l'app
  scale un jour à plusieurs instances (migration concurrente = risque).
- **Postgres/Redis non exposés sur l'hôte** en prod (pas de `ports:` dans
  `docker-compose.prod.yml`), accessibles uniquement via le réseau Docker interne —
  contrairement au `docker-compose.yml` local qui expose 5432/6379 pour l'outillage
  dev.
- **`docs/decisions/architecture.md`** mentionne encore "Nginx" (ligne 211) alors
  que `README.md`/`roadmap.md` disent Caddy — corrigé dans ce plan (Phase 5).
- **Secrets prod** : GitHub *environment* `production` (pas repository secrets),
  restreint à la branche `main`, cf. échange précédent avec l'utilisateur. Liste
  finale (mise à jour avec `DOMAIN` en variable non-secrète, absent de la liste
  initiale donnée en conversation) :
  - Secrets : `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `JWT_SIGNING_KEY`, `POSTGRES_PASSWORD`
  - Variable (non-secrète) : `DOMAIN`

## Hypothèses non confirmées par l'utilisateur (à valider avant/pendant Phase 0)

- Chemin de déploiement sur le VPS : `/opt/watodoo` (assumé, pas confirmé).
- Utilisateur de déploiement : `deploy`, membre du groupe `docker` (pas de sudo
  nécessaire pour `docker compose`).
- Docker + le plugin Docker Compose sont présupposés installables via le script
  officiel Docker s'ils ne sont pas déjà présents (Phase 0 les installe si absents).

## Stratégie de tests pour cette feature

Feature d'infra/outillage, pas de nouvelle logique métier :
- **Pas de tests unitaire/intégration/fonctionnel/interface/QA e2e/architecture/
  mutation** — aucun code applicatif métier n'est ajouté ou modifié (seul
  `Program.cs` gagne un appel `Database.Migrate()` conditionnel, déjà couvert
  indirectement par les tests fonctionnels existants qui bootent l'app via
  `WebApplicationFactory`).
- **Vérification = smoke test réel** après déploiement effectif : `curl` sur
  `https://<domaine>/health` et `https://<domaine>/`. Docker a été activé dans cet
  environnement en cours de plan (indisponible au moment de l'écriture initiale,
  confirmé disponible depuis) — les `docker build` sont donc vérifiés
  autonomement ci-dessous. Seul le déploiement réel sur le VPS reste hors de
  portée de cet environnement (pas d'accès SSH au VPS).

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary**; run the phase's
**Verification Plan** and record the result before moving on.

## Phase 0: Prérequis infra (manuel, hors code)
Status: Complete

- [x] Docker + plugin Docker Compose installés sur le VPS (vérifier :
      `docker --version && docker compose version`, installer via
      `curl -fsSL https://get.docker.com | sh` si absent)
- [x] Utilisateur de déploiement `deploy` créé sur le VPS, ajouté au groupe
      `docker` (`sudo usermod -aG docker deploy`)
- [x] Paire de clés SSH dédiée générée et clé publique copiée sur le VPS (déjà
      guidé en conversation)
- [x] Répertoire `/opt/watodoo` créé sur le VPS, appartenant à `deploy`
      (`sudo mkdir -p /opt/watodoo && sudo chown deploy:deploy /opt/watodoo`)
- [x] GitHub environment `production` créé (Settings → Environments), restreint
      à la branche `main`
- [x] Les 5 secrets + 1 variable listés ci-dessus configurés dans cet environment
- [x] Enregistrement DNS `A` du domaine pointant vers l'IP du VPS, propagation
      vérifiée (`dig +short <domaine>`)
- [x] Ports 80/443 ouverts sur le firewall du VPS (443 nécessaire pour Let's
      Encrypt), port 22 restreint si possible

### Verification Plan
- Manuel — l'agent n'a pas d'accès SSH au VPS. L'utilisateur coche chaque item
  après l'avoir fait. Seul `dig +short <domaine>` peut être vérifié depuis cet
  environnement s'il a accès réseau sortant.

### Phase Summary
Checklist confirmée faite par l'utilisateur le 2026-08-03. Non re-vérifiée de
façon autonome par l'agent (pas d'accès SSH au VPS ni le nom de domaine réel
en contexte pour un `dig`). À confirmer réellement lors du premier run du job
`deploy` (voir Deployment Plan) : si un item de cette liste est en fait
incomplet, ce sera l'étape SSH ou le smoke test qui échouera en premier.

## Phase 1: Dockerisation backend
Status: Complete

- [x] `Watodoo.Api/Dockerfile` — multi-stage `mcr.microsoft.com/dotnet/sdk:10.0`
      (build/publish) → `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime). Le
      build context est la racine du repo (pas `Watodoo.Api/`) pour que
      `Directory.Build.props`/`Directory.Packages.props` (Central Package
      Management) soient trouvés par MSBuild — copier ces deux fichiers avant
      `Watodoo.Api/Watodoo.Api.csproj`, dans le même arbre relatif. `EXPOSE 8080`,
      `ENV ASPNETCORE_URLS=http://+:8080`.
- [x] `.dockerignore` à la racine : exclure `**/bin/`, `**/obj/`, `**/node_modules/`,
      `frontend/dist/`, `.git/`, `**/*.env`, `plans/`, `docs/`
- [x] `Watodoo.Api/Program.cs` : après `var app = builder.Build();`, ajouter
      migration auto en prod uniquement :
      ```csharp
      if (app.Environment.IsProduction())
      {
          using var scope = app.Services.CreateScope();
          scope.ServiceProvider.GetRequiredService<WatodooDbContext>().Database.Migrate();
      }
      ```
      Placer avant `app.Run()`, après le bloc `if (app.Environment.IsDevelopment())`.

### Verification Plan
- `dotnet build` → succès (0 erreur)
- `dotnet test` → tous les tests existants toujours verts (aucune régression
  introduite par l'ajout du `Database.Migrate()`)
- `docker build -f Watodoo.Api/Dockerfile -t watodoo-api .` → succès

### Phase Summary
`dotnet build` (0 erreur), `dotnet test` (28/28 verts, aucune régression liée à
l'ajout du `Database.Migrate()`), et `docker build -f Watodoo.Api/Dockerfile .`
tous vérifiés avec succès. Le multi-stage build préservant l'arbre relatif
(`Directory.Build.props`/`Directory.Packages.props` à la racine du contexte)
fonctionne comme prévu — restore et publish OK sans erreur de CPM.

## Phase 2: Dockerisation frontend + edge Caddy
Status: Complete

- [x] `Dockerfile.edge` à la racine — stage 1 `node:22-alpine` : `corepack enable`,
      `pnpm install --frozen-lockfile` puis `pnpm build` dans `frontend/` avec
      `VITE_API_URL=/api` (build arg, baké au build Vite car lu via
      `import.meta.env` — pas une variable runtime). Stage 2 `caddy:2-alpine` :
      `COPY --from=build /app/frontend/dist /srv`, `COPY Caddyfile /etc/caddy/Caddyfile`
- [x] `Caddyfile` à la racine :
      ```
      {$DOMAIN} {
          handle /api/* {
              uri strip_prefix /api
              reverse_proxy backend:8080
          }
          handle {
              root * /srv
              try_files {path} /index.html
              file_server
          }
      }
      ```
      (`strip_prefix /api` nécessaire : les routes backend n'ont pas de préfixe
      `/api`, ex. `/auth/login`, `/health` — confirmé dans
      `Watodoo.Api/Features/Auth/AuthEndpoints.cs`)
- [x] Vérifié : seul `frontend/src/features/auth/api.ts` fait des appels API,
      via `${API_URL}/auth/...` — aucune autre URL absolue hardcodée dans
      `frontend/src`

### Verification Plan
- `cd frontend && VITE_API_URL=/api pnpm build` → succès, `frontend/dist/`
  généré sans erreur
- `docker build -f Dockerfile.edge -t watodoo-edge --build-arg VITE_API_URL=/api .`
  → succès

### Phase Summary
Build frontend (`pnpm build`) et build Docker de l'image edge tous deux réussis.
Caddyfile validé syntaxiquement via `caddy validate` dans l'image construite.
Un détail non anticipé dans la décision d'architecture initiale : `uri
strip_prefix /api` est nécessaire côté Caddy car les endpoints backend n'ont pas
de préfixe `/api` (`/auth/login`, `/health`, ...) — sans ce strip, le proxy
aurait envoyé `/api/auth/login` tel quel au backend, qui n'a pas cette route.

## Phase 3: docker-compose.prod.yml + template d'environnement
Status: Complete

- [x] `docker-compose.prod.yml` à la racine : services `postgres` (image
      `postgres:18-alpine`, pas de `ports:`, `POSTGRES_PASSWORD` via `${POSTGRES_PASSWORD}`,
      volume nommé, healthcheck repris de `docker-compose.yml`), `redis` (idem,
      pas de `ports:`), `backend` (build `Watodoo.Api/Dockerfile`, `depends_on`
      postgres/redis avec `condition: service_healthy`, env `ConnectionStrings__Postgres`,
      `ConnectionStrings__Redis`, `Jwt__SigningKey`, `Jwt__Issuer=Watodoo`,
      `Jwt__Audience=Watodoo`, `Cors__AllowedOrigins__0=https://${DOMAIN}`,
      `ASPNETCORE_ENVIRONMENT=Production`), `edge` (build `Dockerfile.edge`,
      ports `80:80`/`443:443`, env `DOMAIN`, volumes `caddy_data:/data`,
      `caddy_config:/config`). Tous les services `restart: unless-stopped`.
      **Ajout suite à un bug trouvé en vérification** : `name: watodoo-prod` au
      niveau racine du fichier, et volumes Postgres/Redis renommés
      `postgres_data_prod`/`redis_data_prod` (au lieu de `postgres_data`/`redis_data`
      comme dans `docker-compose.yml`) — sans ça, le compose prod partage le même
      nom de projet Compose par défaut (nom du dossier) que le compose dev, donc
      les mêmes noms de volumes/conteneurs. Voir Phase Summary pour le détail.
- [x] `.env.prod.example` à la racine (template versionné, pas de vraies valeurs) :
      liste `POSTGRES_PASSWORD=`, `JWT_SIGNING_KEY=`, `DOMAIN=`

### Verification Plan
- `docker compose -f docker-compose.prod.yml config` avec un `.env` de test
  (valeurs bidon copiées depuis `.env.prod.example`) → pas d'erreur de parsing,
  toutes les variables résolues
- `docker compose -f docker-compose.prod.yml up -d` avec ce `.env` de test →
  les 4 services démarrent, `docker compose ps` montre `postgres`/`redis`
  healthy et `backend` running ; `curl http://localhost/health` via le conteneur
  `edge` (ou `docker compose exec backend curl localhost:8080/health` en
  interne, faute de domaine réel/DNS local pour déclencher le cert Let's
  Encrypt) → 200
- Nettoyage : `docker compose -f docker-compose.prod.yml down -v` après le test

### Phase Summary
`docker compose config` validé avec des valeurs de test, puis `up -d --build`
réel avec les 4 services. **Bug trouvé et corrigé pendant la vérification** :
la première tentative utilisait les mêmes noms de volumes (`postgres_data`,
`redis_data`) que `docker-compose.yml` (dev). Comme les deux fichiers vivent
dans le même dossier, Docker Compose leur donne le même nom de projet par
défaut (`watodoo`) et donc les mêmes noms de volumes/conteneurs. Le conteneur
Postgres prod s'est attaché au volume de données **dev existant** (créé le
2026-07-08, mot de passe `watodoo`), donc le backend n'arrivait pas à
s'authentifier avec `POSTGRES_PASSWORD` de test → crash loop (exit 139).
Aucune donnée dev perdue (pas de `down -v` avant la correction), mais le risque
de collision était réel. Corrigé en ajoutant `name: watodoo-prod` et en
renommant les volumes Postgres/Redis en `*_data_prod`.

Après correction, stack complète vérifiée de bout en bout :
`Applying migration '20260730124708_AddAuthIdentity'` au démarrage du backend
(migration auto confirmée fonctionnelle), `GET https://localhost/api/health`
→ `{"status":"ok"}` via le proxy Caddy, `GET https://localhost/` → HTML du
frontend statique, `GET http://localhost/` → redirection 308 vers HTTPS.
Stack et volumes de test nettoyés (`down -v` + suppression des images/volumes
de test), volumes dev d'origine (`watodoo_postgres_data`, `watodoo_redis_data`)
non touchés.

## Phase 4: Pipeline de déploiement continu
Status: Complete

- [x] Nouveau job `deploy` dans `.github/workflows/ci.yml` :
      `needs: [backend-test, frontend-test]`,
      `if: github.ref == 'refs/heads/main' && github.event_name == 'push'`,
      `environment: production`.
      **Écart par rapport à la description initiale** : au lieu de
      `git clone`/`git pull` exécuté depuis le VPS (qui suppose des identifiants
      git configurés sur le VPS pour un repo potentiellement privé — jamais
      vérifié), le code est synchronisé par `rsync` depuis le runner GitHub
      (déjà authentifié via `actions/checkout`) grâce à l'action
      `easingthemes/ssh-deploy@v5` — le VPS n'a besoin d'aucun accès à GitHub,
      seulement de la clé SSH de déploiement déjà en place. Étapes du job :
      (1) `actions/checkout@v4`, (2) `easingthemes/ssh-deploy@v5` : rsync
      `-az --delete` vers `/opt/watodoo/`, exclut `.git/`, `node_modules/`,
      `bin/`, `obj/`, `frontend/dist/`, `frontend/test-results/`, (3)
      `appleboy/ssh-action@v1` : écrit `/opt/watodoo/.env` depuis les secrets
      (`POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, `DOMAIN`) via heredoc,
      `chmod 600 .env`, puis `docker compose -f docker-compose.prod.yml up -d
      --build` et `docker image prune -f`
- [x] Étape finale du job : smoke test `curl -sf https://${{ vars.DOMAIN }}/api/health`
      (chemin `/api/health`, pas `/health` — c'est la route publique exposée par
      Caddy, cf. Phase 2) avec 10 tentatives espacées de 5s — le job échoue si
      aucune ne répond 200

### Verification Plan
- Validation syntaxique YAML du workflow
- Vérification manuelle qu'aucun secret n'apparaît en clair dans les logs
  (`appleboy/ssh-action` masque `key`/`password` par défaut si passés via
  `secrets.*`)
- Vérification réelle possible seulement au premier merge sur `main` (hors
  contrôle de cet environnement d'agent)

### Phase Summary
YAML validé (`yaml.safe_load`, aucune erreur de parsing, 3 jobs détectés :
`backend-test`, `frontend-test`, `deploy`). Le déclenchement réel du job (push
sur `main`) n'a pas pu être testé depuis cet environnement — aucun accès à
GitHub Actions ni au VPS. À vérifier au premier merge `develop` → `main`, voir
Deployment Plan.

## Phase 5: Documentation
Status: Complete

- [x] `docs/decisions/architecture.md` : remplacé "Nginx reverse proxy" par
      "Caddy reverse proxy + HTTPS automatique", documenté le conteneur edge
      unique (Caddy sert le frontend statique + proxy `/api` avec
      `strip_prefix`), l'absence d'exposition Postgres/Redis sur l'hôte en
      prod, et la décision de migration auto au démarrage
- [x] `docs/roadmap.md` : coché "CI/CD GitHub Actions → déploiement automatique
      sur le VPS" et "Structure de base du Caddyfile pour l'app réelle"

### Verification Plan
- `grep -n "Nginx" docs/decisions/architecture.md` → aucun résultat
- `grep -n "\[x\] CI/CD\|\[x\] Structure de base du Caddyfile" docs/roadmap.md`
  → les deux lignes trouvées cochées

### Phase Summary
Les deux `grep` de vérification confirment les changements : plus aucune
mention de Nginx, les deux items roadmap cochés.

## Final Recap

Les 5 phases sont complètes. Fichiers créés : `Watodoo.Api/Dockerfile`,
`Dockerfile.edge`, `Caddyfile`, `docker-compose.prod.yml`, `.env.prod.example`,
`.dockerignore`. Fichiers modifiés : `Watodoo.Api/Program.cs` (migration auto
en prod), `.github/workflows/ci.yml` (job `deploy`), `docs/decisions/architecture.md`,
`docs/roadmap.md`.

Toute la chaîne a été vérifiée réellement en local (Docker activé en cours de
route) : build des deux images, stack complète démarrée avec
`docker-compose.prod.yml`, migration EF Core appliquée automatiquement au
démarrage, `/api/health` et `/` répondant correctement via le proxy Caddy,
redirection HTTP→HTTPS fonctionnelle. Un bug réel de collision de volumes avec
l'environnement dev local a été trouvé et corrigé pendant cette vérification
(voir Phase 3 Summary) — sans lui, un déploiement prod sur une machine ayant
aussi le stack dev aurait pu se connecter aux mauvaises données.

Ce qui n'a **pas** pu être vérifié depuis cet environnement (pas d'accès au
VPS ni à GitHub Actions) : le déclenchement réel du job `deploy`, la
connexion SSH avec les vraies credentials, l'obtention d'un certificat Let's
Encrypt pour le vrai domaine, le comportement de `rsync` vers le vrai VPS.

## Deployment Plan

1. Vérifier que tous les items de la **Phase 0** (checklist manuelle) sont
   cochés par l'utilisateur : Docker/Compose installés sur le VPS, utilisateur
   `deploy` créé, `/opt/watodoo` créé, clé SSH copiée, environment GitHub
   `production` configuré avec les 5 secrets + 1 variable, DNS `A` en place
2. `git add` + commit sur la branche `chore/ci-cd-deploiement-vps` (créée
   depuis `develop`)
3. PR `chore/ci-cd-deploiement-vps` → `develop` — CI (`backend-test`,
   `frontend-test`) doit passer ; le job `deploy` ne se déclenche pas encore
   (branche ≠ `main`)
4. Une fois mergé sur `develop`, PR `develop` → `main` — c'est ce merge qui
   déclenche pour la première fois le job `deploy`
5. Suivre le run du job `deploy` dans l'onglet Actions de GitHub :
   - Étape `easingthemes/ssh-deploy` : le code doit apparaître dans
     `/opt/watodoo` sur le VPS
   - Étape `appleboy/ssh-action` : `docker compose up -d --build` doit
     terminer sans erreur, les 4 conteneurs doivent démarrer
   - Étape smoke test : doit obtenir un 200 sur `https://<domaine>/api/health`
6. Vérification manuelle post-déploiement : ouvrir `https://<domaine>/` dans
   un navigateur, vérifier le certificat HTTPS (émis par Let's Encrypt, pas
   l'autorité locale de test utilisée dans ce plan), tester un flux minimal
   (inscription/connexion) pour confirmer que le backend parle bien à Postgres
   en prod
7. Si tout est vert : cocher définitivement les deux items roadmap (déjà fait
   dans ce plan, à re-confirmer visuellement) et archiver ce plan dans
   `plans/done/`
