import argparse
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

import backend

spec = importlib.util.spec_from_file_location('importer_setup', Path(__file__).with_name('setup.py'))
boot = importlib.util.module_from_spec(spec)
spec.loader.exec_module(boot)


class SetupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.sdk = Path(self.temp.name).resolve()
        (self.sdk / 'ProjectSettings').mkdir()
        (self.sdk / 'ProjectSettings/ProjectVersion.txt').write_text(
            'm_EditorVersion: 2022.3.62f3\nm_EditorVersionWithRevision: 2022.3.62f3 (96770f904ca7)\n')
        (self.sdk / 'Support/ImporterApp').mkdir(parents=True)
        (self.sdk / 'Support/ImporterApp/Launcher.cs').write_text('launcher')
        (self.sdk / 'OpenBrush Importer.exe').write_text('existing launcher')
        self.unity = self.sdk / 'Unity.exe'
        self.unity.write_text('placeholder; never executed')
        self.args = argparse.Namespace(no_install=False, no_launch=False, non_interactive=True,
                                       force_initialize=False, unity=None, brush_assets=None)
        self.quiet = mock.patch.object(boot, 'say')
        self.quiet.start()

    def tearDown(self):
        self.quiet.stop()
        self.temp.cleanup()

    def test_version_requires_the_pinned_changeset(self):
        self.assertEqual(boot.project_version(self.sdk), ('2022.3.62f3', '96770f904ca7'))
        (self.sdk / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f3')
        with self.assertRaises(boot.SetupError):
            boot.project_version(self.sdk)

    def test_explicit_wrong_unity_does_not_install_another_version(self):
        with mock.patch.object(boot, 'unity_matches', return_value=False):
            with self.assertRaises(boot.SetupError):
                boot.find_unity('2022.3.62f3', '96770f904ca7', {}, self.sdk, str(self.unity))

    def run_setup(self, unity_results):
        settings = backend.defaults(self.sdk)
        settings.update(source='C:/絵/作品.glb', port='12575', animated=True, combine_meshes=True,
                        brushes=str(self.sdk / 'brushes'))
        (self.sdk / 'brushes').mkdir(exist_ok=True)
        backend.save_settings(settings, self.sdk)
        with mock.patch.object(boot, 'ensure_environment', return_value=self.sdk / '.venv-openbrush/Scripts/python.exe'), \
             mock.patch.object(boot, 'find_unity', side_effect=unity_results), \
             mock.patch.object(boot, 'find_git', return_value=Path('C:/Git/cmd/git.exe')), \
             mock.patch.object(boot, 'warmup') as warm, \
             mock.patch.object(boot, 'find_brushes', return_value=self.sdk / 'brushes'), \
             mock.patch.object(boot, 'install') as install, \
             mock.patch.object(boot.subprocess, 'Popen') as launch:
            boot.setup(self.args, self.sdk)
        return install, launch, warm

    def test_existing_environment_skips_installs_preserves_preferences_and_launches(self):
        install, launch, warm = self.run_setup([self.unity])
        install.assert_not_called()
        warm.assert_called_once()
        self.assertEqual(launch.call_args.args[0][0], str(self.sdk / '.venv-openbrush/Scripts/pythonw.exe'))
        settings = backend.read_settings(self.sdk)
        self.assertEqual(settings['port'], '12575')
        self.assertEqual(settings['source'], 'C:/絵/作品.glb')
        self.assertTrue(settings['animated'] and settings['combine_meshes'])
        self.assertEqual(settings['unity'], str(self.unity))

    def test_missing_unity_installs_once_then_rechecks(self):
        install, launch, warm = self.run_setup([None, self.unity])
        self.assertEqual(install.call_count, 1)
        self.assertEqual(install.call_args.args[0], 'Unity')
        self.assertEqual(install.call_args.args[-2:], ('2022.3.62f3', '96770f904ca7'))
        launch.assert_called_once()

    def test_failed_install_never_launches_gui_or_warms_wrong_editor(self):
        with mock.patch.object(boot, 'ensure_environment'), \
             mock.patch.object(boot, 'find_unity', return_value=None), \
             mock.patch.object(boot, 'install', side_effect=boot.SetupError('download failed')), \
             mock.patch.object(boot, 'warmup') as warm, \
             mock.patch.object(boot.subprocess, 'Popen') as launch:
            with self.assertRaisesRegex(boot.SetupError, 'download failed'):
                boot.setup(self.args, self.sdk)
        warm.assert_not_called(); launch.assert_not_called()

    def test_no_install_never_invokes_installer(self):
        self.args.no_install = True
        with mock.patch.object(boot, 'run') as run:
            with self.assertRaises(boot.SetupError):
                boot.install('Unity', self.sdk, self.sdk, self.args)
        run.assert_not_called()

    def test_healthy_venv_and_requirements_skip_network(self):
        with mock.patch.object(boot, 'probe_environment', return_value=True), \
             mock.patch.object(boot, 'requirements_satisfied', return_value=True), \
             mock.patch.object(boot, 'run') as run:
            result = boot.ensure_environment(self.sdk, self.sdk, False)
        run.assert_not_called()
        self.assertEqual(result, self.sdk / '.venv-openbrush/Scripts/python.exe')

    def test_copied_broken_venv_is_preserved_before_recreation(self):
        old = self.sdk / '.venv-openbrush'
        old.mkdir(); (old / 'sentinel.txt').write_text('keep me')
        with mock.patch.object(boot, 'probe_environment', side_effect=[False, True]), \
             mock.patch.object(boot, 'requirements_satisfied', return_value=True), \
             mock.patch.object(boot, 'run', return_value=0) as run:
            boot.ensure_environment(self.sdk, self.sdk, False)
        self.assertEqual(len(list(self.sdk.glob('.venv-openbrush.broken-*'))), 1)
        self.assertEqual(next(self.sdk.glob('.venv-openbrush.broken-*/sentinel.txt')).read_text(), 'keep me')
        self.assertIn('venv', run.call_args.args[0])

    def test_failed_warmup_needs_positive_receipt_not_just_exit_zero(self):
        with mock.patch.object(boot, 'setup_fingerprint', return_value='abc'), \
             mock.patch.object(boot, 'run', return_value=0):
            with self.assertRaises(boot.SetupError):
                boot.warmup(self.sdk, self.unity, self.sdk, self.args)
        self.assertFalse((self.sdk / '.local/importer/setup-ready.json').exists())

    def test_successful_warmup_is_cached_but_missing_library_invalidates_it(self):
        (self.sdk / 'Library/ScriptAssemblies').mkdir(parents=True)
        assembly = self.sdk / 'Library/ScriptAssemblies/Assembly-CSharp-Editor.dll'
        assembly.write_bytes(b'assembly')
        def execute(command, log, timeout):
            receipt = Path(command[command.index('-openbrushSetupReceipt') + 1])
            boot.write_json(receipt, dict(completed=True, unityVersion='2022.3.62f3'))
            return 0
        with mock.patch.object(boot, 'setup_fingerprint', return_value='abc'), \
             mock.patch.object(boot, 'run', side_effect=execute) as run:
            boot.warmup(self.sdk, self.unity, self.sdk, self.args)
            boot.warmup(self.sdk, self.unity, self.sdk, self.args)
            self.assertEqual(run.call_count, 1)
            assembly.unlink()
            self.assertFalse(boot.ready_cache(self.sdk, self.unity, 'abc'))

    def test_license_failure_noninteractive_does_not_launch_hub(self):
        (self.sdk / 'unity-0.log').write_text('No valid Unity Editor license found')
        with mock.patch.object(boot, 'setup_fingerprint', return_value='abc'), \
             mock.patch.object(boot, 'run', return_value=1), \
             mock.patch.object(boot.subprocess, 'Popen') as launch:
            with self.assertRaises(boot.SetupError):
                boot.warmup(self.sdk, self.unity, self.sdk, self.args)
        launch.assert_not_called()

    def test_setup_fingerprint_changes_with_sdk_source(self):
        folder = self.sdk / 'Assets'; folder.mkdir()
        source = folder / 'a.cs'; source.write_text('before')
        before = boot.setup_fingerprint(self.sdk, self.unity)
        source.write_text('after')
        self.assertNotEqual(before, boot.setup_fingerprint(self.sdk, self.unity))

    def test_existing_brush_setting_is_not_replaced_by_discovery(self):
        brushes = self.sdk / 'custom materials'; brushes.mkdir()
        self.assertEqual(boot.find_brushes(self.sdk, dict(brushes=str(brushes))), brushes)


if __name__ == '__main__':
    unittest.main()
