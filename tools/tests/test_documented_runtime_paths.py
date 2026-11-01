from pathlib import Path
import os
import subprocess


ROOT = Path(__file__).resolve().parents[2]


def test_manual_matches_delivered_assets_and_entrypoint_paths(tmp_path):
    manual = (ROOT / 'docs/13_user_manual.md').read_text()
    assert '部分完整车库资源仍未补传' not in manual
    assert '~/.local/share/agribot/scanned_scenes' not in manual
    assert '~/.local/state/agribot-unity-sim' not in manual
    assert '.local/scanned_scenes/' in manual
    assert '.local/state/agribot-unity-sim/' in manual
    assert '$XDG_STATE_HOME/agribot-unity-sim/latest.log' in manual
    env = dict(os.environ)
    for key in ('AGRIBOT_REPORT_STATE_HOME', 'AGRIBOT_SCANNED_SCENE_ROOT'):
        env.pop(key, None)
    command = 'source scripts/env.sh; printf "%s\\n%s\\n" "$XDG_STATE_HOME" "$AGRIBOT_SCANNED_SCENE_ROOT"'
    result = subprocess.run(['bash', '-c', command], cwd=ROOT, env=env,
                            capture_output=True, text=True, check=True)
    assert result.stdout.splitlines() == [str(ROOT / '.local/state'), str(ROOT / '.local/scanned_scenes')]
    env.update(AGRIBOT_REPORT_STATE_HOME=str(tmp_path / 'state'),
               AGRIBOT_SCANNED_SCENE_ROOT=str(tmp_path / 'scenes'))
    result = subprocess.run(['bash', '-c', command], cwd=ROOT, env=env,
                            capture_output=True, text=True, check=True)
    assert result.stdout.splitlines() == [str(tmp_path / 'state'), str(tmp_path / 'scenes')]
