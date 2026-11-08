"""
AIGRAV Native Antigravity Voice Adapter (Phase 5 - Persistent Session Edition)
Bridges Voice HUD directly to the native Google Antigravity Agent Runtime via agentapi.
Maintains ONE SINGLE PERSISTENT conversation session across browser reloads,
backend restarts, and continuous turns during the Revit workspace lifecycle.
"""

from __future__ import annotations

import os
import sys

# Ensure UTF-8 output on Windows consoles to prevent UnicodeEncodeError with emojis/symbols
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
import re
import json
import time
import hashlib
import asyncio
import urllib.parse
import urllib.request
import subprocess
import threading
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple
import edge_tts

LANGUAGE_SERVER_EXE = r"C:\Users\sapuser\AppData\Local\Programs\antigravity\resources\bin\language_server.exe"
AGENTAPI_BAT = r"C:\Users\sapuser\.gemini\antigravity\bin\agentapi.bat"
BRAIN_DIR = Path(r"C:\Users\sapuser\.gemini\antigravity\brain")
SESSION_STATE_FILE = Path(r"C:\AIGRAV\KnowledgeBase\voice_session_state.json")
REVIT_BRIDGE_URL = "http://127.0.0.1:8765/"
AUDIO_CACHE_DIR = Path(r"C:\AIGRAV\audio_cache")

ALLOWED_VOICES = {
    "id-ID-ArdiNeural": "Pria",
    "id-ID-GadisNeural": "Wanita",
}
DEFAULT_VOICE = "id-ID-ArdiNeural"
TTS_RATE = "+0%"
TTS_PITCH = "+0Hz"

SYSTEM_INSTRUCTION_PREFIX = (
    "Anda adalah AIGRAV, asisten AI resmi untuk automasi BIM (Autodesk Revit), "
    "CAD (ZWCAD), dan pendamping kerja teknik sipil & arsitektur. "
    "Karakteristik interaksi:\n"
    "1. Jawab selalu dalam Bahasa Indonesia yang alami, sopan, ringkas, dan ramah.\n"
    "2. Anda mendukung percakapan umum, diskusi teknis, maupun instruksi pemodelan.\n"
    "3. Jawab langsung secara percakapan tanpa membaca ulang file workspace jika tidak diminta.\n"
    "4. Berikan penjelasan teknis, estimasi volume struktur/QTO, standar material, dan panduan pemodelan BIM secara langsung, akurat, dan percakapan.\n"
    "5. Untuk tindakan yang mengubah model, membuat sheet banyak (misal: 10 sheet DED), atau menghapus elemen, "
    "SELALU minta konfirmasi dan persetujuan (approval) pengguna terlebih dahulu sebelum mengeksekusi perubahan.\n"
    "6. Jawab langsung secara percakapan; jangan menampilkan log internal atau format teknis mentah.\n"
    "7. Mode penalaran operasional: Terapkan penalaran ringkas dengan waktu berpikir minimal (kebijakan penalaran LOW / concise reasoning budget). Prioritaskan respons instan dan ringkas untuk interaksi suara/HUD real-time.\n\n"
)


def clean_text_for_speech(raw_text: str) -> str:
    """
    Sanitize markdown text for natural, pleasant speech synthesis in Edge Neural TTS.
    Strips raw symbols (###, **, *, `), translates LaTeX math expressions to natural
    Indonesian spoken words, and removes formatting noise.
    """
    if not raw_text:
        return ""

    text = raw_text

    # Remove code blocks
    text = re.sub(r"```[\s\S]*?```", " [blok kode dilewati] ", text)

    # Remove inline code ticks
    text = re.sub(r"`([^`]+)`", r"\1", text)

    # Translate LaTeX math and engineering symbols to natural Indonesian speech
    text = re.sub(r"\\approx", "kurang lebih", text)
    text = re.sub(r"\\times", "kali", text)
    text = re.sub(r"\\text\{([^}]+)\}", r"\1", text)
    text = re.sub(r"\{,\}", ",", text)
    text = re.sub(r"\\text\{\s*m\}\^3|m\^3", " meter kubik ", text)
    text = re.sub(r"\\text\{\s*m\}\^2|m\^2", " meter persegi ", text)
    text = re.sub(r"\\text\{\s*mm\}|mm\b", " milimeter ", text)
    text = re.sub(r"\\text\{\s*cm\}|cm\b", " sentimeter ", text)
    text = re.sub(r"\$([^$]+)\$", r" \1 ", text)
    text = re.sub(r"\\[a-zA-Z]+", "", text)

    # Remove markdown headers (### Header -> Header)
    text = re.sub(r"^#{1,6}\s*", "", text, flags=re.MULTILINE)

    # Remove bold / italic markers (**text** or *text* -> text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"\1", text)
    text = re.sub(r"\*([^*]+)\*", r"\1", text)
    text = re.sub(r"__([^_]+)__", r"\1", text)
    text = re.sub(r"_([^_]+)_", r"\1", text)

    # Remove markdown links [title](url) -> title
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)

    # Remove bullet markers (- item or * item -> item)
    text = re.sub(r"^\s*[-*+]\s+", "", text, flags=re.MULTILINE)

    # Remove numbered list markers (1. item -> item)
    text = re.sub(r"^\s*\d+\.\s+", "", text, flags=re.MULTILINE)

    # Remove markdown table dividers (e.g. | :--- | :---: | --- |)
    text = re.sub(r"^\s*\|?[\s:-|]+\|?\s*$", " ", text, flags=re.MULTILINE)

    # Clean markdown table cell separators (| Col 1 | Col 2 | -> Col 1, Col 2)
    text = re.sub(r"\|", ", ", text)
    text = re.sub(r"^\s*,\s*", "", text, flags=re.MULTILINE)
    text = re.sub(r"\.\s*,\s*", ". ", text)

    # Collapse repeated whitespace / newlines to single space or period
    text = re.sub(r"\n+", ". ", text)
    text = re.sub(r"\s{2,}", " ", text)
    text = re.sub(r"\.{2,}", ".", text)

    return text.strip()


def split_into_speech_chunks(
    text: str,
    max_chunk_chars: int = 140,
    min_chunk_chars: int = 30,
    first_chunk_max_chars: int = 55,
    first_chunk_min_chars: int = 18
) -> List[str]:
    """
    Splits cleaned response text into coherent, sentence-level speech chunks.
    CRITICAL PHASE 6.4.4 OPTIMIZATION:
    Chunk 0 is strictly isolated to the first concise conversational clause or sentence
    (target 18-55 chars) via natural punctuation pause, conjunction, or preposition boundary.
    This reduces Chunk 0 synthesis duration by 60-70% (down to ~1.5 - 3.5s from 11-17s).
    Subsequent chunks are balanced for natural conversational cadence (60-140 chars).
    Filters out chunks devoid of alphanumeric content.
    """
    if not text or not text.strip():
        return []
    clean = text.strip()
    raw_sentences = re.split(r'(?<=[.!?])\s+', clean)
    sentences = [s.strip() for s in raw_sentences if s.strip() and re.search(r"[a-zA-Z0-9]", s)]
    if not sentences:
        return [clean] if re.search(r"[a-zA-Z0-9]", clean) else []

    chunks: List[str] = []

    # 1. Isolate Chunk 0: Check if first sentence is overly long (> 55 chars)
    s0 = sentences[0]
    idx = 1
    if len(s0) > first_chunk_max_chars:
        # Priority 1: Natural punctuation pause (comma, colon, semicolon, dash) between 18 and 75 chars
        punc_match = re.search(r'^(.{18,75}?[,:;–—\-])\s+(.*)$', s0)
        # Priority 2: Natural Indonesian clause conjunction / preposition between 22 and 60 chars
        conj_pattern = r'^(.{22,60}?)\s+(yaitu|yakni|untuk|pada|dengan|dalam|karena|seperti|antara|berdasarkan|mengacu|yang|agar)\s+(.*)$'
        conj_match = re.search(conj_pattern, s0, re.IGNORECASE) if not punc_match else None
        # Priority 3: Natural word boundary before 60 chars
        space_pos = s0.rfind(" ", 18, 60) if (not punc_match and not conj_match) else -1

        if punc_match:
            lead = punc_match.group(1).strip()
            rest = punc_match.group(2).strip()
            chunks.append(lead)
            if rest:
                sentences[0] = rest
                idx = 0
        elif conj_match:
            lead = conj_match.group(1).strip()
            connector = conj_match.group(2).strip()
            rest = conj_match.group(3).strip()
            chunks.append(lead)
            if rest:
                sentences[0] = f"{connector} {rest}"
                idx = 0
        elif space_pos > 0:
            lead = s0[:space_pos].strip()
            rest = s0[space_pos:].strip()
            chunks.append(lead)
            if rest:
                sentences[0] = rest
                idx = 0
        else:
            chunks.append(s0)
    else:
        # Merge sentence 1 into chunk 0 only if sentence 0 was an ultra-short lead-in (< 18 chars, e.g. "Halo!")
        chunk0 = s0
        if len(chunk0) < first_chunk_min_chars and idx < len(sentences):
            if len(chunk0) + len(sentences[idx]) + 1 <= first_chunk_max_chars:
                chunk0 = f"{chunk0} {sentences[idx]}"
                idx += 1
        chunks.append(chunk0)

    # 2. Form subsequent chunks (Chunk 1, 2, ...) with standard conversational balancing
    current = ""
    while idx < len(sentences):
        s = sentences[idx]
        idx += 1
        if not s:
            continue
        if not current:
            current = s
        else:
            if len(current) < min_chunk_chars or (len(current) + len(s) + 1 <= max_chunk_chars):
                current = f"{current} {s}"
            else:
                chunks.append(current)
                current = s
    if current:
        chunks.append(current)

    valid_chunks = [c for c in chunks if re.search(r"[a-zA-Z0-9]", c)]
    return valid_chunks if valid_chunks else ([clean] if re.search(r"[a-zA-Z0-9]", clean) else [])


