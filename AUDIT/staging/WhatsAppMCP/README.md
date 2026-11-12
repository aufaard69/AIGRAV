# WhatsApp Web MCP (Hardened with Explicit Send Approval)

Local-first Model Context Protocol (MCP) server for WhatsApp Web with strict server-side approval gates, draft staging, cryptographic integrity checks, and no raw sending tools.

---

## Key Security Features

1. **No Raw Send Tool**: The agent has zero access to raw sending tools (`send_message`, `quick_send`, etc.).
2. **Two-Stage Explicit Approval**:
   - `whatsapp_create_message_draft`: Stages message text, generates a UUIDv4 draft ID, computes SHA-256 fingerprint, and sets a 10-minute expiry (TTL). **Does NOT send.**
   - `whatsapp_approve_and_send`: Server validates that `draft_id`, `recipient`, and `exact_message_text` match the stored draft 100%. Any discrepancy or modification immediately aborts the send.
3. **Anti-Replay / Idempotent**: A draft can only be approved once. Once in `SENDING` or `SENT`, it cannot be re-executed.
4. **Uncertain Delivery Protection**: If message dispatch is triggered but WhatsApp's delivery confirmation check marks do not appear within the timeout, the draft is marked `UNCERTAIN` and aborted to prevent blind duplicate retries.
5. **Session Isolation**: Browser cookies and local session data reside in `.browser_data/`, completely ignored by git.
6. **Log Sanitization**: Logs never print raw message bodies; recipient identifiers are masked.

---

## Exposed MCP Tools

| Tool | Type | Purpose |
|:---|:---|:---|
| `whatsapp_get_status` | Read-only | Returns connection and authentication status (`authenticated`, `qr_needed`, or `disconnected`). |
| `whatsapp_list_recent_chats` | Read-only | Returns a list of recent chats with titles, unread counts, and last message previews. |
| `whatsapp_get_conversation_messages` | Read-only | Fetches recent message history for a specified recipient without mutating state. |
| `whatsapp_create_message_draft` | Staging | Stages a draft for future approval. Never dispatches. |
| `whatsapp_approve_and_send` | Approval Gate | Dispatches message if and only if all 3 parameters (`draft_id`, `recipient`, `exact_message_text`) pass strict validation. |

---

## Setup & Running

### Requirements
- Windows 10/11
- Python 3.11+ (Python 3.14 verified)
- Google Chrome or Microsoft Edge installed

### Installation
```powershell
cd C:\AIGRAV\WhatsAppMCP
python -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
```

### Running Tests
All tests run with mocked Playwright objects and offline fixtures:
```powershell
.\.venv\Scripts\pytest -v tests
```

---

## Antigravity MCP Configuration

Add this entry to `C:\Users\sapuser\.gemini\config\mcp_config.json`:

```json
{
  "mcpServers": {
    "whatsapp-mcp": {
      "command": "C:\\AIGRAV\\WhatsAppMCP\\.venv\\Scripts\\python.exe",
      "args": ["-m", "whatsapp_mcp.server"],
      "cwd": "C:\\AIGRAV\\WhatsAppMCP",
      "env": {
        "WHATSAPP_HEADLESS": "true",
        "WHATSAPP_USER_DATA_DIR": "C:\\AIGRAV\\WhatsAppMCP\\.browser_data"
      }
    }
  }
}
```

---

## First-Time Authentication & Manual Verification

1. When running for the first time, call `whatsapp_get_status()`.
2. If `status` is `"qr_needed"`, set `"WHATSAPP_HEADLESS": "false"` temporarily or run a dedicated QR scanning script to scan the QR code using WhatsApp on your phone.
3. Once logged in, session tokens are persisted in `.browser_data/`.
4. Subsequent calls will automatically report `"status": "authenticated"`.
