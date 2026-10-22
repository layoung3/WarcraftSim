/* CP161: shared latency control and melee-first Hunter UI enhancements.
   Loaded after the existing development Simulation Lab to preserve its API and
   per-specialization rendering without modifying combat calculations in JS. */
'use strict';

// Hide the obsolete one-way "switch to ranged" quick control. It remains
// available in Advanced JSON for legacy scripted hybrid regression scenarios.
const cp161SwitchIndex = simpleFields.findIndex(([key]) => key === 'switchAtSeconds');
if (cp161SwitchIndex !== -1) simpleFields.splice(cp161SwitchIndex, 1);

// Use the existing request/advanced JSON state while supplying a discoverable
// combat-mode selector and movement settings only for Survival Hunter.
simpleFields.push(
  ['weaveTravelSeconds', 'Movement travel each way (s)'],
  ['weaveMinimumMeleeSeconds', 'Minimum melee time (s)'],
  ['weaveManaReserve', 'Weaving mana reserve']
);

const cp161RenderRequest = renderRequest;
renderRequest = function () {
  cp161RenderRequest();
  const q = currentRequest;
  const ms = document.getElementById('inputLatencyMs');
  if (q && Object.prototype.hasOwnProperty.call(q, 'inputLatencyMs'))
    ms.value = q.inputLatencyMs;

  if (!q || !Object.prototype.hasOwnProperty.call(q, 'combatMode')) return;
  // Other specs that expose a combatMode can keep using Advanced JSON until
  // they have their own documented choices.
  if (document.getElementById('classSelect').value !== 'Hunter' ||
      document.getElementById('specSelect').value !== 'Survival') return;

  const wrap = document.createElement('label');
  wrap.textContent = 'Survival combat behavior';
  const select = document.createElement('select');
  const modes = [
    ['weave', 'Melee-first: ranged cooldown excursions'],
    ['melee', 'Melee only'],
    ['ranged', 'Ranged only']
  ];
  if (q.combatMode === 'hybrid') modes.push(['hybrid', 'Legacy fixed one-way switch']);
  for (const [value, name] of modes) select.add(new Option(name, value));
  select.value = q.combatMode;
  select.addEventListener('change', () => {
    currentRequest.combatMode = select.value;
    renderRequest();
  });
  wrap.append(select);
  document.getElementById('scenarioOptions').prepend(wrap);
};

// Capture before the existing Run handler serializes the request. The API
// middleware consumes inputLatencyMs for all classes without changing every DTO.
document.getElementById('run').addEventListener('click', function (event) {
  const ms = document.getElementById('inputLatencyMs');
  const value = Number(ms.value);
  if (!Number.isFinite(value) || value < 0 || value > 1000) {
    event.preventDefault();
    event.stopImmediatePropagation();
    status('Input latency must be between 0 and 1000 milliseconds.');
    return;
  }
  if (currentRequest) {
    currentRequest.inputLatencyMs = value;
    writeEditor();
  }
}, true);
