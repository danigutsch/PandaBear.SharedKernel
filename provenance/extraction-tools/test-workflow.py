#!/usr/bin/env python3
"""Regression checks for standalone selection and extraction boundaries."""
import importlib.util
import tempfile
import unittest
from pathlib import Path


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


selector = load('selector', 'select-projects.py')
extractor = load('extractor', 'extract.py')


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.projects = ['src/Core/Core.csproj', 'src/Tool/Tool.csproj',
                         'tests/Core.Tests/Core.Tests.csproj', 'tests/Package.Tests/Package.Tests.csproj',
                         'tests/Other.Tests/Other.Tests.csproj']
        for project in self.projects:
            path = self.root / project
            path.parent.mkdir(parents=True)
            refs = '<ProjectReference Include="../Core/Core.csproj" />' if '/Tool/' in project else ''
            if '/Core.Tests/' in project:
                refs = '<ProjectReference Include="../../src/Core/Core.csproj" />'
            tests = '<PackageReference Include="xunit.v3.mtp-v2" />' if project.startswith('tests/') else ''
            path.write_text(f'<Project><ItemGroup>{refs}{tests}</ItemGroup></Project>')
        self.manifest = {'projects': self.projects, 'paths': ['src/', 'tests/'],
                         'packageConsumptionEdges': {self.projects[3]: [self.projects[0]]}}

    def test_reverse_dependencies_and_explicit_package_edges(self):
        builds, tests, reasons = selector.select(self.root, self.manifest, ['src/Core/Deleted.cs'])
        self.assertEqual(builds, sorted(self.projects[:4]))
        self.assertEqual(tests, sorted(self.projects[2:4]))
        self.assertTrue(reasons)

    def test_rename_selects_old_and_new_owners(self):
        builds, tests, _ = selector.select(self.root, self.manifest,
                                          ['src/Core/Old.cs', 'tests/Other.Tests/New.cs'])
        self.assertEqual(builds, sorted(self.projects))
        self.assertEqual(tests, sorted(self.projects[2:]))

    def test_missing_history_and_global_inputs_select_every_project(self):
        for changes in [None, ['Directory.Packages.props'], ['unknown.file'], ['src/custom.targets']]:
            with self.subTest(changes=changes):
                builds, tests, _ = selector.select(self.root, self.manifest, changes)
                self.assertEqual(builds, sorted(self.projects))
                self.assertEqual(tests, sorted(self.projects[2:]))

    def test_unevaluated_import_and_reference_fall_back(self):
        for xml in ['<Project><Import Project="custom.build" /></Project>',
                    '<Project><ProjectReference Include="$(Unknown)/Project.csproj" /></Project>']:
            (self.root / self.projects[1]).write_text(xml)
            builds, _, reasons = selector.select(self.root, self.manifest, ['src/Core/Changed.cs'])
            self.assertEqual(builds, sorted(self.projects))
            self.assertIn('fallback', reasons[0])

    def test_manifest_rejects_cross_boundary_reference(self):
        extractor.validate_manifest(self.root, self.manifest)
        self.manifest['projects'].remove(self.projects[0])
        with self.assertRaisesRegex(ValueError, 'crosses boundary'):
            extractor.validate_manifest(self.root, self.manifest)

    def test_extraction_refuses_existing_destination_before_mutation(self):
        with self.assertRaisesRegex(ValueError, 'must not exist'):
            extractor.extract(self.root, self.root, self.root / 'missing.json', 'missing-filter-repo')


if __name__ == '__main__':
    unittest.main()
