# AIGRAV — MODELING FIDELITY RISK REGISTER & MITIGATION STRATEGY
**Document ID:** AIGRAV-RSK-MF0-06  
**Status:** MF0_READY_FOR_INDEPENDENT_REVIEW  
**Date:** 2026-11-12  
**Target Platform:** Autodesk Revit 2026 / .NET 10 / C# Revit Bridge / FastMCP Python  
**Staging Path:** `C:\AIGRAV\_governance_review\modeling_fidelity\MODELING_FIDELITY_RISK_REGISTER.md`  
**Classification:** MANDATORY RISK GOVERNANCE & RESILIENCE DIRECTIVE  

---

## 1. Executive Summary

This Risk Register establishes the technical risk governance framework for AIGRAV's Architectural Reference-to-BIM system. 

It explicitly incorporates the risks identified during the forensic audit of the failed villa scenario (`REG-MF-01`) and the technical refinements established in `AIGRAV-POL-MF0-02` through `AIGRAV-PLAN-MF0-05`, specifically emphasizing the **Strict Input Completeness & User Clarification Policy**, package-level maturity customization, non-leading clarification questions (Q0 through Q8), conceptual model dimensional confidence, and the five-way conditional dependency model.

---

## 2. Risk Assessment Matrix (5x5)

```text
       LIKELIHOOD -->
IMPACT   [1] Rare     [2] Unlikely    [3] Possible   [4] Likely     [5] Almost Certain
  [5]    -----------------------------------------------------------------------------
Critical | RSK-10(Size| RSK-07 (Liab) | RSK-02 (API) | RSK-04 (QA)  | RSK-01 (Occlusion)|
  [4]    -----------------------------------------------------------------------------
 Major   |            | RSK-06 (Loop) | RSK-11 (Dep) | RSK-03 (Fam) | RSK-08 (Maturity) |
  [3]    -----------------------------------------------------------------------------
Moderate |            | RSK-09 (Fatig)| RSK-05 (Perf)|              |                   |
  [2]    -----------------------------------------------------------------------------
 Minor   |            |               |              |              |                   |
  [1]    -----------------------------------------------------------------------------
Negligible
```

---

## 3. Comprehensive Risk Identification & Mitigation Strategy

### RSK-01: Occlusion & Silent Assumption Hallucination
- **Category:** Cognitive Interpretation & Input Completeness
- **Likelihood:** [5] Almost Certain | **Impact:** [5] Critical | **Risk Score:** **25 (EXTREME)**
- **Description:**  
  Single-view perspective renderings cannot show rear elevations, hidden internal stairwells, structural grid baselines, foundation depths, or concealed roof drainage. Historically, AI models silently invented geometry, defaulting to flat slabs and unconstrained columns.
- **Mitigation Strategy:**
  1. **Strict Core Principle:** AIGRAV must understand the design before modeling. If required design information is incomplete, AIGRAV must invoke **Mode B** and ask the user first.
  2. **Requirement Completeness Matrix (RCM):** All requirements mapped to 6 canonical confidence statuses (`PROJECT_INPUT_VERIFIED`, `OBSERVED_FROM_REFERENCE`, `INFERRED_FROM_REFERENCE`, `ASSUMED_PENDING_USER_APPROVAL`, `DESIGN_INPUT_REQUIRED`, `CONFLICTING_DESIGN_INPUT`).
  3. **No Silent Fact Conversion:** `ASSUMED_PENDING_USER_APPROVAL` does **not** authorize modeling until the user explicitly reviews and approves the assumption.
  4. **Scope-Specific Blocking:** Dependent elements in the DAG are locked from generation until unresolved inputs are clarified. Generic placeholder geometry is strictly banned.
  5. **Conceptual Dimensional Confidence Protocol:** Approximate dimensions derived from 2D images retain inherent uncertainty ($\pm 15\%$), are categorized as `INFERRED_FROM_REFERENCE`, require explicit user sign-off (`ASSUMED_PENDING_USER_APPROVAL (USER_APPROVED)`), and are barred from promotion to `PROJECT_INPUT_VERIFIED` or conversion to structural sizes.

---

