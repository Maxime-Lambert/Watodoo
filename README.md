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

## Licence

MIT — voir [LICENSE](LICENSE)
