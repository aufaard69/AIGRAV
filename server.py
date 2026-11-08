"""
AIGRAV - Autonomous Voice Interface Server
Input/Output adapter serving the Voice HUD and bridging directly to
the shared AIGRAV Intelligence, Orchestrator, TaskStore, QTO Engine, and Steel Knowledge Base.
"""

import os
import sys
import json
import time

# Ensure UTF-8 output on Windows consoles to prevent UnicodeEncodeError with emojis/symbols
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
import hashlib
import asyncio
import subprocess
import urllib.parse
import urllib.request
from http.server import HTTPServer, ThreadingHTTPServer, BaseHTTPRequestHandler
import edge_tts

# Import shared legacy voice adapter (Fallback / Rollback)
from voice_adapter import voice_adapter, EXCEL_OUTPUT_PATH, AUDIO_CACHE_DIR, ORCH_DIR

# Import native Google Antigravity Agent adapter (Primary Brain) & low-latency TTS services
from antigravity_voice_adapter import (
    antigravity_voice_adapter,
    generate_tts_mp3,
    prewarm_tts,
    split_into_speech_chunks,
    clean_text_for_speech,
    ALLOWED_VOICES,
    DEFAULT_VOICE
)

# Primary Engine Switch: True = Native Antigravity Agent, False = Legacy Regex/Templates
USE_ANTIGRAVITY_AGENT = True

PORT = 8080
HTML_PATH = r"c:\AIGRAV\aigrav_voice_hud.html"

TTS_VOICE = DEFAULT_VOICE
TTS_RATE = "+0%"
TTS_PITCH = "+0Hz"
TTS_VOLUME = "+0%"

os.makedirs(AUDIO_CACHE_DIR, exist_ok=True)

def safe_open_file(filepath: str):
    """Safely open file via cmd start on Windows host."""
    try:
        subprocess.Popen(["cmd.exe", "/c", "start", "", filepath], shell=True)
    except Exception as e:
        print(f"[OPEN_ERROR]: {e}", file=sys.stderr)

