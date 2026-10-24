"""Run all shipped visual AI modes on the installed package's public sample."""
import hashlib
import json
import os
from pathlib import Path
import sys
import time

import cv2
import torch
import ultralytics

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'ros2_ws/src/agribot_visual_perception'))
from agribot_visual_perception.algorithms import VisualProcessor


def main():
    torch.set_num_threads(4)
    sample = Path(ultralytics.__file__).parent / 'assets/bus.jpg'
    image = cv2.imread(str(sample))
    if image is None:
        raise SystemExit('缺少已安装视觉包中的公开验证样图。')
    output = ROOT / '.validation/vision_actual'
    output.mkdir(parents=True, exist_ok=True)
    results = []
    model_dir = ROOT / 'resources/vision_models'
    for mode in ('object_detection', 'instance_segmentation', 'pose_estimation'):
        start = time.monotonic()
        processor = VisualProcessor(mode, model_dir=str(model_dir), device=os.environ.get('AGRIBOT_VISION_DEVICE', 'cpu'))
        result = processor.process(image)
        if result.image.shape != image.shape or not result.objects:
            raise RuntimeError('真实模型推理没有产生预期的图像或目标：' + mode)
        if mode == 'instance_segmentation' and not any(obj.get('mask_polygon') for obj in result.objects):
            raise RuntimeError('没有产生实例分割轮廓')
        if mode == 'pose_estimation' and not any(obj.get('keypoints') for obj in result.objects):
            raise RuntimeError('没有产生姿态关键点')
        cv2.imwrite(str(output / (mode + '.png')), result.image)
        path = processor.resolve_model_path(mode, str(model_dir), '')
        results.append(dict(mode=mode, objects=len(result.objects), metric_name=result.metric_name,
            checkpoint_sha256=hashlib.sha256(path.read_bytes()).hexdigest(), seconds=time.monotonic()-start))
    summary = dict(ok=True, backend='actual_ultralytics_models', torch=torch.__version__,
        package=ultralytics.__version__, sample_source='installed ultralytics/assets/bus.jpg', results=results)
    (output / 'result.json').write_text(json.dumps(summary, indent=2) + '\n')
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    main()
