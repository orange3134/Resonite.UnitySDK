import json
from pathlib import Path
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest import mock

import backend


class PipelineTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def test_modes_and_paths_are_distinct_argv_without_shell_quoting(self):
        source = Path('C:/絵/space & name/作品.glb')
        common = (Path(sys.executable), self.root, source, self.root / 'new bundle', None)
        animated = backend.prepare_command(*common, animated=True)
        completed = backend.prepare_command(*common, animated=False)
        self.assertIn(str(source), animated)
        self.assertNotIn('--completed', animated)
        self.assertIn('--completed', completed)
        command = backend.send_command(Path('C:/Unity.exe'), self.root, self.root / 'bundle', 12575, '作品 & test', self.root/'report.json', self.root/'log', self.root/'progress')
        self.assertEqual(command[command.index('-openbrushName')+1], '作品 & test')
        self.assertNotIn('-nographics', command)

    def test_incremental_progress_handles_split_unicode_and_invalid_lines(self):
        path = self.root / 'progress'
        raw = (backend.PREFIX + json.dumps(dict(stage='convert', detail='変換中'), ensure_ascii=False) + '\n').encode()
        tail = backend.ProgressTail(path, backend.PREFIX)
        self.assertEqual(tail.read(), [])
        path.write_bytes(raw[:35])
        self.assertEqual(tail.read(), [])
        with path.open('ab') as stream:
            stream.write(raw[35:] + b'noise\nOPENBRUSH_PROGRESS broken\n')
        self.assertEqual(tail.read()[0]['detail'], '変換中')
        self.assertEqual(tail.read(), [])

    def test_success_requires_report_and_exit_status(self):
        path = self.root / 'report.json'
        with self.assertRaises(backend.ImportFailure):
            backend.require_report(path, 0)
        path.write_text(json.dumps(dict(completed=False, error='failed', rootId='test')))
        with self.assertRaisesRegex(backend.ImportFailure, '途中'):
            backend.require_report(path, 0)
        path.write_text(json.dumps(dict(completed=True)))
        with self.assertRaises(backend.ImportFailure):
            backend.require_report(path, 1)
        self.assertTrue(backend.require_report(path, 0)['completed'])

    def test_sdk_lock_rejects_concurrent_instances_and_releases(self):
        with backend.import_lock(self.root):
            with self.assertRaises(backend.ImportFailure):
                with backend.import_lock(self.root):
                    pass
        with backend.import_lock(self.root):
            pass

    def test_subprocess_reports_progress_and_returns_failure(self):
        events = []
        runner = backend.Pipeline(self.root, events.append)
        code = runner.run_child([sys.executable, '-c', "import json;print('OPENBRUSH_PROGRESS '+json.dumps({'stage':'convert','current':1,'total':2}));raise SystemExit(7)"], self.root/'test.log', prefix=backend.PREFIX)
        self.assertEqual(code, 7)
        self.assertEqual(events[0]['current'], 1)

    def test_cancel_stops_owned_subprocess_and_does_not_report_success(self):
        events = []
        runner = backend.Pipeline(self.root, events.append)
        def work():
            try:
                runner.run_child([sys.executable, '-c', 'import time;time.sleep(60)'], self.root/'cancel.log')
            except backend.Cancelled:
                events.append('cancelled')
        thread = threading.Thread(target=work)
        thread.start()
        deadline = time.monotonic()+5
        while runner.process is None and time.monotonic()<deadline:
            time.sleep(.02)
        child = runner.process
        self.assertIsNotNone(child)
        runner.cancel(); thread.join(25)
        self.assertFalse(thread.is_alive())
        self.assertIsNotNone(child.poll())
        self.assertEqual(events, ['cancelled'])

    def test_closed_port_is_an_actionable_error(self):
        with socket.socket() as sock:
            sock.bind(('localhost', 0))
            port = sock.getsockname()[1]
            with self.assertRaisesRegex(backend.ImportFailure, str(port)):
                backend.check_connection(port)

    def test_validation_rejects_corrupt_glb_before_unity(self):
        path = self.root/'bad.glb';path.write_bytes(b'not a glb')
        with self.assertRaisesRegex(backend.ImportFailure, 'GLB'):
            backend.validate(dict(source=str(path)), self.root)


if __name__ == '__main__':
    unittest.main()
