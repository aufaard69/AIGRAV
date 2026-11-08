import json
import urllib.request
import urllib.error
from typing import Any

from mcp.server import MCPServer


# ============================================================
# AIGRAV MCP SERVER
# Compatible with MCP SDK 2.3.0
# ============================================================

mcp = MCPServer("AIGRAV MCP Server")

AIGRAV_URL = "http://127.0.0.1:8765/"
AIGRAV_TIMEOUT = 60


# ============================================================
# HTTP BRIDGE
# ============================================================

def call_aigrav(
    tool: str,
    argument: str | None = None,
) -> str:

    payload: dict[str, Any] = {
        "tool": tool
    }

    if argument is not None:
        payload["argument"] = argument

    data = json.dumps(payload).encode("utf-8")

    request = urllib.request.Request(
        AIGRAV_URL,
        data=data,
        headers={
            "Content-Type": "application/json"
        },
        method="POST",
    )

    try:

        with urllib.request.urlopen(
            request,
            timeout=AIGRAV_TIMEOUT,
        ) as response:

            response_data = (
                response
                .read()
                .decode("utf-8")
            )

            result = json.loads(
                response_data
            )

            success = result.get(
                "Success",
                result.get(
                    "success",
                    False
                )
            )

            result_text = result.get(
                "Result",
                result.get(
                    "result",
                    ""
                )
            )

            if not success:

                return (
                    "AIGRAV Error: "
                    + str(result_text)
                )

            return str(result_text)

    except urllib.error.URLError as ex:

        return (
            "AIGRAV Revit Bridge is not available. "
            f"Details: {ex}"
        )

    except Exception as ex:

        return (
            "AIGRAV MCP Error: "
            f"{ex}"
        )


# ============================================================
# GENERIC TOOL FACTORY
# ============================================================

def create_tool_function(
    tool_name: str,
    description: str,
):

    bridge_tool_name = tool_name
    if bridge_tool_name.startswith("aigrav_"):
        bridge_tool_name = bridge_tool_name[len("aigrav_"):]

    def tool_function(
        argument: str = "",
    ) -> str:

        return call_aigrav(
            bridge_tool_name,
            argument,
        )

    tool_function.__name__ = tool_name

    tool_function.__doc__ = description

    return tool_function


# ============================================================
# TOOL CATALOG
# ============================================================

