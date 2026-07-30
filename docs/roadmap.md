# Roadmap Watodoo

Source de vérité de l'avancement produit, découpée en semaines indicatives.
À cocher à chaque PR mergée (pas besoin d'attendre la fin d'une semaine pour avancer sur la suivante).

## Semaine 1 — Fondations
- [x] Initialisation projet .NET 10 + React (Claude Code)
- [x] Docker Compose local fonctionnel (PostgreSQL + Redis)
- [ ] Authentification complète (inscription, connexion, JWT + refresh token rotatif)
- [ ] CI/CD GitHub Actions → déploiement automatique sur le VPS (CI de tests en place ; déploiement auto à faire)
- [ ] Structure de base du Caddyfile pour l'app réelle
- [ ] Vérification email à l'inscription (dépend du choix d'un provider d'envoi d'email — reset password et autres emails transactionnels à regrouper avec ce choix)
- [ ] Job Hangfire de nettoyage périodique des refresh tokens expirés/révoqués (suivi de la review sécurité de l'auth — pas bloquant, juste de l'accumulation en base)
- [ ] Révocation en cascade des refresh tokens d'un utilisateur en cas de détection de réutilisation d'un token révoqué (durcissement anti-vol, écarté du MVP auth par décision produit)
- [ ] Partitionner le rate limiter `/auth` par IP (actuellement un compteur global — un seul client en rafale peut bloquer tout le monde, DoS facile à déclencher, à corriger avant prod)
- [ ] Lockout de compte après échecs de connexion répétés (`UserManager.CheckPasswordAsync` ne l'active pas ; nécessite `SignInManager`/`CheckPasswordSignInAsync`)

## Semaine 2 — Ingestion des données
- [ ] Scripts d'ingestion TMDB (films + séries)
- [ ] Scripts d'ingestion IGDB (jeux vidéo)
- [ ] Scripts d'ingestion Jikan (anime)
- [ ] Scripts d'ingestion MangaDex (manga)
- [ ] Scripts d'ingestion Google Books (livres)
- [ ] Jobs Hangfire pour les mises à jour nocturnes

## Semaine 3 — Moteur de suggestion
- [ ] Schéma de données final par catégorie
- [ ] API de filtres par catégorie (genres, dates, notes...)
- [ ] Algorithme de suggestion "un clic une réponse"
- [ ] Logique "surprise" / "proche de mes goûts" / "déjà fait"
- [ ] Cache Redis des résultats calculés

## Semaine 4 — Interface "quoi faire ce soir"
- [ ] Page principale avec sélecteur de catégorie
- [ ] Dropdowns de filtres adaptatifs par catégorie
- [ ] Le fameux bouton "un clic" avec animation
- [ ] Fiche détail d'une œuvre
- [ ] Page d'accueil publique

## Semaine 5 — Compte utilisateur + bibliothèque
- [ ] Page profil
- [ ] Ajout d'une œuvre à la bibliothèque
- [ ] Statuts (à faire / en cours / terminé)
- [ ] Système de notation (1-10)

## Semaine 6 — Personnalisation
- [ ] Filtrage des œuvres déjà consommées dans les suggestions
- [ ] Algorithme de proximité basé sur l'historique
- [ ] Agent IA de similarité sémantique (API Anthropic)
- [ ] Reviews courtes (500 caractères)

## Semaine 7 — Polish
- [ ] Page découverte (trending, nouveautés par catégorie)
- [ ] SEO (react-helmet-async, meta tags, sitemap)
- [ ] RGPD (mentions légales, cookies, suppression de compte)
- [ ] i18n (français / anglais)

## Semaine 8 — Stabilisation et déploiement
- [ ] Tests d'intégration sur les endpoints critiques
- [ ] Tests E2E sur les parcours principaux (Playwright)
- [ ] Monitoring (UptimeRobot déjà en place)
- [ ] Backup PostgreSQL sur OVH Object Storage
- [ ] Déploiement prod final et smoke tests
- [ ] Landing page de lancement
