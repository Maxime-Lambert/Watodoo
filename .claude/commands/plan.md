# /plan — Démarrer une feature

Avant d'écrire la moindre ligne de code :

1. Explorer le code existant pertinent pour la feature demandée
   (fichiers liés, interfaces à respecter, patterns déjà en place)

2. Poser des questions si des ambiguïtés bloquantes existent —
   maximum 2 tours. Si une incertitude reste, la documenter comme
   hypothèse dans le plan et continuer.

3. Écrire le plan dans `plans/active-plan.md` en utilisant le skill real-work :
   - Objectif de la feature en 1-2 phrases
   - Fichiers à créer / modifier avec chemins exacts
   - Ordre d'implémentation (phases)
   - Tests à écrire pour chaque phase, listés par catégorie (voir
     `docs/testing-strategy.md`) : unitaire / intégration / fonctionnel /
     interface / QA e2e / architecture / mutation. Toutes les catégories ne
     s'appliquent pas à chaque feature — justifier explicitement lesquelles
     s'appliquent et pourquoi les autres sont omises (ex : "pas de test QA
     e2e ici, pas de nouveau parcours utilisateur critique").
   - Edge cases à couvrir
   - Décisions d'architecture spécifiques à cette feature

4. Présenter le plan et attendre la validation explicite avant d'implémenter.
