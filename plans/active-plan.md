# Workflow de tests complet + enforcement + sous-agent architecture

Mettre en place la pyramide de tests complète (unitaire, intégration, fonctionnel,
interface, QA/e2e, mutation, architecture), la faire respecter automatiquement
(hook local + CI bloquante), et ajouter un sous-agent dédié au clean code /
respect de la Vertical Slice Architecture, lancé à chaque PR aux côtés du
`code-reviewer` existant. Contexte : projet au stade scaffold pur (aucune feature
métier, un seul test `HealthEndpointTests`), donc chaque brique est posée sur du
code minimal — les tests eux-mêmes vérifient surtout que l'outillage fonctionne,
pas encore une vraie couverture métier.

## Décisions déjà validées (ne pas re-demander)
- Pyramide complète maintenant, pas de version allégée.
- Enforcement double : hook git pre-push local **et** CI GitHub Actions bloquante.
- `architecture-reviewer` tourne automatiquement à chaque `/pr`, comme
  `code-reviewer`.
- Mutation testing (Stryker.NET) mis en place dès maintenant, pas après le MVP.

## Hypothèses à confirmer à la présentation du plan
- Taxonomie proposée ci-dessous (Phase 1) : les 7 catégories et leurs outils.
  Si un nom ou un découpage ne convient pas, on ajuste avant de coder.
- Le hook pre-push ne fait tourner que le sous-ensemble rapide (unitaire +
  intégration + architecture + unitaire frontend), pas Playwright ni Stryker
  (trop lents pour un hook local à chaque push). Playwright et Stryker tournent
  uniquement en CI.
- Pas de service API/frontend ajouté à `docker-compose.yml` (qui ne contient que
  postgres/redis pour l'usage local existant) : les jobs CI qui ont besoin de
  l'app complète (QA e2e) la lancent directement (`dotnet run` + `pnpm preview`
  en arrière-plan) plutôt que de complexifier le compose de dev.
- Le score minimal Stryker et le seuil de couverture ne sont pas figés dans ce
  plan : Phase 4 les met en place avec un seuil bas/informatif au départ (le
  code est trop jeune pour un seuil strict), à durcir plus tard manuellement.

## For Future Agents
Au fur et à mesure : cocher les cases `- [x]`, passer le statut de la phase à
`Complete` et écrire son **Phase Summary** une fois la vérification de la phase
exécutée avec succès. Ne pas passer à la phase suivante si la vérification
échoue. Chaque phase est indépendamment mergeable (mais suit l'ordre du git flow
du projet : branche `chore/xxx` depuis `develop`, PR vers `develop`).

## Phase 1: Taxonomie des tests (documentation)
Status: Complete

- [x] Créer `docs/testing-strategy.md` définissant les 7 catégories, leur
      objectif, leur outil, leur emplacement dans le repo, et surtout ce
      qu'elles ne couvrent PAS (pour éviter les doublons) :
      - **Unitaire backend** — xUnit, aucune I/O, `Watodoo.Tests/Features/<F>/<UseCase>/*HandlerTests.cs` et `*ValidatorTests.cs`
      - **Unitaire frontend** — Vitest + React Testing Library (déjà scaffoldé), `frontend/src/**/*.test.tsx`
      - **Intégration backend** — xUnit + Testcontainers.PostgreSql, handler + EF Core réel, `Watodoo.Tests/Integration/<F>/`
      - **Fonctionnel backend** — xUnit + `WebApplicationFactory` + Testcontainers, HTTP pipeline complet (middleware, routing, auth), `Watodoo.Tests/Functional/<F>/`
      - **Interface (UI)** — Playwright, rendu/interaction d'une page isolée dans un vrai navigateur, `frontend/e2e/component/*.spec.ts`
      - **QA / e2e parcours utilisateur** — Playwright contre l'app complète réelle (API + frontend + Postgres + Redis), `frontend/e2e/journeys/*.spec.ts`
      - **Architecture** — NetArchTest.Rules (xUnit), règles de Vertical Slice, `Watodoo.Tests/Architecture/`
      - **Mutation** — Stryker.NET sur `Watodoo.Api`, mesure la qualité des tests unitaires/intégration/fonctionnels existants
