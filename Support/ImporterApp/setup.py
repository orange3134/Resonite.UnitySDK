"""Clone-to-GUI setup. Invoked by setup.ps1 after finding a usable Python.

No Resonite world writes. Existing installations and import settings are retained.
"""
import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import uuid
import winreg

from backend import SDK, HIDDEN, import_lock, read_settings, save_settings


class SetupError(Exception):
    pass


def say(message):
    print(message, flush=True)


def run(command, log, timeout=1800):
    """Wait only for our own child; retain diagnostics and show elapsed progress."""
    log = Path(log)
    log.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    with log.open('wb') as output:
        child = subprocess.Popen([str(x) for x in command], stdout=output, stderr=subprocess.STDOUT,
                                 stdin=subprocess.DEVNULL, creationflags=HIDDEN,
                                 env=dict(os.environ, PYTHONUTF8='1', PYTHONUNBUFFERED='1'))
        try:
            while True:
                try:
                    return child.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    elapsed = int(time.monotonic() - started)
                    say(f'  処理中 {elapsed // 60:02d}:{elapsed % 60:02d}  ログ: {log.name}')
                    if elapsed >= timeout:
                        raise SetupError(f'処理がタイムアウトしました。ログ: {log}')
        finally:
            if child.poll() is None:
                # Terminate only this setup's owned process tree, including its installers.
                subprocess.run(['taskkill.exe', '/PID', str(child.pid), '/T', '/F'],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=HIDDEN)
                child.wait(timeout=30)


def capture(command, timeout=20):
    try:
        result = subprocess.run([str(x) for x in command], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                stdin=subprocess.DEVNULL, creationflags=HIDDEN, timeout=timeout,
                                env=dict(os.environ, PYTHONUTF8='1'))
        return result.stdout.decode('utf-8-sig', errors='replace').strip() if result.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None


def read_json(path):
    try:
        return json.loads(Path(path).read_text(encoding='utf-8-sig'))
    except (OSError, ValueError):
        return None


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, path)


