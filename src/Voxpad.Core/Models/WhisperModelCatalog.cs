namespace Voxpad.Core.Models;

public static class WhisperModelCatalog
{
    public static IReadOnlyList<WhisperModelInfo> Default { get; } =
    [
        new WhisperModelInfo(
            id: "tiny",
            displayName: "Tiny (multilingual)",
            fileName: "ggml-tiny.bin",
            downloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
            sha256: "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21",
            sizeBytes: 77691713,
            language: "multilingual",
            isMultilingual: true),
        new WhisperModelInfo(
            id: "tiny.en",
            displayName: "Tiny (English)",
            fileName: "ggml-tiny.en.bin",
            downloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin",
            sha256: "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f",
            sizeBytes: 77704715,
            language: "en",
            isMultilingual: false),
        new WhisperModelInfo(
            id: "base",
            displayName: "Base (multilingual)",
            fileName: "ggml-base.bin",
            downloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            sha256: "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe",
            sizeBytes: 147951465,
            language: "multilingual",
            isMultilingual: true),
        new WhisperModelInfo(
            id: "base.en",
            displayName: "Base (English)",
            fileName: "ggml-base.en.bin",
            downloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",
            sha256: "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002",
            sizeBytes: 147964211,
            language: "en",
            isMultilingual: false),
        new WhisperModelInfo(
            id: "small",
            displayName: "Small (multilingual)",
            fileName: "ggml-small.bin",
            downloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            sha256: "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b",
            sizeBytes: 487601967,
            language: "multilingual",
            isMultilingual: true),
        new WhisperModelInfo(
            id: "small.en",
            displayName: "Small (English)",
            fileName: "ggml-small.en.bin",
            downloadUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin",
            sha256: "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d",
            sizeBytes: 487614201,
            language: "en",
            isMultilingual: false)
    ];
}
