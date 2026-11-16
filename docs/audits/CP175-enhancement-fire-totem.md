# CP175 Enhancement fire-only Call correction

## Scope and inherited model

This stage follows accepted Enhancement head `bdfe1e2d055e64e1480693870216b1fc488eeb7a`. Enhancement already learned Searing Totem and all three programmed Calls, but its runner omitted `ShamanFireTotemProcessor`. A Searing-only preset tests the source-owned `shaman-fire-totem-active` marker; without its producer, the higher-priority Call repeatedly casts while affordable and starves Shield and weapon imbues.

The sole production change registers the existing processor after auras and before lifecycle/priority processing. No spell, aura definition, damage constant, priority, multi-preset policy, Recall gate or Fire Nova capability changes. Enhancement still does not import Fire Nova. Elemental and opt-in Restoration already use this processor.

The imported Searing model remains historical/provisional: `(47 + .017 * Spell Power) * 1.15` at the imported default ranks, 170 Mana, 55-second duration and two-second ticks. These are preserved conventions, not new current-server or client-source certification. Call damage remains attributed to the selected Call ability.

## Direct numerical verification

Unchanged production produced **12 intended failures and four passing controls/harness cases**. The corrected direct suite passes all **16 cases** across Elements, Ancestors and Spirits, both downstream imbues, owned refresh/removal, and active/expired Recall.

In each controlled 40-second, 100-SP, zero-regeneration fire-only fixture, completed Calls decrease from **13 to one**, Searing damage increases from **672.06 to 1008.09**, and the isolated Mana saving is **2040**. One Call completes at 3 seconds and pays 170 Mana. Eighteen 56.005-damage ticks occur at 5–39 seconds. With sufficient Mana and duration, 27 ticks occur through 57 seconds, the marker expires at 58, and the next Call completes at 61. The active one-Searing Recall fixture refunds **42.5 Mana** once and prevents later ticks; expired Searing refunds nothing.

Real downstream fixtures now produce **1602 Shield damage** plus **899.1 Flametongue** or **1422 Frostbrand** damage. These are deterministic test examples, not universal DPS improvements or optimized builds. Additional legitimate ticks/procs can change random-roll ordering in the corrected fire-only runs.

## Compatibility and timeline boundary

Complete exact-Decimal request/response comparisons preserve the no-Call Enhancement, Elemental and opt-in Restoration controls, normalizing only generated identities with literal anchors and identity joins preserved. Three multi-slot controls (Earth, Air or Water plus Searing) preserve every other serialized value and public event after explicitly accounting for the new owned marker application at 3 seconds, removal at 58, and five additional processed events. The latter consist of the two visible marker events, an internal expiration and two source-aura-triggered priority decisions. Public timelines never include internal events.

The existing prepared-input scenario 3 gains marker events during Call/Recall. Its new full timeline fingerprint remains mandatory. A second assertion retains its old literal fingerprint after excluding only the expected owned marker events, with positive identity, count, application-time and same-ID Recall-removal checks. Other scenario fingerprints remain unchanged. This fingerprint covers its original event-field projection; the separate six control comparisons cover complete responses.

## Local verification and remaining gate

Final local verification passes a zero-warning/error Release rebuild and **7,143 .NET tests**, including the existing Fire Nova/lifecycle suites. All **25 Node suites**, nine Python entrypoints (**230 passed, three optional historical retained-input skips**), **126 importer tests**, importer smoke and syntax checks pass.

The single-owner API run passes Enhancement **38 strict and 58 UI HTTP cases**, existing Feral/Rogue/Combat HTTP checks, and two 155-case Serrated captures with arithmetic analysis. It shuts down normally with unchanged source and runtime hashes. These are HTTP checks, not local browser or pixel evidence.

The tested source manifest is `271d945521129aff4d814630d858ff581a9971b59aebe630d5dd05a86ad9a298`; this audit and ledger addition follow execution and are accounted separately. Failed test-first and initial additive-fingerprint receipts remain preserved. **Publication, independent final acceptance and exact-new-head GitHub Actions/native-browser acceptance remain pending.** No merge, deployment, new permanent branch/PR, spending, PR text/comment change or overall-completion claim is made.
