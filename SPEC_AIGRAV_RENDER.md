# Technical Specification: AIGRAV Render Module Integration (Revit 2026)

## 1. Integration Scope
Extend the existing AIGRAV Revit 2026 plugin (.NET 8.0) by adding a dedicated "AIGRAV Render" button directly beside the existing "AIGRAV Bridge" button on the Revit Ribbon. Do NOT create a separate .addin file or overwrite existing Bridge/MCP socket handlers.

## 2. Ribbon UI Layout (App.cs Extension)
- Ribbon Panel Name: "AIGRAV AI Suite"
- Button 1 (Existing): "AIGRAV Bridge" -> Toggles MCP / Antigravity Workspace Bridge.
- Button 2 (New): "AIGRAV Render" -> Executes `LaunchAigravRenderCommand` to open the non-modal WPF/WebView2 Render Studio window.
- Threading Isolation: `AIGRAV Render` must register its own `MultiPassExportHandler : IExternalEventHandler` initialized during `OnStartup` so it never blocks or collides with the Bridge ExternalEvent queue.

## 3. 3-Pass 4K Extraction & Transaction Rollback (Shared RevitAdapter)
When the user clicks "Capture & Render" in the AIGRAV Render UI:
1. Open a temporary `Transaction(doc, "AIGRAV_Temp_Material_Map")` inside `MultiPassExportHandler`.
2. Temporarily detach View Template and hide Grids, Levels, Section Boxes, and Annotation categories.
3. Export Pass 1 (`geometry_pass.png` - Hidden Line, 3840px 4K PNG) and Pass 2 (`lighting_pass.png` - Realistic/Shaded with Sun Shadows, 3840px 4K PNG).
4. Collect all visible materials via `GetMaterialIds(false)` and `GetMaterialIds(true)`. Override each material's `SurfaceForegroundPatternId` to Solid Fill (`FillPattern.IsSolidFill`) with a unique high-contrast RGB color, and force `material.Transparency = 0`.
5. Export Pass 3 (`material_id_pass.png` - Flat Colors, 3840px 4K PNG).
6. Execute `transaction.RollBack()` inside a `finally` block to guarantee zero changes to the user's Revit model.
7. Run `ImageQuantizerService` (SixLabors.ImageSharp) using Euclidean RGB nearest-neighbor matching to eliminate viewport anti-aliasing along material edges, and save `material_map.json`.

## 4. Dual-Mode Execution Engine (`IAigravRenderDispatcher`)

### Mode A: Antigravity Bridge Handoff (`AntigravityBridgeRenderClient`)
- Writes the exported 4K passes (`geometry_pass.png`, `lighting_pass.png`, `material_id_pass.png`), `style_reference.png`, and `material_map.json` into `{WorkspaceRoot}/.aigrav/render_jobs/{JobId}/`.
- Generates `render_prompt.md` containing the structured anti-hallucination prompt and exact RGB-to-Revit-Material mapping table.
- Dispatches a JSON-RPC / MCP trigger payload over the active AIGRAV Bridge connection notifying the Antigravity Agent to execute the render workflow and watch `{WorkspaceRoot}/.aigrav/render_jobs/{JobId}/output.png` using `FileSystemWatcher` to auto-display the completed image in Revit.

### Mode B: Direct Multi-Model API Router (`DirectApiRenderRouter`)
Users can select their preferred provider and input their API Key (stored encrypted via Windows DPAPI `ProtectedData` in `%APPDATA%/AIGRAV/keys.dat`):

1. **Google Gemini Provider (`GeminiNanoBananaClient`)**:
   - Endpoint: Google GenAI REST API (`v1beta/models/{model}:generateContent`)
   - Supported Models:
     - `gemini-3-pro-image` (Nano Banana Pro - Native 4K Studio Precision)
     - `gemini-nano-banana-2.1` (Nano Banana 2.1 - High-Speed Multi-Reference)
   - Payload: Multimodal `contents` array attaching all 3 Revit 4K passes + optional Style Reference image inline as Base64/File API URI + structured JSON material instruction.
   - Output Config: `image_size: "4K"`, matching aspect ratio of the Revit viewport.

2. **OpenAI ChatGPT Provider (`OpenAiGptImageClient`)**:
   - Endpoint: OpenAI Images/Responses API
   - Supported Models:
     - `gpt-image-2.6-sunburst` (Maximum structural fidelity & complex multi-image reference editing)
     - `gpt-image-2.6-flare` (Fast architectural concept iteration)
   - Payload: Multi-image reference array (`geometry_pass.png`, `material_id_pass.png`, `style_reference.png`), `resolution: "4k"`, and strict prompt mapping RGB masks to PBR materials.

3. **xAI Grok Provider (`XaiGrokImagineClient`)**:
   - Endpoint: `https://api.x.ai/v1/images/generations` (and image-edit endpoint)
   - Supported Model: `grok-imagine-image-2.0`
   - Payload: Composite guide image + structured prompt emphasizing architectural photorealism, realistic scale, and dramatic natural lighting.