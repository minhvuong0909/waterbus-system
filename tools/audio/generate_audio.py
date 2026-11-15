"""Sinh MP3 (vi + en) cho thông báo bến và thuyết minh địa danh.

Chạy một lần khi đổi nội dung (cần Internet): `pip install edge-tts mutagen` rồi `python tools/audio/generate_audio.py`.
Đầu vào : tools/audio/texts.json
Đầu ra  : waterbus-be/src/WaterbusSystem.Infrastructure/Persistence/SeedData/Audio/*.mp3 + manifest.json
Seeder (ApplicationDbContextInitializer.SeedAudioClipsAsync) nạp manifest.json + MP3 vào bảng AudioClips.
"""
import asyncio
import json
from pathlib import Path

import edge_tts
from mutagen.mp3 import MP3

ROOT = Path(__file__).resolve().parents[2]
TEXTS = Path(__file__).with_name("texts.json")
OUT = ROOT / "waterbus-be/src/WaterbusSystem.Infrastructure/Persistence/SeedData/Audio"
VOICES = {"vi": "vi-VN-HoaiMyNeural", "en": "en-US-JennyNeural"}


async def synth(text: str, lang: str, path: Path) -> int:
    # Dịch vụ miễn phí thỉnh thoảng không trả audio: thử lại vài lần.
    for attempt in range(1, 6):
        try:
            await edge_tts.Communicate(text, VOICES[lang]).save(str(path))
            return round(MP3(str(path)).info.length)
        except edge_tts.exceptions.NoAudioReceived:
            if attempt == 5:
                raise
            await asyncio.sleep(2 * attempt)
    raise RuntimeError("unreachable")


async def main() -> None:
    texts = json.loads(TEXTS.read_text(encoding="utf-8"))
    OUT.mkdir(parents=True, exist_ok=True)
    for old in OUT.glob("*.mp3"):
        old.unlink()

    manifest = []
    for st in texts["stations"]:
        for event, template in texts["stationEvents"].items():
            clip_id = f"station-{st['code']}-{event}"
            entry = {"id": clip_id, "kind": f"Station{event.capitalize()}", "subject": st["code"], "clips": {}}
            for lang in ("vi", "en"):
                text = template[lang].format(vi=st["vi"], en=st["en"])
                seconds = await synth(text, lang, OUT / f"{clip_id}.{lang}.mp3")
                entry["clips"][lang] = {"text": text, "seconds": seconds}
            manifest.append(entry)

    for poi in texts["pois"]:
        entry = {"id": poi["id"], "kind": "Poi", "subject": poi["name"], "clips": {}}
        for lang in ("vi", "en"):
            seconds = await synth(poi[lang], lang, OUT / f"{poi['id']}.{lang}.mp3")
            entry["clips"][lang] = {"text": poi[lang], "seconds": seconds}
        manifest.append(entry)

    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"{len(manifest)} thông báo, {len(manifest) * 2} clip -> {OUT}")


if __name__ == "__main__":
    asyncio.run(main())
