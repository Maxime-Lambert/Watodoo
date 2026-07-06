# /pr — Finaliser et créer la PR

1. Vérifier que tous les tests passent :
   ```
   dotnet test
   cd frontend && npm test
   ```
   Ne pas continuer si des tests échouent.

2. Lancer le code-reviewer sur le diff complet de la feature.
   Corriger tous les points 🔴 Bloquants et 🟠 Importants avant de continuer.

3. Générer un message de commit conventionnel :
   ```
   feat(scope): description courte
   
   - Ce qui a changé et pourquoi
   - Décisions notables
   ```

4. Créer la PR avec :
   - Titre : même format que le commit
   - Description : ce qui change, pourquoi, comment tester manuellement
   - Lien vers le plan file si pertinent

5. Marquer le plan comme complet :
   - Remplir le Final Recap dans `plans/active-plan.md`
   - Déplacer le fichier dans `plans/done/`
