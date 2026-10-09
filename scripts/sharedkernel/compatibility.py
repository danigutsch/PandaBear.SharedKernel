#!/usr/bin/env python3
"""Validate an already prepared package consumer separately from library merge."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import xml.etree.ElementTree as ET


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('consumer', type=Path)
    parser.add_argument('candidate', type=Path)
    parser.add_argument('--output', type=Path, default=Path('TestResults/consumer-compatibility.json'))
    args = parser.parse_args()
    consumer = args.consumer.resolve()
    candidate_path = args.candidate.resolve()
    evidence = json.loads(candidate_path.read_text())
    evidence['consumerRevision'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=consumer, text=True).strip()
    evidence['consumerDirty'] = bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=consumer, text=True).strip())
    evidence['consumerCompatibility'] = 'pending'
    try:
        if evidence['libraryDirty'] or evidence['consumerDirty']:
            raise ValueError('Compatibility requires committed library and consumer revisions.')
        if not evidence['packageSha256']:
            raise ValueError('Candidate package hashes are unavailable.')
        for name, digest in evidence['packageSha256'].items():
            if hashlib.sha256((candidate_path.parent / name).read_bytes()).hexdigest() != digest:
                raise ValueError(f'Candidate package changed: {name}')
        projects = [ET.parse(p) for p in (consumer / 'src').rglob('*.csproj')]
        if any('SharedKernel/' in ref.get('Include', '').replace('\\', '/')
               for document in projects for ref in document.iter('ProjectReference')):
            raise ValueError('Consumer still references SharedKernel sources; package adoption is pending.')
        central = ET.parse(consumer / 'Directory.Packages.props')
        versions = [p.get('Version') for p in central.iter('PackageVersion')
                    if p.get('Include', '').startswith('SharedKernel.')]
        if not versions or any(v not in (evidence['packageVersion'], '[' + evidence['packageVersion'] + ']') for v in versions):
            raise ValueError('Consumer must declare the candidate minimum version before validating adoption.')
        solution = ET.parse(consumer / 'ViajantesTurismo.slnx')
        for project in solution.iter('Project'):
            lock = consumer / Path(project.get('Path')).parent / 'packages.lock.json'
            if not lock.exists():
                raise ValueError(f'Consumer lock file is unavailable: {lock}')
            for dependencies in json.loads(lock.read_text())['dependencies'].values():
                for package, dependency in dependencies.items():
                    if package.startswith('SharedKernel.') and dependency.get('type') != 'Project':
                        if dependency.get('resolved') != evidence['packageVersion']:
                            raise ValueError(f'{lock}: {package} does not resolve to the tested candidate.')
        config = candidate_path.parent / '.restore-check/NuGet.config'
        if not config.exists():
            config = candidate_path.parent / 'sample-checks/Mediator.Sample/NuGet.config'
        if not config.exists():
            raise ValueError('Candidate local-feed configuration is unavailable.')
        cache = args.output.resolve().parent / ('consumer-packages-' + evidence['packageVersion'])
        evidence['consumerCompatibility'] = 'failed'
        for command in [
            ['dotnet', 'restore', 'ViajantesTurismo.slnx', '--locked-mode', '--configfile', str(config),
             '--packages', str(cache), '--no-cache', '-m:2'],
            ['dotnet', 'build', 'ViajantesTurismo.slnx', '-c', 'Release', '--no-restore', '-m:2'],
            ['dotnet', 'run', '--project', 'tools/ViajantesTurismo.OpenApi.Tool', '-c', 'Release',
             '--no-build', '--no-restore', '--', 'generate', 'all'],
            ['dotnet', 'test', '--solution', 'ViajantesTurismo.slnx', '-c', 'Release', '--no-build', '--no-restore']
        ]:
            subprocess.run(command, cwd=consumer, check=True)
        evidence['consumerCompatibility'] = 'passed'
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        evidence['reason'] = str(error)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(evidence, indent=2) + '\n')
    print(evidence['consumerCompatibility'])
    raise SystemExit(0 if evidence['consumerCompatibility'] == 'passed' else 1)