def project_version(sdk):
    text = (sdk / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8-sig')
    match = re.search(r'm_EditorVersionWithRevision:\s*(\d+\.\d+\.\d+[fpab]\d+)\s*\(([a-f0-9]{12})\)', text)
    if not match:
        raise SetupError('ProjectVersion.txtのUnityバージョンとリビジョンを確認できません。')
    return match.groups()


def registry_values(root, path, names):
    try:
        with winreg.OpenKey(root, path) as key:
            for name in names:
                try:
                    value = winreg.QueryValueEx(key, name)[0]
                    if isinstance(value, str) and value:
                        yield value
                except OSError:
                    pass
    except OSError:
        pass


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


def unity_candidates(version, settings, install_home):
    if settings.get('unity'):
        yield Path(settings['unity'])
    yield install_home / 'Unity' / version / 'Editor/Unity.exe'
    for prefix in [os.environ.get('ProgramFiles', 'C:/Program Files'), os.environ.get('ProgramW6432', 'C:/Program Files')]:
        yield Path(prefix) / 'Unity/Hub/Editor' / version / 'Editor/Unity.exe'
        yield Path(prefix) / 'Unity/Editor/Unity.exe'
    hub = Path(os.environ['APPDATA']) / 'UnityHub'
    for filename in ['editors-v2.json', 'editors.json', 'secondaryInstallPath.json']:
        for value in strings(read_json(hub / filename)):
            if not Path(value).is_absolute():
                continue
            p = Path(value)
            if p.suffix.lower() == '.exe':
                yield p
            else:
                yield p / 'Editor/Unity.exe'
                yield p / 'Unity.exe'
                yield p / version / 'Editor/Unity.exe'
    for hive in [winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE]:
        for value in registry_values(hive, r'Software\Microsoft\Windows\CurrentVersion\App Paths\Unity.exe', ['']):
            yield Path(value.strip('"'))
        # Both standalone Editor installations and Hub-managed custom locations.
        for base in [r'Software\Microsoft\Windows\CurrentVersion\Uninstall', r'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall']:
            try:
                with winreg.OpenKey(hive, base) as key:
                    for i in range(winreg.QueryInfoKey(key)[0]):
                        name = winreg.EnumKey(key, i)
                        if 'unity' not in name.lower():
                            continue
                        for value in registry_values(hive, base + '\\' + name, ['InstallLocation', 'DisplayIcon']):
                            match = re.match(r'"?(.+?\.exe)(?:"|,|$)', value, flags=re.I)
                            yield Path(match[1]) if match else Path(value) / 'Editor/Unity.exe'
            except OSError:
                pass


def unity_matches(path, version, changeset):
    if not path.is_file():
        return False
    # Read executable metadata without starting an Editor or contacting licensing.
    script = '$v=(Get-Item -LiteralPath $env:OPENBRUSH_UNITY_PROBE).VersionInfo.ProductVersion; Write-Output $v'
    env = dict(os.environ, OPENBRUSH_UNITY_PROBE=str(path))
    try:
        result = subprocess.run(['powershell.exe', '-NoProfile', '-Command', script], capture_output=True,
                                timeout=15, creationflags=HIDDEN, env=env)
        product = result.stdout.decode(errors='replace').strip()
        return result.returncode == 0 and product == f'{version}_{changeset}'
    except (OSError, subprocess.TimeoutExpired):
        return False


def find_unity(version, changeset, settings, install_home, explicit=None):
    candidates = [Path(explicit)] if explicit else unity_candidates(version, settings, install_home)
    for path in dict.fromkeys(candidates):
        if unity_matches(path, version, changeset):
            return path.resolve()
    if explicit:
        raise SetupError(f'指定したUnityは {version} ({changeset}) ではありません: {explicit}')
    return None


def find_git():
    candidates = [shutil.which('git.exe'), str(Path(os.environ['ProgramFiles']) / 'Git/cmd/git.exe'),
                  str(Path(os.environ['LOCALAPPDATA']) / 'Programs/Git/cmd/git.exe')]
    for candidate in candidates:
        if candidate and capture([candidate, '--version']):
            return Path(candidate)
    return None


def find_hub():
    for path in [Path(os.environ['ProgramFiles']) / 'Unity Hub/Unity Hub.exe',
                 Path(os.environ['LOCALAPPDATA']) / 'Programs/Unity Hub/Unity Hub.exe']:
        if path.is_file():
            return path
    return None


def find_brushes(sdk, settings):
    # Respect an existing user choice; texture availability is checked per GLB.
    if settings.get('brushes') and Path(settings['brushes']).is_dir():
        return Path(settings['brushes']).resolve()
    candidates = [sdk / 'Support/shaders/brushes', sdk.parent / 'Builds/OpenXR_Release_OpenBrush/Support/shaders/brushes']
    steam_roots = [Path(os.environ.get('ProgramFiles(x86)', 'C:/Program Files (x86)')) / 'Steam']
    steam_roots += [Path(v) for v in registry_values(winreg.HKEY_CURRENT_USER, r'Software\Valve\Steam', ['SteamPath'])]
    for steam in list(steam_roots):
        try:
            text = (steam / 'steamapps/libraryfolders.vdf').read_text(encoding='utf-8')
            steam_roots += [Path(p.replace('\\\\', '\\')) for p in re.findall(r'"path"\s+"([^"]+)"', text)]
        except OSError:
            pass
    for steam in steam_roots:
        candidates.append(steam / 'steamapps/common/Open Brush/Support/shaders/brushes')
    return next((p.resolve() for p in candidates if p.is_dir() and any(p.rglob('*.png'))), None)


def probe_environment(python):
    code = 'import sys,struct,tkinter; assert (3,10)<=sys.version_info[:2]<(3,15) and struct.calcsize("P")==8; r=tkinter.Tk(); r.withdraw(); r.destroy(); print("OK")'
    return capture([python, '-I', '-c', code]) == 'OK'


def requirements_satisfied(python, requirements):
    code = ('import sys; from importlib.metadata import version; from pip._vendor.packaging.requirements import Requirement; '
            'reqs=[Requirement(s) for s in open(sys.argv[1],encoding="utf-8-sig") if s.strip() and not s.lstrip().startswith("#")]; '
            'assert all((r.marker and not r.marker.evaluate()) or version(r.name) in r.specifier for r in reqs); '
            'import numpy; print("OK")')
    return capture([python, '-I', '-c', code, requirements]) == 'OK'


def ensure_environment(sdk, logs, no_install):
    folder = sdk / '.venv-openbrush'
    python = folder / 'Scripts/python.exe'
    if not probe_environment(python):
        if no_install:
            raise SetupError('変換用Python環境がありません。通常の起動ファイルから実行してください。')
        if folder.exists():
            # Preserve a copied/broken venv; never recursively delete user files.
            if folder.is_symlink() or folder.resolve().parent != sdk.resolve():
                raise SetupError('Python環境のリンク先がSDK外です。環境を手動で確認してください。')
            backup = sdk / ('.venv-openbrush.broken-' + uuid.uuid4().hex[:8])
            folder.rename(backup)
            say(f'既存のPython環境を退避: {backup.name}')
        say('変換用Python環境を作成しています…')
        base = getattr(sys, '_base_executable', sys.executable)
        if run([base, '-m', 'venv', folder], logs / 'venv.log') != 0 or not probe_environment(python):
            raise SetupError(f'Python環境を作成できません。ログ: {logs / "venv.log"}')
    else:
        say('Python環境: 既存のものを使用')
    requirements = sdk / 'Support/Python/requirements-brush-animation.txt'
    if not requirements_satisfied(python, requirements):
        if no_install:
            raise SetupError('必要なPythonライブラリが不足しています。')
        say('必要なPythonライブラリをインストールしています…')
        if run([python, '-m', 'ensurepip'], logs / 'ensurepip.log') != 0:
            raise SetupError(f'pipを準備できません。ログ: {logs}')
        if run([python, '-m', 'pip', 'install', '--disable-pip-version-check', '-r', requirements], logs / 'pip.log') != 0 or not requirements_satisfied(python, requirements):
            raise SetupError(f'Pythonライブラリを準備できません。ログ: {logs / "pip.log"}')
    else:
        say('Pythonライブラリ: インストール済み')
    return python


def setup_fingerprint(sdk, unity):
    digest = hashlib.sha256((str(sdk.resolve()) + str(unity.resolve())).encode('utf-8'))
    paths = [sdk / 'Packages/manifest.json', sdk / 'Packages/packages-lock.json', sdk / 'ProjectSettings/ProjectVersion.txt']
    paths += [p for p in (sdk / 'Assets').rglob('*') if p.is_file() and
              p.suffix.lower() in {'.cs', '.shader', '.compute', '.asmdef', '.asmref', '.dll', '.meta'} and
              'OpenBrushGenerated' not in p.relative_to(sdk).parts]
    for path in sorted(paths):
        if path.is_file():
            digest.update(str(path.relative_to(sdk)).encode('utf-8'))
            digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def ready_cache(sdk, unity, fingerprint):
    state = read_json(sdk / '.local/importer/setup-ready.json') or {}
    return (state.get('fingerprint') == fingerprint and state.get('unity') == str(unity) and
            (sdk / 'Library/ScriptAssemblies/Assembly-CSharp-Editor.dll').is_file())


def install(kind, sdk, logs, args, version='', changeset=''):
    if args.no_install:
        raise SetupError(f'{kind}が見つかりません。自動インストールは無効になっています。')
    say(f'{kind}を準備しています。ダウンロードや管理者確認に時間がかかる場合があります。')
    command = ['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
               sdk / 'Support/ImporterApp/setup-install.ps1', '-Kind', kind]
    if version:
        command += ['-Version', version, '-Changeset', changeset]
    log = logs / f'install-{kind}.log'
    if run(command, log, timeout=7200) != 0:
        tail = log.read_text(encoding='utf-8-sig', errors='replace')[-2000:]
        raise SetupError(f'{kind}のインストールに失敗しました。\n{tail}\nログ: {log}')


def warmup(sdk, unity, logs, args):
    lock = sdk / 'Temp/UnityLockfile'
    if lock.exists():
        try:
            with lock.open('r+b'):
                pass
        except OSError:
            raise SetupError('このSDKを開いているUnityを閉じてから、起動ファイルを再実行してください。')
    fingerprint = setup_fingerprint(sdk, unity)
    if not args.force_initialize and ready_cache(sdk, unity, fingerprint):
        say('Unityプロジェクト: 初期化済み')
        return
    say('Unityプロジェクトを初期化しています（初回のパッケージ取得・コンパイル）…')
    for attempt in range(2):
        receipt = logs / f'unity-ready-{attempt}.json'
        log = logs / f'unity-{attempt}.log'
        code = run([unity, '-batchmode', '-projectPath', sdk, '-executeMethod', 'OpenBrushSetupCheck.Run',
                    '-openbrushSetupReceipt', receipt, '-logFile', log], logs / f'unity-process-{attempt}.log', timeout=1800)
        result = read_json(receipt) or {}
        if code == 0 and result.get('completed') is True and result.get('unityVersion') == project_version(sdk)[0]:
            # Package resolution may update packages-lock.json during first import.
            write_json(sdk / '.local/importer/setup-ready.json', dict(unity=str(unity), fingerprint=setup_fingerprint(sdk, unity)))
            return
        text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
        needs_license = re.search(r'No valid Unity Editor license|No valid license|license.*(?:not active|not found|activation|failed)|Licensing.*(?:failed|error)', text, re.I)
        if attempt == 0 and needs_license and not args.non_interactive:
            hub = find_hub()
            if not hub:
                install('Hub', sdk, logs, args)
                hub = find_hub()
            if not hub:
                raise SetupError('Unity Hubを開き、ログインとライセンス認証を済ませてから再実行してください。')
            # This is deliberately visible: the user must authenticate themselves.
            subprocess.Popen([str(hub)], creationflags=HIDDEN)
            say('Unity Hubでログインし、ライセンスを認証してください。完了したらここへ戻ってください。')
            input('Enterで初期化を再試行します（中断はCtrl+C）: ')
            continue
        raise SetupError(f'Unityの初期化が完了しませんでした。ライセンス・通信・コンパイルエラーを確認してください。\nログ: {log}')


def setup(args, sdk=SDK):
    sdk = Path(sdk).resolve()
    logs = sdk / '.local/importer/setup-logs' / (datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
    logs.mkdir(parents=True)
    say(f'セットアップログ: {logs}')
    with import_lock(sdk):
        python = ensure_environment(sdk, logs, args.no_install)
        settings = read_settings(sdk)
        version, changeset = project_version(sdk)
        install_home = Path(os.environ['LOCALAPPDATA']) / 'OpenBrushImporter'
        unity = find_unity(version, changeset, settings, install_home, args.unity)
        if unity is None:
            install('Unity', sdk, logs, args, version, changeset)
            unity = find_unity(version, changeset, settings, install_home)
        if unity is None:
            raise SetupError(f'Unity {version}を検出できません。-UnityPathでUnity.exeを指定して再実行できます。')
        say(f'Unity: {unity}')
        git = find_git()
        if git is None:
            install('Git', sdk, logs, args)
            git = find_git()
        if git is None:
            raise SetupError('Gitを検出できません。Gitをインストールして再実行してください。')
        os.environ['PATH'] = str(git.parent) + os.pathsep + os.environ.get('PATH', '')
        say(f'Git: {git}')
        warmup(sdk, unity, logs, args)
        brushes = Path(args.brush_assets).resolve() if args.brush_assets else find_brushes(sdk, settings)
        if args.brush_assets and not brushes.is_dir():
            raise SetupError(f'ブラシ素材フォルダーが見つかりません: {brushes}')
        if not brushes and not args.non_interactive:
            import tkinter as tk
            from tkinter import filedialog
            root = tk.Tk(); root.withdraw()
            try:
                chosen = filedialog.askdirectory(parent=root, title='Open BrushのSupport/shaders/brushesを選択（後で設定する場合はキャンセル）')
                brushes = Path(chosen) if chosen else None
                if brushes and (brushes / 'Support/shaders/brushes').is_dir():
                    brushes = brushes / 'Support/shaders/brushes'
            finally:
                root.destroy()
        settings.update(unity=str(unity), brushes=str(brushes) if brushes else '')
        save_settings(settings, sdk)
        if brushes:
            say(f'ブラシ素材: {brushes}')
        else:
            say('ブラシ素材は未設定です。外部テクスチャを使うGLBは、GUIの設定で素材フォルダーを指定してください。')
        launcher = sdk / 'OpenBrush Importer.exe'
        source = sdk / 'Support/ImporterApp/Launcher.cs'
        if not launcher.is_file() or launcher.stat().st_mtime < source.stat().st_mtime:
            say('起動用exeを作成しています…')
            code = run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', sdk / 'Support/ImporterApp/build.ps1'], logs / 'launcher.log')
            if code != 0:
                say(f'exeを作れなかったためPythonからGUIを起動します。ログ: {logs / "launcher.log"}')
        say('セットアップ完了。次回も同じ起動ファイルを使えます。')
    if not args.no_launch:
        subprocess.Popen([str(python.with_name('pythonw.exe')), str(sdk / 'Support/ImporterApp/app.py')],
                         cwd=sdk, creationflags=HIDDEN)
        say('GUIを起動しました。')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--unity')
    parser.add_argument('--brush-assets')
    parser.add_argument('--no-install', action='store_true')
    parser.add_argument('--no-launch', action='store_true')
    parser.add_argument('--non-interactive', action='store_true')
    parser.add_argument('--force-initialize', action='store_true')
    try:
        setup(parser.parse_args())
        return 0
    except (SetupError, OSError, KeyboardInterrupt) as error:
        say(f'セットアップを中断しました: {error}\n解決後、Start-OpenBrush-Importer.cmdをもう一度実行してください。')
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