### RSK-02: Single-Threaded Revit Write Transaction Deadlocks & Freezes
- **Category:** Systems Engineering & Revit API
- **Likelihood:** [3] Possible | **Impact:** [5] Critical | **Risk Score:** **15 (HIGH)**
- **Description:**  
  The Revit API write engine is strictly single-threaded. Multi-agent writes or large monolithic transactions with hundreds of elements can cause Revit UI freezes, API deadlocks, or transaction timeouts.
- **Mitigation Strategy:**
  1. **Single Executor Invariance:** Exactly one (1) Revit Bridge Executor holds write transaction authority. All other agents operate in read-only mode.
  2. **Transactional Sub-Milestones:** Execution is partitioned into sequential sub-transactions inside a single `TransactionGroup`, enabling clean rollback on failure.

---

### RSK-03: Library Family Starvation & Controlled Exceptions
- **Category:** BIM Asset Management
- **Likelihood:** [4] Likely | **Impact:** [4] Major | **Risk Score:** **16 (HIGH)**
- **Description:**  
  Specific loadable families (e.g., modern pivot doors, frameless glass shoe railings, spiral stairs) may be absent from the active project or standard Autodesk content library. If unhandled, this risks runtime crashes or fallback to unauthorized basic wall proxies.
- **Mitigation Strategy:**
  1. **Pre-Flight Asset Resolution:** The Blueprint compiler queries available `FamilySymbols` prior to opening write transactions.
  2. **Tiered Native Fallback:** Load official Revit library family $\rightarrow$ duplicate/parameterize related native family $\rightarrow$ build native system family assembly (e.g. native curtain wall or railing).
  3. **Controlled DirectShape Exception Governance:** Non-standard forms may use `DirectShape` **only if** accompanied by documented justification, expected BIM parameters, and explicit user approval in the Blueprint Exception Log. Unauthorized basic wall proxies are strictly barred.

---

### RSK-04: False-Positive Validation & QA Rubber-Stamping
- **Category:** Quality Assurance
- **Likelihood:** [4] Likely | **Impact:** [5] Critical | **Risk Score:** **20 (EXTREME)**
- **Description:**  
  As demonstrated in the failed villa run, an internal validator can easily issue an unearned "100% PASS / 🟢 LAYAK" verdict if checks are limited to internal error codes and token parameter existence, ignoring visual and structural defects.
- **Mitigation Strategy:**
  1. **Gate 0 Pre-Execution Gate:** Rejects execution if any requirements are unverified, blocked, or lacking user blueprint approval.
  2. **Conditional 5-Gate Acceptance Matrix:** Enforces Gates A through E. Any `UNINTENDED_ROOF_PROTRUSION` or unauthorized semantic proxy triggers an immediate **HARD REJECT**, overriding any numerical score.
  3. **Multi-View Batch Rendering:** Mandatory export of 7 standard views (4 elevations, 2 plans, 1 section, 1 perspective) before any acceptance verdict is issued.

---

### RSK-05: Multi-View Viewport Rendering Timeouts & Memory Bloat
- **Category:** Performance & Graphics
- **Likelihood:** [3] Possible | **Impact:** [3] Moderate | **Risk Score:** **9 (MEDIUM)**
- **Description:**  
  Batch-exporting 7 high-resolution views with photorealistic textures and shadows can strain memory or cause Revit export timeouts.
- **Mitigation Strategy:**
  1. **Dedicated Lightweight ViewTemplates:** QA views use `DisplayStyle.ConsistentColors` with anti-aliasing disabled during preliminary checks.
  2. **Resolution Cap:** Standardized to $1920 \times 1080$ at $96\text{ DPI}$.
  3. **Single Read Session:** All views exported in a single read transaction to eliminate repetitive view activation overhead.

---

### RSK-06: Self-Intersecting Boundary Curve Loops
- **Category:** Computational Geometry
- **Likelihood:** [2] Unlikely | **Impact:** [4] Major | **Risk Score:** **8 (LOW-MEDIUM)**
- **Description:**  
  Multi-plane roofs or complex floor boundaries with micro-segments ($< 1\text{ mm}$) or self-intersections cause Revit API `NewFootPrintRoof` or `Floor.Create` to throw fatal exceptions.