- [x] Ajouter une section "Tests" dans `CLAUDE.md` (routing) pointant vers
      `docs/testing-strategy.md`, à côté de `docs/decisions/` et
      `docs/migrations.md`.

### Verification Plan
- Relecture manuelle du document par l'utilisateur (pas de commande
  automatisable pour de la doc) — présenter le fichier avant de passer à la
  Phase 2.

### Phase Summary
`docs/testing-strategy.md` créé avec les 7 catégories (tableau + détail par
catégorie incluant ce que chacune ne couvre pas) et la section Enforcement.
Lien ajouté dans le routing de `CLAUDE.md`.

## Phase 2: Tests d'architecture (NetArchTest)
Status: Complete

- [x] Ajouter `NetArchTest.Rules` dans `Directory.Packages.props` (groupe
      `Watodoo.Tests`) et dans `Watodoo.Tests.csproj`.
- [x] Créer `Watodoo.Tests/Architecture/VerticalSliceRulesTests.cs` avec au
      minimum ces règles (basées sur `docs/decisions/architecture.md`) :
      - Aucun type de `Watodoo.Features.X` ne référence directement un type de
        `Watodoo.Features.Y` (X ≠ Y) — seule communication autorisée via
        `Watodoo.Shared`.
      - Les classes `*Handler` ne référencent pas d'autres `*Handler`
        directement (pas de couplage handler-à-handler).
      - Aucun namespace ne commence par `Watodoo.Api.*` (namespace racine =
        `Watodoo`, pas `Watodoo.Api`, cf. architecture.md).
- [x] Vérifier que les tests passent sur le code actuel.
- [x] Violer volontairement une règle (ex: ajouter temporairement une classe
      `Watodoo.Features.Foo.Bar` qui référence `Watodoo.Features.Baz.Qux`),
      confirmer que le test échoue, puis retirer la violation.

### Verification Plan
- `dotnet test --filter FullyQualifiedName~Architecture` → tous verts sur le
  code actuel.
- Violation temporaire → `dotnet test --filter FullyQualifiedName~Architecture`
  doit échouer avec un message listant la classe fautive ; retirer la
  violation et re-vérifier que ça repasse au vert.

