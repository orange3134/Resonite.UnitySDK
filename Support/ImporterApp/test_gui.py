import json
from pathlib import Path
import tempfile
import threading
import time
import tkinter as tk
import unittest
from unittest import mock

import app
import backend


class DesktopTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.sdk = Path(self.temp.name)
        (self.sdk/'ProjectSettings').mkdir()
        (self.sdk/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f3\n')
        self.root = tk.Tk()
        self.root.withdraw()
        self.calls = 0
        self.release = threading.Event()
        self.outcome = dict(stage='done', detail='読み込み完了', report=dict(name='fixture'))
        owner = self
        class Worker:
            def __init__(self, sdk, emit):
                self.emit = emit
            def run(self, settings):
                owner.calls += 1
                owner.used_settings = settings
                self.emit(dict(stage='send', detail='アセットを送信中', current=2, total=4))
                owner.release.wait(3)
                self.emit(owner.outcome)
            def cancel(self):
                owner.release.set()
        self.ui = app.App(self.root, self.sdk, Worker)
        self.ui.source.set('C:/絵/作品.glb')
        self.ui.port.set('12575')

    def tearDown(self):
        self.release.set()
        if self.ui.thread:
            self.ui.thread.join(3)
        self.root.update_idletasks()
        self.ui.busy = False
        self.ui.close()
        self.temp.cleanup()

    def wait(self, predicate):
        end = time.monotonic()+4
        while not predicate() and time.monotonic()<end:
            self.root.update();time.sleep(.03)
        self.assertTrue(predicate())

    def test_success_reenables_inputs_and_keeps_full_bar(self):
        self.ui.on.invoke()
        self.ui.start_button.invoke()
        self.ui.start()
        self.wait(lambda: self.ui.stage=='send')
        self.assertEqual(self.calls, 1)
        self.assertTrue(self.used_settings['animated'])
        self.assertTrue(self.ui.file_entry.instate(['disabled']))
        self.assertEqual(float(self.ui.bar['value']), 50)
        self.release.set()
        self.wait(lambda: not self.ui.busy)
        self.assertEqual(self.ui.stage, 'done')
        self.assertEqual(float(self.ui.bar['value']), 100)
        self.assertTrue(self.ui.start_button.instate(['!disabled']))
        self.assertTrue(backend.read_settings(self.sdk)['animated'])

    def test_failure_is_not_success_and_retry_is_enabled(self):
        self.outcome = dict(stage='error', detail='読み込みに失敗しました', error='接続が切れました')
        self.ui.start_button.invoke();self.release.set()
        self.wait(lambda: not self.ui.busy)
        self.assertEqual(self.ui.stage, 'error')
        self.assertEqual(float(self.ui.bar['value']), 0)
        self.assertIn('接続', self.ui.detail['text'])
        self.assertTrue(self.ui.start_button.instate(['!disabled']))

    def test_combining_is_opt_in_and_only_enabled_for_completed_mode(self):
        self.assertFalse(self.ui.combine_meshes.get())
        self.ui.combine_check.invoke()
        self.assertTrue(self.ui.combine_meshes.get())
        self.ui.on.invoke()
        self.assertTrue(self.ui.combine_check.instate(['disabled']))
        self.ui.off.invoke()
        self.assertTrue(self.ui.combine_check.instate(['!disabled']))
        self.outcome['report']['meshCombine'] = dict(uniqueMeshesBefore=100, uniqueMeshesAfter=12)
        self.ui.start_button.invoke()
        self.wait(lambda: self.ui.stage=='send')
        self.assertTrue(self.ui.combine_check.instate(['disabled']))
        self.assertTrue(self.used_settings['combine_meshes'])
        self.assertFalse(self.used_settings['animated'])
        self.release.set()
        self.wait(lambda: not self.ui.busy)
        self.assertTrue(self.ui.combine_check.instate(['!disabled']))
        self.assertIn('100 → 12', self.ui.detail['text'])
        self.assertTrue(backend.read_settings(self.sdk)['combine_meshes'])

    def test_file_picker_and_bad_port_do_not_launch(self):
        with mock.patch.object(app.filedialog, 'askopenfilename', return_value='C:/新しい絵.glb'):
            self.ui.browse_button.invoke()
        self.assertEqual(self.ui.source.get(), 'C:/新しい絵.glb')
        self.ui.port.set('70000');self.ui.start_button.invoke()
        self.assertFalse(self.ui.busy)
        self.assertEqual(self.calls, 0)


if __name__ == '__main__':
    unittest.main()