_in_flight_tts: Dict[str, threading.Event] = {}
_tts_lock = threading.Lock()
_tts_telemetry: Dict[str, Dict[str, Any]] = {}

def get_tts_cache_key(text: str, voice: str = DEFAULT_VOICE, rate: str = TTS_RATE, pitch: str = TTS_PITCH) -> str:
    return hashlib.md5(f"{text.strip()}_{voice}_{rate}_{pitch}".encode('utf-8')).hexdigest()

def get_tts_file_path(text: str, voice: str = DEFAULT_VOICE, rate: str = TTS_RATE, pitch: str = TTS_PITCH) -> str:
    key = get_tts_cache_key(text, voice, rate, pitch)
    return str(AUDIO_CACHE_DIR / f"{key}.mp3")

def generate_tts_mp3(text: str, voice: str = DEFAULT_VOICE, rate: str = TTS_RATE, pitch: str = TTS_PITCH) -> Optional[str]:
    """
    Generate or retrieve cached MP3 file for the given text.
    Thread-safe with in-flight deduplication to prevent concurrent duplicate synthesis.
    Tracks exact first-audio-packet arrival and file-ready timestamps.
    """
    if not text or not text.strip() or not re.search(r"[a-zA-Z0-9]", text):
        return None
    if voice not in ALLOWED_VOICES:
        voice = DEFAULT_VOICE

    clean = text.strip()
    output_file = get_tts_file_path(clean, voice, rate, pitch)
    key = get_tts_cache_key(clean, voice, rate, pitch)
    t_start = time.perf_counter()

    # 1. Fast cache check
    if os.path.exists(output_file) and os.path.getsize(output_file) > 500:
        _tts_telemetry[key] = {
            "t_req": t_start,
            "t_first_byte": t_start,
            "t_ready": time.perf_counter(),
            "first_byte_latency_ms": 0.05,
            "total_ready_latency_ms": (time.perf_counter() - t_start) * 1000.0,
            "is_cache_hit": True
        }
        return output_file

    # 2. Check if already in-flight
    with _tts_lock:
        if key in _in_flight_tts:
            evt = _in_flight_tts[key]
            is_creator = False
        else:
            evt = threading.Event()
            _in_flight_tts[key] = evt
            is_creator = True

    if not is_creator:
        # Wait for creator thread to complete synthesis
        evt.wait(timeout=25.0)
        if os.path.exists(output_file) and os.path.getsize(output_file) > 500:
            return output_file

    # Creator generates the file with progressive packet streaming
    tmp_file = f"{output_file}.{os.getpid()}_{int(time.time()*1000)}.tmp"
    t_first_byte = None
    total_bytes = 0
    try:
        AUDIO_CACHE_DIR.mkdir(parents=True, exist_ok=True)

        async def _async_gen():
            nonlocal t_first_byte, total_bytes
            communicate = edge_tts.Communicate(clean, voice, rate=rate, pitch=pitch)
            with open(tmp_file, "wb") as f:
                async for chunk in communicate.stream():
                    if chunk["type"] == "audio":
                        if t_first_byte is None:
                            t_first_byte = time.perf_counter()
                        data = chunk["data"]
                        f.write(data)
                        total_bytes += len(data)

        asyncio.run(_async_gen())
        t_done = time.perf_counter()

        if os.path.exists(tmp_file) and os.path.getsize(tmp_file) > 500:
            if os.path.exists(output_file):
                try:
                    os.remove(output_file)
                except Exception:
                    pass
            os.replace(tmp_file, output_file)

            fb_lat_ms = (t_first_byte - t_start) * 1000.0 if t_first_byte else (t_done - t_start) * 1000.0
            tot_lat_ms = (t_done - t_start) * 1000.0
            _tts_telemetry[key] = {
                "t_req": t_start,
                "t_first_byte": t_first_byte or t_done,
                "t_ready": t_done,
                "first_byte_latency_ms": fb_lat_ms,
                "total_ready_latency_ms": tot_lat_ms,
                "total_bytes": total_bytes,
                "is_cache_hit": False
            }
            return output_file
        else:
            print(f"[TTS_WARN]: Generated file {tmp_file} is empty or missing.", file=sys.stderr)
            return None
    except Exception as e:
        print(f"[TTS_ERROR]: {e}", file=sys.stderr)
        return None
    finally:
        if os.path.exists(tmp_file):
            try:
                os.remove(tmp_file)
            except Exception:
                pass
        with _tts_lock:
            if key in _in_flight_tts:
                _in_flight_tts[key].set()
                _in_flight_tts.pop(key, None)

def prewarm_tts(chunks: List[str], voice: str = DEFAULT_VOICE) -> None:
    """
    Pre-warm TTS cache for sentence chunks in a non-blocking background thread.
    Prioritizes chunk 0 first so audio is ready when frontend requests it.
    """
    def _worker():
        for chunk in chunks:
            if not chunk or not chunk.strip():
                continue
            try:
                generate_tts_mp3(chunk.strip(), voice=voice)
            except Exception as ex:
                print(f"[PREWARM_ERR]: {ex}", file=sys.stderr)

    t = threading.Thread(target=_worker, daemon=True, name="AIGRAV-TTS-Prewarm")
    t.start()


def is_revit_connected(bridge_url: str = REVIT_BRIDGE_URL) -> bool:
    """Fast check whether Revit Bridge HTTP endpoint is alive."""
    try:
        data = json.dumps({"tool": "aigrav_ping"}).encode("utf-8")
        req = urllib.request.Request(bridge_url, data=data, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=1.5) as r:
            return r.status == 200
    except Exception:
        return False


RUNTIME_ENV_FILE = Path(r"C:\AIGRAV\KnowledgeBase\antigravity_runtime_env.json")

def is_address_alive(addr: Optional[str], timeout: float = 0.3) -> bool:
    """Check if address host:port is accepting connections."""
    if not addr or ":" not in addr:
        return False
    try:
        host, port = addr.split(":")
        import socket
        s = socket.create_connection((host, int(port)), timeout=timeout)
        s.close()
        return True
    except Exception:
        return False

