// Runs the v4 handoff package's own golden tests in Node, the same way tests/rules.test.html does:
// rules.js, bots.js, mockServer.js, runner.js and server.tests.js loaded in order into one vm context.
// Usage: node tools/v4/run_package_tests.js [handoffDir] [--json out.json]
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const args = process.argv.slice(2);
const jsonAt = args.indexOf('--json');
const jsonOut = jsonAt >= 0 ? args[jsonAt + 1] : null;
const positional = args.filter((a, i) => a !== '--json' && i !== jsonAt + 1);
const root = path.resolve(positional[0] || path.join(__dirname, '..', '..', 'Design', 'Galaxy101_ClaudeCode', 'prototip', 'galaxy101_handoff_v4'));

const ctx = { console };
ctx.self = ctx; ctx.window = ctx; ctx.globalThis = ctx;
vm.createContext(ctx);
for (const f of ['logic/rules.js', 'logic/bots.js', 'logic/mockServer.js', 'tests/runner.js', 'tests/server.tests.js']) {
  vm.runInContext(fs.readFileSync(path.join(root, f), 'utf8'), ctx, { filename: f });
}

const R = ctx.G101Rules, B = ctx.G101Bots, TR = ctx.G101TestRunner;
const idx = JSON.parse(fs.readFileSync(path.join(root, 'tests/rules/index.json'), 'utf8'));
const report = { version: idx.version, vectors: [], flows: [] };
let vPass = 0, vFail = 0;
for (const f of idx.files) {
  const suite = JSON.parse(fs.readFileSync(path.join(root, 'tests/rules', f), 'utf8'));
  for (const t of suite.tests) {
    const fails = TR.run(R, B, t);
    fails.length ? vFail++ : vPass++;
    report.vectors.push({ file: f, id: t.id, ok: !fails.length, fails });
    if (fails.length) console.log('FAIL ' + t.id + '\n  ' + fails.join('\n  '));
  }
}
const SV = ctx.G101ServerTests.run();
let sPass = 0, sFail = 0;
for (const t of SV) {
  t.fails.length ? sFail++ : sPass++;
  report.flows.push({ id: t.id, title: t.title, ok: !t.fails.length, fails: t.fails });
  if (t.fails.length) console.log('FAIL ' + t.id + '\n  ' + t.fails.join('\n  '));
}
console.log(`vectors ${vPass}/${vPass + vFail} · server flows ${sPass}/${sPass + sFail}`);
if (jsonOut) fs.writeFileSync(jsonOut, JSON.stringify(report, null, 1));
process.exitCode = vFail || sFail ? 1 : 0;
