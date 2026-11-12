"""
cad_security_policy.py - AIGRAV CAD Command Risk Policy & Safety Gates
Enforces read-only execution, mutation verification, and explicit confirmation
for destructive operations in ZWCAD 2023.
"""

from enum import Enum
from typing import Dict, Any, Optional, Set


class CADRiskCategory(str, Enum):
    READ_ONLY = "READ_ONLY"
    LOW_MUTATION = "LOW_MUTATION"
    STANDARD_MUTATION = "STANDARD_MUTATION"
    DESTRUCTIVE = "DESTRUCTIVE"


class CADReadWriteType(str, Enum):
    READ = "READ"
    WRITE = "WRITE"


# Explicit Classification of all ZWCAD Tools
ZWCAD_TOOL_RISK_MAP: Dict[str, CADRiskCategory] = {
    # Diagnostics & Read-Only Queries
    "diagnose_cad": CADRiskCategory.READ_ONLY,
    "get_current_drawing": CADRiskCategory.READ_ONLY,
    "list_layers": CADRiskCategory.READ_ONLY,
    "list_linetypes": CADRiskCategory.READ_ONLY,
    "list_text_styles": CADRiskCategory.READ_ONLY,
    "list_dim_styles": CADRiskCategory.READ_ONLY,
    "query_entities": CADRiskCategory.READ_ONLY,
    "get_entity_properties": CADRiskCategory.READ_ONLY,
    "get_entity_geometry": CADRiskCategory.READ_ONLY,
    "find_entities": CADRiskCategory.READ_ONLY,
    "count_entities": CADRiskCategory.READ_ONLY,
    "calculate_entity_stats": CADRiskCategory.READ_ONLY,
    "measure_distance": CADRiskCategory.READ_ONLY,
    "measure_polyline": CADRiskCategory.READ_ONLY,

    # Low Mutation / Viewport
    "create_layer": CADRiskCategory.LOW_MUTATION,
    "set_layer_visibility": CADRiskCategory.LOW_MUTATION,
    "zoom_extents": CADRiskCategory.LOW_MUTATION,
    "regenerate_drawing": CADRiskCategory.LOW_MUTATION,

    # Standard Mutations (Drawing & Annotation)
    "create_line": CADRiskCategory.STANDARD_MUTATION,
    "create_circle": CADRiskCategory.STANDARD_MUTATION,
    "create_arc": CADRiskCategory.STANDARD_MUTATION,
    "create_point": CADRiskCategory.STANDARD_MUTATION,
    "create_polyline": CADRiskCategory.STANDARD_MUTATION,
    "create_rectangle": CADRiskCategory.STANDARD_MUTATION,
    "create_text": CADRiskCategory.STANDARD_MUTATION,
    "create_mtext": CADRiskCategory.STANDARD_MUTATION,
    "create_linear_dimension": CADRiskCategory.STANDARD_MUTATION,
    "create_aligned_dimension": CADRiskCategory.STANDARD_MUTATION,
    "create_radial_dimension": CADRiskCategory.STANDARD_MUTATION,
    "create_diametric_dimension": CADRiskCategory.STANDARD_MUTATION,
    "create_hatch": CADRiskCategory.STANDARD_MUTATION,
    "create_block": CADRiskCategory.STANDARD_MUTATION,
    "insert_block": CADRiskCategory.STANDARD_MUTATION,
    "set_entity_layer": CADRiskCategory.STANDARD_MUTATION,
    "set_entity_color": CADRiskCategory.STANDARD_MUTATION,
    "move_entity": CADRiskCategory.STANDARD_MUTATION,
    "copy_entity": CADRiskCategory.STANDARD_MUTATION,
    "rotate_entity": CADRiskCategory.STANDARD_MUTATION,
    "scale_entity": CADRiskCategory.STANDARD_MUTATION,
    "offset_entity": CADRiskCategory.STANDARD_MUTATION,
    "mirror_entity": CADRiskCategory.STANDARD_MUTATION,
    "execute_command": CADRiskCategory.STANDARD_MUTATION,

    # Destructive Operations (Require Explicit Approval)
    "delete_entities": CADRiskCategory.DESTRUCTIVE,
    "explode_entity": CADRiskCategory.DESTRUCTIVE,
}

DESTRUCTIVE_CONFIRMATION_TOKENS = {
    "delete_entities": "DELETE THESE ENTITIES",
    "explode_entity": "CONFIRM_EXPLODE",
    "execute_command_destructive": "CONFIRMED",
}


class CADSecurityPolicy:
    """Evaluates and enforces CAD execution risk gates."""

    @staticmethod
    def get_tool_risk(tool_name: str) -> CADRiskCategory:
        clean_name = tool_name.lower().replace("zwcad_", "")
        return ZWCAD_TOOL_RISK_MAP.get(clean_name, CADRiskCategory.STANDARD_MUTATION)

    @staticmethod
    def get_read_write(tool_name: str) -> CADReadWriteType:
        risk = CADSecurityPolicy.get_tool_risk(tool_name)
        return CADReadWriteType.READ if risk == CADRiskCategory.READ_ONLY else CADReadWriteType.WRITE

    @staticmethod
    def validate_execution_permission(
        tool_name: str,
        arguments: Dict[str, Any],
        user_confirmed: bool = False
    ) -> None:
        """Raise PermissionError if a destructive operation lacks proper authorization."""
        risk = CADSecurityPolicy.get_tool_risk(tool_name)
        clean_name = tool_name.lower().replace("zwcad_", "")

        if risk == CADRiskCategory.DESTRUCTIVE:
            expected_token = DESTRUCTIVE_CONFIRMATION_TOKENS.get(clean_name)
            provided_token = arguments.get("confirmation", "")

            if expected_token and provided_token != expected_token and not user_confirmed:
                raise PermissionError(
                    f"Destructive operation '{tool_name}' requires explicit confirmation. "
                    f"Please provide confirmation='{expected_token}' or user approval."
                )

        if clean_name == "execute_command":
            cmd = str(arguments.get("command_str", "")).strip().split()
            cmd_token = cmd[0].lstrip("._-").upper() if cmd else ""
            if cmd_token in {"ERASE", "DELETE", "PURGE", "AUDIT", "CLOSE", "QUIT"}:
                conf = arguments.get("confirmation", "")
                if conf != "CONFIRMED" and not user_confirmed:
                    raise PermissionError(
                        f"Destructive CAD command '{cmd_token}' requires explicit confirmation='CONFIRMED'."
                    )