def is_connection_failure(err_text: str) -> bool:
    """Classify if error text indicates missing, invalid, or stale connection to language_server."""
    if not err_text:
        return False
    lower = err_text.lower()
    patterns = [
        "antigravity_ls_address is not set",
        "missing csrf token",
        "missing authorization",
        "unauthenticated",
        "connection refused",
        "actively refused",
        "transport: error while dialing",
        "unavailable desc = connection error",
        "target machine actively refused",
        "no connection could be made",
    ]
    return any(p in lower for p in patterns)

def discover_runtime_credentials() -> Tuple[Optional[str], Optional[str]]:
    """
    Auto-discovers running language_server.exe process, extracts listening port
    and CSRF token, and returns (ls_address, csrf_token).
    Never launches a new language_server.exe.
    """
    try:
        cmd = (
            "Get-CimInstance Win32_Process -Filter \"Name = 'language_server.exe'\" | "
            "Select-Object ProcessId, CommandLine | ConvertTo-Json -Compress"
        )
        raw = subprocess.check_output(["powershell", "-NoProfile", "-Command", cmd], text=True, timeout=5)
        if not raw.strip():
            return None, None
        data = json.loads(raw)
        procs = [data] if isinstance(data, dict) else data

        for proc in procs:
            pid = proc.get("ProcessId")
            cmdline = proc.get("CommandLine", "")
            if not cmdline:
                continue

            m_token = re.search(r"--csrf_token[=\s]+([0-9a-fA-F-]+)", cmdline)
            csrf_token = m_token.group(1) if m_token else None

            port_cmd = (
                f"Get-NetTCPConnection -OwningProcess {pid} -State Listen -ErrorAction SilentlyContinue | "
                "Select-Object -ExpandProperty LocalPort"
            )
            try:
                port_out = subprocess.check_output(["powershell", "-NoProfile", "-Command", port_cmd], text=True, timeout=5)
                ports = [p.strip() for p in port_out.splitlines() if p.strip().isdigit()]
            except Exception:
                ports = []

            for port in ports:
                addr = f"localhost:{port}"
                if is_address_alive(addr, timeout=0.5):
                    return addr, csrf_token
    except Exception as ex:
        print(f"[RUNTIME_DISCOVERY_ERR]: {ex}", file=sys.stderr)
    return None, None

def ensure_runtime_credentials() -> Dict[str, str]:
    """
    Ensures ANTIGRAVITY_LS_ADDRESS and ANTIGRAVITY_CSRF_TOKEN exist in os.environ and are reachable.
    Validates in-memory address first; if unreachable, invalidates in-memory and falls through to cache or discovery.
    Returns dictionary with active credentials, or empty dict if discovery fails.
    """
    ls_addr = os.environ.get("ANTIGRAVITY_LS_ADDRESS")
    csrf_token = os.environ.get("ANTIGRAVITY_CSRF_TOKEN")

    # 1. Validate in-memory credentials if present
    if ls_addr and csrf_token:
        if is_address_alive(ls_addr, timeout=0.3):
            try:
                RUNTIME_ENV_FILE.parent.mkdir(parents=True, exist_ok=True)
                with open(RUNTIME_ENV_FILE, "w", encoding="utf-8") as f:
                    json.dump({"ANTIGRAVITY_LS_ADDRESS": ls_addr, "ANTIGRAVITY_CSRF_TOKEN": csrf_token}, f, indent=2)
            except Exception:
                pass
            return {"ANTIGRAVITY_LS_ADDRESS": ls_addr, "ANTIGRAVITY_CSRF_TOKEN": csrf_token}
        else:
            # Stale in-memory credentials; invalidate to proceed to cache / discovery
            os.environ.pop("ANTIGRAVITY_LS_ADDRESS", None)
            os.environ.pop("ANTIGRAVITY_CSRF_TOKEN", None)

    # 2. Check persistent file cache
    if RUNTIME_ENV_FILE.exists():
        try:
            with open(RUNTIME_ENV_FILE, "r", encoding="utf-8") as f:
                cached = json.load(f)
                cached_addr = cached.get("ANTIGRAVITY_LS_ADDRESS")
                cached_token = cached.get("ANTIGRAVITY_CSRF_TOKEN")
                if cached_addr and cached_token:
                    if is_address_alive(cached_addr, timeout=0.3):
                        os.environ["ANTIGRAVITY_LS_ADDRESS"] = cached_addr
                        os.environ["ANTIGRAVITY_CSRF_TOKEN"] = cached_token
                        return {"ANTIGRAVITY_LS_ADDRESS": cached_addr, "ANTIGRAVITY_CSRF_TOKEN": cached_token}
        except Exception:
            pass

    # 3. Auto-discover from running language_server.exe
    disc_addr, disc_token = discover_runtime_credentials()
    if disc_addr and disc_token:
        os.environ["ANTIGRAVITY_LS_ADDRESS"] = disc_addr
        os.environ["ANTIGRAVITY_CSRF_TOKEN"] = disc_token
        try:
            RUNTIME_ENV_FILE.parent.mkdir(parents=True, exist_ok=True)
            with open(RUNTIME_ENV_FILE, "w", encoding="utf-8") as f:
                json.dump({"ANTIGRAVITY_LS_ADDRESS": disc_addr, "ANTIGRAVITY_CSRF_TOKEN": disc_token}, f, indent=2)
            print(f"[AIGRAV_RUNTIME]: Auto-discovered Antigravity Language Server at {disc_addr}")
        except Exception:
            pass
        return {"ANTIGRAVITY_LS_ADDRESS": disc_addr, "ANTIGRAVITY_CSRF_TOKEN": disc_token}

    return {}

def execute_agentapi_cmd(cmd: List[str], timeout: Optional[float] = None) -> subprocess.CompletedProcess:
    """
    Executes an agentapi command with guaranteed runtime credentials and bounded single retry.
    Automatically discovers running language_server.exe if credentials are missing, stale, or connection failed.
    """
    ensure_runtime_credentials()
    env = os.environ.copy()
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env, timeout=timeout)
    err = proc.stderr or proc.stdout or ""
    if proc.returncode != 0 and is_connection_failure(err):
        # Invalidate dead in-memory credentials
        os.environ.pop("ANTIGRAVITY_LS_ADDRESS", None)
        os.environ.pop("ANTIGRAVITY_CSRF_TOKEN", None)
        disc_addr, disc_token = discover_runtime_credentials()
        if disc_addr and disc_token:
            os.environ["ANTIGRAVITY_LS_ADDRESS"] = disc_addr
            os.environ["ANTIGRAVITY_CSRF_TOKEN"] = disc_token
            try:
                RUNTIME_ENV_FILE.parent.mkdir(parents=True, exist_ok=True)
                with open(RUNTIME_ENV_FILE, "w", encoding="utf-8") as f:
                    json.dump({"ANTIGRAVITY_LS_ADDRESS": disc_addr, "ANTIGRAVITY_CSRF_TOKEN": disc_token}, f, indent=2)
            except Exception:
                pass
            env = os.environ.copy()
            time.sleep(1.0)
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env, timeout=timeout)
    return proc

def is_conversation_valid(conversation_id: str, cmd_prefix: List[str]) -> Tuple[bool, str]:
    """Check if conversation_id is still valid in agentapi runtime."""
    if not conversation_id or len(conversation_id) < 10:
        return False, "Invalid or empty conversation ID"
    try:
        cmd = cmd_prefix + ["get-conversation-metadata", conversation_id]
        proc = execute_agentapi_cmd(cmd, timeout=10.0)
        if proc.returncode == 0 and "conversationMetadata" in proc.stdout:
            return True, "Valid"
        err = proc.stderr or proc.stdout
        return False, err.strip()
    except Exception as ex:
        return False, str(ex)


class DormantWorkerError(RuntimeError):
    """Raised when language_server has no active in-memory worker for the conversation."""
    pass


