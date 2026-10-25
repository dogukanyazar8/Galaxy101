// Generates the rules.js ↔ C# parity fixture: four-bot matches (bots.js nextAction, rules.js apply) for many seeds and
// option sets; for each, the SHA-256 of every event object and of the final match state in canonical JSON (keys sorted).
// The C# test (Packages/com.okey101.core/Tests/Editor/V4/V4ParityTests.cs) replays the same matches on G101Rules/G101Bots
// and must produce the same hashes.
// Usage (repo root): node tools/v4/nunit/gen_parity.js > Packages/com.okey101.core/Tests/Editor/V4/v4-parity.json
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const crypto = require('crypto');

const pkg = path.join(__dirname, '..', '..', '..', 'Design', 'Galaxy101_ClaudeCode', 'prototip', 'galaxy101_handoff_v4');
const ctx = { console };
ctx.self = ctx;
vm.createContext(ctx);
for (const f of ['logic/rules.js', 'logic/bots.js']) vm.runInContext(fs.readFileSync(path.join(pkg, f), 'utf8'), ctx, { filename: f });
const R = ctx.G101Rules, B = ctx.G101Bots;

function canon(v) {
  if (v === null || v === undefined) return 'null';
  if (Array.isArray(v)) return '[' + v.map(canon).join(',') + ']';
  if (typeof v === 'object') return '{' + Object.keys(v).filter(k => v[k] !== undefined).sort().map(k => JSON.stringify(k) + ':' + canon(v[k])).join(',') + '}';
  return JSON.stringify(v);
}
const sha = s => crypto.createHash('sha256').update(s, 'utf8').digest('hex');

function play(opts, seed) {
  let S = R.newMatch(opts, seed);
  const events = [];
  let r = R.apply(S, { t: 'startHand' });
  events.push(...r.events); S = r.state;
  let steps = 0, fails = 0;
  for (let guard = 20000; guard-- > 0;) {
    if (S.status === 'matchOver') break;
    if (S.status === 'handOver') { r = R.apply(S, { t: 'nextHand' }); events.push(...r.events); S = r.state; continue; }
    const seat = S.hand.turn.seat, a = B.nextAction(S, seat);
    let res = R.apply(S, a);
    if (!res.ok) { fails++; res = R.apply(S, { t: 'timeout', seat }); if (!res.ok) throw new Error('fatal ' + res.error); }
    events.push(...res.events); S = res.state; steps++;
  }
  return { steps, fails, events: sha(canon(events)), state: sha(canon(S)), totals: S.totals };
}

const configs = [
  { mode: 'solo', hands: 5 },
  { mode: 'pairs', hands: 5 },
  { mode: 'solo', hands: 3, katlama: true },
  { mode: 'pairs', hands: 3, katlama: true, katlamaPartner: false },
  { mode: 'solo', hands: 3, isle: false },
  { mode: 'solo', hands: 3, penalties: false }
];
const cases = [];
configs.forEach((opts, ci) => {
  for (let seed = 1; seed <= 12; seed++) {
    const s = seed * 7919 + ci;
    cases.push(Object.assign({ opts, seed: s }, play(opts, s)));
  }
});
process.stdout.write(JSON.stringify({ source: 'rules.js ' + R.VERSION + ' + bots.js', cases }, null, 1) + '\n');