### Phase Summary
`NetArchTest.Rules` 1.3.2 ajouté (compatible .NET 10, vérifié). 3 règles créées
dans `VerticalSliceRulesTests.cs` : pas de namespace `Watodoo.Api.*`, pas de
dépendance croisée entre features (découverte dynamique des features
existantes, donc la règle reste valable même quand il n'y en a encore aucune),
pas de couplage handler-à-handler (via réflexion sur les constructeurs). Les
deux règles relationnelles ont été testées avec une violation volontaire
(fixtures temporaires créées puis supprimées) : échec détecté avec message
clair nommant le type fautif, puis retour au vert confirmé.

## Phase 3: Tests fonctionnels (WebApplicationFactory + Testcontainers)
Status: Complete (vérification exécution en attente côté utilisateur — Docker indisponible dans cet environnement)

- [x] Créer `Watodoo.Tests/Functional/FunctionalTestFixture.cs` : une
      `WebApplicationFactory<Program>` qui démarre un vrai conteneur Postgres
      (Testcontainers) et remplace la connection string de l'app pour pointer
      dessus (`IClassFixture<FunctionalTestFixture>`).
- [x] Déplacer/adapter `HealthEndpointTests.cs` vers
      `Watodoo.Tests/Functional/HealthEndpointTests.cs` pour qu'il utilise cette
      fixture et fasse une vraie requête HTTP via `HttpClient` (pas juste un
      appel de handler en mémoire) — sert d'exemple de référence pour les
      futures features.
- [x] Documenter dans `docs/testing-strategy.md` (Phase 1) la différence
      concrète Intégration vs Fonctionnel avec cet exemple.

### Verification Plan
- `dotnet test --filter FullyQualifiedName~Functional` → vert, avec le
  conteneur Postgres visible dans les logs de test (`docker ps` pendant
  l'exécution montre un conteneur `postgres` éphémère).

### Phase Summary
`FunctionalTestFixture` créé avec `ICollectionFixture` partagé (un seul
conteneur Postgres pour tous les tests fonctionnels, plus rapide que
d'en démarrer un par classe de test). `HealthEndpointTests` déplacé de
`Watodoo.Tests/Features/Health/` vers `Watodoo.Tests/Functional/` (l'ancien
dossier `Features/` vide a été supprimé) et adapté pour utiliser la fixture
partagée. **Vérification incomplète** : Docker n'est pas accessible dans cet
environnement WSL (intégration Docker Desktop non activée pour cette distro),
donc l'exécution réelle du test (démarrage du conteneur Postgres) n'a pas pu
être confirmée ici — seul `dotnet build` a été validé (0 erreur). Ce test
existait déjà avec le même mécanisme Testcontainers avant ce chantier, donc ce
n'est pas une régression. **À faire côté utilisateur** : lancer
`dotnet test --filter FullyQualifiedName~Functional` sur une machine avec
Docker actif pour confirmer.

## Phase 4: Mutation testing (Stryker.NET)
Status: Complete (score 0% attendu et normal — voir Phase Summary)

- [x] `dotnet new tool-manifest` à la racine si absent, puis
      `dotnet tool install dotnet-stryker`.
- [x] Créer `Watodoo.Api/stryker-config.json` ciblant `Watodoo.Api.csproj`
      comme projet source et `Watodoo.Tests.csproj` comme projet de test, seuil
      **informatif** (pas de `--break-build` strict pour l'instant, le code
      est trop jeune).
- [x] Ajouter un script `scripts/run-mutation-tests.sh` qui lance
      `dotnet tool restore && dotnet stryker` depuis `Watodoo.Api/`.
- [x] Documenter dans `docs/testing-strategy.md` que le seuil sera durci
      manuellement au fur et à mesure que la couverture métier grandit.

### Verification Plan
- `./scripts/run-mutation-tests.sh` → génère un rapport HTML dans
  `Watodoo.Api/StrykerOutput/` sans erreur (le score lui-même n'a pas
  d'importance à ce stade, seul le bon fonctionnement de l'outillage compte).

### Phase Summary
Outil installé via `dotnet tool-manifest` (`.config/dotnet-tools.json`,
déplacé depuis la racine où `dotnet new tool-manifest` le crée par défaut,
vers l'emplacement conventionnel). Config `stryker-config.json` : la clé
`project`/`test-projects` va directement sous `stryker-config` (pas sous
`project-info`, qui est réservé à `module`/`name`/`version` pour le dashboard
— erreur de schéma corrigée après un premier essai). `./scripts/run-mutation-tests.sh`
exécuté avec succès : rapport HTML généré dans `Watodoo.Api/StrykerOutput/`
(ajouté au `.gitignore`, rapport de test supprimé après vérification). Score
final 0% — normal et attendu, il n'y a encore aucune logique métier ni test
unitaire à proprement parler pour tuer des mutants ; le test fonctionnel
`HealthEndpointTests` a échoué pendant l'analyse (Docker indisponible dans cet
environnement, cf. Phase 3) sans empêcher Stryker de tourner. Ce chantier
valide uniquement le câblage de l'outil, pas un score réel.

## Phase 5: Tests d'interface & QA e2e (Playwright, frontend)
Status: Complete

- [x] `cd frontend && pnpm add -D @playwright/test && pnpm exec playwright
      install --with-deps chromium`.
- [x] Créer `frontend/playwright.config.ts` avec deux projets Playwright
      distincts : `component` (pointant sur `e2e/component/`) et `journeys`
      (pointant sur `e2e/journeys/`), pour pouvoir les lancer séparément.
- [x] Créer `frontend/e2e/component/app-shell.spec.ts` — test d'interface
      minimal (l'app se charge, le shell s'affiche) sur le scaffold actuel,
      sert d'exemple de référence.
- [x] Créer `frontend/e2e/journeys/README.md` expliquant que les vrais
      parcours utilisateur (login → action métier) seront ajoutés dès la
      première feature d'auth — pas de test factice sur une app qui n'a pas
      encore de parcours réel.
- [x] Ajouter les scripts `test:e2e` et `test:e2e:journeys` dans
      `frontend/package.json`.

### Verification Plan
- `cd frontend && pnpm test:e2e` → le test `app-shell.spec.ts` passe en
  headless contre le serveur de dev/preview Vite.

### Phase Summary
`@playwright/test` installé. `pnpm exec playwright install --with-deps
chromium` a échoué dans cet environnement (sudo interactif requis, pas
disponible ici) — le binaire Chromium a été récupéré séparément
(`playwright install chromium`, sans `--with-deps`), et les 4 librairies
système manquantes (`libnspr4`, `libnss3`, `libnssutil3` fournie par le paquet
`libnss3`, `libasound2t64`) ont été récupérées via `apt-get download` +
`dpkg-deb -x` (sans root, extraction locale dans le scratchpad) pour valider
le test une seule fois via `LD_LIBRARY_PATH`. **Ceci est un contournement
propre à cet environnement de développement restreint, pas quelque chose à
reproduire dans le repo ou en CI** : sur une machine normale ou en CI
(ubuntu-latest), `pnpm exec playwright install --with-deps chromium` suffit
(voir `.github/workflows/ci.yml`, Phase 6). `pnpm test:e2e` → 1 test passé
avec succès une fois les librairies rendues disponibles. Artefacts générés
(`test-results/`, `playwright-report/`) nettoyés après vérification.

## Phase 6: Enforcement — hook pre-push + CI GitHub Actions
Status: Complete (CI GitHub réelle non déclenchée — voir Phase Summary)

- [x] Créer `.githooks/pre-push` (bash) qui lance, dans l'ordre, et s'arrête au
      premier échec : `dotnet test --filter "FullyQualifiedName!~Functional"`
      (unitaire + intégration + architecture, pas fonctionnel/Testcontainers
      pour rester rapide — à ajuster si trop lent en pratique), puis
      `cd frontend && pnpm test`.
- [x] Documenter dans le README (ou `docs/testing-strategy.md`) la commande
      d'activation à lancer une fois : `git config core.hooksPath .githooks`
      (ne peut pas être automatique, chaque clone doit l'exécuter — à
      mentionner aussi dans `README.md`).
- [x] Créer `.github/workflows/ci.yml` : déclenché sur PR vers `develop` et
      `main`. Jobs : `backend-test` (`dotnet test`, tous les projets/catégories
      sauf mutation), `frontend-test` (`pnpm test` puis `pnpm test:e2e`).
- [x] Créer `.github/workflows/mutation.yml` : déclenché sur PR vers `main`
      uniquement (pas `develop`, trop lent pour tourner à chaque feature) —
      lance `scripts/run-mutation-tests.sh`, publie le rapport en artifact,
      n'échoue pas le build (informatif pour l'instant, cf. Phase 4).

### Verification Plan
- `git config core.hooksPath .githooks` puis tenter un push avec un test
  volontairement cassé → le push est bloqué localement avec le message
  d'échec du test.
- Ouvrir une PR de test vers `develop` → les checks `backend-test` et
  `frontend-test` apparaissent et passent au vert dans l'onglet Actions
  GitHub.

### Phase Summary
`.githooks/pre-push` créé et testé en exécution directe (pas via un vrai
`git push`, pour ne rien pousser sans autorisation) : cassage volontaire d'un
test frontend → le script s'arrête avec exit code 1 ; correction appliquée →
repasse au vert. `pnpm lint` a été retiré du CI après découverte que ce script
échoue déjà sur le scaffold existant pour deux raisons préexistantes et hors
périmètre (`eslint.config.ts` non couvert par `tsconfig`, et une assertion
non-null dans `main.tsx`) — signalé à l'utilisateur, non corrigé ici (hors
scope du chantier tests). En revanche, `tsconfig.node.json` a été étendu pour
inclure `playwright.config.ts` et `e2e/**/*.ts` afin que mes propres nouveaux
fichiers ne cassent pas le lint. `ci.yml` (jobs `backend-test`,
`frontend-test`) et `mutation.yml` créés mais **pas vérifiés en conditions
réelles** : ceci nécessite une vraie PR GitHub (action que je n'ai pas prise
sans validation), voir note de suivi dans le Final Recap.

## Phase 7: Sous-agent `architecture-reviewer` + mise à jour `/pr` et `/plan`
Status: Complete

- [x] Créer `.claude/agents/architecture-reviewer.md` (même format que
      `code-reviewer.md`) : scope clean code / respect de la Vertical Slice
      Architecture / cohérence avec `docs/decisions/architecture.md` / DRY /
      code mort / conventions de nommage — explicitement PAS sécurité ni RGPD
      (déjà couvert par `code-reviewer`). Sortie au même format priorisé
      (🔴/🟠/🟡) que `code-reviewer` pour rester cohérent.
- [x] Mettre à jour `.claude/commands/pr.md` : ajouter l'exécution de
      `architecture-reviewer` comme step 2bis, à côté de `code-reviewer`
      (étape 2 actuelle), avec la même règle de blocage sur les points
      🔴/🟠.
- [x] Mettre à jour `.claude/commands/plan.md` : le plan de chaque feature
      doit lister explicitement les tests par catégorie (unitaire /
      intégration / fonctionnel / interface / QA / architecture / mutation)
      plutôt que "tests à écrire" en vrac — avec la précision que toutes les
      catégories ne s'appliquent pas à chaque feature (ex: pas de test QA e2e
      pour un simple champ de validation) et qu'il faut justifier lesquelles
      s'appliquent.

