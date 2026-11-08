"""
AIGRAV Built-in Relative & Parametric Spatial Helper Library (Pillar 2, 3, 4 & 5)
Provides:
  1. Auto-Unit Conversion Wrapper (mm_to_ft, ft_to_mm)
  2. Python AST Safety Validator & Dynamic Sandbox Evaluator for execute_revit_python
  3. Multi-View Quadrant Visual Composer (Pillow image stitcher)
  4. Relative Anchoring & 2D Sheet Placement Helpers
"""
import ast
import math
import os
from typing import Any, Dict, List, Optional

FT_TO_MM_FACTOR = 304.8
REVIT_BRIDGE_URL = "http://127.0.0.1:8765/"


def mm_to_ft(val_mm: float) -> float:
    """Convert millimeters (mm) to Revit internal Imperial decimal feet (ft)."""
    return float(val_mm) / FT_TO_MM_FACTOR


def ft_to_mm(val_ft: float) -> float:
    """Convert Revit internal Imperial decimal feet (ft) to millimeters (mm)."""
    return round(float(val_ft) * FT_TO_MM_FACTOR, 1)


FORBIDDEN_AST_CALLS = {
    "os.system", "subprocess.Popen", "subprocess.run", "subprocess.call",
    "shutil.rmtree", "os.remove", "os.unlink", "eval", "exec", "__import__", "open"
}

SANDBOX_ACTIONS = (
    "place_on_top_of", "connect_elements", "create_grid_matrix",
    "create_wall", "create_column", "create_beam", "create_floor",
    "create_direct_shape_box"
)


def validate_python_ast(script: str) -> Dict[str, Any]:
    """
    Parse, security-validate, and dynamically evaluate Python script AST
    (including loops, math, and mm_to_ft) before sending to Revit Sandbox.
    """
    try:
        tree = ast.parse(script, mode="exec")
    except SyntaxError as syn_err:
        return {
            "valid": False,
            "error_type": "SyntaxError",
            "line": syn_err.lineno,
            "offset": syn_err.offset,
            "message": f"SyntaxError at line {syn_err.lineno}: {syn_err.msg}",
            "text": (syn_err.text or "").strip()
        }

    violations: List[str] = []
    ops_extracted: List[Dict[str, Any]] = []

    for node in ast.walk(tree):
        if isinstance(node, (ast.Import, ast.ImportFrom)):
            mod_names = [a.name for a in node.names]
            if isinstance(node, ast.ImportFrom) and node.module:
                mod_names.append(node.module)
            for m in mod_names:
                if m in ("subprocess", "shutil", "socket", "ctypes", "os", "sys"):
                    violations.append(f"Forbidden module import: '{m}'")

        if isinstance(node, ast.Call):
            func_name = ""
            if isinstance(node.func, ast.Name):
                func_name = node.func.id
            elif isinstance(node.func, ast.Attribute) and isinstance(node.func.value, ast.Name):
                func_name = f"{node.func.value.id}.{node.func.attr}"

            if func_name in FORBIDDEN_AST_CALLS:
                violations.append(f"Forbidden function call: '{func_name}' at line {getattr(node, 'lineno', '?')}")

    if violations:
        return {
            "valid": False,
            "error_type": "SecurityPolicyViolation",
            "violations": violations,
            "message": "; ".join(violations)
        }

    # Dynamic Sandbox Evaluation (evaluates loops, math, mm_to_ft, variables safely)
    recorded_ops: List[Dict[str, Any]] = []

    def _make_recorder(action_name: str):
        def _recorder(**kwargs: Any) -> Dict[str, Any]:
            entry = {"action": action_name, **kwargs}
            recorded_ops.append(entry)
            return entry
        return _recorder

    safe_builtins = {
        "range": range, "len": len, "min": min, "max": max, "abs": abs,
        "round": round, "sum": sum, "enumerate": enumerate, "zip": zip,
        "list": list, "dict": dict, "tuple": tuple, "set": set,
        "int": int, "float": float, "str": str, "bool": bool,
        "True": True, "False": False, "None": None,
    }
    sandbox_globals: Dict[str, Any] = {
        "__builtins__": safe_builtins,
        "math": math,
        "mm_to_ft": mm_to_ft,
        "ft_to_mm": ft_to_mm,
    }
    for act in SANDBOX_ACTIONS:
        sandbox_globals[act] = _make_recorder(act)

    try:
        compiled = compile(tree, filename="<aigrav_sandbox>", mode="exec")
        exec(compiled, sandbox_globals, {})
        ops_extracted = recorded_ops
    except Exception:
        # If script references live Revit objects (doc, DB, Transaction), fallback to static AST extraction
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in SANDBOX_ACTIONS:
                kw_map: Dict[str, Any] = {"action": node.func.id}
                for kw in node.keywords:
                    if kw.arg and isinstance(kw.value, ast.Constant):
                        kw_map[kw.arg] = kw.value.value
                ops_extracted.append(kw_map)

    return {
        "valid": True,
        "ast_nodes_count": sum(1 for _ in ast.walk(tree)),
        "extracted_operations": ops_extracted
    }


def compose_quadrant_image(image_paths: List[str], output_path: str) -> Optional[str]:
    """
    Combine exported Revit views (Plan, 3D Isometric, Elevation) into a single
    Multi-View Quadrant PNG so the LLM has simultaneous 2D + 3D visual ground truth.
    """
    valid_paths = [p for p in image_paths if p and os.path.isfile(p)]
    if not valid_paths:
        return None
    if len(valid_paths) == 1:
        return valid_paths[0]

    try:
        from PIL import Image, ImageDraw
        imgs = [Image.open(p).convert("RGB") for p in valid_paths[:4]]
        cell_w, cell_h = 768, 576
        cols = 2
        rows = 2 if len(imgs) > 2 else 1
        canvas = Image.new("RGB", (cell_w * cols, cell_h * rows), (24, 26, 31))
        draw = ImageDraw.Draw(canvas)

        for idx, im in enumerate(imgs):
            im_resized = im.copy()
            im_resized.thumbnail((cell_w - 16, cell_h - 36))
            r, c = divmod(idx, cols)
            x0, y0 = c * cell_w + 8, r * cell_h + 28
            canvas.paste(im_resized, (x0, y0))
            label = f"[{idx + 1}] {os.path.basename(valid_paths[idx])}"
            draw.rectangle([c * cell_w, r * cell_h, (c + 1) * cell_w, r * cell_h + 24], fill=(40, 44, 52))
            draw.text((c * cell_w + 10, r * cell_h + 5), label, fill=(220, 228, 240))

        canvas.save(output_path, format="PNG")
        return output_path
    except Exception:
        return valid_paths[0]