TOOLS = {

    # --------------------------------------------------------
    # PROJECT / CONTEXT
    # --------------------------------------------------------

    "aigrav_ping":
        "Check whether the AIGRAV MCP server is running.",

    "aigrav_get_revit_info":
        "Get active Revit project information.",

    "aigrav_get_active_view":
        "Get the currently active Revit view.",

    "aigrav_get_selection":
        "Get currently selected Revit elements.",

    "aigrav_get_element":
        "Get basic information about a Revit element.",

    "aigrav_get_project_units":
        "Get project unit settings.",

    "aigrav_get_worksharing_info":
        "Get Revit worksharing information.",

    "aigrav_get_project_location":
        "Get Revit project location information.",

    "aigrav_get_phases":
        "Get project phases.",

    "aigrav_get_design_options":
        "Get project design options.",


    # --------------------------------------------------------
    # MODEL QUERY
    # --------------------------------------------------------

    "aigrav_get_categories":
        "Get categories available in the Revit model.",

    "aigrav_get_levels":
        "Get all levels in the current Revit document.",

    "aigrav_get_grids":
        "Get all grids in the current Revit document.",

    "aigrav_get_families":
        "Get loaded Revit families.",

    "aigrav_get_family_types":
        "Get available family types.",

    "aigrav_get_elements_by_category":
        "Get elements filtered by category.",

    "aigrav_get_elements_by_class":
        "Get elements filtered by Revit class.",

    "aigrav_get_elements_by_type":
        "Get elements belonging to a specified type.",

    "aigrav_get_elements_by_family":
        "Get elements belonging to a specified family.",

    "aigrav_get_elements_by_level":
        "Get elements associated with a level.",

    "aigrav_get_elements_in_view":
        "Get elements visible in a specified view.",

    "aigrav_get_elements_in_category":
        "Get elements from a specified category.",

    "aigrav_get_structural_elements":
        "Get structural elements from the model.",

    "aigrav_get_architecture_elements":
        "Get architectural elements from the model.",

    "aigrav_get_mep_elements":
        "Get MEP elements from the model.",

    "aigrav_get_walls":
        "Get walls from the model.",

    "aigrav_get_floors":
        "Get floors from the model.",

    "aigrav_get_roofs":
        "Get roofs from the model.",

    "aigrav_get_columns":
        "Get structural columns from the model.",

    "aigrav_get_beams":
        "Get structural framing elements.",

    "aigrav_get_doors":
        "Get doors from the model.",

    "aigrav_get_windows":
        "Get windows from the model.",

    "aigrav_get_rooms":
        "Get rooms from the model.",

    "aigrav_get_spaces":
        "Get MEP spaces from the model.",

    "aigrav_get_sheets":
        "Get sheets from the model.",

    "aigrav_get_views":
        "Get views from the model.",

    "aigrav_get_schedules":
        "Get schedules from the model.",

    "aigrav_get_materials":
        "Get materials from the model.",

    "aigrav_get_worksets":
        "Get worksets from the model.",

    "aigrav_get_links":
        "Get linked Revit models.",

    "aigrav_get_warnings":
        "Get current Revit warnings.",

    "aigrav_get_document_counts":
        "Get element counts in the document.",


    # --------------------------------------------------------
    # ELEMENT INTELLIGENCE
    # --------------------------------------------------------

    "aigrav_get_element_parameters":
        "Get parameters of a Revit element.",

    "aigrav_get_element_location":
        "Get the location of a Revit element.",

    "aigrav_get_element_bounding_box":
        "Get the bounding box of a Revit element.",

    "aigrav_get_element_type":
        "Get the type of a Revit element.",

    "aigrav_get_element_family":
        "Get the family of a Revit element.",

    "aigrav_get_element_level":
        "Get the level associated with a Revit element.",

    "aigrav_get_element_dimensions":
        "Get dimensions of a Revit element.",

    "aigrav_get_element_materials":
        "Get materials associated with a Revit element.",

    "aigrav_get_element_geometry":
        "Get geometry information for a Revit element.",

    "aigrav_get_element_host":
        "Get host information for a Revit element.",

    "aigrav_get_element_dependents":
        "Get dependent elements.",

    "aigrav_get_element_dependent_elements":
        "Get elements dependent on a given element.",

    "aigrav_get_element_constraints":
        "Get constraint information for an element.",

    "aigrav_get_element_joins":
        "Get join information for an element.",

    "aigrav_get_element_workset":
        "Get workset information for an element.",

    "aigrav_get_element_design_option":
        "Get design option information for an element.",

    "aigrav_get_element_phase":
        "Get phase information for an element.",

    "aigrav_get_element_created_phase":
        "Get created phase information.",

    "aigrav_get_element_demolished_phase":
        "Get demolished phase information.",

    "aigrav_get_element_category":
        "Get category information.",

    "aigrav_get_element_class":
        "Get Revit class information.",

    "aigrav_get_element_id":
        "Get element ID information.",

    "aigrav_get_element_name":
        "Get element name.",


    # --------------------------------------------------------
    # VIEW
    # --------------------------------------------------------

    "aigrav_activate_view":
        "Activate a Revit view.",

    "aigrav_rename_view":
        "Rename a Revit view.",

    "aigrav_duplicate_view":
        "Duplicate a Revit view.",

    "aigrav_set_view_scale":
        "Set the scale of a view.",

    "aigrav_get_view_elements":
        "Get elements visible in a view.",

    "aigrav_hide_elements":
        "Hide elements in a view.",

    "aigrav_unhide_elements":
        "Unhide elements in a view.",

    "aigrav_isolate_elements":
        "Temporarily isolate elements in a view.",

    "aigrav_get_view_detail_level":
        "Get view detail level.",

    "aigrav_set_view_detail_level":
        "Set view detail level.",

    "aigrav_get_view_discipline":
        "Get view discipline.",

    "aigrav_set_view_discipline":
        "Set view discipline.",

    "aigrav_get_view_properties":
        "Get view properties.",


    # --------------------------------------------------------
    # CREATE
    # --------------------------------------------------------

    "aigrav_create_level":
        "Create a new Revit level.",

    "aigrav_create_grid":
        "Create a new Revit grid.",

    "aigrav_create_wall":
        "Create a wall.",

    "aigrav_create_floor":
        "Create a floor.",

    "aigrav_create_column":
        "Create a structural column.",

    "aigrav_create_beam":
        "Create structural framing.",

    "aigrav_create_family_instance":
        "Create a family instance.",

    "aigrav_create_generic_model":
        "Create a generic model instance.",

    "aigrav_create_room":
        "Create a room.",

    "aigrav_create_area":
        "Create an area.",

    "aigrav_create_view":
        "Create a Revit view.",

    "aigrav_create_sheet":
        "Create a sheet.",

    "aigrav_create_dimension":
        "Create a dimension.",

    "aigrav_create_text_note":
        "Create a text note.",

    "aigrav_create_detail_curve":
        "Create a detail curve.",

    "aigrav_create_model_curve":
        "Create a model curve.",

    "aigrav_create_schedule":
        "Create a schedule.",


    # --------------------------------------------------------
    # MODIFY
    # --------------------------------------------------------

    "aigrav_set_parameter":
        "Set a Revit element parameter.",

    "aigrav_move_element":
        "Move a Revit element.",

    "aigrav_copy_element":
        "Copy a Revit element.",

    "aigrav_rotate_element":
        "Rotate a Revit element.",

    "aigrav_mirror_element":
        "Mirror a Revit element.",

    "aigrav_delete_element":
        "Delete a Revit element.",

    "aigrav_pin_element":
        "Pin a Revit element.",

    "aigrav_unpin_element":
        "Unpin a Revit element.",

    "aigrav_join_geometry":
        "Join geometry between elements.",

    "aigrav_unjoin_geometry":
        "Unjoin geometry between elements.",

    "aigrav_check_joined":
        "Check whether elements are joined.",

    "aigrav_switch_join_order":
        "Switch join order between elements.",

    "aigrav_change_type":
        "Change the type of an element.",

    "aigrav_change_level":
        "Change the level of an element.",

    "aigrav_set_location":
        "Set element location.",

    "aigrav_set_workset":
        "Set element workset.",

    "aigrav_set_phase":
        "Set element phase.",

    "aigrav_set_design_option":
        "Set design option.",

    "aigrav_set_comments":
        "Set element comments.",

    "aigrav_set_mark":
        "Set element mark.",

    "aigrav_set_name":
        "Set element name where supported.",

    "aigrav_change_wall_type":
        "Change wall type.",

    "aigrav_change_family_type":
        "Change family instance type.",

    "aigrav_change_view_template":
        "Change view template.",

    "aigrav_set_view_template":
        "Set view template.",

    "aigrav_set_view_crop":
        "Set view crop configuration.",

    "aigrav_set_visibility":
        "Set element visibility.",

    "aigrav_set_parameter_batch":
        "Set multiple parameters.",

    "aigrav_delete_elements":
        "Delete multiple elements.",

    "aigrav_move_elements":
        "Move multiple elements.",

    "aigrav_copy_elements":
        "Copy multiple elements.",

    "aigrav_rotate_elements":
        "Rotate multiple elements.",

    "aigrav_mirror_elements":
        "Mirror multiple elements.",

    "aigrav_pin_elements":
        "Pin multiple elements.",

    "aigrav_unpin_elements":
        "Unpin multiple elements.",

    "aigrav_join_elements":
        "Join multiple element pairs.",

    "aigrav_unjoin_elements":
        "Unjoin multiple element pairs.",

    "aigrav_rename_elements":
        "Rename multiple elements where supported.",

    "aigrav_regenerate":
        "Regenerate the Revit document.",


    # --------------------------------------------------------
    # DOCUMENT
    # --------------------------------------------------------

    "aigrav_save_document":
        "Save the active Revit document.",

    "aigrav_save_as":
        "Save the active document as another file.",

    "aigrav_sync_with_central":
        "Synchronize with central for workshared projects.",

    "aigrav_reload_latest":
        "Reload latest from central.",

    "aigrav_get_document_path":
        "Get document path.",

    "aigrav_get_document_title":
        "Get document title.",

    "aigrav_get_document_version":
        "Get document version.",

    "aigrav_get_document_state":
        "Get document state.",


    # --------------------------------------------------------
    # ANALYSIS
    # --------------------------------------------------------

    "aigrav_measure_distance":
        "Measure distance between points or elements.",

    "aigrav_get_element_length":
        "Get element length.",

    "aigrav_get_curve_endpoints":
        "Get curve endpoints.",

    "aigrav_validate_element":
        "Validate a Revit element.",

    "aigrav_validate_model":
        "Run model validation.",

    "aigrav_count_elements":
        "Count elements.",

    "aigrav_count_category":
        "Count elements by category.",

    "aigrav_find_duplicate_marks":
        "Find duplicate element marks.",

    "aigrav_find_duplicate_names":
        "Find duplicate names.",

    "aigrav_find_empty_parameters":
        "Find empty parameters.",

    "aigrav_find_warnings":
        "Find Revit warnings.",

    "aigrav_check_model_health":
        "Check general model health.",
}


