# Décisions de design — Watodoo

Charte graphique et décisions produit transverses, posées avant toute
implémentation visuelle. Complète `docs/decisions/architecture.md` (qui fixe
déjà Tailwind sans shadcn/ui) sans le contredire.

## Identité visuelle

### Ambiance : sombre "cinéma/streaming", thème clair en option
**Décision** : thème sombre par défaut, toggle vers un thème clair disponible
partout (persisté côté client, cf. `docs/decisions/architecture.md` qui prévoit
déjà le "thème" comme état Zustand local).

**Raison** : cohérent avec l'usage principal ("soirée canapé, quoi regarder/jouer
ce soir"), mais un thème clair reste attendu par une partie des utilisateurs et
coûte peu une fois les tokens posés en variables.

### Couleurs
**Décision** : tokens sémantiques définis en CSS (Tailwind v4, `@theme` dans
`frontend/src/index.css`), jamais de couleur Tailwind brute (`slate-300`,
`amber-500`...) directement dans les composants.

**Valeurs de départ** (à affiner visuellement une fois les premiers écrans
stylés — ce sont des points de départ, pas des valeurs gravées) :

| Token | Sombre (défaut) | Clair |
|---|---|---|
| `--color-background` | `#0F0F12` | `#FAFAF9` |
| `--color-surface` | `#1A1A1F` | `#FFFFFF` |
| `--color-foreground` | `#F2F1ED` | `#18181B` |
| `--color-muted` | `#8A8A93` | `#6B6B70` |
| `--color-border` | `#2A2A31` | `#E4E4E7` |
| `--color-accent` | `#E8A33D` | `#B87A1F` |
| `--color-accent-foreground` | `#1A1206` | `#FFFFFF` |

**Raison de l'ambre/or chaud** : évite le rouge "cinéma" trop associé à
Netflix/Letterboxd, reste chaleureux et lisible sur fond sombre, se décline
naturellement en variante plus foncée pour le thème clair (contraste AA).

### Typographie
**Décision** : Inter pour le texte courant et l'UI, une police d'accent
(Space Grotesk) pour les titres et le logo/wordmark.

**Raison** : Inter est conçu pour les interfaces (haute lisibilité à petite
taille) ; Space Grotesk donne une identité plus marquée aux titres sans
sacrifier la lisibilité du contenu.

**Implémentation** : self-host via `@fontsource/inter` et
`@fontsource/space-grotesk` (npm), pas de lien Google Fonts CDN — évite une
requête tierce non nécessaire et le partage d'IP avec Google (cohérent avec
l'approche RGPD-friendly, cf. section Légal).

### Formes
**Décision** : coins arrondis doux — `--radius-sm: 0.375rem` (inputs, badges),
`--radius-md: 0.625rem` (boutons), `--radius-lg: 0.875rem` (cards, modals).

### Icônes
**Décision** : `lucide-react`.

**Raison** : open-source, catalogue large, tree-shakable, standard de facto
dans l'écosystème Tailwind/React actuel — pas de dépendance à une charte
d'icônes propriétaire.

### Composants
**Décision** confirmée (déjà actée dans `architecture.md`) : Tailwind pur +
tokens custom, pas de shadcn/ui ni Radix comme base.

**Conséquence à assumer** : l'accessibilité des composants complexes (modals,
selects, dropdowns) — focus trap, `aria-*`, navigation clavier — doit être
écrite à la main. Ajouter Radix primitives ultérieurement si un composant
complexe le justifie reste possible sans tout casser (mêmes tokens CSS).

---

## Ton éditorial

### Tutoiement partout
**Décision** : tutoiement dans toute l'UI, la doc utilisateur et les
communications (déjà le ton du README).

---

## Responsive

### Mobile-first
**Décision** : chaque écran est conçu d'abord pour mobile, puis adapté au
desktop (`sm:`/`md:`/`lg:` en ajout, jamais en retrait).

**Raison** : l'usage principal ("un clic, quoi faire ce soir") correspond à un
réflexe téléphone en main plus qu'à une session de recherche posée au bureau.

---

## Produit

### Modèle économique : gratuit
**Décision** : pas de plan payant au lancement, aucun mur payant dans le MVP.

**Raison** : simplifie l'auth, le design (pas de bannière upsell à intégrer
dans la charte) et le périmètre technique (pas de Stripe/paiement à
sécuriser). Peut être reconsidéré plus tard si traction, sans que ce choix
bloque quoi que ce soit d'irréversible aujourd'hui.

### Nom et logo
**Décision** : "Watodoo" est le nom définitif. Pas de logo illustré au
lancement — un wordmark typographique (Space Grotesk, couleur accent) suffit
pour le MVP.

---

## Légal

### Mentions légales + CGU minimales avant toute mise en ligne publique
**Décision** : sorties du planning "Semaine 7" — à livrer en parallèle des
prochaines features, avant toute ouverture à de vrais utilisateurs (même en
beta).

**Raison** : dès qu'il y a un compte utilisateur, l'absence de mentions
légales/CGU est un manquement légal en France, pas un simple "polish". Pas
besoin d'un juriste pour un MVP solo — deux pages statiques minimales
suffisent pour démarrer, à faire réviser plus tard si le projet grossit.

**Reste en Semaine 7** : bannière cookies (pas nécessaire tant qu'aucun
cookie non-essentiel n'est posé — le refresh token JWT en cookie HttpOnly est
strictement nécessaire, donc exempté) et suppression de compte.
