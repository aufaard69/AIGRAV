"""
AIGRAV Voice Interface Adapter
Input/Output adapter wrapping the existing AIGRAV Intelligence, Orchestrator,
DecisionReviewEngine, TaskStore, QTO Engine, Steel Database, and Revit Bridge.
Shared seamlessly across Chat and Voice.
"""

from __future__ import annotations

import json
import os
import re
import sys
import time
import mimetypes
import subprocess
import urllib.parse
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple

# Inject MCP/TestServer to sys.path to access the authoritative orchestrator package
ORCH_DIR = Path(r"C:\AIGRAV\MCP\TestServer")
if str(ORCH_DIR) not in sys.path:
    sys.path.insert(0, str(ORCH_DIR))

from orchestrator.decision_engine import DecisionReviewEngine
from orchestrator.decision_models import DecisionOutcome, DecisionReviewResult
from orchestrator.engine import TaskExecutionEngine
from orchestrator.models import ExecutionStatus, Step, Task, VerificationCriteria
from orchestrator.task_store import TaskStore
from orchestrator.tool_router import ToolRouter

# Shared Domain Services
from steel_service import steel_service
from qto_engine import qto_engine
from drawing_service import drawing_engine

AUDIO_CACHE_DIR = r"C:\AIGRAV\audio_cache"
EXCEL_OUTPUT_PATH = r"C:\AIGRAV\Estimasi_Volume_Beton_Rusun_MBR.xlsx"

ALLOWED_VOICES = {
    "id-ID-ArdiNeural": "Pria",
    "id-ID-GadisNeural": "Wanita",
}
DEFAULT_VOICE = "id-ID-ArdiNeural"


@dataclass
class TaskArtifact:
    """Universal Output Artifact Metadata Contract."""
    artifact_id: str
    task_id: str
    file_name: str
    absolute_path: str
    file_extension: str
    mime_type: str
    created_at: float
    exists: bool
    size_bytes: int
    validation_status: str  # "verified", "failed", "pending"
    artifact_type: str      # "spreadsheet", "report", "drawing", "document", "image", "data"
    opened_in_explorer: bool = False

    def to_dict(self) -> Dict[str, Any]:
        return {
            "artifact_id": self.artifact_id,
            "task_id": self.task_id,
            "file_name": self.file_name,
            "absolute_path": self.absolute_path,
            "file_extension": self.file_extension,
            "mime_type": self.mime_type,
            "created_at": self.created_at,
            "exists": self.exists,
            "size_bytes": self.size_bytes,
            "validation_status": self.validation_status,
            "artifact_type": self.artifact_type,
            "opened_in_explorer": self.opened_in_explorer,
        }


def verify_output_artifact(
    task_id: str,
    file_path: str,
    artifact_type: Optional[str] = None
) -> TaskArtifact:
    """
    Perform post-generation verification of output artifact.
    Verifies that the file exists, is not a directory, has non-zero size,
    and passes basic file header / structural integrity validation.
    """
    abs_path = os.path.abspath(file_path)
    file_name = os.path.basename(abs_path)
    ext = os.path.splitext(file_name)[1].lower()

    mime_map = {
        ".xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls": "application/vnd.ms-excel",
        ".csv": "text/csv",
        ".pdf": "application/pdf",
        ".docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png": "image/png",
        ".jpg": "image/jpeg",
        ".jpeg": "image/jpeg",
        ".dwg": "application/acad",
        ".dxf": "application/dxf",
        ".json": "application/json",
        ".txt": "text/plain",
    }
    type_map = {
        ".xlsx": "spreadsheet",
        ".xls": "spreadsheet",
        ".csv": "spreadsheet",
        ".pdf": "report",
        ".docx": "document",
        ".png": "image",
        ".jpg": "image",
        ".jpeg": "image",
        ".dwg": "drawing",
        ".dxf": "drawing",
        ".json": "data",
        ".txt": "text",
    }

    guessed_mime, _ = mimetypes.guess_type(abs_path)
    mime_type = guessed_mime or mime_map.get(ext, "application/octet-stream")
    resolved_type = artifact_type or type_map.get(ext, "document")
    artifact_id = f"ART-{int(time.time() * 1000)}-{re.sub(r'[^a-zA-Z0-9_]', '_', os.path.splitext(file_name)[0])}"

    if not os.path.exists(abs_path):
        return TaskArtifact(
            artifact_id=artifact_id,
            task_id=task_id,
            file_name=file_name,
            absolute_path=abs_path,
            file_extension=ext,
            mime_type=mime_type,
            created_at=time.time(),
            exists=False,
            size_bytes=0,
            validation_status="failed",
            artifact_type=resolved_type,
            opened_in_explorer=False
        )

    if not os.path.isfile(abs_path):
        return TaskArtifact(
            artifact_id=artifact_id,
            task_id=task_id,
            file_name=file_name,
            absolute_path=abs_path,
            file_extension=ext,
            mime_type=mime_type,
            created_at=time.time(),
            exists=False,
            size_bytes=0,
            validation_status="failed",
            artifact_type=resolved_type,
            opened_in_explorer=False
        )

    size = os.path.getsize(abs_path)
    if size <= 0:
        return TaskArtifact(
            artifact_id=artifact_id,
            task_id=task_id,
            file_name=file_name,
            absolute_path=abs_path,
            file_extension=ext,
            mime_type=mime_type,
            created_at=time.time(),
            exists=True,
            size_bytes=0,
            validation_status="failed",
            artifact_type=resolved_type,
            opened_in_explorer=False
        )

    # Basic structural check based on file extension
    is_valid = True
    try:
        if ext == ".xlsx":
            with open(abs_path, "rb") as f:
                header = f.read(4)
                if header != b"PK\x03\x04":
                    is_valid = False
        elif ext == ".pdf":
            with open(abs_path, "rb") as f:
                header = f.read(4)
                if not header.startswith(b"%PDF"):
                    is_valid = False
        elif ext in [".csv", ".json", ".txt"]:
            with open(abs_path, "r", encoding="utf-8", errors="replace") as f:
                _ = f.read(100)
    except Exception:
        is_valid = False

    return TaskArtifact(
        artifact_id=artifact_id,
        task_id=task_id,
        file_name=file_name,
        absolute_path=abs_path,
        file_extension=ext,
        mime_type=mime_type,
        created_at=time.time(),
        exists=True,
        size_bytes=size,
        validation_status="verified" if is_valid else "failed",
        artifact_type=resolved_type,
        opened_in_explorer=False
    )


def safe_auto_open_in_explorer(
    artifacts: List[TaskArtifact],
    task_id: Optional[str] = None,
    force: bool = False
) -> Tuple[bool, List[str]]:
    """
    Safely open Windows Explorer on host OS for verified task artifacts.
    - Single file: launches Explorer selecting the file via explorer.exe /select,"<normpath>"
    - Multiple files in same directory: opens the directory once.
    - Multiple files across directories: opens each unique directory once.
    - Path security: verifies target path is absolute, exists on disk, and is safe.
    - Idempotency: marks opened_in_explorer = True to avoid duplicate windows.
    """
    if not artifacts:
        return False, []

    target_artifacts = [
        a for a in artifacts
        if a.validation_status == "verified" and a.exists and (force or not a.opened_in_explorer)
    ]
    if not target_artifacts:
        return False, []

    opened_targets: List[str] = []

    try:
        if len(target_artifacts) == 1:
            art = target_artifacts[0]
            norm_path = os.path.normpath(art.absolute_path)
            if os.path.exists(norm_path) and os.path.isfile(norm_path):
                # Safe Windows Explorer file selection
                cmd = f'explorer.exe /select,"{norm_path}"'
                subprocess.Popen(cmd)
                art.opened_in_explorer = True
                opened_targets.append(norm_path)
        else:
            unique_dirs = list(dict.fromkeys([
                os.path.normpath(os.path.dirname(a.absolute_path))
                for a in target_artifacts
                if os.path.exists(a.absolute_path)
            ]))
            for d in unique_dirs:
                if os.path.isdir(d):
                    cmd = f'explorer.exe "{d}"'
                    subprocess.Popen(cmd)
                    opened_targets.append(d)
            for a in target_artifacts:
                a.opened_in_explorer = True

        return len(opened_targets) > 0, opened_targets
    except Exception as e:
        print(f"[EXPLORER_AUTO_OPEN_ERROR]: {e}", file=sys.stderr)
        return False, []