# ============================================================
# REGISTER ALL LEGACY / STATIC TOOLS (100% BACKWARD COMPATIBLE)
# ============================================================

for _tool_name, _description in TOOLS.items():

    _function = create_tool_function(
        _tool_name,
        _description,
    )

    mcp.add_tool(
        _function,
        name=_tool_name,
        description=_description,
        structured_output=False,
    )


# ============================================================
# 5-PILLAR DYNAMIC AGENTIC SANDBOX & SPATIAL INTELLIGENCE
# ============================================================

import base64
import os
from aigrav_spatial_helper import (
    compose_quadrant_image,
    ft_to_mm,
    mm_to_ft,
    validate_python_ast,
)


@mcp.prompt(
    name="aigrav_spatial_modeling_sop",
    description="Mandatory 4-Phase Anti-Spatial-Blindness & DED Sheet Layout SOP for AIGRAV Revit AI Agent.",
)
def aigrav_spatial_modeling_sop() -> str:
    return (
        "# AIGRAV 4-PHASE SPATIAL MODELING & DED SHEET SOP\n"
        "1. **Fase 1 (Context & Datum)**: Wajib panggil `aigrav_get_project_context()` sebelum membuat geometri apa pun "
        "untuk memeriksa Level, Grid, dan Family yang tersedia. Jika belum ada Grid/Level, gunakan `aigrav_create_grid_matrix()`.\n"
        "2. **Fase 2 (Spatial Blueprinting)**: Tuliskan tabel/JSON rencana koordinat (dalam milimeter `mm`) dan titik jangkar (`anchor points`) di ruang chat. "
        "Perhatikan: Revit API adalah single-threaded untuk penulisan, gunakan tepat SATU (1) Executor untuk menulis transaksi.\n"
        "3. **Fase 3 (Iterative Build & Telemetry Check)**: Bangun struktur bertahap dari bawah ke atas. "
        "Gunakan fungsi relatif (`aigrav_place_on_top_of`, `aigrav_connect_elements`). Dilarang menelan error dengan catch kosong; wajib laporkan JSON terstruktur.\n"
        "4. **Fase 4 (Dual-Layer Independent Validation & Hak Veto)**:\n"
        "   - **Layer 1 (Validasi Kode Deterministik)**: Panggil `aigrav_detect_spatial_clashes()` (pastikan zero clash furnitur vs dinding), "
        "`aigrav_audit_material_transparency()` (kaca wajib transparan >= 50%), dan `aigrav_audit_unresolved_warnings()`.\n"
        "   - **Layer 2 (Validasi Visual)**: Panggil `aigrav_inspect_model_visually()` untuk cek warna fasad, kaca tembus pandang, dan halangan pohon.\n"
        "   - **Hak Veto Validator**: Jika Layer 1 atau Layer 2 gagal, Validator WAJIB menolak (REJECT) dan mengembalikan revisi ke Executor (maks 2x)."
    )


