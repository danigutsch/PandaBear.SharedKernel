#!/usr/bin/env python3
"""Extract only manifest paths into a new, independent local repository."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path
import xml.etree.ElementTree as ET


def run(*args, cwd=None):
    return subprocess.check_output(args, cwd=cwd, text=True).strip()


def validate_manifest(source, manifest):
    projects = set(manifest['projects'])
    if not projects or len(projects) != len(manifest['projects']):
        raise ValueError('Manifest must contain unique projects.')
    for entry in manifest['paths']:
        path = Path(entry)
        if path.is_absolute() or '..' in path.parts or not (source / path).exists():
            raise ValueError(f'Invalid or missing manifest path: {entry}')
    for project in projects:
        path = source / project
        if not any(project == item or project.startswith(item.rstrip('/') + '/') for item in manifest['paths']):
            raise ValueError(f'Project is not covered by manifest paths: {project}')
        for reference in ET.parse(path).iter('ProjectReference'):
            include = reference.get('Include', '')
            if not include or any(char in include for char in '$*;'):
                raise ValueError(f'Unevaluated project reference: {project}: {include}')
            dependency = (path.parent / include.replace('\\', '/')).resolve().relative_to(source).as_posix()
            if dependency not in projects:
                raise ValueError(f'Extraction crosses boundary: {project}: {dependency}')


def extract(source, destination, manifest_path, filter_repo):
    source = source.resolve()
    destination = destination.resolve()
    if destination.exists():
        raise ValueError('Destination must not exist; existing repositories are never rewritten.')
    if source == destination or source.is_relative_to(destination):
        raise ValueError('Destination must not contain the source repository.')
    manifest_bytes = manifest_path.read_bytes()
    manifest = json.loads(manifest_bytes)
    validate_manifest(source, manifest)
    if run('git', 'status', '--porcelain', '--', *manifest['paths'], cwd=source):
        raise ValueError('Commit retained source changes before extraction; cloning reads committed history only.')
    revision = run('git', 'rev-parse', 'HEAD', cwd=source)
    # No shared object store, tags, remote publishing, or force-filtering of existing repositories.
    run('git', 'clone', '--no-local', '--single-branch', '--no-tags', str(source), str(destination))
    path_file = destination / '.git' / 'extraction-paths.txt'
    path_file.write_text('\n'.join(manifest['paths']) + '\n')
    run(filter_repo, '--paths-from-file', str(path_file), cwd=destination)
    worktree = destination / '.worktrees' / 'standalone'
    run('git', 'worktree', 'add', '-b', 'feat/standalone', str(worktree), cwd=destination)
    solution = ET.Element('Solution')
    for project in manifest['projects']:
        ET.SubElement(solution, 'Project', Path=project)
    ET.indent(solution)
    ET.ElementTree(solution).write(worktree / 'SharedKernel.slnx', encoding='unicode')
    for path in (worktree / 'tests').rglob('*.cs'):
        text = path.read_text()
        # Repository discovery only; preserve application-specific fixture expectations.
        text = text.replace('"ViajantesTurismo.slnx"', '"SharedKernel.slnx"')
        path.write_text(text)
    inventory = worktree / 'src/SharedKernel/SharedKernel.Versioning/PackageLockInventory.cs'
    inventory.write_text(inventory.read_text().replace('Escape("ViajantesTurismo " + options.Version)', 'Escape("SharedKernel " + options.Version)'))
    for path in (worktree / 'src/SharedKernel').rglob('AssemblyInfo.cs'):
        path.write_text('\n'.join(line for line in path.read_text().splitlines()
                                  if 'InternalsVisibleTo("ViajantesTurismo.' not in line) + '\n')
    for path in (worktree / 'src/SharedKernel').rglob('*.csproj'):
        lines = path.read_text().splitlines()
        path.write_text('\n'.join(line for line in lines
                                  if '<InternalsVisibleTo Include="ViajantesTurismo.' not in line) + '\n')
    scripts = worktree / 'scripts/sharedkernel'
    shutil.copytree(Path(__file__).parent, scripts, ignore=shutil.ignore_patterns('__pycache__'))
    workflows = worktree / '.github/workflows'
    workflows.mkdir(parents=True)
    shutil.copyfile(scripts / 'library-ci.yml', workflows / 'ci.yml')
    shutil.copyfile(source / 'docs/sharedkernel/development.md', worktree / 'README.md')
    provenance = worktree / 'provenance'
    provenance.mkdir()
    shutil.copyfile(destination / '.git/filter-repo/commit-map', provenance / 'commit-map')
    (provenance / 'extraction-manifest.json').write_bytes(manifest_bytes)
    (provenance / 'source.json').write_text(json.dumps({
        'sourceRevision': revision,
        'filteredRevision': run('git', 'rev-parse', 'HEAD', cwd=destination),
        'manifestSha256': hashlib.sha256(manifest_bytes).hexdigest(),
        'filterRepoVersion': run(filter_repo, '--version'),
        'adaptations': ['standalone solution', 'test repository discovery', 'SBOM title',
                        'application friend removal', 'private development scripts and CI'],
        'scriptSha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                         for p in sorted(scripts.glob('*')) if p.is_file()},
        'consumerCompatibility': 'pending',
        'stablePromotion': 'pending'
    }, indent=2) + '\n')
    print(worktree)
    return worktree


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('destination', type=Path)
    parser.add_argument('--source', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--manifest', type=Path, default=Path(__file__).with_name('extraction-manifest.json'))
    parser.add_argument('--filter-repo', default='git-filter-repo')
    options = parser.parse_args()
    extract(options.source, options.destination, options.manifest, options.filter_repo)
