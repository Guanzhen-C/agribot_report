"""Optional app-owned HIL paths; legacy independent experiments keep defaults."""
import os
from pathlib import Path


def scene_root(role='pc'):
    value = os.environ.get('AGRIBOT_HIL_SCENE_ROOT')
    if value:
        root = Path(value).resolve()
        return root/'rdk' if role == 'rdk' else root
    return Path.home()/('agribot_hil_ws/scenario' if role == 'rdk' else '.cache/agribot/hil_apple')
