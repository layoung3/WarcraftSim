# CP174 Solo DPS frontend — manual acceptance procedure

**State:** draft integration, not an assertion of nine-specialization certification.

## Start
```powershell
git checkout cp174-unified-solo-dps-frontend
dotnet restore WarcraftSim.slnx
dotnet build WarcraftSim.slnx -c Release --no-restore -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
python -m unittest discover -s tools/ForeverEquipmentImportCli -p "test_*.py"
dotnet run --project WarcraftSim.Api
```
Open the HTTPS or HTTP launch URL shown by the API with `/solo-dps.html`. Keep `/sim-lab.html` available for regression comparison.

## Mage representative: Human Fire
1. Choose Mage → Fire → Human.
2. Leave strict reviewed baseline selected. Request character stats; note any missing reviewed baseline failure. Only check historical provisional after consciously accepting the source status.
3. Inspect the Arcane/Fire/Frost supported talent rank list. Change one rank and verify the numeric cap; inspect aggregate points and attempt an invalid allocation. The typed endpoint `/api/forever/talents/mage/fire/validate` must reject it.
4. Select a catalog weapon. Validate the equipment through `/api/forever/equipment/resolve`. Add a compatible permanent enchant; ensure an incompatible weapon-only enchant is not offered for a nonweapon.
5. Select a flask/elixir and Curse of the Elements target debuff; choose a seed, duration, and timeline.
6. Resolve stats. Confirm the backend source attribution is displayed.
7. Run combat, inspect server DPS, total damage, ability breakdown and raw response. Confirm seed is carried through.
8. Save locally; switch spec and load. Assert race, talents, equipment/enchant, modifiers and encounter settings are restored. Repeat the same seed and compare exported damage and DPS for determinism.

## Warlock representative
Select Warlock → Demonology → Orc, choose an explicitly supported baseline, talent ranks and modifiers; enable demon using the selected typed defaults and inspect `petDamage` and `demonType` in the server response. Repeat identical run and verify determinism.

## Hunter representative
Select Hunter → Beast Mastery → Orc, select a pet family and valid ranged weapon. Inspect source-owned talent caps and validate. Run, confirm owner/pet attribution, pet family and response timeline. Repeat for Marksmanship and Survival; verify both can run with supported pet configuration without a fabricated Lone Wolf pet.

## Full matrix
Repeat the run and raw-response inspection for Mage Arcane/Fire/Frost; Warlock Affliction/Demonology/Destruction; Hunter Marksmanship/Survival/Beast Mastery. For each, verify the typed request property, selected race, baseline flags, equipment IDs, enchant IDs, modifier IDs, talent ranks, seed, duration, and target health. Record any rejected combination and error message.

## Known gaps preventing acceptance
- Source-backed tree topology, full tier positions, prerequisites and unmodeled filler metadata remain incomplete. Server validates implemented caps and prerequisites.
- Current equipment selector still relies on server rejection rather than proactive class/faction/armor filtering; additional pagination UX, tooltip status and set indicators remain outstanding.
- Browser-automated tests and actual nine-spec HTTP simulation receipts are not yet included.
- No assertion is made here that any particular result is Forever-gameplay accurate.