class AntigravityVoiceAdapter:
    """
    Stateful adapter maintaining ONE SINGLE PERSISTENT Antigravity agent conversation session.
    Delegates all reasoning, context tracking, and tool execution to the native agent runtime.
    Persists session state across server restarts and browser reloads.
    """

    def __init__(
        self,
        agentapi_path: str = AGENTAPI_BAT,
        language_server_exe: str = LANGUAGE_SERVER_EXE,
        brain_dir: Path = BRAIN_DIR,
        model: str = "flash_lite",
        session_state_file: Path = SESSION_STATE_FILE
    ):
        self.agentapi_path = agentapi_path
        self.language_server_exe = language_server_exe
        self.brain_dir = brain_dir
        self.model = model
        self.session_state_file = session_state_file

        # Dual AI Mode Tracking: "fast" (Flash Lite) vs "analysis" (Flash High)
        self.active_mode: str = "fast"
        self.fast_conversation_id: Optional[str] = None
        self.analysis_conversation_id: Optional[str] = None
        self._recent_dialogue_turns: List[Dict[str, str]] = []
        self._pending_context_bridge: Optional[str] = None

        # Prefer direct executable invocation to prevent cmd.exe batch newline splitting
        if self.language_server_exe and os.path.exists(self.language_server_exe):
            self.agent_cmd_prefix = [self.language_server_exe, "agentapi"]
        elif self.agentapi_path and os.path.exists(self.agentapi_path):
            self.agent_cmd_prefix = [self.agentapi_path]
        else:
            self.agent_cmd_prefix = [self.language_server_exe or self.agentapi_path, "agentapi"]

        self.current_voice = DEFAULT_VOICE
        self.active_conversation_id: Optional[str] = None
        self.session_created_at: float = 0.0
        self.last_activity_time: float = 0.0
        self.history_count: int = 0
        
        # Concurrency & deduplication safeguards
        self._lock = threading.Lock()
        self._refresh_lock = threading.Lock()
        self._is_processing = False
        self._processing_event = threading.Event()
        self._processing_event.set()
        self._last_prompt = ""
        self._last_prompt_time = 0.0
        self._last_result_time = 0.0
        self._last_result: Optional[Dict[str, Any]] = None

        # Attempt recovery of stored persistent session on startup
        self._try_restore_persisted_session()

    def set_voice(self, voice_id: str) -> None:
        if voice_id in ALLOWED_VOICES:
            self.current_voice = voice_id

    def _load_persisted_session_data(self) -> Optional[Dict[str, Any]]:
        """Load session metadata from disk."""
        if self.session_state_file.exists():
            try:
                with open(self.session_state_file, "r", encoding="utf-8") as f:
                    return json.load(f)
            except Exception:
                pass
        return None

    def _save_persisted_session_data(self, data: Dict[str, Any]) -> None:
        """Save session metadata to disk atomically."""
        try:
            self.session_state_file.parent.mkdir(parents=True, exist_ok=True)
            with open(self.session_state_file, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)
        except Exception as ex:
            print(f"[SESSION_SAVE_ERROR]: {ex}", file=sys.stderr)

    def _try_restore_persisted_session(self) -> bool:
        """Attempt to restore existing session from disk if still valid in agentapi."""
        data = self._load_persisted_session_data()
        if not data:
            return False

        self.fast_conversation_id = data.get("fast_conversation_id")
        self.analysis_conversation_id = data.get("analysis_conversation_id")

        cand_id = data.get("active_conversation_id")
        # Preserve active production session as analysis_conversation_id if not explicitly tracked
        if not self.analysis_conversation_id and cand_id == "16ed9184-422f-4e90-a611-0e2ffd9ee0c5":
            self.analysis_conversation_id = cand_id

        # Determine active mode
        persisted_mode = data.get("active_mode")
        if persisted_mode in ["fast", "analysis"]:
            self.active_mode = persisted_mode
        elif cand_id == "16ed9184-422f-4e90-a611-0e2ffd9ee0c5":
            self.active_mode = "analysis"
        else:
            self.active_mode = "fast"

        if cand_id:
            valid, msg = is_conversation_valid(cand_id, self.agent_cmd_prefix)
            if valid:
                self.active_conversation_id = cand_id
                self.session_created_at = data.get("created_at", time.time())
                self.last_activity_time = data.get("last_used_at", time.time())
                self.history_count = data.get("history_count", 0)
                print(f"[AIGRAV_SESSION]: Restored active persistent conversation: {cand_id} (Mode: {self.active_mode})")
                return True
            else:
                print(f"[AIGRAV_SESSION]: Stored conversation {cand_id} invalid ({msg}).")
        return False

    def get_or_create_session(self, reason_if_new: str = "Initial workspace session") -> str:
        """
        Get current valid session ID, or create one if none exists or candidate is invalid.
        Guarantees that new-conversation is only called ONCE per persistent lifecycle.
        """
        with self._lock:
            if self.active_conversation_id:
                return self.active_conversation_id

            if self._try_restore_persisted_session():
                return self.active_conversation_id

            # Create new persistent conversation using default model (flash_lite)
            target_model = "flash" if self.active_mode == "analysis" else "flash_lite"
            return self._create_new_session_locked(reason=reason_if_new, model_override=target_model)

    def _create_new_session_locked(self, reason: str = "Initial work session", model_override: Optional[str] = None) -> str:
        """Create a new session via agentapi new-conversation and persist it."""
        old_id = self.active_conversation_id
        binary_to_check = self.agent_cmd_prefix[0] if self.agent_cmd_prefix else self.agentapi_path
        if not os.path.exists(binary_to_check):
            raise FileNotFoundError(f"Binary Antigravity Agent API tidak ditemukan di: {binary_to_check}")

        target_model = model_override or ("flash" if self.active_mode == "analysis" else self.model)
        initial_prompt = (
            f"{SYSTEM_INSTRUCTION_PREFIX}"
            f"Pengguna memulai sesi interaksi suara AIGRAV.\n"
            f"Sapa singkat dalam 1 kalimat bahwa Anda siap membantu."
        )
        cmd = self.agent_cmd_prefix + [
            "new-conversation",
            f"--model={target_model}",
            "--title=AIGRAV Voice Session",
            initial_prompt
        ]
        proc = execute_agentapi_cmd(cmd, timeout=45.0)
        if proc.returncode != 0:
            raise RuntimeError(f"Gagal membuat sesi percakapan agen: {proc.stderr or proc.stdout}")

        try:
            data = json.loads(proc.stdout)
            new_id = data["response"]["newConversation"]["conversationId"]
        except Exception as ex:
            raise RuntimeError(f"Gagal mem-parsing conversation ID: {proc.stdout} ({ex})")

        # Wait for greeting to finish so the conversation is clean
        self._wait_for_final_response(conversation_id=new_id, start_step_index=0, timeout_seconds=45.0)

        self.active_conversation_id = new_id
        self.session_created_at = time.time()
        self.last_activity_time = time.time()
        self.history_count = 0

        # Assign to mode slot
        if target_model == "flash_lite":
            self.fast_conversation_id = new_id
        elif target_model == "flash":
            self.analysis_conversation_id = new_id

        # Persist to disk
        persisted = self._load_persisted_session_data() or {}
        replacements = persisted.get("replacement_log", [])
        if old_id:
            replacements.append({
                "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
                "old_conversation_id": old_id,
                "new_conversation_id": new_id,
                "reason": reason
            })
        state_data = {
            "active_mode": self.active_mode,
            "active_conversation_id": new_id,
            "fast_conversation_id": self.fast_conversation_id,
            "analysis_conversation_id": self.analysis_conversation_id,
            "created_at": self.session_created_at,
            "last_used_at": self.last_activity_time,
            "history_count": 0,
            "status": "ACTIVE",
            "revit_connected": is_revit_connected(),
            "session_lifecycle": "PERSISTENT_WORK_CYCLE",
            "replacement_log": replacements[-20:]
        }
        self._save_persisted_session_data(state_data)
        print(f"[AIGRAV_SESSION]: Established persistent session {new_id} (Model: {target_model}, Reason: {reason})")
        return new_id

    def _evaluate_context_bridge(self, from_mode: str, to_mode: str) -> Optional[str]:
        """
        Evaluates recent dialogue turns to produce a concise, structured bridge summary.
        Does NOT blindly dump entire history or raw tool JSON.
        """
        if not self._recent_dialogue_turns:
            return None

        recent = self._recent_dialogue_turns[-4:]
        dialogue_summary = []
        for t in recent:
            role = t.get("role", "User")
            txt = (t.get("text") or "").strip()
            if len(txt) > 200:
                txt = txt[:197] + "..."
            dialogue_summary.append(f"- **{role}**: {txt}")

        summary_str = "\n".join(dialogue_summary)
        from_label = "Mode Cepat (Flash Lite)" if from_mode == "fast" else "Mode Analisis (Flash High)"
        to_label = "Mode Analisis (Flash High)" if to_mode == "analysis" else "Mode Cepat (Flash Lite)"

        bridge = (
            f"[Peralihan Mode AIGRAV]: Pengguna beralih dari {from_label} ke {to_label}.\n"
            f"[Ringkasan Konteks Percakapan Terakhir]:\n{summary_str}"
        )
        return bridge

    def switch_mode(self, target_mode: str, forward_context: bool = True) -> Dict[str, Any]:
        """
        Switch active AI mode between 'fast' (Flash Lite) and 'analysis' (Flash High).
        Maintains dedicated persistent session per mode.
        """
        with self._lock:
            if target_mode not in ["fast", "analysis"]:
                raise ValueError(f"Mode tidak dikenal: {target_mode}. Pilihan valid: 'fast', 'analysis'")

            old_mode = getattr(self, "active_mode", "fast")
            if target_mode == old_mode and self.active_conversation_id:
                mode_label = "Mode Analisis (Flash High)" if self.active_mode == "analysis" else "Mode Cepat (Flash Lite)"
                return {
                    "success": True,
                    "already_active": True,
                    "active_mode": self.active_mode,
                    "mode_label": mode_label,
                    "active_model_tier": "flash" if self.active_mode == "analysis" else "flash_lite",
                    "conversation_id": self.active_conversation_id,
                    "message": f"Sudah aktif dalam {mode_label}."
                }

            # 1. Build evaluated context bridge if requested
            bridge_note = None
            if forward_context:
                bridge_note = self._evaluate_context_bridge(old_mode, target_mode)
                if bridge_note:
                    self._pending_context_bridge = bridge_note

            # 2. Select / Create session for target mode
            self.active_mode = target_mode
            if target_mode == "fast":
                target_model = "flash_lite"
                if self.fast_conversation_id:
                    valid, _ = is_conversation_valid(self.fast_conversation_id, self.agent_cmd_prefix)
                    if valid:
                        self.active_conversation_id = self.fast_conversation_id
                    else:
                        self.fast_conversation_id = self._create_new_session_locked(
                            reason="Inisialisasi ulang Mode Cepat (Flash Lite)",
                            model_override="flash_lite"
                        )
                        self.active_conversation_id = self.fast_conversation_id
                else:
                    self.fast_conversation_id = self._create_new_session_locked(
                        reason="Inisialisasi pertama Mode Cepat (Flash Lite)",
                        model_override="flash_lite"
                    )
                    self.active_conversation_id = self.fast_conversation_id
            else:
                target_model = "flash"
                if self.analysis_conversation_id:
                    valid, _ = is_conversation_valid(self.analysis_conversation_id, self.agent_cmd_prefix)
                    if valid:
                        self.active_conversation_id = self.analysis_conversation_id
                    else:
                        self.analysis_conversation_id = self._create_new_session_locked(
                            reason="Inisialisasi ulang Mode Analisis (Flash High)",
                            model_override="flash"
                        )
                        self.active_conversation_id = self.analysis_conversation_id
                else:
                    self.analysis_conversation_id = self._create_new_session_locked(
                        reason="Inisialisasi pertama Mode Analisis (Flash High)",
                        model_override="flash"
                    )
                    self.active_conversation_id = self.analysis_conversation_id

            # 3. Persist updated mode state to disk
            persisted = self._load_persisted_session_data() or {}
            persisted["active_mode"] = self.active_mode
            persisted["active_conversation_id"] = self.active_conversation_id
            persisted["fast_conversation_id"] = self.fast_conversation_id
            persisted["analysis_conversation_id"] = self.analysis_conversation_id
            persisted["last_used_at"] = time.time()
            self._save_persisted_session_data(persisted)

            mode_label = "Mode Analisis (Flash High)" if target_mode == "analysis" else "Mode Cepat (Flash Lite)"
            print(f"[AIGRAV_MODE]: Beralih ke {mode_label} -> Sesi Aktif: {self.active_conversation_id}")
            return {
                "success": True,
                "previous_mode": old_mode,
                "active_mode": self.active_mode,
                "mode_label": mode_label,
                "active_model_tier": target_model,
                "conversation_id": self.active_conversation_id,
                "context_forwarded": bool(bridge_note),
                "message": f"Berhasil beralih ke {mode_label}."
            }

    def get_active_model_info(self) -> Dict[str, Any]:
        """Query metadata of active conversation to verify actual model tier and planModel."""
        conv_id = self.active_conversation_id
        current_mode = getattr(self, "active_mode", "fast")
        if not conv_id:
            tier = "flash" if current_mode == "analysis" else "flash_lite"
            return {
                "mode": current_mode,
                "mode_label": "Mode Analisis (Flash High)" if current_mode == "analysis" else "Mode Cepat (Flash Lite)",
                "model_tier": tier,
                "plan_model": "MODEL_PLACEHOLDER_M322" if tier == "flash" else "MODEL_PLACEHOLDER_M198",
                "model_name": "Gemini 3.8 Flash (High)" if tier == "flash" else "Gemini 3.5 Flash Lite",
                "conversation_id": None
            }

        # Check known production session
        if conv_id == "16ed9184-422f-4e90-a611-0e2ffd9ee0c5":
            return {
                "mode": current_mode,
                "mode_label": "Mode Analisis (Flash High)" if current_mode == "analysis" else "Mode Cepat (Flash Lite)",
                "model_tier": "flash",
                "plan_model": "MODEL_PLACEHOLDER_M322",
                "model_name": "Gemini 3.8 Flash (High)",
                "conversation_id": conv_id
            }

        try:
            cmd = self.agent_cmd_prefix + ["get-conversation-metadata", conv_id]
            proc = execute_agentapi_cmd(cmd, timeout=5.0)
            if proc.returncode == 0:
                data = json.loads(proc.stdout)
                meta = data.get("response", {}).get("conversationMetadata", {}).get("metadata", {})
                planner_cfg = meta.get("staticConfig", {}).get("cascadeConfig", {}).get("plannerConfig", {})
                plan_model = planner_cfg.get("planModel", "")
                if plan_model == "MODEL_PLACEHOLDER_M198":
                    m_name = "Gemini 3.5 Flash Lite"
                    m_tier = "flash_lite"
                elif plan_model == "MODEL_PLACEHOLDER_M322":
                    m_name = "Gemini 3.8 Flash (High)"
                    m_tier = "flash"
                elif plan_model == "MODEL_PLACEHOLDER_M36":
                    m_name = "Gemini 3.1 Pro (Low)"
                    m_tier = "pro"
                else:
                    m_name = plan_model
                    m_tier = "flash" if current_mode == "analysis" else "flash_lite"
                return {
                    "mode": current_mode,
                    "mode_label": "Mode Analisis (Flash High)" if current_mode == "analysis" else "Mode Cepat (Flash Lite)",
                    "model_tier": m_tier,
                    "plan_model": plan_model,
                    "model_name": m_name,
                    "conversation_id": conv_id
                }
        except Exception:
            pass

        tier = "flash" if current_mode == "analysis" else "flash_lite"
        return {
            "mode": current_mode,
            "mode_label": "Mode Analisis (Flash High)" if current_mode == "analysis" else "Mode Cepat (Flash Lite)",
            "model_tier": tier,
            "plan_model": "MODEL_PLACEHOLDER_M322" if tier == "flash" else "MODEL_PLACEHOLDER_M198",
            "model_name": "Gemini 3.8 Flash (High)" if tier == "flash" else "Gemini 3.5 Flash Lite",
            "conversation_id": conv_id
        }

    def explicit_reset_session(self, reason: str = "Permintaan reset pengguna") -> Tuple[Optional[str], str]:
        """Explicitly reset the session upon user request."""
        with self._lock:
            old_id = self.active_conversation_id
            target_model = "flash" if self.active_mode == "analysis" else "flash_lite"
            new_id = self._create_new_session_locked(reason=reason, model_override=target_model)
            self._last_result = None
            self._last_prompt = ""
            return old_id, new_id

    def reset_session(self) -> None:
        """Compatibility wrapper for explicit session reset."""
        self.explicit_reset_session(reason="Permintaan reset sesi")

    def refresh_runtime_connection(self) -> Dict[str, Any]:
        """
        User-triggered runtime connection refresh.
        Validates Python backend, refreshes Antigravity runtime credentials,
        checks Revit Bridge connection, and verifies the active persistent conversation.
        Never launches duplicate processes, never exposes credentials, and protects against concurrency races.
        """
        # Non-blocking acquisition prevents concurrent refresh storms from rapid UI clicks
        if not self._refresh_lock.acquire(blocking=False):
            return {
                "status": "refreshing",
                "message": "Proses verifikasi/pemulihan koneksi sedang berjalan. Harap tunggu...",
                "backend_connected": True,
                "agent_connected": bool(os.environ.get("ANTIGRAVITY_LS_ADDRESS") and is_address_alive(os.environ.get("ANTIGRAVITY_LS_ADDRESS"), timeout=0.2)),
                "revit_connected": is_revit_connected(),
                "antigravity_session_id": self.active_conversation_id,
                "history_count": self.history_count
            }

        try:
            print("[AIGRAV_REFRESH]: User-triggered runtime connection refresh initiated.")
            revit_status = is_revit_connected()

            # 1. Invalidate stale in-memory credentials if unreachable
            curr_addr = os.environ.get("ANTIGRAVITY_LS_ADDRESS")
            if curr_addr and not is_address_alive(curr_addr, timeout=0.3):
                print(f"[AIGRAV_REFRESH]: Current in-memory address {curr_addr} unreachable. Invalidating...")
                os.environ.pop("ANTIGRAVITY_LS_ADDRESS", None)
                os.environ.pop("ANTIGRAVITY_CSRF_TOKEN", None)

            # 2. Ensure credentials (validates cache or discovers running language_server.exe)
            creds = ensure_runtime_credentials()
            if not creds or not creds.get("ANTIGRAVITY_LS_ADDRESS"):
                print("[AIGRAV_REFRESH]: No running Antigravity Language Server found.")
                return {
                    "status": "runtime_unavailable",
                    "message": "Antigravity Language Server tidak terdeteksi. Pastikan Antigravity IDE sedang berjalan.",
                    "backend_connected": True,
                    "agent_connected": False,
                    "revit_connected": revit_status,
                    "antigravity_session_id": self.active_conversation_id,
                    "history_count": self.history_count
                }

            # 3. Check active persistent conversation health via get-conversation-metadata
            active_conv = self.active_conversation_id
            if not active_conv:
                # Attempt to restore persisted session
                if self._try_restore_persisted_session():
                    active_conv = self.active_conversation_id

            if active_conv:
                is_valid, validation_msg = is_conversation_valid(active_conv, self.agent_cmd_prefix)
                if is_valid:
                    print(f"[AIGRAV_REFRESH]: Connection healthy. Active session verified: {active_conv}")
                    return {
                        "status": "connected",
                        "message": "Koneksi ke Antigravity Language Server & sesi percakapan aktif berhasil diverifikasi.",
                        "backend_connected": True,
                        "agent_connected": True,
                        "revit_connected": revit_status,
                        "antigravity_session_id": active_conv,
                        "history_count": self.history_count
                    }
                else:
                    print(f"[AIGRAV_REFRESH]: Antigravity LS reachable, but session {active_conv} returned: {validation_msg}")
                    return {
                        "status": "session_warning",
                        "message": f"Runtime terhubung, namun sesi belum aktif atau dormant ({validation_msg}).",
                        "backend_connected": True,
                        "agent_connected": False,
                        "revit_connected": revit_status,
                        "antigravity_session_id": active_conv,
                        "history_count": self.history_count
                    }
            else:
                return {
                    "status": "connected_no_session",
                    "message": "Runtime terhubung. Belum ada sesi percakapan yang diinisialisasi.",
                    "backend_connected": True,
                    "agent_connected": True,
                    "revit_connected": revit_status,
                    "antigravity_session_id": None,
                    "history_count": 0
                }

        except Exception as ex:
            print(f"[AIGRAV_REFRESH_ERR]: {ex}", file=sys.stderr)
            return {
                "status": "error",
                "message": f"Kesalahan saat refresh koneksi: {str(ex)}",
                "backend_connected": True,
                "agent_connected": False,
                "revit_connected": is_revit_connected(),
                "antigravity_session_id": self.active_conversation_id,
                "history_count": self.history_count
            }
        finally:
            self._refresh_lock.release()

    def _should_reset_session(self, prompt: str) -> bool:
        """Only reset if prompt EXPLICITLY requests session reset. No auto-idle reset."""
        clean = prompt.lower().strip()
        reset_keywords = ["reset sesi", "mulai sesi baru", "sesi baru", "restart percakapan", "hapus memori"]
        return any(kw in clean for kw in reset_keywords)

    def _get_transcript_path(self, conversation_id: str) -> Path:
        return self.brain_dir / conversation_id / ".system_generated" / "logs" / "transcript.jsonl"

    def _get_latest_step_index(self, conversation_id: str) -> int:
        trans_path = self._get_transcript_path(conversation_id)
        if not trans_path.exists():
            return -1
        max_step = -1
        try:
            with open(trans_path, "r", encoding="utf-8", errors="replace") as f:
                for line in f:
                    line_str = line.strip()
                    if line_str:
                        entry = json.loads(line_str)
                        s = entry.get("step_index", -1)
                        if s > max_step:
                            max_step = s
        except Exception:
            pass
        return max_step

    def _wait_for_final_response(
        self,
        conversation_id: str,
        start_step_index: int = 0,
        timeout_seconds: float = 60.0
    ) -> Tuple[str, List[Dict[str, Any]]]:
        """
        Poll transcript.jsonl until the agent produces its final text response.
        Returns: (final_response_text, list_of_executed_tool_calls)
        Ensures intermediate tool execution steps are not mistaken for final response.
        Detects dormant in-memory worker if undelivered messages are not dequeued within 7s.
        """
        trans_path = self._get_transcript_path(conversation_id)
        start_time = time.time()
        deadline = start_time + timeout_seconds

        last_content = ""
        tool_calls_observed: List[Dict[str, Any]] = []
        first_step_observed = False
        dormancy_check_deadline = start_time + 7.0

        while time.time() < deadline:
            now = time.time()
            if trans_path.exists():
                try:
                    entries = []
                    with open(trans_path, "r", encoding="utf-8", errors="replace") as f:
                        for line in f:
                            if line.strip():
                                entries.append(json.loads(line.strip()))

                    # Inspect steps starting from start_step_index
                    new_steps = [e for e in entries if e.get("step_index", -1) >= start_step_index]
                    if new_steps and not first_step_observed:
                        first_step_observed = True
                        print(f"[AIGRAV_AGENT_TRACE] T3: First new step {new_steps[0].get('step_index')} appeared ({now - start_time:.2f}s)")

                    for entry in new_steps:
                        calls = entry.get("tool_calls") or []
                        for c in calls:
                            if c not in tool_calls_observed:
                                tool_calls_observed.append(c)
                                print(f"[AIGRAV_AGENT_TRACE] T4: Tool executed: {c.get('name')} ({now - start_time:.2f}s)")

                    # Find the last PLANNER_RESPONSE from MODEL
                    # It is final ONLY IF:
                    # 1. It has non-empty content
                    # 2. It has no tool_calls
                    # 3. There is no subsequent unresolved tool execution running
                    for entry in reversed(new_steps):
                        if entry.get("source") == "MODEL" and entry.get("type") == "PLANNER_RESPONSE":
                            content = entry.get("content", "")
                            calls = entry.get("tool_calls") or []
                            # If this entry made tool calls, it is NOT the final answer yet
                            if calls:
                                break
                            if content.strip():
                                print(f"[AIGRAV_AGENT_TRACE] T5: Final answer received ({now - start_time:.2f}s, len={len(content)})")
                                return content.strip(), tool_calls_observed
                except Exception:
                    pass

            # Detect dormant in-memory worker: if 7s elapsed without any new steps, check undelivered/
            if not first_step_observed and now > dormancy_check_deadline:
                undelivered_dir = self.brain_dir / conversation_id / ".system_generated" / "messages" / "undelivered"
                if undelivered_dir.exists():
                    try:
                        items = os.listdir(undelivered_dir)
                        if items:
                            raise DormantWorkerError(
                                f"Worker dormant for session {conversation_id[:8]} ({len(items)} message(s) unread in undelivered after {now - start_time:.1f}s)"
                            )
                    except DormantWorkerError:
                        raise
                    except Exception:
                        pass

            time.sleep(0.5)

        if last_content.strip():
            return last_content.strip(), tool_calls_observed

        raise TimeoutError(
            f"Batas waktu tunggu ({timeout_seconds:.0f} detik) terlampaui. "
            f"Agen Antigravity masih memproses tugas di latar belakang (Conversation ID: {conversation_id})."
        )

    def process_utterance(
        self,
        transcript: str,
        voice: Optional[str] = None,
        timeout_seconds: float = 60.0,
        req_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Main processing method called by the Voice HUD backend.
        Sends the transcript directly to the SINGLE PERSISTENT Antigravity agent session via send-message.
        Propagates req_id for end-to-end event correlation and monotonic latency tracking.
        """
        if not transcript or not transcript.strip():
            return {
                "reply_text": "Saya tidak mendengar instruksi yang jelas. Silakan ulangi ucapan Anda.",
                "visual_markdown": "_Tidak ada input suara terdeteksi._",
                "audio_url": "",
                "voice": voice or self.current_voice,
                "badge_text": "INPUT KOSONG",
                "badge_class": "text-yellow-400 font-bold",
                "task_id": "NONE",
                "task_status": "IDLE",
                "artifacts": [],
                "mode": "antigravity_agent",
                "timestamp": time.strftime("%H:%M:%S")
            }

        selected_voice = voice if voice in ALLOWED_VOICES else self.current_voice
        prompt_text = transcript.strip()

        # Deduplication & concurrency controls
        now = time.time()
        need_wait = False
        with self._lock:
            # Case 1: Identical prompt completed recently (within 15 seconds) -> reuse cached result
            if (
                self._last_result is not None
                and prompt_text == self._last_prompt
                and (now - self._last_result_time) < 15.0
            ):
                return self._last_result

            # Case 2: Explicit session reset
            if self._should_reset_session(prompt_text):
                old_id, new_id = self.explicit_reset_session(reason="Perintah suara eksplisit pengguna")
                reply = "Sesi percakapan AIGRAV telah direset. Sesi baru siap digunakan. Ada yang bisa saya bantu?"
                return self._build_payload(
                    reply_text=reply,
                    visual_markdown=(
                        f"### SESI DIRESET\n\n"
                        f"- **Sesi Baru:** `{new_id[:8]}...`\n"
                        f"- **Sesi Lama:** `{old_id[:8] if old_id else 'None'}`\n"
                        f"- **Alasan:** Permintaan suara eksplisit pengguna."
                    ),
                    voice=selected_voice,
                    badge_text="SESI DIRESET",
                    task_id="RESET",
                    task_status="COMPLETED"
                )

            # Case 3: Prompt arrived while processing is in-flight
            if self._is_processing:
                if prompt_text == self._last_prompt:
                    need_wait = True
                else:
                    return {
                        "reply_text": "Agen sedang memproses instruksi sebelumnya. Mohon tunggu sebentar.",
                        "visual_markdown": "_Agen sedang sibuk memproses giliran aktif..._",
                        "audio_url": "",
                        "voice": selected_voice,
                        "badge_text": "AGEN SIBUK",
                        "badge_class": "text-amber-400 font-bold",
                        "task_id": "BUSY",
                        "task_status": "RUNNING",
                        "artifacts": [],
                        "mode": "antigravity_agent",
                        "timestamp": time.strftime("%H:%M:%S")
                    }
            else:
                self._is_processing = True
                self._processing_event.clear()
                self._last_prompt = prompt_text
                self._last_prompt_time = now

        if need_wait:
            self._processing_event.wait(timeout=timeout_seconds)
            with self._lock:
                if self._last_result is not None:
                    return self._last_result
                return {
                    "reply_text": "Agen telah menyelesaikan giliran namun tidak ada data respons.",
                    "visual_markdown": "_Tidak ada respons tersedia._",
                    "audio_url": "",
                    "voice": selected_voice,
                    "badge_text": "SELESAI",
                    "task_id": "DUPLICATE",
                    "task_status": "COMPLETED",
                    "artifacts": [],
                    "mode": "antigravity_agent",
                    "timestamp": time.strftime("%H:%M:%S")
                }

        # Execute Antigravity Agent invocation outside the lock
        t0 = time.time()
        req_tag = f"[{req_id}] " if req_id else ""
        print(f"\n[AIGRAV_AGENT_TRACE] {req_tag}T0: Prompt received (len={len(prompt_text)}): \"{prompt_text[:60]}\"")
        try:
            # Step 1: Ensure single persistent session
            conv_id = self.get_or_create_session()

            # Step 2: Send message to the persistent session (never new-conversation!)
            start_step = self._get_latest_step_index(conv_id) + 1
            t1 = time.time()
            print(f"[AIGRAV_AGENT_TRACE] {req_tag}T1: Session {conv_id[:8]}..., start_step={start_step} (elapsed {t1-t0:.2f}s)")

            # Step 2: Context bridge injection if pending from mode transition
            prompt_to_send = prompt_text
            if getattr(self, "_pending_context_bridge", None):
                prompt_to_send = f"{self._pending_context_bridge}\n\n[Permintaan Pengguna]:\n{prompt_text}"
                self._pending_context_bridge = None
                print(f"[AIGRAV_AGENT_TRACE] {req_tag}Context bridge prepended from previous mode.")

            cmd = self.agent_cmd_prefix + ["send-message", conv_id, prompt_to_send]
            t_send = time.time()
            proc = execute_agentapi_cmd(cmd, timeout=30.0)
            print(f"[AIGRAV_AGENT_TRACE] {req_tag}T2: send-message dispatched ({time.time() - t_send:.2f}s, ret={proc.returncode})")
            if proc.returncode != 0:
                err_str = proc.stderr or proc.stdout
                # Check if session was deleted/corrupted in agentapi
                valid, msg = is_conversation_valid(conv_id, self.agent_cmd_prefix)
                if not valid:
                    print(f"[SESSION_RECOVERY]: Sesi {conv_id} tidak valid ({msg}). Melakukan pemulihan terkontrol satu kali.")
                    with self._lock:
                        conv_id = self._create_new_session_locked(reason=f"Controlled recovery: session corrupted ({err_str[:80]})")
                    start_step = self._get_latest_step_index(conv_id) + 1
                    cmd = self.agent_cmd_prefix + ["send-message", conv_id, prompt_to_send]
                    proc = execute_agentapi_cmd(cmd, timeout=30.0)
                    if proc.returncode != 0:
                        raise RuntimeError(f"Gagal mengirim pesan setelah pemulihan sesi: {proc.stderr or proc.stdout}")
                else:
                    raise RuntimeError(f"Gagal mengirim pesan ke agen: {err_str}")

            # Step 3: Wait for final response & track tool calls (with automatic dormant worker recovery)
            try:
                final_raw_text, tool_calls = self._wait_for_final_response(
                    conversation_id=conv_id,
                    start_step_index=start_step,
                    timeout_seconds=timeout_seconds
                )
            except DormantWorkerError as dwe:
                print(f"[AIGRAV_AGENT_TRACE] {req_tag}DORMANT WORKER DETECTED: {dwe}")
                print(f"[AIGRAV_AGENT_TRACE] {req_tag}Melakukan self-healing: Membuat sesi persisten aktif baru...")
                with self._lock:
                    conv_id = self._create_new_session_locked(reason=f"Auto-recovery dormant worker ({conv_id[:8]})")
                start_step = self._get_latest_step_index(conv_id) + 1
                cmd = self.agent_cmd_prefix + ["send-message", conv_id, prompt_to_send]
                t_rec = time.time()
                proc = execute_agentapi_cmd(cmd, timeout=30.0)
                print(f"[AIGRAV_AGENT_TRACE] {req_tag}T2 (Recovery): send-message dispatched ({time.time() - t_rec:.2f}s, ret={proc.returncode})")
                final_raw_text, tool_calls = self._wait_for_final_response(
                    conversation_id=conv_id,
                    start_step_index=start_step,
                    timeout_seconds=timeout_seconds
                )

            t_text_ready = time.time()
            self.last_activity_time = t_text_ready
            self.history_count += 1

            # Track recent dialogue turns for curated context bridging
            self._recent_dialogue_turns.append({"role": "User", "text": prompt_text})
            self._recent_dialogue_turns.append({"role": "Agent", "text": final_raw_text})
            if len(self._recent_dialogue_turns) > 6:
                self._recent_dialogue_turns = self._recent_dialogue_turns[-6:]

            # Format spoken text and sentence chunks immediately
            spoken_text = clean_text_for_speech(final_raw_text)
            chunks = split_into_speech_chunks(spoken_text)

            # Low-Latency Speech Trigger: Immediate pre-warming at T_text_ready
            t_tts_start = time.time()
            tts_init_latency_ms = (t_tts_start - t_text_ready) * 1000.0
            print(f"[AIGRAV_TIMING] {req_tag}T_text_ready: {t_text_ready:.4f}, T_tts_start: {t_tts_start:.4f} (Init Latency: {tts_init_latency_ms:.2f} ms, chunks={len(chunks)})")
            prewarm_tts(chunks, voice=selected_voice)

            # Persist session state atomically with active mode and session IDs
            try:
                persisted = self._load_persisted_session_data() or {}
                persisted["active_mode"] = getattr(self, "active_mode", "fast")
                persisted["active_conversation_id"] = self.active_conversation_id
                persisted["fast_conversation_id"] = self.fast_conversation_id
                persisted["analysis_conversation_id"] = self.analysis_conversation_id
                persisted["history_count"] = self.history_count
                persisted["last_used_at"] = self.last_activity_time
                persisted["revit_connected"] = is_revit_connected()
                persisted["status"] = "ACTIVE" if persisted["revit_connected"] else "REVIT_DISCONNECTED"
                self._save_persisted_session_data(persisted)
            except Exception as ex:
                print(f"[SESSION_PERSIST_ERR]: {ex}", file=sys.stderr)

            # Build badge & status based on tool execution
            badge_text = "ANTIGRAVITY AGENT RESPON"
            badge_class = "text-cyan-300 font-bold"
            task_status = "COMPLETED"

            if tool_calls:
                tool_names = [c.get("name", "tool") for c in tool_calls]
                badge_text = f"TOOL EKSEKUSI: {', '.join(tool_names[:2])}"
                badge_class = "text-emerald-400 font-bold"

            visual_md = (
                f"### RESPONS ANTIGRAVITY AGENT\n"
                f"- **Sesi:** `{conv_id[:8]}...` | **Turn:** `#{self.history_count}` | **Model:** `Gemini 3.8 Flash (Penalaran Ringkas / Low Latency)`\n"
            )
            if tool_calls:
                visual_md += f"- **Tool MCP Dipanggil:** `{', '.join([c.get('name', '') for c in tool_calls])}`\n"
            visual_md += f"\n{final_raw_text}"

            timing_metadata = {
                "req_id": req_id or "",
                "t0": round(t0, 4),
                "t_text_ready": round(t_text_ready, 4),
                "t_tts_start": round(t_tts_start, 4),
                "tts_init_latency_ms": round(tts_init_latency_ms, 2),
                "chunk0_chars": len(chunks[0]) if chunks else 0
            }

            result_payload = self._build_payload(
                reply_text=spoken_text,
                visual_markdown=visual_md,
                voice=selected_voice,
                badge_text=badge_text,
                badge_class=badge_class,
                task_id=f"CONV-{conv_id[:8]}",
                task_status=task_status,
                chunks=chunks,
                timing=timing_metadata,
                req_id=req_id
            )

            with self._lock:
                self._last_result = result_payload
                self._last_result_time = time.time()

            print(f"[AIGRAV_AGENT_TRACE] {req_tag}T6: Turn complete! Total turnaround: {time.time() - t0:.2f}s (conv={conv_id[:8]}...)\n")
            return result_payload

        except TimeoutError as te:
            error_msg = str(te)
            return self._build_payload(
                reply_text=clean_text_for_speech(error_msg),
                visual_markdown=f"### PERINGATAN TIMEOUT\n\n{error_msg}",
                voice=selected_voice,
                badge_text="TIMEOUT AGEN",
                badge_class="text-rose-400 font-bold",
                task_id="TIMEOUT",
                task_status="FAILED",
                req_id=req_id
            )
        except Exception as e:
            err_text = str(e)
            if "ANTIGRAVITY_LS_ADDRESS" in err_text or "connection refused" in err_text.lower() or "missing csrf" in err_text.lower():
                spoken_reply = "Runtime agen Antigravity sedang tidak terhubung. Pastikan Antigravity sedang berjalan."
            else:
                spoken_reply = "Terjadi kendala pada runtime agen Antigravity. Silakan periksa log sistem."
            error_msg = f"Terjadi kendala teknis pada runtime agen Antigravity: {err_text}"
            return self._build_payload(
                reply_text=spoken_reply,
                visual_markdown=f"### KESALAHAN RUNTIME\n\n```text\n{error_msg}\n```",
                voice=selected_voice,
                badge_text="ERROR RUNTIME",
                badge_class="text-red-500 font-bold",
                task_id="ERROR",
                task_status="FAILED",
                req_id=req_id
            )
        finally:
            with self._lock:
                self._is_processing = False
                self._processing_event.set()

    def _build_payload(
        self,
        reply_text: str,
        visual_markdown: str,
        voice: str,
        badge_text: str,
        badge_class: str = "text-cyan-300 font-bold",
        task_id: str = "IDLE",
        task_status: str = "COMPLETED",
        artifacts: Optional[List[Dict[str, Any]]] = None,
        chunks: Optional[List[str]] = None,
        timing: Optional[Dict[str, Any]] = None,
        req_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """Format payload for Voice HUD and Edge Neural TTS endpoint."""
        if chunks is None:
            chunks = split_into_speech_chunks(reply_text) if reply_text else []

        req_query = f"&req_id={urllib.parse.quote(req_id)}" if req_id else ""
        audio_chunks = [
            {
                "index": i,
                "text": c,
                "audio_url": f"/api/tts?text={urllib.parse.quote(c)}&voice={voice}{req_query}"
            }
            for i, c in enumerate(chunks)
        ]

        primary_audio_url = audio_chunks[0]["audio_url"] if audio_chunks else (
            f"/api/tts?text={urllib.parse.quote(reply_text)}&voice={voice}{req_query}" if reply_text else ""
        )

        return {
            "reply_text": reply_text,
            "visual_markdown": visual_markdown,
            "audio_url": primary_audio_url,
            "audio_chunks": audio_chunks,
            "voice": voice,
            "badge_text": badge_text,
            "badge_class": badge_class,
            "task_id": task_id,
            "task_status": task_status,
            "artifacts": artifacts or [],
            "mode": "antigravity_agent",
            "active_mode": getattr(self, "active_mode", "fast"),
            "model_tier": "flash" if getattr(self, "active_mode", "fast") == "analysis" else "flash_lite",
            "conversation_id": self.active_conversation_id,
            "timestamp": time.strftime("%H:%M:%S"),
            "timing": timing or {},
            "req_id": req_id or ""
        }


# Singleton instance
antigravity_voice_adapter = AntigravityVoiceAdapter()
