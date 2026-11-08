import asyncio
import edge_tts

async def generate():
    voice = "id-ID-ArdiNeural" # Calm, mature, professional Indonesian JARVIS persona
    text = "Halo. AIGRAV siap membantu. Apa yang ingin kita kerjakan hari ini?"
    output_path = "c:/AIGRAV/test_speech.mp3"
    communicate = edge_tts.Communicate(text, voice, rate="+0%", pitch="+0Hz")
    await communicate.save(output_path)
    print("SUCCESS: Generated " + output_path)

if __name__ == "__main__":
    asyncio.run(generate())
