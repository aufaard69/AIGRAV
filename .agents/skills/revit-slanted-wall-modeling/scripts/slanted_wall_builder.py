"""
AIGRAV Slanted Wall & Donut Floor Orchestration Script
Calculates inclination angles, formats donut curve loops, and submits to Revit Bridge.
"""

import math
import json
import urllib.request

def calculate_slant_angle(delta_offset_m: float, height_m: float, slant_inward: bool = True) -> float:
    """
    Calculates the slant angle from vertical in radians.
    In CCW perimeter walls:
      Negative angle slants inward (tapering tower / obelisk).
      Positive angle slants outward (inverted chalice / splay).
    """
    theta = math.atan(abs(delta_offset_m) / height_m)
    return -theta if slant_inward else theta

def create_donut_loops_data(outer_half_w: float, outer_half_h: float, inner_half_w: float, inner_half_h: float, z_elevation: float):
    """
    Generates CCW outer boundary and CW inner void loop coordinates.
    """
    outer_loop = [
        {"x": -outer_half_w, "y": -outer_half_h, "z": z_elevation},
        {"x":  outer_half_w, "y": -outer_half_h, "z": z_elevation},
        {"x":  outer_half_w, "y":  outer_half_h, "z": z_elevation},
        {"x": -outer_half_w, "y":  outer_half_h, "z": z_elevation}
    ]
    inner_loop = [
        {"x": -inner_half_w, "y": -inner_half_h, "z": z_elevation},
        {"x": -inner_half_w, "y":  inner_half_h, "z": z_elevation},
        {"x":  inner_half_w, "y":  inner_half_h, "z": z_elevation},
        {"x":  inner_half_w, "y": -inner_half_h, "z": z_elevation}
    ]
    return {
        "outer_boundary_ccw": outer_loop,
        "inner_void_cw": inner_loop,
        "net_area_m2": (4 * outer_half_w * outer_half_h) - (4 * inner_half_w * inner_half_h)
    }

if __name__ == "__main__":
    print("=== AIGRAV Slanted Wall & Zero-Overlap Slab Helper ===")
    ang_obelisk = calculate_slant_angle(2.20, 98.00, slant_inward=True)
    ang_cawan = calculate_slant_angle(10.50, 6.00, slant_inward=False)
    print(f"Obelisk Inward Slant Angle: {ang_obelisk:.6f} rad ({math.degrees(ang_obelisk):.3f} deg)")
    print(f"Cawan Outward Slant Angle:  {ang_cawan:.6f} rad ({math.degrees(ang_cawan):.3f} deg)")
    
    donut = create_donut_loops_data(150.0, 150.0, 80.0, 80.0, 0.0)
    print(f"Lawn Donut Net Area: {donut['net_area_m2']:.2f} m2 (Gross: 90000 m2, Cutout: 25600 m2)")