@mcp.tool(
    name="aigrav_get_project_context",
    description="[Fase 1 Datum] Ambil seluruh konteks proyek: unit, daftar Level (mm), Grid beserta koordinat start/end (mm), dan FamilySymbol yang tersedia.",
)
def aigrav_get_project_context() -> str:
    return call_aigrav("get_project_context", "{}")


@mcp.tool(
    name="aigrav_create_grid_matrix",
    description="[Pillar 5 Relative Helper] Buat sistem Grid otomatis dari x_spacings_mm dan y_spacings_mm (dalam mm) dan kembalikan intersection_dictionary_mm (misal {'A-1': {x_mm:0, y_mm:0, z_mm:0}}).",
)
def aigrav_create_grid_matrix(
    x_spacings_mm: list[float],
    y_spacings_mm: list[float],
    x_labels: list[str] | None = None,
    y_labels: list[str] | None = None,
) -> str:
    payload = {
        "x_spacings_mm": x_spacings_mm,
        "y_spacings_mm": y_spacings_mm,
    }
    if x_labels:
        payload["x_labels"] = x_labels
    if y_labels:
        payload["y_labels"] = y_labels
    return call_aigrav("create_grid_matrix", json.dumps(payload))


@mcp.tool(
    name="aigrav_place_on_top_of",
    description="[Pillar 5 Relative Helper] Tempatkan elemen baru tepat di atas BoundingBox Max.Z dari base_element_id (dalam mm), mencegah elemen melayang.",
)
def aigrav_place_on_top_of(
    base_element_id: int,
    family_symbol: str,
    offset_x_mm: float = 0.0,
    offset_y_mm: float = 0.0,
    height_mm: float = 3500.0,
) -> str:
    payload = {
        "base_element_id": base_element_id,
        "family_symbol": family_symbol,
        "offset_x_mm": offset_x_mm,
        "offset_y_mm": offset_y_mm,
        "height_mm": height_mm,
    }
    return call_aigrav("place_on_top_of", json.dumps(payload))


