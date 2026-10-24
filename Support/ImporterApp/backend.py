"""Local desktop pipeline. No shell interpolation, HTTP server or third-party GUI runtime."""
import base64
from contextlib import contextmanager
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import struct
import subprocess
import sys
import threading
import time
import uuid

SDK = Path(__file__).resolve().parents[2]
PREFIX = 'OPENBRUSH_PROGRESS '
HIDDEN = getattr(subprocess, 'CREATE_NO_WINDOW', 0)


class ImportFailure(Exception):
    pass


class Cancelled(Exception):
    pass


def defaults(sdk=SDK):
    version = (sdk / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8').splitlines()[0].split(':', 1)[1].strip()
    return dict(source='', port='', animated=False,
                unity=str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Unity/Hub/Editor' / version / 'Editor/Unity.exe'),
                brushes=str(sdk.parent / 'Builds/OpenXR_Release_OpenBrush/Support/shaders/brushes'))


def read_settings(sdk=SDK):
    result = defaults(sdk)
    try:
        value = json.loads((sdk / '.local/importer/settings.json').read_text(encoding='utf-8'))
        result.update({k: v for k, v in value.items() if k in result and isinstance(v, type(result[k]))})
    except (OSError, ValueError, AttributeError):
        pass
    return result


def save_settings(value, sdk=SDK):
    path = sdk / '.local/importer/settings.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(uuid.uuid4().hex + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, path)


def validate(value, sdk=SDK):
    source = Path(value['source'].strip().strip('"')).expanduser().resolve()
    if source.suffix.lower() != '.glb' or not source.is_file():
        raise ImportFailure('GLBファイルを選択してください。')
    with source.open('rb') as stream:
        header = stream.read(12)
    if len(header) != 12 or struct.unpack('<4sII', header) != (b'glTF', 2, source.stat().st_size):
        raise ImportFailure('有効なGLB 2.0ファイルではありません。')
    try:
        port = int(value['port'])
        if not 1 <= port <= 65535:
            raise ValueError()
    except (ValueError, TypeError):
        raise ImportFailure('ポートは1〜65535で指定してください。') from None
    unity = Path(value['unity']).expanduser().resolve()
    if not unity.is_file():
        raise ImportFailure('Unityが見つかりません。設定でUnity.exeを選択してください。')
    python = sdk / '.venv-openbrush/Scripts/python.exe'
    if not python.is_file():
        raise ImportFailure('変換用Pythonが見つかりません。SDKのセットアップ手順を確認してください。')
    # An empty setting is allowed for GLBs containing all textures.
    brushes = Path(value['brushes']).expanduser().resolve() if value.get('brushes') else None
    if brushes and not brushes.is_dir():
        raise ImportFailure('ブラシ素材が見つかりません。設定でbrushesフォルダーを選択してください。')
    if (sdk / 'Temp/UnityLockfile').exists():
        try:
            with (sdk / 'Temp/UnityLockfile').open('r+b'):
                pass
        except OSError:
            raise ImportFailure('このSDKを開いているUnityを閉じてから再実行してください。') from None
    return source, port, unity, python, brushes


def check_connection(port):
    """Check a local WebSocket upgrade before spending time on conversion."""
    key = base64.b64encode(os.urandom(16)).decode('ascii')
    expected = base64.b64encode(hashlib.sha1((key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').encode()).digest()).decode()
    try:
        with socket.create_connection(('localhost', port), timeout=4) as sock:
            sock.settimeout(4)
            request = f'GET / HTTP/1.1\r\nHost: localhost:{port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: {key}\r\n\r\n'
            sock.sendall(request.encode('ascii'))
            response = b''
            while b'\r\n\r\n' not in response and len(response) < 16384:
                part = sock.recv(1024)
                if not part:
                    break
                response += part
            lines = response.decode('latin1').split('\r\n')
            headers = dict((k.strip().lower(), v.strip()) for k, v in (line.split(':', 1) for line in lines[1:] if ':' in line))
            if len(lines[0].split()) < 2 or lines[0].split()[1] != '101' or headers.get('sec-websocket-accept') != expected:
                raise ValueError('Not a WebSocket endpoint')
            # Masked empty close frame; do not send any world mutations.
            sock.sendall(b'\x88\x80' + os.urandom(4))
    except (OSError, ValueError) as error:
        raise ImportFailure(f'ポート {port} に接続できません。対象ワールドのResoniteLinkを確認してください。') from error


@contextmanager
def import_lock(sdk):
    import msvcrt
    path = sdk / '.local/importer/import.lock'
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('a+b') as stream:
        stream.seek(0, 2)
        if stream.tell() == 0:
            stream.write(b'0'); stream.flush()
        stream.seek(0)
        try:
            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        except OSError:
            raise ImportFailure('別の読み込みが実行中です。完了してから再実行してください。') from None
        try:
            yield
        finally:
            stream.seek(0)
            msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)


class ProgressTail:
    def __init__(self, path, prefix=''):
        self.path, self.prefix, self.offset, self.pending = Path(path), prefix, 0, b''

    def read(self):
        try:
            with self.path.open('rb') as stream:
                stream.seek(self.offset)
                chunk = stream.read()
                self.offset = stream.tell()
        except OSError:
            return []
        lines = (self.pending + chunk).split(b'\n')
        self.pending = lines.pop()
        results = []
        for raw in lines:
            line = raw.decode('utf-8', errors='replace').strip()
            if self.prefix and not line.startswith(self.prefix):
                continue
            try:
                item = json.loads(line[len(self.prefix):])
                if isinstance(item, dict) and isinstance(item.get('stage'), str):
                    results.append(item)
            except ValueError:
                pass
        return results


def prepare_command(python, sdk, source, bundle, brushes, animated):
    command = [str(python), '-u', str(sdk / 'Support/Python/prepare-openbrush-bundle.py'), str(source), str(bundle), '--progress-json']
    if brushes:
        command += ['--brush-assets', str(brushes)]
    if not animated:
        command.append('--completed')
    return command


def send_command(unity, sdk, bundle, port, name, report, log, progress):
    return [str(unity), '-batchmode', '-projectPath', str(sdk), '-executeMethod', 'OpenBrushSendModel.Run',
            '-openbrushBundle', str(bundle / 'bundle.json'), '-openbrushPort', str(port), '-openbrushName', name,
            '-openbrushPosition', '0,0,2', '-openbrushReport', str(report), '-openbrushProgress', str(progress), '-logFile', str(log)]


def require_report(path, code):
    try:
        report = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    except (OSError, ValueError):
        raise ImportFailure(f'Unityが完了レポートを作成せず終了しました（終了コード {code}）。ログを確認してください。') from None
    if code != 0 or report.get('completed') is not True:
        detail = report.get('error') or 'SDKが送信完了を確認できませんでした。'
        if report.get('rootId'):
            detail += '\nワールドに途中まで作成されたモデルが残っている可能性があります。'
        raise ImportFailure(detail)
    return report


class Pipeline:
    def __init__(self, sdk=SDK, emit=lambda event: None):
        self.sdk, self.emit = Path(sdk), emit
        self.cancelled = threading.Event()
        self.process = None
        self.run_dir = None
        self.report_path = None

    def cancel(self):
        self.cancelled.set()

    def checkpoint(self):
        if self.cancelled.is_set():
            raise Cancelled()

    def stop_child(self):
        child = self.process
        if child is not None and child.poll() is None:
            # Only this job's child PID and descendants; never kill by process name.
            subprocess.run(['taskkill.exe', '/PID', str(child.pid), '/T', '/F'], creationflags=HIDDEN,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=20, check=False)
            child.wait(timeout=20)

    def run_child(self, command, log, progress=None, prefix=''):
        self.checkpoint()
        env = dict(os.environ, PYTHONUTF8='1', PYTHONUNBUFFERED='1')
        tail = ProgressTail(progress or log, prefix)
        with Path(log).open('wb') as output:
            self.process = subprocess.Popen(command, cwd=self.sdk, env=env, stdout=output, stderr=subprocess.STDOUT,
                                            stdin=subprocess.DEVNULL, creationflags=HIDDEN)
            try:
                while self.process.poll() is None:
                    for event in tail.read():
                        self.emit(event)
                    self.checkpoint()
                    self.cancelled.wait(.2)
                self.checkpoint()
                for event in tail.read():
                    self.emit(event)
                return self.process.returncode
            finally:
                self.stop_child()
                self.process = None

    def run(self, settings):
        try:
            with import_lock(self.sdk):
                source, port, unity, python, brushes = validate(settings, self.sdk)
                self.emit(dict(stage='connect', detail='ResoniteLinkに接続中'))
                check_connection(port)
                self.checkpoint()
                stamp = datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8]
                self.run_dir = self.sdk / '.local/importer/runs' / stamp
                self.run_dir.mkdir(parents=True)
                name = re.sub(r'[^\w\-.]', '_', source.stem, flags=re.UNICODE)[:80] or 'sketch'
                mode = 'animated' if settings['animated'] else 'completed'
                bundle = self.sdk.parent / 'OpenBrushImports' / f'{name}-{mode}-{stamp}'
                self.report_path = self.run_dir / 'send-report.json'
                self.emit(dict(stage='convert', detail='GLBを読み込み中', run_dir=str(self.run_dir)))
                code = self.run_child(prepare_command(python, self.sdk, source, bundle, brushes, settings['animated']),
                                      self.run_dir / 'conversion.log', prefix=PREFIX)
                if code != 0:
                    lines = (self.run_dir / 'conversion.log').read_text(encoding='utf-8', errors='replace').splitlines()
                    errors = [line for line in lines if not line.startswith(PREFIX)]
                    raise ImportFailure('\n'.join(errors[-12:]) or f'変換に失敗しました（終了コード {code}）。')
                if not (bundle / 'bundle.json').is_file():
                    raise ImportFailure('変換後のbundle.jsonがありません。')
                self.emit(dict(stage='unity', detail='Unityを起動中'))
                code = self.run_child(send_command(unity, self.sdk, bundle, port, source.stem + ' ' + mode,
                                                   self.report_path, self.run_dir / 'unity.log', self.run_dir / 'progress.jsonl'),
                                      self.run_dir / 'unity-process.log', self.run_dir / 'progress.jsonl')
                result = require_report(self.report_path, code)
                self.emit(dict(stage='done', detail='読み込み完了', report=result, run_dir=str(self.run_dir), bundle=str(bundle)))
        except Cancelled:
            partial = False
            if self.report_path and self.report_path.exists():
                try:
                    partial = bool(json.loads(self.report_path.read_text()).get('rootId'))
                except (OSError, ValueError):
                    partial = True
            self.emit(dict(stage='cancelled', detail='中止しました', error='ワールドに途中まで作成されたモデルが残っている可能性があります。' if partial else ''))
        except Exception as error:
            self.emit(dict(stage='error', detail='読み込みに失敗しました', error=str(error), run_dir=str(self.run_dir or '')))
