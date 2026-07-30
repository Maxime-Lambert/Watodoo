# Parcours utilisateur (QA e2e)

Ce dossier contiendra les tests de parcours utilisateur complets (Playwright,
projet `journeys` dans `playwright.config.ts`), exécutés contre l'application
réelle (API + PostgreSQL + Redis + frontend), pas contre des mocks.

`auth.spec.ts` couvre le premier parcours critique : inscription → état
connecté visible → déconnexion → reconnexion. Voir `docs/testing-strategy.md`
pour la définition de cette catégorie de test.

Pour lancer ces tests contre une app déjà démarrée (ex: `docker compose up`) :

```bash
E2E_BASE_URL=http://localhost:5173 pnpm test:e2e:journeys
```