@mcp.tool(
    name="aigrav_connect_elements",
    description="[Pillar 5 Relative Helper] Hubungkan dua elemen (start_element_id ke end_element_id) dengan balok/framing_type secara otomatis dari titik jangkar Top/Centroid/Bottom.",
)
def aigrav_connect_elements(
    start_element_id: int,
    end_element_id: int,
    framing_type: str = "",
    z_anchor: str = "Top",
) -> str:
    payload = {
        "start_element_id": start_element_id,
        "end_element_id": end_element_id,
        "framing_type": framing_type,
        "z_anchor": z_anchor,
    }
    return call_aigrav("connect_elements", json.dumps(payload))


@mcp.tool(
    name="aigrav_execute_revit_python",
    description="[Pillar 2 Sapu Jagat Tool] Eksekusi script Python / batch operasi dinamis di dalam Revit Safe Sandbox (TransactionGroup + AST Validation + Auto-Spatial Telemetry).",
)
def aigrav_execute_revit_python(script: str) -> str:
    ast_check = validate_python_ast(script)
    if not ast_check.get("valid", False):
        return json.dumps(
            {
                "status": "ASTValidationFailed",
                "ast_validation": ast_check,
            },
            indent=2,
        )
    payload = {
        "script": script,
        "operations": ast_check.get("extracted_operations", []),
    }
    return call_aigrav("execute_revit_python", json.dumps(payload))


