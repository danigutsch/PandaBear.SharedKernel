#!/usr/bin/env python3
"""Restore, build and run existing samples through exact local packages only."""
import argparse
import platform
import shutil
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('feed', type=Path)
    parser.add_argument('--version', required=True)
    parser.add_argument('--aot', action='store_true')
    args = parser.parse_args()
    feed = args.feed.resolve()
    central = ET.parse('Directory.Packages.props')
    external_versions = {p.get('Include'): p.get('Version') for p in central.iter('PackageVersion')}
    for sample in ['samples/Results/BasicResults.Sample', 'samples/Mediator/Mediator.Sample']:
        source = Path(sample)
        target = feed / 'sample-checks' / source.name
        # Refuse cache reuse so an old package cannot turn this check green.
        cache = feed / 'sample-check-caches' / source.name
        if target.exists() or cache.exists():
            raise ValueError(f'Sample check already exists: {target}')
        shutil.copytree(source, target, ignore=shutil.ignore_patterns('bin', 'obj', 'Generated', 'packages.lock.json'))
        project_file = next(target.glob('*.csproj'))
        project = ET.parse(project_file)
        versions = {}
        for group in project.getroot():
            for item in list(group):
                if item.tag == 'ProjectReference':
                    package_id = Path(item.get('Include').replace('\\', '/')).stem
                    group.remove(item)
                    ET.SubElement(group, 'PackageReference', Include=package_id)
                    versions[package_id] = '[' + args.version + ']'
                elif item.tag == 'PackageReference':
                    package_id = item.get('Include')
                    versions[package_id] = external_versions[package_id]
        props = ET.SubElement(project.getroot(), 'PropertyGroup')
        ET.SubElement(props, 'TargetFramework').text = 'net10.0'
        ET.SubElement(props, 'ImplicitUsings').text = 'enable'
        ET.SubElement(props, 'Nullable').text = 'enable'
        ET.SubElement(props, 'TreatWarningsAsErrors').text = 'true'
        project.write(project_file, encoding='unicode')
        (target / '.editorconfig').write_text('root = true\n')
        (target / 'Directory.Build.props').write_text('<Project />\n')
        (target / 'Directory.Build.targets').write_text('<Project />\n')
        package_props = ET.Element('Project')
        properties = ET.SubElement(package_props, 'PropertyGroup')
        ET.SubElement(properties, 'ManagePackageVersionsCentrally').text = 'true'
        group = ET.SubElement(package_props, 'ItemGroup')
        for package_id, version in versions.items():
            ET.SubElement(group, 'PackageVersion', Include=package_id, Version=version)
        ET.ElementTree(package_props).write(target / 'Directory.Packages.props', encoding='unicode')
        config = ET.Element('configuration')
        sources = ET.SubElement(config, 'packageSources')
        ET.SubElement(sources, 'clear')
        ET.SubElement(sources, 'add', key='local', value=str(feed))
        ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
        mapping = ET.SubElement(config, 'packageSourceMapping')
        local = ET.SubElement(mapping, 'packageSource', key='local')
        ET.SubElement(local, 'package', pattern='SharedKernel.*')
        public = ET.SubElement(mapping, 'packageSource', key='nuget.org')
        ET.SubElement(public, 'package', pattern='*')
        ET.SubElement(ET.SubElement(config, 'fallbackPackageFolders'), 'clear')
        ET.ElementTree(config).write(target / 'NuGet.config', encoding='unicode')
        for command in [
            ['dotnet', 'restore', str(project_file), '--configfile', str(target / 'NuGet.config'),
             '--packages', str(cache), '--no-cache', '--use-lock-file'],
            ['dotnet', 'restore', str(project_file), '--configfile', str(target / 'NuGet.config'),
             '--packages', str(cache), '--locked-mode'],
            ['dotnet', 'build', str(project_file), '-c', 'Release', '--no-restore', '-m:2'],
            ['dotnet', 'run', '--project', str(project_file), '-c', 'Release', '--no-build', '--no-restore']
        ]:
            subprocess.run(command, check=True)
        if args.aot and source.name == 'Mediator.Sample':
            rid = 'win-x64' if platform.system() == 'Windows' else 'linux-x64'
            output = target / 'native'
            subprocess.run(['dotnet', 'publish', str(project_file), '-c', 'Release', '-r', rid,
                            '-p:PublishAot=true', '--configfile', str(target / 'NuGet.config'),
                            '--packages', str(cache), '-o', str(output), '-m:2'], check=True)
            binary = output / ('Mediator.Sample.exe' if platform.system() == 'Windows' else 'Mediator.Sample')
            subprocess.run([str(binary)], check=True)
