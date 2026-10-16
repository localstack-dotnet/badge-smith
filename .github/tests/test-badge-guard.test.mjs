import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const workflow = readFileSync(new URL('../workflows/ci-cd.yml', import.meta.url), 'utf8');
const steps = workflow.split(/^      - /m).slice(1);
const badge = steps.find(step => step.startsWith('name: Update test badge via BadgeSmith API\n'));
const apiTests = steps.find(step => step.startsWith('name: Run tests with BadgeSmith integration\n'));
assert.ok(badge, 'Badge publication step must exist');
assert.ok(apiTests, 'API test step must exist');
const apiTestsId = apiTests.match(/^        id: (\S+)$/m)?.[1];
const condition = badge.match(/^        if: (.+)((?:\n          .+)*)/m);
assert.ok(condition, 'Badge publication must have a condition');
const expression = condition[1] === '>-'
  ? condition[2].trim().replace(/\s+/g, ' ')
  : condition[1];

// Interpret only the workflow guard's string comparisons, boolean operators,
// parentheses and status functions. Unsupported syntax fails closed; no eval.
function evaluate(source, context) {
  const tokens = [];
  let remaining = source.trim();
  while (remaining) {
    const match = /^(\s+|'[^']*'|==|!=|&&|\|\||[()]|[A-Za-z_][A-Za-z0-9_.-]*)/.exec(remaining);
    assert.ok(match, `Unsupported expression syntax: ${remaining}`);
    remaining = remaining.slice(match[0].length);
    if (match[0].trim()) tokens.push(match[0]);
  }
  let position = 0;
  function take(token) {
    if (tokens[position] !== token) return false;
    position++;
    return true;
  }
  function primary() {
    if (take('(')) {
      const value = or();
      assert.ok(take(')'), 'Missing closing parenthesis');
      return value;
    }
    const token = tokens[position++];
    assert.ok(token, 'Missing expression operand');
    if (token.startsWith("'")) return token.slice(1, -1);
    if (['always', 'success', 'failure'].includes(token)) {
      assert.ok(take('(') && take(')'), 'Status function must have no arguments');
      return token === 'always' || context.status === token;
    }
    assert.match(token, /^(github\.(event_name|ref)|steps\.[\w-]+\.(outcome|outputs\.(passed|failed|skipped)))$/);
    return token.split('.').reduce((value, key) => value?.[key], context) ?? '';
  }
  function comparison() {
    const left = primary();
    if (take('==')) return left === primary();
    if (take('!=')) return left !== primary();
    return left;
  }
  function and() {
    let value = comparison();
    while (take('&&')) {
      const right = comparison();
      value = Boolean(value && right);
    }
    return value;
  }
  function or() {
    let value = and();
    while (take('||')) {
      const right = and();
      value = Boolean(value || right);
    }
    return value;
  }
  const result = or();
  assert.equal(position, tokens.length, 'Unexpected expression tokens');
  return Boolean(result);
}

function shouldPublish({ outcome, outputs = {}, status = 'success', event = 'push', ref = 'refs/heads/master' }) {
  // Actions implicitly requires success() unless the condition has a status function.
  if (!/\b(always|success|failure)\s*\(/.test(expression) && status !== 'success') return false;
  return evaluate(expression, {
    status,
    github: { event_name: event, ref },
    steps: {
      ...(apiTestsId ? { [apiTestsId]: { outcome } } : {}),
      'test-results': { outputs },
    },
  });
}

test('Badge_Should_Not_Publish_When_Restore_Failure_Skips_Tests', () => {
  assert.equal(shouldPublish({ outcome: 'skipped', status: 'failure', outputs: { passed: '0', failed: '0', skipped: '0' } }), false);
});

for (const [name, input, expected] of [
  ['Build_Failure_Skips_Tests', { outcome: 'skipped', status: 'failure' }, false],
  ['Skipped_Tests_Have_Unexpected_Report_Outputs', { outcome: 'skipped', outputs: { passed: '5' } }, false],
  ['Test_Launch_Fails_Without_Report', { outcome: 'failure', status: 'failure' }, false],
  ['Successful_Test_Step_Has_No_Report', { outcome: 'success' }, false],
  ['Missing_Report_Emits_Zero_Outputs', { outcome: 'failure', status: 'failure', outputs: { passed: '0', failed: '0', skipped: '0' } }, false],
  ['Report_Outputs_Are_Empty', { outcome: 'success', outputs: { passed: '', failed: '', skipped: '' } }, false],
  ['Report_Is_Empty', { outcome: 'success', outputs: { passed: '0', failed: '0', skipped: '0' } }, false],
  ['Tests_Pass_With_Report', { outcome: 'success', outputs: { passed: '42', failed: '0', skipped: '0' } }, true],
  ['Tests_Fail_With_Report_Despite_Reporter_Failure', { outcome: 'failure', status: 'failure', outputs: { passed: '0', failed: '2', skipped: '0' } }, true],
  ['Report_Has_Only_Skipped_Results', { outcome: 'success', outputs: { passed: '0', failed: '0', skipped: '3' } }, true],
  ['Tests_Are_Cancelled', { outcome: 'cancelled', outputs: { passed: '5' } }, false],
  ['Event_Is_Pull_Request', { outcome: 'success', event: 'pull_request', outputs: { passed: '5' } }, false],
  ['Push_Targets_Another_Branch', { outcome: 'success', ref: 'refs/heads/feature/example', outputs: { passed: '5' } }, false],
]) {
  test(`Badge_Should_${expected ? '' : 'Not_'}Publish_When_${name}`, () => {
    assert.equal(shouldPublish(input), expected);
  });
}
