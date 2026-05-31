# YOLO Models

This directory holds ONNX model files used for player detection.  
Model files are **not committed** (see `.gitignore`) — download them separately.

## Required: yolo11n.onnx

### Option A — Python export (recommended)
```bash
pip install ultralytics
python -c "from ultralytics import YOLO; YOLO('yolo11n.pt').export(format='onnx', opset=17)"
cp yolo11n.onnx src/PickleIQ.Infrastructure/Models/
```

### Option B — Direct download
Download `yolo11n.onnx` from:
https://github.com/ultralytics/assets/releases

Place the file in this directory.

## Configuration

The model path is configurable in `appsettings.json`:
```json
"YoloModel": {
  "Path": "path/to/custom/model.onnx"
}
```

If not set, defaults to `Models/yolo11n.onnx` relative to the application base directory.

## License Note

Ultralytics YOLO models are AGPL-3.0 licensed for open source use.  
A commercial license is required for commercial deployment.  
See: https://www.ultralytics.com/license
