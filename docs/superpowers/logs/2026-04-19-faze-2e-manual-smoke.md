# Fáze 2E manual smoke checklist

**Scope:** Runtime verifikace gov-dialog migrace na Citrix (nebo dev prostředí
s přístupem k SQLSERVER01). Spustit po každém deployi Fáze 2E.

## Předpoklady
- Dev server běží na Citrix (nebo local s reachable SQLSERVER01)
- Uživatel má role, která otvírá modaly (admin / projektový manažer)

## Universal checks pro KAŽDÝ modal

Pro každý z 18 views (níže) ověřit:

- [ ] **Otevření:** kliknutí na trigger (`[data-modal-url]` nebo `[data-record-editor-url]`) zobrazí modal
- [ ] **Markup:** DevTools → v `<div id="modal-root">` vidíme `<gov-dialog open block-close="true" block-backdrop-close="true" data-modal-container ...>`
- [ ] **Backdrop:** kliknutí mimo modal otevře náš app-level close guard (NE native gov-dialog close)
- [ ] **Esc klávesa:** pokud form je dirty → app-level confirm dialog; pokud čistý → modal se zavře
- [ ] **X close button:** klik na `[data-modal-close]` (vlevo vedle headeru nebo uvnitř obsahu — gov-dialog default slot) → zavřete modal (po dirty checku)
- [ ] **Focus trap:** Tab cykluje uvnitř modalu (ne vnější okno)
- [ ] **Floating pickery (kde existují):** person-picker / datetime-picker autocomplete dropdown se zobrazí NAD gov-dialog backdrop (z-index OK)

## Seznam 18 views

- [ ] `Osoby/AdPersonModal` — otevření z search triggeru, submit
- [ ] `Osoby/ManualPersonModal` — otevření, form submit
- [ ] `Projekty/AddTeamMemberModal` — person picker funguje + submit
- [ ] `Projekty/AssignProjectRoleModal` — otevření + submit
- [ ] `Projekty/AssignProjectSubsystemModal` — otevření + submit
- [ ] `Projekty/AssignProjectSubsystemRoleModal` — otevření + submit
- [ ] `Projekty/AssignMeetingIdentifierModal` — otevření + submit
- [ ] `Projekty/DeleteProjectModal` — otevření + confirm delete
- [ ] `Projekty/DeleteRecordModal` — **Destructive submit button je ČERVENÝ** (pm-button variant Destructive) + confirm delete
- [ ] `Projekty/EditZaznamModal` — **NEJKOMPLEXNĚJŠÍ**: všechny 3 taby fungují (basic / external / collaboration), schedule planner kreslí bary, Quill editor plní šířku, person pickery floatují NAD modal, dirty-check při X zavření
- [ ] `Projekty/NewMeetingModal` — otevření + submit
- [ ] `Projekty/ProjectModal` — otevření + submit (new/edit project)
- [ ] `Nastaveni/PermissionModal` — otevření + submit
- [ ] `Nastaveni/RoleModal` — otevření + submit
- [ ] `Nastaveni/RolePermissionModal` — otevření + submit
- [ ] `Nastaveni/UserRolesModal` — otevření + submit
- [ ] `Jednani/AddMeetingParticipantModal` — otevření + submit
- [ ] `Ciselniky/EditRow` — otevření + submit

## Varianty pro ověření

- [ ] **default** (šířka ~52rem): typicky DeleteProject, RoleModal, etc.
- [ ] **wide** (`ModalVariant="wide"`): AssignProjectRoleModal, AssignProjectSubsystemRoleModal — širší
- [ ] **record-editor** (`ModalVariant="record-editor"`): EditZaznamModal — nejširší + vyšší
- [ ] **overflow-visible** (`ModalOverflowVisible="true"`): pro office-search-panel — picker přečnívá mimo modal

## Očekávané regrese (flag v návaznosti)

- Žádné nativní native browser "Opravdu odejít?" dialogy (byly smazány v commitu 75684be)
- Žádná ztráta dirty-check flow (ESC, backdrop click → app dialog, ne gov-dialog self-close)
- Žádné stack traces v konzoli o `gov-dialog is not defined`

## Výsledek

Podepsat dolem: `___________________ (jméno, datum)`