- **Mitigation Strategy:**
  1. **Topological Polygon Normalization:** Pre-API validation simplifies collinear lines, culls micro-segments, and validates closed CCW/CW loops.

---

### RSK-07: Structural Liability & Over-Reliance on Geometric Sanity Checks
- **Category:** Engineering Liability & Safety
- **Likelihood:** [2] Unlikely | **Impact:** [5] Critical | **Risk Score:** **10 (MEDIUM)**
- **Description:**  
  Users or downstream engineers might mistakenly interpret AIGRAV's "Gate D Engineering Sanity PASS" as formal structural engineering certification.
- **Mitigation Strategy:**
  1. **Mandatory Disclaimer:** Every QA report and artifact must prominently display:  
     *"Gate D verifies geometric integrity, continuous spatial paths, and clearance. It does not replace structural calculation, finite element analysis, or licensed Professional Engineer (PE) approval."*
  2. **Model Maturity Enforcement:** Models derived from visual references are capped at `BIM_COORDINATION_MODEL`.

---

### RSK-08: Premature Claim of Construction Documentation Maturity
- **Category:** Model Contractual Validity
- **Likelihood:** [4] Likely | **Impact:** [4] Major | **Risk Score:** **16 (HIGH)**
- **Description:**  
  Generating a visually appealing 3D model may tempt an AI or user to classify it as ready for construction, fabrication, or contractual takeoff without sufficient underlying engineering data.
- **Mitigation Strategy:**
  1. **Strict Maturity Classification:** Enforce the three internal levels (`CONCEPTUAL_REFERENCE_MODEL`, `BIM_COORDINATION_MODEL`, `CONSTRUCTION_DOCUMENTATION_MODEL`) with package-level flexibility. External LOD 100–400 labeled informative references only.
  2. **Absolute Ban:** AIGRAV is legally and architecturally prohibited from asserting `CONSTRUCTION_DOCUMENTATION_MODEL` readiness from a 2D perspective rendering.

---

### RSK-09: User Clarification Fatigue & Fragmented Interrogation
- **Category:** User Interaction & Agent Ergonomics
- **Likelihood:** [2] Unlikely | **Impact:** [3] Moderate | **Risk Score:** **6 (LOW)**
- **Description:**  
  Under Mode B, an agent could interrogate the user with endless piecemeal questions (e.g., asking about columns in one turn, then beams in the next, then windows in the next), causing user frustration and abandonment.
- **Mitigation Strategy:**
  1. **Consolidated Single-Round Clarification:** Mode B requires completing all reference analysis upfront and grouping all missing items into **exactly one comprehensive clarification round (Q0 through Q8)**.
  2. **Technical Alternatives Provided:** Questions must present reasonable technical alternatives without unsupported structural sizing assertions.
  3. **Mode A Protection:** Complete design inputs must not trigger unnecessary questions.

---

### RSK-10: Premature Structural Sizing Recommendations from 2D Images
- **Category:** Engineering Ethics & Liability
- **Likelihood:** [1] Rare | **Impact:** [5] Critical | **Risk Score:** **5 (LOW)**
- **Description:**  
  An agent might recommend specific structural cross-sections (e.g., $350 \times 700\text{ mm}$ transfer beams or $400 \times 400\text{ mm}$ columns) based purely on visual inspection of a rendering, creating false confidence and potential structural hazards.
- **Mitigation Strategy:**
  1. **Absolute Prohibition on Member Sizing Recommendations:** The system is barred from suggesting specific loadbearing member cross-sections from visual references.
  2. **Explicit Design Status Query:** Clarification rounds must ask whether the user possesses an approved structural design or explicitly requests provisional conceptual framing requiring subsequent PE verification.

---

### RSK-11: Conflation of Distinct Dependency Types in BIM Assembly
- **Category:** Software Architecture & DAG Integrity
- **Likelihood:** [3] Possible | **Impact:** [4] Major | **Risk Score:** **12 (MEDIUM)**
- **Description:**  
  An algorithm could assume that physical gravity support, Revit database hosting, datum constraints, and execution sequences are identical. For example, assuming an unhosted curtain wall can be placed because its beam exists, or assuming walls and stairs are hosted on floor slabs.
