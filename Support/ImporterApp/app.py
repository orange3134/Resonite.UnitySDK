"""Open Brush → Resonite. Native Windows desktop UI, standard-library only."""
import os
from pathlib import Path
import queue
import threading
import time
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from backend import Pipeline, read_settings, save_settings, SDK

BG = '#F4F3ED'
INK = '#203630'
MUTED = '#708079'
ACCENT = '#176B52'
LINE = '#D9DED5'
ERROR = '#A33B2B'


class App:
    def __init__(self, root, sdk=SDK, pipeline_factory=Pipeline):
        self.root, self.sdk, self.pipeline_factory = root, Path(sdk), pipeline_factory
        self.settings = read_settings(self.sdk)
        self.events = queue.Queue()
        self.pipeline = None
        self.thread = None
        self.busy = False
        self.started = 0
        self.run_dir = None
        self.indeterminate = False
        self.stage = 'idle'
        self.source = tk.StringVar(value=self.settings['source'])
        self.port = tk.StringVar(value=self.settings['port'])
        self.animated = tk.BooleanVar(value=self.settings['animated'])
        self.combine_meshes = tk.BooleanVar(value=self.settings['combine_meshes'])
        root.title('Open Brush → Resonite')
        root.geometry('760x650')
        root.minsize(700, 630)
        root.configure(bg=BG)
        root.protocol('WM_DELETE_WINDOW', self.close)
        root.option_add('*Font', ('Yu Gothic UI', 11))
        style = ttk.Style(root)
        style.theme_use('clam')
        style.configure('TFrame', background=BG)
        style.configure('TLabel', background=BG, foreground=INK)
        style.configure('TButton', background='#E7EAE2', foreground=INK, borderwidth=0, padding=(16, 10), focusthickness=2, focuscolor=ACCENT)
        style.map('TButton', background=[('active', '#DCE3D8'), ('disabled', '#E5E6DF')], foreground=[('disabled', '#9AABA2')])
        style.configure('Primary.TButton', background=ACCENT, foreground='white', font=('Yu Gothic UI', 12, 'bold'), padding=(20, 13))
        style.map('Primary.TButton', background=[('active', '#0E543F'), ('disabled', '#BCCAC1')], foreground=[('disabled', '#F5F6F2')])
        style.configure('Mode.TButton', background='#E7EAE2', padding=(22, 10))
        style.map('Mode.TButton', background=[('selected', INK), ('active', '#DCE3D8')], foreground=[('selected', 'white')])
        style.configure('TCheckbutton', background=BG, foreground=INK, padding=(0, 2))
        style.map('TCheckbutton', foreground=[('disabled', '#8D9A92')])
        style.configure('TEntry', fieldbackground='#FFFFFF', foreground=INK, bordercolor=LINE, lightcolor=LINE, darkcolor=LINE, padding=10)
        style.configure('Horizontal.TProgressbar', background=ACCENT, troughcolor='#E2E6DC', borderwidth=0, thickness=6)

        frame = ttk.Frame(root, padding=(36, 28))
        frame.pack(fill='both', expand=True)
        frame.columnconfigure(0, weight=1)
        header = ttk.Frame(frame)
        header.grid(row=0, column=0, sticky='ew', pady=(0, 28))
        ttk.Label(header, text='Open Brush  →  Resonite', font=('Segoe UI', 22, 'bold')).pack(side='left')
        self.settings_button = ttk.Button(header, text='設定', command=self.preferences, padding=(10, 6))
        self.settings_button.pack(side='right')

        ttk.Label(frame, text='GLBファイル', font=('Yu Gothic UI', 10, 'bold')).grid(row=1, column=0, sticky='w', pady=(0, 8))
        file_row = ttk.Frame(frame)
        file_row.grid(row=2, column=0, sticky='ew', pady=(0, 25))
        file_row.columnconfigure(0, weight=1)
        self.file_entry = ttk.Entry(file_row, textvariable=self.source, width=20)
        self.file_entry.grid(row=0, column=0, sticky='ew', padx=(0, 10))
        self.browse_button = ttk.Button(file_row, text='選択…', command=self.browse)
        self.browse_button.grid(row=0, column=1)
        options = ttk.Frame(frame)
        options.grid(row=3, column=0, sticky='ew', pady=(0, 26))
        options.columnconfigure(1, weight=1)
        ttk.Label(options, text='ResoniteLink ポート', font=('Yu Gothic UI', 10, 'bold')).grid(row=0, column=0, sticky='w', pady=(0, 8))
        self.port_entry = ttk.Entry(options, textvariable=self.port, width=12)
        self.port_entry.grid(row=1, column=0, sticky='w')
        modes = ttk.Frame(options)
        modes.grid(row=0, column=1, rowspan=2, sticky='e')
        ttk.Label(modes, text='書き順アニメーション', font=('Yu Gothic UI', 10, 'bold')).grid(row=0, column=0, columnspan=2, sticky='w', pady=(0, 8))
        self.off = ttk.Radiobutton(modes, text='なし', variable=self.animated, value=False, style='Mode.TButton')
        self.on = ttk.Radiobutton(modes, text='あり', variable=self.animated, value=True, style='Mode.TButton')
        self.off.grid(row=1, column=0, padx=(0, 2))
        self.on.grid(row=1, column=1)
        self.combine_check = ttk.Checkbutton(options, text='メッシュを結合', variable=self.combine_meshes)
        self.combine_check.grid(row=2, column=0, columnspan=2, sticky='w', pady=(14, 0))

        self.start_button = ttk.Button(frame, text='読み込み', style='Primary.TButton', command=self.start)
        self.start_button.grid(row=4, column=0, sticky='ew')
        tk.Frame(frame, background=LINE, height=1).grid(row=5, column=0, sticky='ew', pady=(25, 20))

        steps = ttk.Frame(frame)
        steps.grid(row=6, column=0, sticky='ew', pady=(0, 16))
        self.step_labels = []
        for n, label in enumerate(['01  変換', '02  準備', '03  送信']):
            steps.columnconfigure(n, weight=1)
            widget = ttk.Label(steps, text=label, foreground=MUTED, font=('Yu Gothic UI', 11, 'bold'))
            widget.grid(row=0, column=n, sticky='w' if n == 0 else 'e' if n == 2 else '')
            self.step_labels.append(widget)
        self.bar = ttk.Progressbar(frame, maximum=100, mode='determinate')
        self.bar.grid(row=7, column=0, sticky='ew', pady=(0, 13))
        status_row = ttk.Frame(frame)
        status_row.grid(row=8, column=0, sticky='ew')
        self.status = ttk.Label(status_row, text='待機中', font=('Yu Gothic UI', 11, 'bold'))
        self.status.pack(side='left')
        self.elapsed = ttk.Label(status_row, text='', foreground=MUTED)
        self.elapsed.pack(side='right')
        self.detail = ttk.Label(frame, text='', foreground=MUTED, wraplength=640, justify='left')
        self.detail.grid(row=9, column=0, sticky='nw', pady=(8, 8))
        frame.rowconfigure(9, weight=1)
        footer = ttk.Frame(frame)
        footer.grid(row=10, column=0, sticky='ew')
        self.log_button = ttk.Button(footer, text='ログ', command=self.logs, padding=(12, 5), state='disabled')
        self.log_button.pack(side='left')
        self.stop_button = ttk.Button(footer, text='中止', command=self.cancel, padding=(12, 5), state='disabled')
        self.stop_button.pack(side='right')
        self.inputs = [self.file_entry, self.browse_button, self.port_entry, self.off, self.on, self.combine_check, self.settings_button, self.start_button]
        self.animated.trace_add('write', lambda *_: self.update_combine_state())
        self.update_combine_state()
        root.bind('<Control-o>', lambda _: self.browse() if not self.busy else None)
        root.bind('<Return>', lambda _: self.start() if not self.busy and root.focus_get() in [self.file_entry, self.port_entry, self.start_button] else None)
        self.pump_id = root.after(150, self.pump)
        self.file_entry.focus_set()
        self.file_entry.xview_moveto(1)

    def update_combine_state(self):
        self.combine_check.state(['disabled'] if self.busy or self.animated.get() else ['!disabled'])

    def browse(self):
        if self.busy:
            return
        filename = filedialog.askopenfilename(parent=self.root, title='Open Brush のGLBを選択', filetypes=[('GLB', '*.glb')], initialdir=str(Path(self.source.get()).parent) if self.source.get() else None)
        if filename:
            self.source.set(filename)
            self.file_entry.xview_moveto(1)

    def values(self):
        return dict(self.settings, source=self.source.get(), port=self.port.get(), animated=self.animated.get(), combine_meshes=self.combine_meshes.get())

    def start(self):
        if self.busy:
            return
        settings = self.values()
        try:
            # Fast validation; network/GLB work stays in the worker.
            if not settings['source'].strip():
                raise ValueError('GLBファイルを選択してください。')
            if not 1 <= int(settings['port']) <= 65535:
                raise ValueError('ポートは1〜65535で指定してください。')
            save_settings(settings, self.sdk)
        except (ValueError, OSError) as error:
            self.status.configure(text='入力を確認してください', foreground=ERROR)
            self.detail.configure(text=str(error), foreground=ERROR)
            return
        self.busy, self.started, self.run_dir = True, time.monotonic(), None
        for widget in self.inputs:
            widget.state(['disabled'])
        self.stop_button.state(['!disabled'])
        self.log_button.state(['disabled'])
        self.detail.configure(text='', foreground=MUTED)
        self.status.configure(foreground=INK)
        self.apply_event(dict(stage='connect', detail='接続を確認中'))
        self.pipeline = self.pipeline_factory(self.sdk, self.events.put)
        self.thread = threading.Thread(target=self.pipeline.run, args=(settings,), daemon=False)
        self.thread.start()

    def apply_event(self, event):
        self.stage = event['stage']
        if event.get('run_dir'):
            self.run_dir = Path(event['run_dir'])
            self.log_button.state(['!disabled'])
        phase = {'connect': -1, 'convert': 0, 'unity': 1, 'send': 2, 'done': 3}.get(self.stage, -1)
        for index, label in enumerate(self.step_labels):
            label.configure(foreground=ACCENT if index <= phase else MUTED)
        self.bar.stop()
        current, total = event.get('current', 0), event.get('total', 0)
        if self.stage == 'done':
            self.bar.configure(mode='determinate', value=100)
        elif total > 0:
            self.bar.configure(mode='determinate', value=min(100, current / total * 100))
        elif self.stage not in ('error', 'cancelled'):
            self.bar.configure(mode='indeterminate', value=0)
            self.bar.start(15)
        else:
            self.bar.configure(mode='determinate', value=0)
        self.status.configure(text=event.get('detail', ''), foreground=ERROR if self.stage == 'error' else INK)
        if total > 0:
            self.detail.configure(text=f'{current:,} / {total:,}   {current / total:.0%}', foreground=MUTED)
        elif self.stage == 'done':
            report = event.get('report', {})
            adjustments = report.get('bakeAdjustments') or []
            summary = report.get('name', '')
            combined = report.get('meshCombine')
            if combined:
                summary += f"\nメッシュ {combined['uniqueMeshesBefore']:,} → {combined['uniqueMeshesAfter']:,}"
            if adjustments:
                summary += '\n一部のブラシで解像度を調整しました。詳細はログに保存しています。'
            self.detail.configure(text=summary, foreground=MUTED)
        elif self.stage in ('error', 'cancelled'):
            error = event.get('error', '')
            self.detail.configure(text=error[:260] + ('…（続きはログ）' if len(error) > 260 else ''), foreground=ERROR if self.stage == 'error' else MUTED)
        else:
            self.detail.configure(text='', foreground=MUTED)

    def pump(self):
        while True:
            try:
                self.apply_event(self.events.get_nowait())
            except queue.Empty:
                break
        if self.busy:
            duration = int(time.monotonic() - self.started)
            self.elapsed.configure(text=f'{duration // 60:02d}:{duration % 60:02d}')
            if self.thread and not self.thread.is_alive():
                self.busy = False
                self.bar.stop()
                if self.stage == 'done':
                    self.bar.configure(mode='determinate', value=100)
                for widget in self.inputs:
                    widget.state(['!disabled'])
                self.update_combine_state()
                self.stop_button.state(['disabled'])
                if self.stage not in ('done', 'error', 'cancelled'):
                    self.apply_event(dict(stage='error', detail='処理が終了しました', error='完了状態を確認できません。ログを確認してください。'))
        self.pump_id = self.root.after(150, self.pump)

    def cancel(self):
        if self.busy and messagebox.askyesno('読み込みを中止', '処理を中止しますか？\n送信中の場合、ワールドに途中までのモデルが残ることがあります。', parent=self.root):
            self.pipeline.cancel()
            self.stop_button.state(['disabled'])
            self.status.configure(text='中止しています…')

    def close(self):
        if self.busy:
            self.cancel()
            return
        try:
            save_settings(self.values(), self.sdk)
        except OSError:
            pass
        self.root.after_cancel(self.pump_id)
        self.root.destroy()

    def preferences(self):
        dialog = tk.Toplevel(self.root)
        dialog.title('設定')
        dialog.configure(bg=BG)
        dialog.transient(self.root)
        dialog.resizable(False, False)
        body = ttk.Frame(dialog, padding=24)
        body.pack(fill='both', expand=True)
        variables = {}
        for index, (key, label) in enumerate([('unity', 'Unity.exe'), ('brushes', 'ブラシ素材フォルダー')]):
            variables[key] = tk.StringVar(value=self.settings[key])
            ttk.Label(body, text=label).grid(row=index * 2, column=0, sticky='w', pady=(10, 6))
            entry = ttk.Entry(body, textvariable=variables[key], width=54)
            entry.grid(row=index * 2 + 1, column=0, padx=(0, 8))
            entry.xview_moveto(1)
            def choose(k=key):
                path = filedialog.askdirectory(parent=dialog) if k == 'brushes' else filedialog.askopenfilename(parent=dialog, filetypes=[('Unity', 'Unity.exe')])
                if path:
                    variables[k].set(path)
            ttk.Button(body, text='選択…', command=choose).grid(row=index * 2 + 1, column=1)
        def save():
            settings = dict(self.values(), **{key: variable.get() for key, variable in variables.items()})
            try:
                save_settings(settings, self.sdk)
                self.settings = settings
                dialog.destroy()
            except OSError as error:
                messagebox.showerror('設定を保存できません', str(error), parent=dialog)
        ttk.Button(body, text='保存', style='Primary.TButton', command=save).grid(row=5, column=0, columnspan=2, sticky='e', pady=(22, 0))
        dialog.grab_set()

    def logs(self):
        if not self.run_dir:
            return
        dialog = tk.Toplevel(self.root)
        dialog.title('読み込みログ')
        dialog.geometry('900x560')
        dialog.configure(bg=BG)
        tabs = ttk.Notebook(dialog)
        tabs.pack(fill='both', expand=True, padx=12, pady=12)
        for name, label in [('conversion.log', '変換'), ('unity.log', 'Unity'), ('send-report.json', '結果')]:
            pane = ttk.Frame(tabs)
            text = tk.Text(pane, background='#FAFAF6', foreground=INK, wrap='word', font=('Consolas', 10))
            scrollbar = ttk.Scrollbar(pane, command=text.yview)
            text.configure(yscrollcommand=scrollbar.set)
            scrollbar.pack(side='right', fill='y')
            text.pack(fill='both', expand=True)
            path = self.run_dir / name
            try:
                with path.open('rb') as stream:
                    stream.seek(max(0, path.stat().st_size - 150000))
                    content = stream.read().decode('utf-8-sig', errors='replace')
            except OSError:
                content = 'まだログはありません。'
            text.insert('1.0', content)
            text.configure(state='disabled')
            text.see('end')
            tabs.add(pane, text=label)
        ttk.Button(dialog, text='フォルダーを開く', command=lambda: os.startfile(str(self.run_dir))).pack(anchor='e', padx=12, pady=(0, 12))


def main():
    if os.name == 'nt':
        import ctypes
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except OSError:
            pass
    root = tk.Tk()
    App(root)
    root.mainloop()


if __name__ == '__main__':
    main()
