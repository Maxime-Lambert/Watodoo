# Watodoo 🎬🎮📚

> En quelques clics, découvre quoi faire ce soir.

Watodoo est une application web qui t'aide à choisir ton prochain film,
série, jeu vidéo, anime, manga ou livre — sans te noyer dans les choix.

## Stack

- **Backend** : ASP.NET Core 10, C#, PostgreSQL, Redis, Hangfire
- **Frontend** : React 18, TypeScript, Vite, Tailwind CSS
- **Infra** : Docker, GitHub Actions, OVH VPS, Caddy, Cloudflare

## Développement local

```bash
docker compose up        # démarre PostgreSQL et Redis
cd src && dotnet run     # démarre l'API
cd frontend && pnpm dev  # démarre le frontend
```

## Tests

Voir `docs/testing-strategy.md` pour le détail des catégories de tests.

Une fois après le clone, activer le hook pre-push qui fait tourner la suite
rapide (unitaire + intégration + architecture backend, unitaire frontend)
avant chaque push :

```bash
git config core.hooksPath .githooks
```

Playwright (tests interface + QA e2e) a besoin de bibliothèques système pour
son navigateur headless. Une fois après le clone :

```bash
cd frontend && pnpm exec playwright install-deps chromium
```

Cette commande demande un mot de passe `sudo` (installation de paquets système)
— à lancer manuellement dans un vrai terminal, elle ne peut pas être exécutée
par un agent sans accès interactif. En CI (`.github/workflows/ci.yml`), c'est
déjà automatisé via `pnpm exec playwright install --with-deps chromium`, aucune
action requise.

## Licence

MIT — voir [LICENSE](LICENSE)