- **Mitigation Strategy:**
  1. **Five-Way Dependency Disaggregation:** Explicitly distinguish `PHYSICAL_SUPPORT_DEPENDENCY`, `REVIT_HOST_DEPENDENCY`, `DATUM_AND_CONSTRAINT_DEPENDENCY`, `MODELING_EXECUTION_ORDER`, and `VALIDATION_DEPENDENCY`.
  2. **Explicit System Conditionality:** Audit conditional dependencies (e.g., ground slab support: slab-on-grade rests on soil vs suspended slab bears on beams/columns; curtain wall doors: panel replacement vs basic wall host; roof support: indirect via upper vertical structure).
  3. **Multi-Constraint DAG Resolver:** An element is marked `MODEL_SCOPE_ELIGIBLE_PENDING_APPROVAL` only when all active dependency dimensions are satisfied.

---

## 4. Risk Mitigation Summary Matrix

| Risk ID | Title | Pre-Mitigation Score | Post-Mitigation Score | Mitigation Mechanism |
|---|---|---|---|---|
| **RSK-01** | Occlusion & Silent Assumptions| **25 (Extreme)** | **4 (Low)** | RCM, Mode B Clarification, Scope Blocking & Dimensional Uncertainty Tracking |
| **RSK-02** | Single-Threaded API Deadlocks | **15 (High)** | **4 (Low)** | Single Executor Invariance & Transactional Partitioning |
| **RSK-03** | Family Starvation & Exceptions| **16 (High)** | **4 (Low)** | Tiered Native Fallback & Controlled Exception Log |
| **RSK-04** | False-Positive QA Trap | **20 (Extreme)** | **2 (Negligible)** | Gate 0 Pre-Execution & Conditional 5-Gate Matrix |
| **RSK-05** | Viewport Export Timeouts | **9 (Medium)** | **3 (Low)** | Lightweight QA ViewTemplates & Capped 1080p Resolution |
| **RSK-06** | Self-Intersecting Loops | **8 (Low-Med)** | **2 (Negligible)** | Pre-API Topological Polygon Normalizer |
| **RSK-07** | Structural Liability | **10 (Medium)** | **2 (Negligible)** | Mandatory Engineering Disclaimer & Maturity Level Cap |
| **RSK-08** | Premature Maturity Claim | **16 (High)** | **3 (Low)** | Mandatory Model Maturity Classification Protocol |
| **RSK-09** | User Clarification Fatigue | **6 (Low)** | **2 (Negligible)** | Consolidated Single-Round Clarification with Options (Q0–Q8) |
| **RSK-10** | Premature Structural Sizing | **5 (Low)** | **1 (Negligible)** | Strict Prohibition on Sizing Recommendations |
| **RSK-11** | Dependency Conflation | **12 (Medium)** | **3 (Low)** | Five-Way Dependency Disaggregation & Conditional DAG Evaluation |

---

## 5. Final Unresolved Risk List (Post-Governance Review)

The following residual risks cannot be eliminated purely through software architecture and require explicit human-in-the-loop governance:

1. **Unresolvable Sub-Surface Geotechnical Conditions:**  
   No AI computer vision system can determine subsurface soil bearing capacity ($q_a$), water table elevation, or bedrock depth from an architectural rendering. Foundation depths remain provisional assumptions (`ASSUMED_PENDING_USER_APPROVAL`) until verified by a geotechnical soil investigation report.
2. **Proprietary Curtain Wall / Facade Engineering Systems:**  
   High-end architectural facades often feature proprietary extruded aluminum profiles, motorized louvers, and bespoke structural silicone glazing. While AIGRAV models their exterior form, exact fabrication extrusion dies require specialty subcontractor shop drawings.
3. **Local Municipal Zoning & Code Envelopes:**  
   Setback rules, floor area ratios (FAR), building coverage ratios (BCR), and maximum height limits vary by specific municipal jurisdiction. AIGRAV must rely on user-provided `PROJECT_INPUT_VERIFIED` parameters for zoning compliance.

---
*End of Refined Deliverable 6 — Modeling Fidelity Risk Register is Staged for Independent Review (`MF0_READY_FOR_INDEPENDENT_REVIEW`).*