class VoiceHTTPHandler(BaseHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(200)
        self.end_headers()

    def do_GET(self):
        try:
            self._handle_get()
        except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError):
            pass  # Client disconnected before response was fully transmitted
        except Exception as ex:
            import traceback
            traceback.print_exc()
            try:
                err_bytes = json.dumps({"error": "internal_server_error", "message": str(ex)}).encode('utf-8')
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(err_bytes)))
                self.end_headers()
                self.wfile.write(err_bytes)
            except Exception:
                pass

    def _handle_get(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path
        params = urllib.parse.parse_qs(parsed.query)

        # 1. Root / HUD UI
        if path in ["/", "/index.html", "/aigrav_voice_hud.html"]:
            if os.path.exists(HTML_PATH):
                with open(HTML_PATH, "rb") as f:
                    content = f.read()
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(content)))
                self.end_headers()
                self.wfile.write(content)
            else:
                self.send_error(404, "HUD HTML not found")

        # 2. TTS Audio Stream Endpoint
        elif path == "/api/tts":
            text = params.get("text", [""])[0]
            voice = params.get("voice", [DEFAULT_VOICE])[0]
            req_id = params.get("req_id", [""])[0]
            if not text:
                self.send_error(400, "Missing text parameter")
                return
            if voice not in ALLOWED_VOICES:
                voice = DEFAULT_VOICE
            t_tts_req = time.time()
            mp3_file = generate_tts_mp3(text, voice=voice)
            t_tts_done = time.time()
            req_tag = f"[{req_id}] " if req_id else ""
            if mp3_file and os.path.exists(mp3_file):
                print(f"[TTS_SERVE]{req_tag}: Served {len(text)} chars in {(t_tts_done - t_tts_req)*1000:.1f}ms: \"{text[:45]}...\"")
                with open(mp3_file, "rb") as f:
                    data = f.read()
                self.send_response(200)
                self.send_header("Content-Type", "audio/mpeg")
                self.send_header("Content-Length", str(len(data)))
                self.send_header("Cache-Control", "public, max-age=86400")
                self.end_headers()
                self.wfile.write(data)
            elif not text.strip() or not re.search(r"[a-zA-Z0-9]", text):
                self.send_response(204)
                self.end_headers()
            else:
                self.send_error(500, "TTS generation failed")

        # 3. Voice Preview Endpoint
        elif path == "/api/voice-preview":
            voice = params.get("voice", [DEFAULT_VOICE])[0]
            if voice not in ALLOWED_VOICES:
                voice = DEFAULT_VOICE
            preview_text = "Halo, saya AIGRAV. Saya siap membantu pekerjaan BIM Anda."
            mp3_file = generate_tts_mp3(preview_text, voice=voice)
            if mp3_file and os.path.exists(mp3_file):
                with open(mp3_file, "rb") as f:
                    data = f.read()
                self.send_response(200)
                self.send_header("Content-Type", "audio/mpeg")
                self.send_header("Content-Length", str(len(data)))
                self.send_header("Cache-Control", "public, max-age=86400")
                self.end_headers()
                self.wfile.write(data)
            else:
                self.send_error(500, "Preview generation failed")

        # 4. Shared Task Status
        elif path == "/api/task/status":
            task_id = params.get("task_id", [voice_adapter.active_task_id])[0]
            if task_id:
                task = voice_adapter.store.load(task_id)
                res = json.dumps(task.to_dict() if task else {"error": "not_found"}).encode('utf-8')
            else:
                res = json.dumps({"status": "no_active_task"}).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(res)))
            self.end_headers()
            self.wfile.write(res)

        # 5. Shared System Telemetry & Universal Artifact Status
        elif path == "/api/status":
            try:
                from antigravity_voice_adapter import is_revit_connected, is_address_alive
                revit_conn = is_revit_connected()
            except Exception:
                revit_conn = False

            try:
                ls_addr = os.environ.get("ANTIGRAVITY_LS_ADDRESS")
                agent_conn = bool(ls_addr and is_address_alive(ls_addr, timeout=0.2))
            except Exception:
                agent_conn = False

            try:
                current_active_voice = antigravity_voice_adapter.current_voice if USE_ANTIGRAVITY_AGENT else voice_adapter.current_voice
            except Exception:
                current_active_voice = DEFAULT_VOICE

            try:
                active_session = antigravity_voice_adapter.active_conversation_id or antigravity_voice_adapter.get_or_create_session()
            except Exception:
                active_session = antigravity_voice_adapter.active_conversation_id or "session_initializing"

            try:
                model_info = antigravity_voice_adapter.get_active_model_info() if USE_ANTIGRAVITY_AGENT else {
                    "mode": "legacy",
                    "mode_label": "Legacy Adapter",
                    "model_tier": "regex",
                    "plan_model": "N/A",
                    "model_name": "Legacy Rule-Based Engine",
                    "conversation_id": None
                }
            except Exception:
                model_info = {
                    "mode": "fast",
                    "mode_label": "Mode Cepat (Flash Lite)",
                    "model_tier": "flash_lite",
                    "plan_model": "MODEL_PLACEHOLDER_M198",
                    "model_name": "Gemini 3.5 Flash Lite",
                    "conversation_id": None
                }

            res = json.dumps({
                "system": "AIGRAV Autonomous BIM Intelligence",
                "active_brain": "Native Google Antigravity Agent Runtime" if USE_ANTIGRAVITY_AGENT else "Legacy Orchestrator Adapter",
                "voice_adapter": "Native Antigravity Agent Active" if USE_ANTIGRAVITY_AGENT else "Legacy Adapter Active",
                "active_mode": model_info.get("mode", "fast"),
                "mode_label": model_info.get("mode_label", "Mode Cepat (Flash Lite)"),
                "active_model_tier": model_info.get("model_tier", "flash_lite"),
                "actual_plan_model": model_info.get("plan_model", ""),
                "actual_model_name": model_info.get("model_name", ""),
                "fast_conversation_id": getattr(antigravity_voice_adapter, "fast_conversation_id", None),
                "analysis_conversation_id": getattr(antigravity_voice_adapter, "analysis_conversation_id", None),
                "backend_connected": True,
                "agent_connected": agent_conn,
                "antigravity_session_id": active_session,
                "session_persisted": True,
                "session_history_count": getattr(antigravity_voice_adapter, "history_count", 0),
                "revit_connected": revit_conn,
                "revit_bridge_target": "http://127.0.0.1:8765/",
                "voice_engine": f"Microsoft Edge Neural TTS ({TTS_VOICE})",
                "supported_voices": ALLOWED_VOICES,
                "current_voice": current_active_voice,
                "locale": "id-ID",
                "shared_task_store": str(voice_adapter.store.storage_dir),
                "active_task_id": voice_adapter.active_task_id,
                "output_directory": r"C:\AIGRAV",
                "total_verified_artifacts": sum(len(a) for a in voice_adapter.task_artifacts.values()),
                "steel_database_path": str(voice_adapter.steel_service.master_path),
                "drawing_automation": "TeklaDrawingAutomationEngine Active",
                "steel_automation": "SteelConnectionAutomationEngine Active"
            }).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(res)))
            self.end_headers()
            self.wfile.write(res)

        else:
            self.send_error(404)

    def do_POST(self):
        try:
            self._handle_post()
        except (ConnectionAbortedError, ConnectionResetError, BrokenPipeError):
            pass  # Client disconnected before response was fully transmitted
        except Exception as ex:
            import traceback
            traceback.print_exc()
            try:
                err_bytes = json.dumps({"error": "internal_server_error", "message": str(ex)}).encode('utf-8')
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(err_bytes)))
                self.end_headers()
                self.wfile.write(err_bytes)
            except Exception:
                pass

    def _handle_post(self):
        global USE_ANTIGRAVITY_AGENT
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path

        # 1. Main Voice Input Processing
        if path == "/api/voice-input":
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length).decode('utf-8')
            try:
                data = json.loads(body)
                prompt = data.get("prompt") or data.get("transcript", "")
                voice = data.get("voice", DEFAULT_VOICE)
                req_id = data.get("req_id") or f"req_{int(time.time()*1000)}_{os.urandom(3).hex()}"
            except Exception:
                prompt = body
                voice = DEFAULT_VOICE
                req_id = f"req_{int(time.time()*1000)}_{os.urandom(3).hex()}"

            if voice not in ALLOWED_VOICES:
                voice = DEFAULT_VOICE

            print(f"\n[AIGRAV_VOICE_IN][{req_id}]: \"{prompt}\" (voice={voice}, engine={'antigravity_agent' if USE_ANTIGRAVITY_AGENT else 'legacy'})")
            
            if USE_ANTIGRAVITY_AGENT:
                result = antigravity_voice_adapter.process_utterance(prompt, voice=voice, req_id=req_id)
            else:
                result = voice_adapter.process_utterance(prompt, voice=voice)

            print(f"[AIGRAV_VOICE_OUT][{req_id}]: \"{result.get('reply_text', '')}\"")
            print(f"[AIGRAV_TASK_STATE][{req_id}]: id={result.get('task_id')}, status={result.get('task_status')}, badge={result.get('badge_text')}")

            resp_bytes = json.dumps(result).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        # 2. Toggle Engine Switch (Runtime Rollback / A-B Testing)
        elif path == "/api/toggle-adapter":
            USE_ANTIGRAVITY_AGENT = not USE_ANTIGRAVITY_AGENT
            res = {
                "success": True,
                "use_antigravity_agent": USE_ANTIGRAVITY_AGENT,
                "active_brain": "Native Google Antigravity Agent Runtime" if USE_ANTIGRAVITY_AGENT else "Legacy Orchestrator Adapter"
            }
            resp_bytes = json.dumps(res).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        # 3. Explicit Session Reset Endpoint
        elif path == "/api/session/reset":
            old_id, new_id = antigravity_voice_adapter.explicit_reset_session(reason="Permintaan reset eksplisit via API")
            res = {
                "success": True,
                "message": "Antigravity persistent voice session reset successfully",
                "old_conversation_id": old_id,
                "new_conversation_id": new_id,
                "reason": "Permintaan reset eksplisit via API"
            }
            resp_bytes = json.dumps(res).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        # 4. Unified Task Control Endpoints
        elif path == "/api/task/pause":
            res = voice_adapter._handle_pause_command()
            resp_bytes = json.dumps(res).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        elif path == "/api/task/resume":
            res = voice_adapter._handle_resume_command()
            resp_bytes = json.dumps(res).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        elif path == "/api/task/cancel":
            res = voice_adapter._handle_cancel_command()
            resp_bytes = json.dumps(res).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        # 5. User-Triggered Runtime Connection Refresh
        elif path == "/api/runtime/refresh":
            try:
                refresh_result = antigravity_voice_adapter.refresh_runtime_connection()
            except Exception as ex:
                refresh_result = {
                    "status": "error",
                    "message": f"Kesalahan internal saat me-refresh runtime: {str(ex)}",
                    "backend_connected": True,
                    "agent_connected": False,
                    "revit_connected": False
                }
            resp_bytes = json.dumps(refresh_result).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        # 6. Manual AI Mode Switch (Mode Cepat vs Mode Analisis)
        elif path == "/api/mode/switch":
            length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(length).decode('utf-8')
            try:
                data = json.loads(body)
                target_mode = data.get("mode", "fast")
                forward_context = data.get("forward_context", True)
            except Exception:
                target_mode = "fast"
                forward_context = True

            try:
                res = antigravity_voice_adapter.switch_mode(target_mode, forward_context=forward_context)
            except Exception as ex:
                res = {"success": False, "error": str(ex)}

            resp_bytes = json.dumps(res).encode('utf-8')
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp_bytes)))
            self.end_headers()
            self.wfile.write(resp_bytes)

        else:
            self.send_error(404)

    def log_message(self, format, *args):
        # Suppress routine 200/304 polling logs to keep console clean, but log errors
        if args and len(args) >= 2:
            code = str(args[1])
            if code not in ("200", "304"):
                sys.stderr.write(f"[HTTP_{code}]: {format % args}\n")
        elif not args:
            sys.stderr.write(f"[HTTP_LOG]: {format}\n")

