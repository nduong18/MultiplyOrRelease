# Project workflow

- Always check Unity MCP connectivity and the active Unity instance before working on this project. Confirm the instance points to `C:/UnityGame/MultiplyOrRelease`.
- Use Unity MCP for editor operations, scene creation/loading/saving, play-mode verification, console inspection, and screenshots. Read editor state before mutations and wait for compilation/domain reload to finish.
- After script edits, refresh/import as needed and check Unity Console for compilation errors before using new components.
- Preserve existing assets and unrelated user changes. Create Multiply or Release content under `Assets/MultiplyOrRelease`.
- The game specification is `C:/Users/tt/Downloads/Multiply or Release Docs.docx`. Keep simulation, team visuals, Plinko, grid appearance, cannon sweep, projectiles, and trails configurable.