@mcp.tool(
    name="aigrav_capture_revit_viewport",
    description="[Pillar 3 Mata AI] Lakukan ZoomToFit pada viewport aktif Revit, ekspor PNG beresolusi tinggi via Document.ExportImage(), dan kembalikan Base64 / lokasi gambar + telemetri.",
)
def aigrav_capture_revit_viewport(resolution_px: int = 1280) -> str:
    raw = call_aigrav("capture_revit_viewport", json.dumps({"resolution_px": resolution_px}))
    try:
        data = json.loads(raw)
        img_path = data.get("primary_image_path", "")
        if img_path and os.path.isfile(img_path):
            with open(img_path, "rb") as f:
                b64 = base64.b64encode(f.read()).decode("ascii")
            data["image_mime_type"] = "image/png"
            data["image_base64_preview"] = b64[:256] + "...(truncated_in_json_text)"
            data["image_size_bytes"] = os.path.getsize(img_path)
        return json.dumps(data, indent=2)
    except Exception:
        return raw


@mcp.tool(
    name="aigrav_inspect_model_visually",
    description="[Pillar 3 Multi-View Quadrant Eye] Ekspor Plan View, 3D Isometric, dan Elevation sekaligus (ZoomToFit), gabungkan menjadi Multi-View Quadrant PNG, dan kembalikan telemetri visual.",
)
def aigrav_inspect_model_visually(resolution_px: int = 1280) -> str:
    raw = call_aigrav("inspect_model_visually", json.dumps({"resolution_px": resolution_px}))
    try:
        data = json.loads(raw)
        exported = data.get("exported_images", [])
        if exported:
            quad_path = os.path.join(os.path.dirname(exported[0]), "aigrav_quadrant_composite.png")
            stitched = compose_quadrant_image(exported, quad_path)
            if stitched:
                data["quadrant_composite_image_path"] = stitched
        return json.dumps(data, indent=2)
    except Exception:
        return raw


@mcp.tool(
    name="aigrav_get_sheet_paper_space",
    description="[Pillar 4 DED Paper Space] Ekstrak dimensi Titleblock (Kop Surat) dalam mm (A0/A1/A2/A3), titik origin/anchor, drawable_canvas_mm, dan daftar Viewport.GetBoxOutline() 2D yang sudah terpasang di Sheet.",
)
def aigrav_get_sheet_paper_space(sheet_id: str) -> str:
    return call_aigrav("get_sheet_paper_space", json.dumps({"sheet_id": sheet_id}))


@mcp.tool(
    name="aigrav_place_view_on_sheet",
    description="[Pillar 4 Collision-Free DED Layout] Tempatkan View ke atas Sheet menggunakan ekstraksi Viewport.GetBoxOutline() 2D dan algoritma anti-tabrakan (layout_rule: Auto, Grid, TopLeft, TopRight, BottomLeft, BottomRight, Center).",
)
def aigrav_place_view_on_sheet(
    sheet_id: str,
    view_id: str,
    layout_rule: str = "Auto",
    margin_mm: float = 15.0,
) -> str:
    payload = {
        "sheet_id": sheet_id,
        "view_id": view_id,
        "layout_rule": layout_rule,
        "margin_mm": margin_mm,
    }
    return call_aigrav("place_view_on_sheet", json.dumps(payload))


