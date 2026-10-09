#!/usr/bin/env python3
"""Run standalone project gates; preserve failure logs and artifact identity."""
import argparse
import hashlib
import json
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path


def run(*args):
    subprocess.run(args, check=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--selection', type=Path, default=Path('TestResults/selection'))
    parser.add_argument('--pack', action='store_true')
    parser.add_argument('--baseline-version')
    args = parser.parse_args()
    builds = (args.selection / 'build-projects.txt').read_text().splitlines()
    tests = (args.selection / 'test-projects.txt').read_text().splitlines()
    for project in builds:
        run('dotnet', 'restore', project, '--locked-mode', '--disable-parallel', '-m:2')
        run('dotnet', 'build', project, '-c', 'Release', '--no-restore', '-m:2')
    for project in tests:
        run('dotnet', 'test', '--project', project, '-c', 'Release', '--no-build', '--no-restore')
    if args.pack:
        if args.baseline_version:
            config = ET.parse('NuGet.Config')
            mappings = config.findall('./packageSourceMapping/packageSource')
            internal = [source.get('key') for source in mappings
                        if any(p.get('pattern') == 'SharedKernel.*' for p in source)]
            sources = {source.get('key'): source.get('value') for source in config.findall('./packageSources/add')}
            if any(source.get('key') not in internal and any(p.get('pattern', '').startswith('SharedKernel.') for p in source)
                   for source in mappings):
                raise ValueError('Internal baseline packages must not map to any other source.')
            if len(internal) != 1 or not sources.get(internal[0]) or not Path(sources[internal[0]]).is_dir():
                raise ValueError('API baseline comparison requires SharedKernel.* mapped exclusively to a local baseline feed.')
        version = '0.1.0-alpha.local.' + uuid.uuid4().hex
        run('dotnet', 'restore', 'tools/SharedKernel.Versioning.Tool/SharedKernel.Versioning.Tool.csproj', '--locked-mode', '-m:2')
        tool = ['dotnet', 'run', '--project', 'tools/SharedKernel.Versioning.Tool', '-c', 'Release',
                '--no-restore', '--']
        run(*tool, 'check-public-api-baselines')
        api = ['api-compatibility', '--version', version, '--release-phase', 'beta']
        if args.baseline_version:
            api += ['--baseline-version', args.baseline_version]
        run(*tool, *api)
        # API validation produces the candidate artifacts; never repack for promotion.
        feed = Path('artifacts/api-compat/packages') / version
        evidence = {
            'libraryRevision': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
            'libraryDirty': bool(subprocess.check_output(['git', 'status', '--porcelain'], text=True).strip()),
            'packageVersion': version,
            'packageSha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                              for p in sorted(feed.glob('*.nupkg'))},
            'consumerCompatibility': 'pending',
            'linuxPackageConsumption': 'passed',
            'apiBaselineComparison': 'passed' if args.baseline_version else 'pending',
            'validationScope': 'full' if len(builds) == len(json.loads(Path('provenance/extraction-manifest.json').read_text())['projects']) else 'affected',
            'windowsPackageConsumption': 'pending',
            'stablePromotion': 'pending'
        }
        run(sys.executable, 'scripts/sharedkernel/smoke.py', str(feed), '--version', version)
        (feed / 'candidate.json').write_text(json.dumps(evidence, indent=2) + '\n')
