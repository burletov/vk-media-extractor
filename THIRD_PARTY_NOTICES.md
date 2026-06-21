# Third-Party Notices

Media Extractor for VK uses third-party software and model files. Their licenses and model cards apply in addition to this project's MIT License.

## ONNX Runtime

- Project: [Microsoft ONNX Runtime](https://github.com/microsoft/onnxruntime)
- Package: `Microsoft.ML.OnnxRuntime`
- License: MIT

## CLIP

- Project: [OpenAI CLIP](https://github.com/openai/CLIP)
- License for the CLIP repository: MIT
- Model card: [openai/clip-vit-base-patch32](https://huggingface.co/openai/clip-vit-base-patch32)

The CLIP model card describes important limitations, biases, and the need for task-specific evaluation. Media Extractor for VK therefore presents recognition only as probabilistic hints and does not automatically delete or move files based on a model result.

## ONNX model distributions

- Provider: [Xenova on Hugging Face](https://huggingface.co/Xenova)
- Bundled model source: [Xenova/clip-vit-base-patch32](https://huggingface.co/Xenova/clip-vit-base-patch32)
- Optional model sources:
  - [Xenova/clip-vit-base-patch16](https://huggingface.co/Xenova/clip-vit-base-patch16)
  - [Xenova/clip-vit-large-patch14](https://huggingface.co/Xenova/clip-vit-large-patch14)

The repository does not store ONNX weights in Git history. The build script downloads the bundled model from its upstream source.

## VK

VK names, APIs, and services belong to their respective owners. Media Extractor for VK is unofficial, is not endorsed by VK, and requires users to supply their own credentials and comply with applicable terms.