@mcp.tool(
    name="aigrav_place_next_to_viewport_on_sheet",
    description="[Pillar 4 & 5 Relative 2D Sheet Helper] Tempatkan View baru tepat di sebelah (Right, Left, Below, Above) viewport referensi pada Sheet dengan jarak gap_mm tanpa bertabrakan.",
)
def aigrav_place_next_to_viewport_on_sheet(
    sheet_id: str,
    view_id: str,
    reference_viewport_or_view_id: int,
    direction: str = "Right",
    gap_mm: float = 15.0,
) -> str:
    payload = {
        "sheet_id": sheet_id,
        "view_id": view_id,
        "reference_viewport_id": reference_viewport_or_view_id,
        "direction": direction,
        "gap_mm": gap_mm,
    }
    return call_aigrav("place_next_to_viewport_on_sheet", json.dumps(payload))


# ============================================================
# DETERMINISTIC VALIDATOR TOOLS (LAYER 1 CODE-BASED AUDIT)
# ============================================================

@mcp.tool(
    name="aigrav_audit_material_transparency",
    description="[Validator Lapis 1] Audit independen material kaca dan atap. Memeriksa apakah jendela/curtain wall memiliki nilai transparansi >= 50% (mendeteksi kaca abu-abu solid/opaque) dan atap memiliki material resmi.",
)
def aigrav_audit_material_transparency() -> str:
    return call_aigrav("audit_material_transparency", "{}")


@mcp.tool(
    name="aigrav_detect_spatial_clashes",
    description="[Validator Lapis 1] Deteksi benturan fisik (clash) antara furnitur (OST_Furniture) dengan dinding (OST_Walls) menggunakan BoundingBox & solid filter untuk mencegah furnitur menembus/keluar dari dinding.",
)
def aigrav_detect_spatial_clashes() -> str:
    return call_aigrav("detect_spatial_clashes", "{}")


@mcp.tool(
    name="aigrav_audit_unresolved_warnings",
    description="[Validator Lapis 1] Audit seluruh peringatan aktif di dokumen Revit (Document.GetWarnings). Mengembalikan daftar warning, tingkat keparahan, dan ID elemen yang bermasalah.",
)
def aigrav_audit_unresolved_warnings() -> str:
    return call_aigrav("audit_unresolved_warnings", "{}")


# ============================================================
# CLOUD VERSIONING & MULTI-PC UPDATE TOOLS (OPSI A & CARA 3)
# ============================================================

import aigrav_version_manager as vm


@mcp.tool(
    name="aigrav_check_version_update",
    description="[Multi-PC Sync] Periksa apakah ada rilis versi AIGRAV baru di repositori GitHub. Mengembalikan perbandingan versi lokal vs remote, nama rilis, tanggal, serta daftar fitur baru (highlights) dan perbaikan (fixes).",
)
def aigrav_check_version_update() -> str:
    res = vm.check_remote_version()
    return json.dumps(res, indent=2, ensure_ascii=False)


@mcp.tool(
    name="aigrav_apply_version_update",
    description="[Multi-PC Sync] Terapkan pembaruan AIGRAV dari GitHub ke PC lokal (menjalankan sync-aigrav.ps1, pull kode & skill baru, kompilasi C#, dan reload Revit). Panggil HANYA jika pengguna telah menyetujui pembaruan.",
)
def aigrav_apply_version_update() -> str:
    res = vm.apply_update_locally()
    return json.dumps(res, indent=2, ensure_ascii=False)


@mcp.tool(
    name="aigrav_publish_version",
    description="[Opsi A Otomatis via AI] Buat dan publikasikan versi rilis AIGRAV baru ke GitHub. Otomatis memperbarui version.json, membuat commit release, membuat tag Git vX.Y.Z, dan meng-push rilis ke GitHub.",
)
def aigrav_publish_version(
    version: str,
    release_name: str,
    highlights: list[str],
    fixes: list[str] | None = None,
) -> str:
    res = vm.publish_new_version(
        version=version,
        release_name=release_name,
        highlights=highlights,
        fixes=fixes or [],
    )
    return json.dumps(res, indent=2, ensure_ascii=False)


# ============================================================
# START MCP
# ============================================================

if __name__ == "__main__":

    mcp.run(
        transport="stdio"
    )