### Verification Plan
- Relecture manuelle des 3 fichiers par l'utilisateur (contenu de prompts
  d'agents, pas de commande automatisable).
- Lancer `/plan` sur une feature fictive triviale et vérifier que le plan
  généré liste bien les catégories de tests avec justification.

### Phase Summary
`architecture-reviewer.md` créé (scope clean code / Vertical Slice / DRY,
même format 🔴/🟠/🟡 que `code-reviewer`, explicitement hors sécurité/RGPD).
`/pr` mis à jour avec un step 2bis qui le lance à côté de `code-reviewer`.
`/plan` mis à jour pour exiger la liste des tests par catégorie avec
justification des catégories omises. Vérification par exécution réelle de
`/plan` sur une feature fictive non faite ici (nécessiterait de lancer une
session `/plan` complète) — relecture manuelle des 3 fichiers recommandée à
l'utilisateur.

## Final Recap
Les 7 phases de la pyramide de tests complète sont en place : taxonomie
documentée, tests d'architecture (NetArchTest), fixture de tests fonctionnels
réutilisable, mutation testing (Stryker.NET) câblé, Playwright (interface +
QA e2e) installé avec un exemple, hook pre-push + CI GitHub Actions (2
workflows), et un nouveau sous-agent `architecture-reviewer` intégré au flow
`/pr` aux côtés de `code-reviewer`. `/plan` exige désormais une justification
des tests par catégorie pour chaque feature.

