"""Compare migration snapshots without changing Unity assets or user parameters."""
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent
plan = json.loads((HERE.parent / 'MovePlan.json').read_text(encoding='utf-8'))
moves = sorted(plan['moves'], key=lambda m: -len(m['from']))


def mapped(path):
    for m in moves:
        if path == m['from'] or path == m['from'] + '.meta' or path.startswith(m['from'] + '/'):
            return m['to'] + path[len(m['from']):]
    return path


reverse = {}
for m in moves:
    for separator in ['/', '\\', '\\\\']:
        reverse[m['to'].replace('/', separator)] = m['from'].replace('/', separator)
reverse['Game/Documentation/TutorialInfo/Layout.wlt'] = 'TutorialInfo/Layout.wlt'
pattern = re.compile('|'.join(re.escape(k) for k in sorted(reverse, key=len, reverse=True)))
before_files = json.loads((HERE / 'BeforeFiles.json').read_text(encoding='utf-8-sig'))
before = json.loads((HERE / 'BeforeNative.json').read_text(encoding='utf-8-sig'))
after = json.loads((HERE / 'AfterNative.json').read_text(encoding='utf-8-sig'))
issues, content_changes, path_only = [], [], []
guids = {}
for meta in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
    if match:
        guid = match.group(1).decode()
        if guid in guids:
            issues.append('Duplicate GUID: ' + guid)
        guids[guid] = meta.relative_to(ROOT).as_posix()

for row in before_files:
    if row['path'] == plan['emptyContainer'] + '.meta':
        continue  # Only the already empty legacy wrapper was removed.
    path = mapped(row['path'])
    target = ROOT / path
    if not target.is_file():
        issues.append('Missing file: ' + path)
        continue
    if row['guid'] and guids.get(row['guid']) != path:
        issues.append('GUID moved incorrectly: ' + path)
    data = target.read_bytes()
    if hashlib.sha256(data).hexdigest() == row['sha256']:
        continue
    try:
        original_paths = pattern.sub(lambda m: reverse[m[0]], data.decode('utf-8')).encode('utf-8')
    except UnicodeDecodeError:
        original_paths = data
    if hashlib.sha256(original_paths).hexdigest() == row['sha256']:
        path_only.append(path)
    else:
        content_changes.append(path)

after_assets = {a['guid']: a for a in after['assets']}
dependency_count = 0
for original in before['assets']:
    current = after_assets.get(original['guid'])
    if current is None:
        issues.append('Unresolved asset GUID: ' + original['path'])
        continue
    if current['path'] != mapped(original['path']):
        issues.append('Unexpected asset path: ' + current['path'])
    for key in ['dependencies', 'type', 'missingScripts']:
        if original[key] != current[key]:
            issues.append(key + ' changed: ' + current['path'])
    if original['dependencies']:
        dependency_count += 1

material_changes = []
after_materials = {m['guid']: m for m in after['materials']}
for original in before['materials']:
    current = after_materials.get(original['guid'])
    if original != current:
        material_changes.append(after_assets[original['guid']]['path'])
expected_build = [mapped(s.split('|')[0]) + '|' + s.split('|')[1] for s in before['buildScenes']]
if after['buildScenes'] != expected_build:
    issues.append('Build scene order or enabled flags changed')
for original, current in zip(before['scenes'], after['scenes']):
    if mapped(original['path']) != current['path'] or original['objects'] != current['objects'] or original['missingScripts'] != current['missingScripts']:
        issues.append('Open scene content changed: ' + current['path'])

result = dict(
    assets_before=len(before['assets']), assets_after=len(after['assets']),
    original_guids_preserved=sum(bool(r['guid']) for r in before_files) - 1,
    dependency_sets_unchanged=dependency_count,
    missing_scripts=sum(a['missingScripts'] for a in after['assets']),
    path_only_file_changes=path_only, other_content_changes=content_changes,
    live_editor_material_differences=material_changes,
    live_editor_note='Live NormalStrength/MicroStrength edits were observed while the user tuned materials; left untouched. Persisted material file hashes are checked separately above.',
    issues=issues,
    passed=not issues and not content_changes,
)
(HERE / 'LayoutVerification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
