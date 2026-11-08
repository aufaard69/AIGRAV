"""
AIGRAV Version & Update Manager
Handles:
  1. Local version reading from version.json
  2. Remote GitHub version checking with token authentication
  3. AI-driven version publishing (increments version, updates changelog, commits, tags, and pushes)
  4. User-controlled update application with automatic Revit hot-reload
"""

import json
import os
import subprocess
import urllib.error
import urllib.request
from typing import Any, Dict, List, Optional

AIGRAV_ROOT = r"C:\AIGRAV"
VERSION_FILE = os.path.join(AIGRAV_ROOT, "version.json")
MCP_CONFIG_PATH = os.path.expanduser(r"~\.gemini\config\mcp_config.json")
GITHUB_REPO = "aufaard69/AIGRAV"


def get_github_token() -> str:
    """Extract GitHub token from mcp_config.json if available."""
    try:
        if os.path.isfile(MCP_CONFIG_PATH):
            with open(MCP_CONFIG_PATH, "r", encoding="utf-8-sig") as f:
                cfg = json.load(f)
            auth_header = cfg.get("mcpServers", {}).get("github-mcp-server", {}).get("headers", {}).get("Authorization", "")
            if auth_header.startswith("Bearer "):
                return auth_header[len("Bearer "):].strip()
            if auth_header.startswith("token "):
                return auth_header[len("token "):].strip()
    except Exception:
        pass
    return ""


def get_local_version_info() -> Dict[str, Any]:
    """Read local version.json metadata."""
    if os.path.isfile(VERSION_FILE):
        try:
            with open(VERSION_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            return {"version": "0.0.0", "error": f"Failed to read local version.json: {e}"}
    return {"version": "0.0.0", "error": "version.json not found"}


def parse_semver(ver_str: str) -> List[int]:
    """Parse semver string like '2.5.0' into [2, 5, 0]."""
    cleaned = ver_str.lstrip("vV").strip()
    parts = []
    for p in cleaned.split("."):
        try:
            parts.append(int(p))
        except ValueError:
            parts.append(0)
    while len(parts) < 3:
        parts.append(0)
    return parts[:3]


def is_newer_version(remote_ver: str, local_ver: str) -> bool:
    """Return True if remote_ver > local_ver."""
    return parse_semver(remote_ver) > parse_semver(local_ver)


def check_remote_version() -> Dict[str, Any]:
    """Check GitHub repository for the latest version.json and changelog."""
    local_info = get_local_version_info()
    local_ver = local_info.get("version", "0.0.0")

    token = get_github_token()
    headers = {
        "Accept": "application/vnd.github.raw+json",
        "User-Agent": "AIGRAV-VersionChecker"
    }
    if token:
        headers["Authorization"] = f"token {token}"

    url = f"https://api.github.com/repos/{GITHUB_REPO}/contents/version.json"
    req = urllib.request.Request(url, headers=headers)

    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            content = resp.read().decode("utf-8")
            remote_info = json.loads(content)
            remote_ver = remote_info.get("version", "0.0.0")

            has_update = is_newer_version(remote_ver, local_ver)
            return {
                "status": "Success",
                "update_available": has_update,
                "local_version": local_ver,
                "remote_version": remote_ver,
                "release_name": remote_info.get("release_name", ""),
                "release_date": remote_info.get("release_date", ""),
                "highlights": remote_info.get("highlights", []),
                "fixes": remote_info.get("fixes", []),
                "action_prompt": (
                    f"Pembaruan AIGRAV v{remote_ver} tersedia! "
                    "Ketik 'update sekarang' atau jalankan 'sync-aigrav.ps1' untuk memperbarui."
                    if has_update else "AIGRAV sudah berada pada versi terbaru."
                )
            }
    except urllib.error.HTTPError as e:
        return {
            "status": "HTTPError",
            "update_available": False,
            "local_version": local_ver,
            "error_code": e.code,
            "message": f"GitHub API error {e.code}: {e.reason}"
        }
    except Exception as e:
        return {
            "status": "Error",
            "update_available": False,
            "local_version": local_ver,
            "message": str(e)
        }


def publish_new_version(
    version: str,
    release_name: str,
    highlights: List[str],
    fixes: Optional[List[str]] = None,
    release_date: Optional[str] = None
) -> Dict[str, Any]:
    """
    Publish a new version to GitHub:
      1. Write updated version.json
      2. Git commit & push to GitHub
      3. Create Git tag vX.Y.Z & push tag
    """
    from datetime import date
    if not release_date:
        release_date = date.today().isoformat()
    if fixes is None:
        fixes = []

    version_data = {
        "version": version.lstrip("vV"),
        "release_name": release_name,
        "release_date": release_date,
        "highlights": highlights,
        "fixes": fixes
    }

    # 1. Update version.json
    with open(VERSION_FILE, "w", encoding="utf-8") as f:
        json.dump(version_data, f, indent=2, ensure_ascii=False)

    tag_name = f"v{version_data['version']}"
    commit_msg = f"release: {tag_name} - {release_name}"

    # 2. Git commit & push
    try:
        subprocess.run(["git", "-C", AIGRAV_ROOT, "add", "version.json"], check=True, capture_output=True)
        subprocess.run(["git", "-C", AIGRAV_ROOT, "commit", "-m", commit_msg], check=True, capture_output=True)
        subprocess.run(["git", "-C", AIGRAV_ROOT, "push", "origin", "main"], check=True, capture_output=True)

        # 3. Create & push tag
        subprocess.run(["git", "-C", AIGRAV_ROOT, "tag", "-a", tag_name, "-m", release_name], check=True, capture_output=True)
        subprocess.run(["git", "-C", AIGRAV_ROOT, "push", "origin", tag_name], check=True, capture_output=True)

        return {
            "status": "Success",
            "published_version": version_data["version"],
            "tag": tag_name,
            "release_name": release_name,
            "release_date": release_date,
            "highlights_count": len(highlights),
            "fixes_count": len(fixes),
            "message": f"AIGRAV {tag_name} berhasil di-publish ke GitHub! Pengguna lain kini dapat mendeteksi pembaruan ini."
        }
    except subprocess.CalledProcessError as err:
        stderr = err.stderr.decode("utf-8", errors="ignore") if err.stderr else ""
        return {
            "status": "GitError",
            "message": f"Git command failed: {err}",
            "details": stderr
        }


def apply_update_locally() -> Dict[str, Any]:
    """Execute sync-aigrav.ps1 to pull and reload Revit skills."""
    ps_script = os.path.join(AIGRAV_ROOT, "sync-aigrav.ps1")
    if not os.path.isfile(ps_script):
        return {"status": "Error", "message": "sync-aigrav.ps1 not found."}

    try:
        cmd = ["powershell.exe", "-ExecutionPolicy", "Bypass", "-File", ps_script]
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        return {
            "status": "Success" if proc.returncode == 0 else "Warning",
            "output": proc.stdout,
            "error_output": proc.stderr,
            "returncode": proc.returncode,
            "current_version": get_local_version_info().get("version", "Unknown")
        }
    except Exception as e:
        return {"status": "Error", "message": str(e)}