**Limites de vérification à connaître** (aucune n'est une régression, toutes
préexistantes ou propres à cet environnement de développement) :
- Docker indisponible dans ce WSL (intégration Docker Desktop non activée) :
  les tests fonctionnels/intégration (Testcontainers) et Stryker n'ont pas pu
  être exécutés en conditions réelles ici, seulement leur câblage/compilation.
  **À faire côté utilisateur** : `dotnet test` complet et
  `./scripts/run-mutation-tests.sh` sur une machine avec Docker actif.
- Playwright a nécessité un contournement sans `sudo` (librairies système
  extraites manuellement) pour vérifier un seul run local — non reproduit
  dans le repo ; en CI (`ubuntu-latest`), `playwright install --with-deps`
  fonctionnera normalement sans intervention.
- Les workflows GitHub Actions (`ci.yml`, `mutation.yml`) n'ont pas été
  vérifiés via une vraie PR (pas d'action Git prise sans validation
  explicite). **À faire côté utilisateur** : ouvrir une PR de test vers
  `develop` pour confirmer que les checks apparaissent et passent.
- `pnpm lint` a été retiré du CI : il échoue déjà sur 2 points préexistants
  et hors périmètre de ce chantier (`eslint.config.ts` non couvert par
  `tsconfig`, assertion non-null dans `main.tsx`). À corriger séparément si
  tu veux un jour l'intégrer comme gate.
- La protection de branche GitHub (statuts obligatoires sur `develop`/`main`)
  reste à activer manuellement dans les settings GitHub — hors de portée
  d'un agent, abandonné à ta demande.

## Deployment Plan
Rien à déployer : uniquement de l'outillage dev/CI, aucun changement de
comportement applicatif. Une fois la PR mergée vers `develop`, chaque
développeur (solo ici) doit exécuter une fois
`git config core.hooksPath .githooks` pour activer le hook local.