if __name__ == "__main__":
    print("=" * 65)
    print("AIGRAV - UNIFIED VOICE INTERFACE ADAPTER SERVER")
    print(f"Shared Orchestrator  : {ORCH_DIR}")
    print(f"Authoritative Store  : {voice_adapter.store.storage_dir}")
    print(f"Revit Bridge Target  : {voice_adapter.router.bridge_url}")
    print(f"Voice Engine (Neural): {TTS_VOICE}")
    print(f"Server Endpoint      : http://localhost:{PORT}/")
    print("=" * 65)

    # 1. Proactive Single-Instance Verification
    try:
        import urllib.request
        check_req = urllib.request.Request(f"http://127.0.0.1:{PORT}/api/status")
        with urllib.request.urlopen(check_req, timeout=1.5) as check_res:
            if check_res.status == 200:
                print(f"[AIGRAV_BACKEND]: An active backend instance is already running on port {PORT}. Exiting redundant instance.")
                sys.exit(0)
    except Exception:
        pass

    # Ensure Antigravity Language Server credentials linked
    try:
        from antigravity_voice_adapter import ensure_runtime_credentials
        creds = ensure_runtime_credentials()
        if creds.get("ANTIGRAVITY_LS_ADDRESS"):
            print(f"[AIGRAV_BACKEND]: Antigravity Language Server linked: {creds['ANTIGRAVITY_LS_ADDRESS']}")
        else:
            print("[AIGRAV_BACKEND]: Warning: Antigravity Language Server not detected at startup.")
    except Exception as ex:
        print(f"[AIGRAV_BACKEND]: Runtime credentials check error: {ex}")

    # 2. Allow immediate re-binding across TIME_WAIT socket states
    ThreadingHTTPServer.allow_reuse_address = True
    try:
        server = ThreadingHTTPServer(("0.0.0.0", PORT), VoiceHTTPHandler)
    except OSError as ex:
        print(f"[AIGRAV_BACKEND_ERROR]: Failed to bind port {PORT}: {ex}. Another process may be using this port.", file=sys.stderr)
        sys.exit(1)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