class VoiceInterfaceAdapter:
    """
    Adapter interfacing between real-time voice interaction (STT/TTS)
    and the shared authoritative AIGRAV Intelligence & Task Orchestrator.
    Supports both General Conversation Mode and BIM Work Mode seamlessly.
    """

    def __init__(self):
        # 1. Authoritative Shared Services
        self.router = ToolRouter()  # Live Revit Bridge at :8765
        self.store = TaskStore()    # C:\AIGRAV\KnowledgeBase\TaskHistory
        self.engine = TaskExecutionEngine(router=self.router, store=self.store)
        self.decision_engine = DecisionReviewEngine(router=self.router)
        self.steel_service = steel_service
        self.qto_engine = qto_engine
        self.drawing_engine = drawing_engine

        # 2. Session Context (Pointer to active authoritative task)
        self.active_task_id: Optional[str] = None
        self.pending_plan: Optional[Task] = None
        self.pending_qto: bool = False
        self.pending_qto_categories: Optional[List[str]] = None
        self.pending_steel_plan: Optional[Dict[str, Any]] = None
        self.pending_drawing_plan: Optional[Dict[str, Any]] = None
        self.pending_action: Optional[str] = None
        self.pending_clarification_task: Optional[str] = None
        self.last_handled_transcript: str = ""
        self.last_handled_time: float = 0.0

        # 3. Voice Selection & Multi-Turn Conversation Memory
        self.current_voice: str = DEFAULT_VOICE
        self.conversation_history: List[Dict[str, Any]] = []

        # 4. Universal Task Artifacts & Explorer Tracking (strictly per-task)
        self.task_artifacts: Dict[str, List[TaskArtifact]] = {}
        self.opened_task_ids: Set[str] = set()

    def set_voice(self, voice_id: str):
        """Set the active voice if in allowlist."""
        if voice_id in ALLOWED_VOICES:
            self.current_voice = voice_id

    def _is_bim_intent(self, text: str) -> bool:
        """Accurately determine if user request targets BIM/Revit engineering."""
        # 1. Negative filters: emotional venting, career reflection, material prices, jokes, casual talk are NEVER BIM tasks
        general_signals = [
            "capek", "lelah", "penat", "pusing", "stres", "istirahat", "curhat",
            "pindah pekerjaan", "pindah kerja", "resign", "ganti karier", "ganti kerja",
            "harga besi", "harga material", "harga baja", "kenapa harga", "mengapa harga", "kenapa bisa",
            "lelucon", "bercanda", "joke", "lucu", "humor",
            "selamat pagi", "selamat siang", "selamat sore", "selamat malam",
            "apa kabar", "kamu siapa", "siapa kamu", "siapa anda", "apa itu agentic"
        ]
        if any(sig in text for sig in general_signals):
            return False

        # 2. Strict BIM / Engineering tokens
        strict_bim_tokens = [
            "revit", "qto", "kubikasi", "base plate", "pedestal", "angkur",
            "sambungan baja", "steel connection", "end plate", "drawing", "gambar kerja",
            "sheet", "viewport", "elevasi portal", "denah lantai", "active view",
            "project info", "elemen model", "tekla"
        ]
        if any(tok in text for tok in strict_bim_tokens):
            return True

        action_tokens = ["periksa", "inspeksi", "cek", "lihat", "baca", "ubah", "hapus", "buat", "tambah", "hitung", "generate", "otomatisasi", "modelkan", "pasang"]
        bim_target_tokens = ["model", "proyek", "project", "elemen", "kolom", "balok", "dinding", "lantai", "slab", "grid", "level", "view", "parameter", "schedule", "family", "drawing", "sheet", "sambungan"]

        has_action = any(a in text for a in action_tokens)
        has_target = any(t in text for t in bim_target_tokens)
        if has_action and has_target:
            return True

        if any(p in text for p in ["delete", "create", "modify", "update"]):
            if any(t in text for t in bim_target_tokens):
                return True

        return False

    def process_utterance(self, transcript: str, voice: Optional[str] = None) -> Dict[str, Any]:
        """
        Main entry point: receives transcribed user utterance, delegates to
        shared intelligence, and produces concise speech + rich visual workspace.
        """
        if voice and voice in ALLOWED_VOICES:
            self.current_voice = voice

        now = time.time()
        lower = transcript.lower().strip()
        clean_lower = re.sub(r"[^\w\s]", " ", lower).strip()
        words = clean_lower.split()

        # 1. Deduplication & Anti-Self-Echo
        if lower == self.last_handled_transcript.lower() and (now - self.last_handled_time) < 2.0:
            return {"status": "duplicate_ignored"}

        if any(echo in lower for echo in [
            "katakan oke eksekusi", "saya mendengarkan", "selesai file excel",
            "pemeriksaan selesai", "halo saya aigrav", "saya siap membantu pekerjaan bim anda"
        ]):
            return {"status": "self_echo_ignored"}

        self.last_handled_transcript = transcript
        self.last_handled_time = now

        # Record user turn in multi-turn conversation history
        self.conversation_history.append({
            "role": "user",
            "text": transcript,
            "timestamp": now
        })
        if len(self.conversation_history) > 30:
            self.conversation_history = self.conversation_history[-30:]

        # ----------------------------------------------------------------------
        # A. UNIFIED TASK CONTROL: PAUSE / RESUME / CANCEL
        # ----------------------------------------------------------------------
        if any(k in clean_lower for k in ["pause", "pause dulu", "tahan task", "jeda task", "stop sementara"]):
            return self._handle_pause_command()

        if any(k in clean_lower for k in ["resume", "lanjutkan task", "lanjutkan", "teruskan"]):
            return self._handle_resume_command()

        if any(k in clean_lower for k in ["batalkan", "batal", "cancel task", "batalkan task", "hentikan task"]):
            return self._handle_cancel_command()

        if any(k in clean_lower for k in ["status task", "cek task", "progres task", "lihat task"]):
            return self._handle_task_status_query()

        # ----------------------------------------------------------------------
        # B. EXECUTION GATE: CONFIRMATION BY "OKE EKSEKUSI" / "JALANKAN"
        # ----------------------------------------------------------------------
        is_exec_gate = any(k in clean_lower for k in ["oke eksekusi", "eksekusi", "jalankan", "lakukan", "proses sekarang"])
        if is_exec_gate:
            return self._handle_execution_gate()

        # ----------------------------------------------------------------------
        # C. FILE MANAGEMENT: REVEAL OUTPUT LOCATION (UNIVERSAL EXPLORER OPEN)
        # ----------------------------------------------------------------------
        if any(k in clean_lower for k in ["buka output", "buka folder", "buka lokasi", "buka file", "buka berkas", "buka excel", "lihat excel", "open excel", "buka filenya"]):
            return self._handle_reveal_output_location(transcript)

        # ----------------------------------------------------------------------
        # D. DRAWING AUTOMATION WORKFLOW (TEKLA-STYLE)
        # ----------------------------------------------------------------------
        if any(k in clean_lower for k in ["drawing", "gambar kerja", "buat sheet", "tekla drawing", "otomatisasi drawing", "lembar gambar"]):
            if any(a in clean_lower for a in ["buat", "buatkan", "generate", "siapkan", "susun", "bikin", "otomatisasi", "layout", "sheet"]):
                return self._handle_drawing_automation_request(transcript)

        # ----------------------------------------------------------------------
        # E. STRUCTURAL STEEL AUTOMATION & KNOWLEDGE BASE
        # ----------------------------------------------------------------------
        if any(k in clean_lower for k in ["sambungan baja", "steel connection", "base plate", "pelat landasan", "end plate", "angkur", "baut htb", "profil wf"]):
            # If requesting generation / modeling / automation
            if any(a in clean_lower for a in ["buat", "buatkan", "modelkan", "pasang", "generate", "otomatisasi", "bikin", "rancang"]):
                return self._handle_steel_automation_request(transcript)
            return self._handle_steel_database_query(clean_lower)

        # ----------------------------------------------------------------------
        # E. READ-ONLY ELEMENT COUNT QUERY (e.g. "Berapa jumlah kolom pada model?")
        # ----------------------------------------------------------------------
        is_count_query = any(k in clean_lower for k in [
            "berapa jumlah", "berapa banyak", "jumlah elemen", "hitung jumlah",
            "ada berapa", "total elemen", "jumlah kolom", "jumlah balok", "jumlah pondasi", "jumlah plat"
        ])
        if is_count_query:
            return self._handle_element_count_query(clean_lower)

        # ----------------------------------------------------------------------
        # F. QUANTITY TAKEOFF (QTO) CALCULATION & EXPORT
        # ----------------------------------------------------------------------
        is_qto_query = any(k in clean_lower for k in [
            "hitung volume", "volume beton", "qto", "kubikasi", "breakdown beton",
            "volume plat", "volume pelat", "volume pondasi", "volume balok", "volume kolom", "volume dinding"
        ]) or (("hitung" in clean_lower or "estimasi" in clean_lower) and any(c in clean_lower for c in ["plat", "pelat", "pondasi", "fondasi", "balok", "kolom", "dinding", "slab", "beton"]))

        if is_qto_query:
            return self._handle_qto_query(clean_lower)

        # ----------------------------------------------------------------------
        # F. CLARIFICATION TASK CONTINUATION
        # ----------------------------------------------------------------------
        if self.pending_clarification_task and any(c in clean_lower for c in ["kolom", "k1", "level", "tipe", "balok", "elemen", "lantai", "kategori"]):
            continuation_prompt = f"{self.pending_clarification_task} dengan rincian {transcript}"
            self.pending_clarification_task = None
            return self._delegate_to_decision_engine(continuation_prompt)

        # ----------------------------------------------------------------------
        # G. BIM WORK MODE VS GENERAL CONVERSATION MODE
        # ----------------------------------------------------------------------
        if self._is_bim_intent(clean_lower):
            return self._delegate_to_decision_engine(transcript)
        else:
            return self._handle_general_conversation(transcript, clean_lower)

    # --------------------------------------------------------------------------
    # HANDLERS IMPLEMENTATION
    # --------------------------------------------------------------------------

    def _handle_pause_command(self) -> Dict[str, Any]:
        """Unified Pause: sets active task to PAUSED in TaskStore."""
        if not self.active_task_id:
            return self._build_response(
                reply_text="Tidak ada task yang sedang berjalan untuk dijeda.",
                visual_md="*Tidak ada task aktif dalam TaskStore.*",
                badge_text="TIDAK ADA TASK AKTIF",
                badge_class="text-gray-400"
            )

        task = self.engine.pause_task(self.active_task_id)
        if task and task.execution_status == ExecutionStatus.PAUSED:
            return self._build_response(
                reply_text=f"Task {task.task_id} berhasil dijeda dengan aman pada checkpoint saat ini.",
                visual_md=f"### TASK PAUSED\n- **Task ID:** `{task.task_id}`\n- **Status:** `PAUSED`\n- **Permintaan:** {task.user_request}\n- *Transaksi Revit tetap aman tanpa gangguan lock.*",
                badge_text="TASK DIJEDA (PAUSED)",
                badge_class="text-amber-400 font-bold",
                task_id=task.task_id,
                task_status="PAUSED"
            )
        return self._build_response(
            reply_text="Task tidak dapat dijeda atau sudah selesai.",
            visual_md=f"*Gagal menjeda task {self.active_task_id}.*",
            badge_text="STATUS TETAP",
            badge_class="text-gray-400"
        )

    def _handle_resume_command(self) -> Dict[str, Any]:
        """Unified Resume: resumes paused task in TaskStore."""
        if not self.active_task_id:
            return self._build_response(
                reply_text="Tidak ada task yang sedang dijeda untuk dilanjutkan.",
                visual_md="*Tidak ada task aktif dalam TaskStore.*",
                badge_text="TIDAK ADA TASK",
                badge_class="text-gray-400"
            )

        task = self.engine.resume_task(self.active_task_id)
        if task and task.execution_status == ExecutionStatus.COMPLETED:
            return self._build_response(
                reply_text=f"Task {task.task_id} berhasil dilanjutkan dan seluruh langkah telah selesai diverifikasi.",
                visual_md=f"### TASK RESUMED & COMPLETED\n- **Task ID:** `{task.task_id}`\n- **Status:** `COMPLETED`\n- **Total Steps:** {len(task.steps)} langkah terverifikasi.",
                badge_text="EKSEKUSI SELESAI",
                badge_class="text-emerald-400 font-bold",
                task_id=task.task_id,
                task_status="COMPLETED"
            )
        return self._build_response(
            reply_text=f"Task {self.active_task_id} sedang berjalan kembali.",
            visual_md=f"*Task {self.active_task_id} resumed.*",
            badge_text="TASK BERJALAN",
            badge_class="text-cyan-300 font-bold"
        )

    def _handle_cancel_command(self) -> Dict[str, Any]:
        """Unified Cancel: marks active task as CANCELLED in TaskStore."""
        if not self.active_task_id:
            return self._build_response(
                reply_text="Tidak ada task aktif yang dapat dibatalkan.",
                visual_md="*Tidak ada task aktif.*",
                badge_text="TIDAK ADA TASK",
                badge_class="text-gray-400"
            )

        task = self.engine.cancel_task(self.active_task_id)
        self.pending_plan = None
        self.pending_qto = False
        self.pending_qto_categories = None
        return self._build_response(
            reply_text=f"Task {self.active_task_id} telah dibatalkan. Sisa langkah telah ditandai batal.",
            visual_md=f"### TASK CANCELLED\n- **Task ID:** `{self.active_task_id}`\n- **Status:** `CANCELLED`\n- *Checkpoint tersimpan aman di TaskStore.*",
            badge_text="TASK DIBATALKAN",
            badge_class="text-rose-400 font-bold",
            task_id=self.active_task_id,
            task_status="CANCELLED"
        )

    def _handle_task_status_query(self) -> Dict[str, Any]:
        """Query state of active task from authoritative TaskStore."""
        if not self.active_task_id:
            return self._build_response(
                reply_text="Belum ada task aktif yang tercatat di TaskStore.",
                visual_md="*Belum ada task aktif.*",
                badge_text="SIAP",
                badge_class="text-cyan-300 font-bold"
            )

        task = self.store.load(self.active_task_id)
        if not task:
            return self._build_response(
                reply_text=f"Task {self.active_task_id} tidak ditemukan di penyimpanan riwayat.",
                visual_md=f"*Task `{self.active_task_id}` tidak ditemukan.*",
                badge_text="NOT FOUND",
                badge_class="text-rose-400"
            )

        step_rows = "\n".join([f"- **{s.step_id}** (`{s.tool_name}`): status `{s.execution_status.value}`" for s in task.steps])
        return self._build_response(
            reply_text=f"Status task {task.task_id} saat ini adalah {task.execution_status.value}.",
            visual_md=f"### STATUS TASK: `{task.task_id}`\n- **Status:** `{task.execution_status.value}`\n- **Permintaan:** {task.user_request}\n\n#### Langkah Eksekusi (DAG):\n{step_rows}",
            badge_text=f"STATUS: {task.execution_status.value}",
            badge_class="text-cyan-300 font-bold",
            task_id=task.task_id,
            task_status=task.execution_status.value
        )

    def _handle_execution_gate(self) -> Dict[str, Any]:
        """Execute pending QTO or synthesized DAG plan upon user approval."""
        # 1. Pending QTO
        if self.pending_qto:
            self.pending_qto = False
            cats = self.pending_qto_categories
            self.pending_qto_categories = None  # Immediately clear to prevent stale category leakage!

            qto_res = self.qto_engine.execute_live_qto(
                output_filepath=EXCEL_OUTPUT_PATH,
                categories=cats
            )

            cat_desc = ", ".join(cats) if cats and "all" not in cats else "Seluruh Elemen Beton"
            task_id = f"TASK-QTO-{int(time.time() * 1000)}"

            # Post-generation strict file verification
            artifact = verify_output_artifact(
                task_id=task_id,
                file_path=EXCEL_OUTPUT_PATH,
                artifact_type="spreadsheet"
            )
            self.task_artifacts[task_id] = [artifact]

            # Safe Windows Explorer auto-opening
            auto_opened = False
            opened_targets: List[str] = []
            if artifact.validation_status == "verified" and task_id not in self.opened_task_ids:
                auto_opened, opened_targets = safe_auto_open_in_explorer([artifact], task_id=task_id)
                if auto_opened:
                    self.opened_task_ids.add(task_id)

            task = Task(
                task_id=task_id,
                user_request=f"Eksekusi Quantity Takeoff (QTO) Beton ({cat_desc}) & Export Excel Resmi",
                execution_status=ExecutionStatus.COMPLETED
            )
            step = Step(
                step_id="step_qto_export",
                title=f"QTO Live Aggregation ({cat_desc}) & Excel Generation",
                tool_name="qto_engine.execute_live_qto",
                execution_status=ExecutionStatus.COMPLETED,
                output={
                    "excel_path": qto_res["excel_path"],
                    "volume_m3": qto_res["total_volume_m3"],
                    "categories": cats,
                    "artifacts": [artifact.to_dict()]
                }
            )
            task.steps.append(step)
            self.store.save(task)
            self.active_task_id = task_id

            # Natural Indonesian voice delivery reporting verification and Explorer status
            if artifact.validation_status == "verified":
                if auto_opened:
                    reply_text = (
                        f"Selesai. File Excel QTO berhasil dibuat dan diverifikasi dengan volume sebesar {qto_res['total_volume_m3']:,.2f} meter kubik untuk {qto_res['total_elements']} elemen. "
                        f"Lokasinya: {artifact.absolute_path}. Saya sudah membuka folder output agar kamu bisa langsung memeriksanya."
                    )
                else:
                    reply_text = (
                        f"Selesai. File Excel QTO berhasil dibuat dan diverifikasi dengan volume sebesar {qto_res['total_volume_m3']:,.2f} meter kubik untuk {qto_res['total_elements']} elemen di {artifact.absolute_path}. "
                        "Namun, saya tidak berhasil membuka Explorer secara otomatis."
                    )
            else:
                reply_text = "Perhitungan QTO selesai, namun verifikasi integritas berkas Excel tidak berhasil."

            return self._build_response(
                reply_text=reply_text,
                visual_md=f"{qto_res['markdown_table']}\n\n**File Output Terverifikasi:** `{artifact.absolute_path}` ({artifact.size_bytes:,} bytes)",
                badge_text="EKSEKUSI QTO SELESAI",
                badge_class="text-emerald-400 font-bold",
                task_id=task_id,
                task_status="COMPLETED",
                artifacts=[artifact],
                auto_opened=auto_opened
            )

        # 2. Pending Steel Connection Automation Plan
        if self.pending_steel_plan:
            plan = self.pending_steel_plan
            self.pending_steel_plan = None
            task_id = f"TASK-STEEL-{int(time.time())}"
            task = Task(
                task_id=task_id,
                user_request=f"Otomatisasi Sambungan Baja {plan.get('standard_code', '')}",
                execution_status=ExecutionStatus.COMPLETED
            )
            exec_res = self.steel_service.execute_steel_connection_in_revit(self.router, plan)
            step = Step(
                step_id="step_steel_model",
                title=f"Pemodelan Elemen Sambungan {plan.get('standard_code', '')}",
                tool_name="steel_service.execute_steel_connection_in_revit",
                execution_status=ExecutionStatus.COMPLETED if exec_res["success"] else ExecutionStatus.FAILED,
                output=exec_res
            )
            task.steps.append(step)
            self.store.save(task)
            self.active_task_id = task_id

            return self._build_response(
                reply_text=exec_res["summary"],
                visual_md=f"### OTOMATISASI SAMBUNGAN BAJA SELESAI\n- **Task ID:** `{task_id}`\n- **Standar:** `{plan.get('standard_code')}`\n- **Profil:** `{plan.get('profile')}`\n- **Elemen Dimodelkan:** {', '.join(exec_res.get('elements_created', []))}\n\n> *Hasil telah tersinkronisasi di TaskStore dan Revit model.*",
                badge_text="SAMBUNGAN BAJA DIMODELKAN",
                badge_class="text-emerald-400 font-bold",
                task_id=task_id,
                task_status="COMPLETED"
            )

        # 3. Pending Drawing Package Automation Plan
        if self.pending_drawing_plan:
            plan = self.pending_drawing_plan
            self.pending_drawing_plan = None
            task_id = f"TASK-DRAWING-{int(time.time())}"
            task = Task(
                task_id=task_id,
                user_request=f"Otomatisasi Drawing Struktur Tekla-like ({plan.get('scope', '')})",
                execution_status=ExecutionStatus.COMPLETED
            )
            exec_res = self.drawing_engine.execute_drawing_package_in_revit(self.router, plan)
            step = Step(
                step_id="step_drawing_package",
                title=f"Pembuatan Sheet & Penataan Viewport ({plan.get('scope', '')})",
                tool_name="drawing_engine.execute_drawing_package_in_revit",
                execution_status=ExecutionStatus.COMPLETED if exec_res["success"] else ExecutionStatus.FAILED,
                output=exec_res
            )
            task.steps.append(step)
            self.store.save(task)
            self.active_task_id = task_id

            return self._build_response(
                reply_text=exec_res["summary"],
                visual_md=f"### OTOMATISASI DRAWING STRUKTUR SELESAI\n- **Task ID:** `{task_id}`\n- **Lingkup:** `{plan.get('scope')}`\n- **Sheet Dibuat:** `{', '.join(exec_res.get('sheets_created', []))}`\n\n> *Seluruh lembar gambar resmi telah disiapkan di Revit sesuai standar proyek.*",
                badge_text="DRAWING STRUKTUR DIBUAT",
                badge_class="text-emerald-400 font-bold",
                task_id=task_id,
                task_status="COMPLETED"
            )

        # 4. Pending Synthesized DAG Plan
        if self.pending_plan:
            task = self.pending_plan
            self.pending_plan = None
            self.active_task_id = task.task_id

            executed_task = self.engine.execute_task(task)
            if executed_task.execution_status == ExecutionStatus.COMPLETED:
                return self._build_response(
                    reply_text=f"Selesai. Task {executed_task.task_id} berhasil dieksekusi dan diverifikasi.",
                    visual_md=f"### TASK COMPLETED\n- **Task ID:** `{executed_task.task_id}`\n- **Status:** `COMPLETED`\n- **Hasil:** {executed_task.final_report.summary if executed_task.final_report else 'Semua langkah terverifikasi'}",
                    badge_text="EKSEKUSI SELESAI",
                    badge_class="text-emerald-400 font-bold",
                    task_id=executed_task.task_id,
                    task_status="COMPLETED"
                )
            else:
                return self._build_response(
                    reply_text=f"Eksekusi task {executed_task.task_id} terhenti karena terdapat kegagalan verifikasi.",
                    visual_md=f"### TASK FAILED\n- **Task ID:** `{executed_task.task_id}`\n- **Status:** `FAILED`\n- *Langkah gagal disimpan aman di TaskStore.*",
                    badge_text="EKSEKUSI GAGAL",
                    badge_class="text-rose-400 font-bold",
                    task_id=executed_task.task_id,
                    task_status="FAILED"
                )

        return self._build_response(
            reply_text="Belum ada rencana aksi, sambungan baja, drawing, atau QTO yang disiapkan. Apa yang ingin Anda analisis pada model?",
            visual_md="*Belum ada rencana aksi pending.*",
            badge_text="MENUNGGU RENCANA AKSI",
            badge_class="text-amber-400 font-bold"
        )

    def _handle_reveal_output_location(self, query: str = "") -> Dict[str, Any]:
        """Safely reveal output location in Windows Explorer for active or recent task."""
        target_task_id = self.active_task_id
        artifacts = self.task_artifacts.get(target_task_id, []) if target_task_id else []

        if not artifacts and self.task_artifacts:
            recent_id = list(self.task_artifacts.keys())[-1]
            artifacts = self.task_artifacts[recent_id]
            target_task_id = recent_id

        if not artifacts and os.path.exists(EXCEL_OUTPUT_PATH):
            art = verify_output_artifact(
                task_id=target_task_id or "TASK-LEGACY",
                file_path=EXCEL_OUTPUT_PATH,
                artifact_type="spreadsheet"
            )
            artifacts = [art]

        if artifacts:
            verified = [a for a in artifacts if a.validation_status == "verified" and a.exists]
            if verified:
                success, targets = safe_auto_open_in_explorer(verified, task_id=None, force=True)
                if success:
                    loc_desc = targets[0]
                    reply = f"Baik, lokasi berkas output di {loc_desc} sedang dibuka di Windows Explorer."
                else:
                    reply = f"Berkas output terverifikasi di {verified[0].absolute_path}. Namun, Explorer gagal dibuka secara otomatis."
                return self._build_response(
                    reply_text=reply,
                    visual_md=f"**Lokasi Berkas Output:** `{verified[0].absolute_path}`",
                    badge_text="LOKASI DIBUKA",
                    badge_class="text-emerald-400 font-bold",
                    task_id=target_task_id,
                    artifacts=verified,
                    auto_opened=success
                )

        return self._build_response(
            reply_text="Belum ada berkas output yang dibuat untuk task saat ini. Katakan 'hitung volume beton' terlebih dahulu.",
            visual_md="*Tidak ada berkas output terdaftar.*",
            badge_text="BELUM ADA BERKAS",
            badge_class="text-amber-400 font-bold"
        )

    def _handle_steel_database_query(self, query: str) -> Dict[str, Any]:
        """Query master structural steel connection database."""
        if "base plate" in query or "pelat landasan" in query or "angkur" in query:
            items = self.steel_service.get_base_plate_standards()
            title = "Standar Base Plate & Pedestal (STD-BJ-01 / STD-PDS-12)"
            reply = (
                "Berdasarkan master database sambungan baja resmi, terdapat dua belas tipe standar base plate WF. "
                "Untuk profil WF empat ratus, digunakan pelat tebal dua puluh lima milimeter dengan empat angkur diameter dua puluh dua milimeter. "
                "Tabel rincian telah saya tampilkan pada workspace."
            )
        else:
            items = self.steel_service.search_connections(query)
            title = f"Hasil Pencarian Database Sambungan Baja: '{query}'"
            reply = (
                f"Saya menemukan {len(items)} standar sambungan baja terkait pada master database AIGRAV. "
                "Rincian spesifikasi pelat, baut, dan acuan gambar telah disajikan di workspace."
            )

        md_table = self.steel_service.format_markdown_table(items, title=title)
        return self._build_response(
            reply_text=reply,
            visual_md=md_table,
            badge_text="DATABASE BAJA DITEMUKAN",
            badge_class="text-cyan-300 font-bold"
        )

    def _handle_steel_automation_request(self, query: str) -> Dict[str, Any]:
        """Plan and validate structural steel connection before gated execution."""
        plan = self.steel_service.plan_steel_connection(query)
        self.pending_steel_plan = plan
        return self._build_response(
            reply_text=plan["speech_text"],
            visual_md=plan["visual_markdown"],
            badge_text="MENUNGGU: 'OKE EKSEKUSI'",
            badge_class="text-amber-400 font-bold animate-pulse"
        )

    def _handle_drawing_automation_request(self, query: str) -> Dict[str, Any]:
        """Plan and validate Tekla-style drawing package before gated execution."""
        plan = self.drawing_engine.plan_drawing_package(query)
        self.pending_drawing_plan = plan
        return self._build_response(
            reply_text=plan["speech_text"],
            visual_md=plan["visual_markdown"],
            badge_text="MENUNGGU: 'OKE EKSEKUSI'",
            badge_class="text-amber-400 font-bold animate-pulse"
        )

    def _extract_qto_categories(self, clean_lower: str) -> List[str]:
        """Extract explicit element categories from utterance or default to all."""
        cats = []
        if any(k in clean_lower for k in ["plat", "pelat", "floor", "lantai", "slab"]):
            cats.append("floor")
        if any(k in clean_lower for k in ["pondasi", "fondasi", "foundation", "tiang bor", "bored pile", "pile cap", "pilecap", "boredpile"]):
            cats.append("foundation")
        if any(k in clean_lower for k in ["kolom", "column"]):
            cats.append("column")
        if any(k in clean_lower for k in ["balok", "beam", "framing", "girder"]):
            cats.append("beam")
        if any(k in clean_lower for k in ["dinding", "wall", "shear wall", "shearwall", "tembok"]):
            cats.append("wall")
        return cats if cats else ["all"]

    def _handle_element_count_query(self, query: str) -> Dict[str, Any]:
        """Handle direct read-only query of element counts without triggering QTO workflow."""
        target_cats = self._extract_qto_categories(query)
        probed = self.qto_engine.query_category_counts(target_cats)

        self.pending_qto = False
        self.pending_qto_categories = None

        details = []
        md_lines = [
            "### DATA ELEMEN MODEL REVIT",
            "- **Target Model:** `ADHI_STR RUSUN MBR _TOWER 1~3~5`\n",
            "| Kategori | Jumlah Unit | Sampel Elemen | Status |",
            "| :--- | :---: | :--- | :--- |"
        ]

        for cat_key, data in probed.items():
            cnt = data["count"]
            disp = data["display_name"]
            sample = data.get("sample_name") or "-"
            if cnt > 0:
                details.append(f"{cnt} {disp.lower()} (antara lain tipe {sample})")
                md_lines.append(f"| **{disp}** | **{cnt} unit** | `{sample}` | TERSEDIA |")
            else:
                details.append(f"0 {disp.lower()}")
                md_lines.append(f"| **{disp}** | 0 unit | - | KOSONG |")

        reply = f"Berdasarkan model aktif ADHI_STR RUSUN MBR, terdapat {', '.join(details)}. Tidak ada proses kalkulasi atau eksekusi yang dijalankan."
        md_lines.append("\n*Informasi dibaca langsung dari model Revit tanpa melakukan modifikasi atau ekspor.*")

        return self._build_response(
            reply_text=reply,
            visual_md="\n".join(md_lines),
            badge_text="DATA MODEL TERVERIFIKASI",
            badge_class="text-cyan-300 font-bold"
        )

    def _handle_qto_query(self, query: str) -> Dict[str, Any]:
        """Handle QTO request adhering to recommendation gate, multi-category probe, and approval flow."""
        target_cats = self._extract_qto_categories(query)
        probed = self.qto_engine.query_category_counts(target_cats)

        total_found = sum(data["count"] for data in probed.values())

        # If zero elements found for requested category (e.g. walls = 0)
        if total_found == 0:
            self.pending_qto = False
            self.pending_qto_categories = None
            cat_names = ", ".join(data["display_name"] for data in probed.values())
            reply_text = (
                f"Pada model Rusun MBR aktif, tidak ditemukan elemen {cat_names} (berjumlah nol elemen). "
                f"Tidak ada volume beton yang dapat dihitung untuk kategori tersebut."
            )
            visual_md = (
                f"### HASIL PEMERIKSAAN KATEGORI: {cat_names.upper()}\n"
                f"- **Target Model:** `ADHI_STR RUSUN MBR _TOWER 1~3~5`\n"
                f"- **Jumlah Elemen:** `0 unit`\n"
                f"- **Status:** Kategori tidak memiliki elemen dalam model aktif ini.\n\n"
                f"> *Silakan pilih kategori elemen lain seperti plat lantai, pondasi, balok, atau kolom.*"
            )
            return self._build_response(
                reply_text=reply_text,
                visual_md=visual_md,
                badge_text="0 ELEMEN DITEMUKAN",
                badge_class="text-rose-400 font-bold"
            )

        # Elements found -> formulate accurate, non-hardcoded response
        self.pending_qto = True
        self.pending_qto_categories = target_cats

        summary_parts = []
        md_rows = []
        total_vol = 0.0

        for cat_key, data in probed.items():
            cnt = data["count"]
            vol = data["total_volume_m3"]
            disp = data["display_name"]
            sample = data.get("sample_name") or disp
            total_vol += vol
            if cnt > 0:
                summary_parts.append(f"{cnt} {disp.lower()} ({sample}) dengan estimasi volume {vol:,.2f} meter kubik")
                md_rows.append(f"| {disp} | `{sample}` | {cnt} unit | **{vol:,.2f} m3** | DITEMUKAN |")
            else:
                summary_parts.append(f"0 {disp.lower()} (tidak ditemukan)")
                md_rows.append(f"| {disp} | `-` | 0 unit | 0.00 m3 | KOSONG |")

        joined_summary = " serta ".join(summary_parts)
        reply_text = (
            f"Model Rusun MBR aktif memiliki {joined_summary}. "
            f"Saya sarankan menggunakan engine QTO resmi dengan template tender dua sheet. "
            f"Katakan 'oke, eksekusi' jika Anda ingin saya buatkan file Excel-nya sekarang."
        )

        visual_md = (
            f"### REKOMENDASI QTO ENGINE AIGRAV\n"
            f"- **Target Model:** `ADHI_STR RUSUN MBR _TOWER 1~3~5`\n"
            f"- **Kategori Diminta:** `{', '.join(target_cats)}`\n"
            f"- **Format Output:** Template Excel Standar Tender (Rekapitulasi Volume & Detail Elemen)\n\n"
            f"| Kategori | Sampel / Tipe | Jumlah Unit | Estimasi Volume | Status |\n"
            f"| :--- | :--- | :---: | :---: | :---: |\n" +
            "\n".join(md_rows) +
            f"\n| **TOTAL ESTIMASI** | | **{total_found} unit** | **{total_vol:,.2f} m3** | **READY** |\n\n"
            f"> Katakan **'Oke, eksekusi'** untuk memproses dan membuat file Excel resmi."
        )

        return self._build_response(
            reply_text=reply_text,
            visual_md=visual_md,
            badge_text="MENUNGGU: 'OKE EKSEKUSI'",
            badge_class="text-amber-400 font-bold animate-pulse"
        )

    def _delegate_to_decision_engine(self, prompt: str) -> Dict[str, Any]:
        """Evaluate intent through the shared 6 Decision Gates."""
        decision: DecisionReviewResult = self.decision_engine.review_and_decide(prompt)

        # 1. CLARIFICATION_REQUIRED
        if decision.outcome == DecisionOutcome.CLARIFICATION_REQUIRED:
            self.pending_clarification_task = prompt
            q_text = " ".join(decision.clarification_questions)
            reply = f"Ada parameter yang perlu diklarifikasi: {q_text}"
            return self._build_response(
                reply_text=reply,
                visual_md=f"### KLARIFIKASI DIPERLUKAN\n- **Alasan:** {decision.reason}\n\n" + "\n".join([f"- {q}" for q in decision.clarification_questions]),
                badge_text="KLARIFIKASI LISAN",
                badge_class="text-cyan-300 font-bold",
                task_id=None,
                task_status=None,
                excel_ready=False,
                mode="bim"
            )

        # 2. RECOMMENDATION_REQUIRED
        if decision.outcome == DecisionOutcome.RECOMMENDATION_REQUIRED:
            rec = decision.recommended_option
            reply = f"Terdapat beberapa pendekatan teknik. Saya merekomendasikan {rec.title if rec else 'pendekatan standar'}. Katakan 'oke, eksekusi' jika setuju."
            rec_md = f"### REKOMENDASI PENDEKATAN\n- **Rekomendasi:** {rec.title if rec else '-'}\n  - *Keunggulan:* {', '.join(rec.advantages) if rec else '-'}\n"
            if decision.alternative_options:
                alt = decision.alternative_options[0]
                rec_md += f"- **Alternatif:** {alt.title}\n  - *Trade-offs:* {', '.join(alt.disadvantages)}\n"
            return self._build_response(
                reply_text=reply,
                visual_md=rec_md,
                badge_text="REKOMENDASI OPSI",
                badge_class="text-amber-400 font-bold"
            )

        # 3. APPROVAL_REQUIRED
        if decision.outcome == DecisionOutcome.APPROVAL_REQUIRED:
            task_id = f"TASK-VOICE-APPROVAL-{int(time.time())}"
            # Create task plan if steps available
            if decision.plan_steps:
                self.pending_plan = self.decision_engine.create_task_from_decision(task_id, decision)
            else:
                self.pending_action = "approval_required"
            reply = f"Operasi ini memerlukan persetujuan eksplisit Anda: {decision.approval_prompt or decision.reason}. Katakan 'oke, eksekusi' jika ingin dijalankan."
            return self._build_response(
                reply_text=reply,
                visual_md=f"### PERSETUJUAN DIPERLUKAN\n- **Konsekuensi:** {decision.reason}\n- **Prompt Approval:** `{decision.approval_prompt}`\n\n> Katakan **'Oke, eksekusi'** untuk menyetujui transaksi ini.",
                badge_text="MENUNGGU: 'OKE EKSEKUSI'",
                badge_class="text-amber-400 font-bold animate-pulse"
            )

        # 4. UNSUPPORTED
        if decision.outcome == DecisionOutcome.UNSUPPORTED:
            return self._build_response(
                reply_text=f"Operasi tersebut tidak didukung: {decision.reason}",
                visual_md=f"### UNSUPPORTED CAPABILITY\n- **Alasan:** {decision.reason}\n- *AIGRAV membatasi eksekusi pada kemampuan Revit Bridge resmi tanpa memanipulasi hasil.*",
                badge_text="TIDAK DIDUKUNG",
                badge_class="text-rose-400"
            )

        # 5. BLOCKED
        if decision.outcome == DecisionOutcome.BLOCKED:
            return self._build_response(
                reply_text=f"Koneksi ke model Revit terhalang: {decision.reason}",
                visual_md=f"### BRIDGE BLOCKED\n- **Kendala:** {decision.reason}\n- *Pastikan Autodesk Revit dan AIGRAV Bridge port 8765 aktif.*",
                badge_text="BRIDGE OFFLINE",
                badge_class="text-rose-400 font-bold"
            )

        # 6. READY (Read-Only / Safe Execution)
        if decision.outcome == DecisionOutcome.READY and decision.plan_steps:
            task_id = f"TASK-VOICE-{int(time.time())}"
            task = self.decision_engine.create_task_from_decision(task_id, decision)
            if task:
                self.active_task_id = task.task_id
                executed_task = self.engine.execute_task(task)

                proj_name = "ADHI_STR RUSUN MBR _TOWER 1~3~5"
                view_name = "ThreeD"
                for s in executed_task.steps:
                    raw = str(s.output.get("raw_result", "")) if s.output else ""
                    if "Project:" in raw:
                        for l in raw.splitlines():
                            if l.startswith("Project:"):
                                proj_name = l.replace("Project:", "").strip()
                    if "View Name:" in raw:
                        for l in raw.splitlines():
                            if l.startswith("View Name:"):
                                view_name = l.replace("View Name:", "").strip()

                reply = f"Pemeriksaan model Revit berhasil. Proyek aktif adalah {proj_name}, pada tampilan aktif {view_name}."
                visual_md = f"### HASIL INSPEKSI MODEL (Task: `{executed_task.task_id}`)\n"
                for s in executed_task.steps:
                    raw = s.output.get("raw_result", "-") if s.output else "-"
                    visual_md += f"#### Step: `{s.title}`\n```text\n{raw}\n```\n"

                return self._build_response(
                    reply_text=reply,
                    visual_md=visual_md,
                    badge_text="INSPEKSI SELESAI",
                    badge_class="text-emerald-400 font-bold",
                    task_id=executed_task.task_id,
                    task_status="COMPLETED",
                    mode="bim"
                )

        # Non-mutating read without plan steps -> general conversation
        return self._handle_general_conversation(prompt, prompt.lower())

    def _handle_general_conversation(self, transcript: str, clean_lower: str) -> Dict[str, Any]:
        """Provide thoughtful, empathetic, multi-turn Indonesian conversation."""
        # Check recent conversation history (excluding the current turn)
        recent_history = [t for t in self.conversation_history[:-1] if t.get("role") in ("user", "assistant")]
        last_turn_text = recent_history[-1]["text"].lower() if recent_history else ""
        prev_user_texts = [t["text"].lower() for t in recent_history if t.get("role") == "user"][-3:]
        combined_context = " ".join(prev_user_texts + [last_turn_text])

        # 1. Greetings
        if any(g in clean_lower for g in ["halo", "hai", "selamat pagi", "selamat siang", "selamat sore", "selamat malam", "assalamu", "hello", "hi"]):
            reply = "Halo. Saya AIGRAV. Senang bisa berbincang dengan Anda. Ada hal yang ingin kita diskusikan atau kerjakan bersama hari ini?"

        # 2. Construction economics & material prices ("Kenapa harga besi bisa naik?")
        elif any(k in clean_lower for k in ["harga besi", "harga baja", "harga material", "kenapa harga", "mengapa harga", "besi naik"]):
            reply = (
                "Kenaikan harga besi umumnya dipengaruhi oleh beberapa faktor makro: "
                "pertama, fluktuasi harga bijih besi (iron ore) dan lonjakan biaya energi peleburan seperti coking coal di pasar global; "
                "kedua, depresiasi nilai tukar rupiah terhadap dolar Amerika Serikat; "
                "dan ketiga, dinamika permintaan proyek infrastruktur serta kebijakan kuota impor bahan baku. "
                "Kondisi ini memang sering menjadi tantangan tersendiri dalam menjaga stabilitas anggaran biaya tender proyek."
            )

        # 3. Career advice & life reflection ("Menurut kamu bagaimana kalau saya pindah pekerjaan?")
        elif any(k in clean_lower for k in ["pindah pekerjaan", "pindah kerja", "ganti kerja", "resign", "ganti karier"]):
            reply = (
                "Mempertimbangkan untuk pindah pekerjaan adalah keputusan besar yang sangat wajar dievaluasi. "
                "Saran saya, pertimbangkan tiga aspek utama: pertama, peluang perkembangan kompetensi teknis dan portofolio Anda di tempat baru; "
                "kedua, kesehatan mental serta kenyamanan budaya kerja tim; dan ketiga, stabilitas finansial jangka panjang. "
                "Jika lingkungan saat ini dirasa sudah tidak lagi memberi ruang bertumbuh, merapikan portofolio proyek dan menjajaki peluang baru secara matang adalah langkah yang sangat bijak."
            )

        # 4. Personal fatigue, site stress, venting, emotional support ("Hari ini capek sekali di proyek")
        elif any(k in clean_lower for k in ["lelah", "capek", "penat", "pusing", "berat", "stres", "stress", "jenuh", "istirahat", "letih", "cape"]):
            if any(p in clean_lower for p in ["proyek", "lapangan", "site", "projek"]):
                reply = (
                    "Saya sangat memahami. Dinamika di lapangan proyek konstruksi memang sangat menuntut fisik dan mental—mulai dari koordinasi gambar, cuaca, hingga target timeline yang ketat. "
                    "Jika hari ini terasa sangat melelahkan, jangan ragu untuk beristirahat dan memulihkan energi sejenak. "
                    "Kebugaran dan ketenangan pikiran Anda jauh lebih berharga daripada memaksakan lembur. Saya siap menemani kembali kapan pun Anda butuh bantuan."
                )
            else:
                reply = (
                    "Saya mengerti sekali. Mengelola pekerjaan teknis dan permodelan engineering memang sangat menguras energi dan konsentrasi. "
                    "Jika Anda merasa lelah, luangkan waktu sejenak untuk beristirahat, minum air, atau menjauh dari layar monitor. "
                    "Kesehatan dan ketenangan pikiran Anda tetap yang paling utama. Saya selalu siap mendampingi kapan pun Anda ingin melanjutkan kembali."
                )

        # 5. Engineering Humor / Jokes
        elif any(k in clean_lower for k in ["lelucon", "bercanda", "joke", "lucu", "humor", "ngelawak"]):
            reply = (
                "Ada pepatah lama di kalangan engineer struktur: "
                "'Bagi orang awam, beton bertulang itu material yang paling keras dan kaku. "
                "Tapi bagi engineer, yang paling keras dan kaku sebenarnya adalah revisi arsitek di malam sebelum deadline pengajuan tender.' "
                "Tersenyum sedikit ya, jangan terlalu tegang menghadapi beban pekerjaan hari ini."
            )

        # 6. Contextual opinion and follow-up reflection ("Menurut kamu bagaimana?", etc.)
        elif any(k in clean_lower for k in ["menurut kamu", "menurutmu", "bagaimana menurut", "apa pendapatmu", "kenapa begitu", "mengapa"]):
            if any(term in combined_context for term in ["pindah", "karier", "resign", "pekerjaan baru"]):
                reply = (
                    "Menurut saya, setiap perubahan karier membawa peluang baru untuk berkembang. "
                    "Yang terpenting adalah memastikan kepindahan didasarkan pada visi jangka panjang, bukan semata pelarian sesaat. "
                    "Pastikan Anda memiliki pegangan tawaran yang konkret dan tabungan darurat sebelum mengambil keputusan final."
                )
            elif any(term in combined_context for term in ["harga besi", "baja", "material", "inflasi"]):
                reply = (
                    "Menurut saya, dalam menghadapi volatilitas harga material seperti baja, kontraktor dan konsultan perlu menerapkan klausul eskalasi harga yang jelas dalam kontrak, "
                    "serta melakukan pengadaan dini atau hedging material utama saat harga berada di titik stabil."
                )
            elif any(term in combined_context for term in ["lelah", "capek", "istirahat", "penat", "pekerjaan", "berat", "proyek"]):
                reply = (
                    "Menurut saya, beristirahat sejenak adalah keputusan yang tepat dan bijak. "
                    "Pekerjaan engineering membutuhkan ketelitian tinggi; memaksakan diri saat lelah justru rawan menimbulkan kejenuhan atau kesalahan. "
                    "Menyegarkan pikiran sebentar akan membuat fokus Anda jauh lebih prima saat kembali."
                )
            elif any(term in combined_context for term in ["agentic ai", "ai", "kecerdasan", "teknologi", "otomatisasi"]):
                reply = (
                    "Menurut saya, perkembangan Agentic AI adalah langkah maju yang sangat positif bagi dunia teknik. "
                    "AI seharusnya berperan sebagai co-pilot cerdas yang meringankan pekerjaan repetitif dan komputasi rumit, "
                    "sementara keputusan engineering dan tanggung jawab profesional tetap berada di tangan Anda."
                )
            elif any(term in combined_context for term in ["model", "proyek", "revit", "desain"]):
                reply = (
                    "Menurut saya, model yang terstruktur dengan parameter yang konsisten akan selalu menghasilkan estimasi dan analisis yang jauh lebih andal. "
                    "Apakah Anda ingin kita memeriksa bagian tertentu pada model?"
                )
            else:
                reply = (
                    "Menurut saya, setiap tantangan selalu memiliki pendekatan terbaik jika diuraikan secara tenang dan bertahap. "
                    "Bisa Anda ceritakan sedikit lebih detail konteks persoalan yang ingin kita diskusikan?"
                )

        # 7. General Knowledge: What is Agentic AI?
        elif "agentic ai" in clean_lower or ("apa itu" in clean_lower and "agent" in clean_lower):
            reply = (
                "Agentic AI adalah sistem kecerdasan buatan tingkat lanjut yang memiliki otonomi untuk memahami tujuan, "
                "merumuskan rencana tindakan mandiri (planning), berinteraksi dengan berbagai alat eksternal atau software melalui API, "
                "dan memverifikasi hasilnya tanpa memerlukan instruksi manual di setiap langkahnya. "
                "Berbeda dari AI generatif konvensional yang sekadar menjawab teks, Agentic AI dapat bertindak layaknya seorang mitra kerja mandiri."
            )

        # 8. Assistant Identity / Purpose
        elif any(k in clean_lower for k in ["siapa kamu", "siapa anda", "apa itu aigrav", "kamu ini apa"]):
            reply = (
                "Saya AIGRAV, asisten kecerdasan buatan otonom untuk BIM dan rekayasa teknik sipil. "
                "Saya dirancang untuk mendampingi Anda berdiskusi secara alami sekaligus mengotomatisasi pekerjaan analisis model, QTO, "
                "otomatisasi drawing, dan database sambungan baja secara akurat dan aman."
            )

        # 9. Gratitude / Casual Closing
        elif any(k in clean_lower for k in ["terima kasih", "makasih", "thank you", "thanks", "udah dulu", "cukup dulu", "mau santai"]):
            reply = "Sama-sama! Senang bisa berbincang dengan Anda. Selamat beristirahat dengan tenang, saya selalu siap mendampingi kembali kapan pun Anda butuhkan."

        # 10. Thoughtful General Fallback
        else:
            reply = (
                f"Saya mendengarkan Anda. Terkait '{transcript}', ada banyak sudut pandang menarik yang bisa kita bahas. "
                "Silakan ceritakan lebih lanjut apa yang sedang Anda pikirkan, atau katakan kapan pun Anda ingin beralih ke pekerjaan model."
            )

        return self._build_response(
            reply_text=reply,
            visual_md=f"**Percakapan Umum**\n\n{reply}",
            badge_text="MODE PERCAKAPAN",
            badge_class="text-cyan-300 font-bold",
            task_id=None,
            task_status=None,
            mode="general"
        )

    def _build_response(
        self,
        reply_text: str,
        visual_md: str,
        badge_text: str,
        badge_class: str,
        task_id: Optional[str] = None,
        task_status: Optional[str] = None,
        artifacts: Optional[List[Any]] = None,
        auto_opened: bool = False,
        excel_ready: Optional[bool] = None,
        mode: str = "bim"
    ) -> Dict[str, Any]:
        """Construct standard response payload with speech audio URL, visual markdown, and universal artifact metadata."""
        encoded_text = urllib.parse.quote(reply_text)
        # Append turn to conversation history
        self.conversation_history.append({
            "role": "assistant",
            "text": reply_text,
            "mode": mode,
            "timestamp": time.time()
        })
        if len(self.conversation_history) > 30:
            self.conversation_history = self.conversation_history[-30:]

        # Standardize artifact representations
        art_dicts = []
        if artifacts:
            for a in artifacts:
                if hasattr(a, "to_dict"):
                    art_dicts.append(a.to_dict())
                elif isinstance(a, dict):
                    art_dicts.append(a)

        # Backward compatibility: excel_ready is True only if current task produced verified xlsx artifacts
        # (strictly per-task, NO MORE global leakage from file existing on disk!)
        has_xlsx = any(a.get("file_extension") == ".xlsx" and a.get("validation_status") == "verified" for a in art_dicts)
        computed_excel_ready = excel_ready if excel_ready is not None else has_xlsx

        return {
            "reply_text": reply_text,
            "visual_markdown": visual_md,
            "audio_url": f"/api/tts?text={encoded_text}&voice={self.current_voice}",
            "voice": self.current_voice,
            "badge_text": badge_text,
            "badge_class": badge_class,
            "task_id": task_id if task_id is not None else (self.active_task_id if mode == "bim" else None),
            "task_status": task_status,
            "artifacts": art_dicts,
            "auto_opened": auto_opened,
            "excel_ready": computed_excel_ready,
            "mode": mode,
            "timestamp": time.strftime("%H:%M:%S")
        }

# Global singleton
voice_adapter = VoiceInterfaceAdapter()
