# Parcours utilisateur (QA e2e)

Ce dossier contiendra les tests de parcours utilisateur complets (Playwright,
projet `journeys` dans `playwright.config.ts`), exécutés contre l'application
réelle (API + PostgreSQL + Redis + frontend), pas contre des mocks.

Aucun test ici pour l'instant : il n'existe encore aucun parcours utilisateur
réel (pas de feature d'authentification, pas d'action métier). Le premier
test de ce dossier viendra avec la première feature qui a un vrai parcours à
protéger (ex: inscription → connexion → première action métier) — voir
`docs/testing-strategy.md` pour la définition de cette catégorie.

Pour lancer ces tests contre une app déjà démarrée (ex: `docker compose up`) :

```bash
E2E_BASE_URL=http://localhost:5173 pnpm test:e2e:journeys
```
