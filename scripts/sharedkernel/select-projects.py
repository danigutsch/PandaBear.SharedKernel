#!/usr/bin/env python3
"""Select whole projects and their reverse transitive dependents, failing closed."""
import argparse
import json
import subprocess
from pathlib import Path
import xml.etree.ElementTree as ET


def select(root, manifest, changes=None):
    projects = set(manifest['projects'])
    tests = {p for p in projects if any(x.get('Include') == 'xunit.v3.mtp-v2'
                                      for x in ET.parse(root / p).iter('PackageReference'))}
    if changes is None:
        return sorted(projects), sorted(tests), ['Full validation requested or comparison history unavailable.']
    graph = {}
    try:
        for project in projects:
            document = ET.parse(root / project)
            if list(document.iter('Import')):
                raise ValueError(f'Unevaluated import: {project}')
            dependencies = set()
            for ref in document.iter('ProjectReference'):
                value = ref.get('Include', '')
                if not value or any(c in value for c in '$*;'):
                    raise ValueError(f'Unevaluated reference: {project}')
                dependency = (root / Path(project).parent / value.replace('\\', '/')).resolve().relative_to(root).as_posix()
                if dependency not in projects:
                    raise ValueError(f'Unknown dependency: {dependency}')
                dependencies.add(dependency)
            graph[project] = dependencies
        for consumer, dependencies in manifest.get('packageConsumptionEdges', {}).items():
            if consumer not in graph or not set(dependencies).issubset(projects):
                raise ValueError('Unknown package-consumption edge.')
            graph[consumer].update(dependencies)
        affected = set()
        reasons = []
        for change in changes:
            path = Path(change)
            if path.suffix in {'.props', '.targets', '.slnx', '.globalconfig'} or path.name in {
                '.editorconfig', '.gitattributes', 'global.json', 'NuGet.Config', 'NuGet.config'
            } or change.startswith(('scripts/', '.github/', 'src/SharedKernel/SharedKernel.Style.', 'src/SharedKernel/SharedKernel.Testing.Analyzers/')):
                raise ValueError(f'Global or imported build input: {change}')
            if change.startswith('docs/') or change in {'README.md', 'LICENSE.txt'}:
                continue
            owners = [p for p in projects if change == p or change.startswith(str(Path(p).parent) + '/')]
            if not owners:
                raise ValueError(f'Uncertain owner (including deleted projects): {change}')
            owner = max(owners, key=len)
            affected.add(owner)
            reasons.append(f'{change}: owning project {owner}')
        while True:
            dependents = {p for p, refs in graph.items() if refs & affected}
            added = dependents - affected
            if not added:
                break
            reasons.extend(f'{p}: reverse transitive dependent' for p in sorted(added))
            affected.update(added)
        return sorted(affected), sorted(tests & affected), reasons
    except (ValueError, OSError, ET.ParseError) as error:
        return sorted(projects), sorted(tests), [f'Full validation fallback: {error}']


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path.cwd())
    parser.add_argument('--manifest', type=Path, default=Path('provenance/extraction-manifest.json'))
    parser.add_argument('--base')
    parser.add_argument('--head', default='HEAD')
    parser.add_argument('--output', type=Path, default=Path('TestResults/selection'))
    args = parser.parse_args()
    root = args.root.resolve()
    manifest = json.loads(args.manifest.read_text())
    changes = None
    if args.base:
        try:
            changes = subprocess.check_output(['git', 'diff', '--name-only', '--no-renames',
                                               f'{args.base}...{args.head}', '--'], cwd=root, text=True).splitlines()
        except subprocess.CalledProcessError:
            pass
    builds, tests, reasons = select(root, manifest, changes)
    args.output.mkdir(parents=True, exist_ok=True)
    for name, values in [('build-projects', builds), ('test-projects', tests), ('selection-reasons', reasons)]:
        (args.output / f'{name}.txt').write_text(''.join(f'{value}\n' for value in values))
    print('\n'.join(reasons))
