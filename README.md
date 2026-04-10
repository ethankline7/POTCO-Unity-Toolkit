# POTCO + Toontown Unity Toolkit

> A Unity Editor toolkit for Panda3D-style game assets and world data workflows, with POTCO support and an active Toontown migration path.
---

## Fork Notice and Authorship Disclosure
- This repository is a community fork derived from the original upstream project.
- The maintainer of this fork is **not affiliated** with the original creator.
- The modifications introduced in this fork’s migration work were produced through a **vibe-coding workflow using Codex tooling**.
- Upstream project ownership and original authorship remain with their respective contributors.

---
## ⚠️ Unity may take up to an hour to process all .egg files during the initial import, as it needs to cache all model data. Your patience is appreciated! ⚠️
---

## ✨ What is this?

This toolkit brings **Pirates of the Caribbean Online** workflows into Unity and now includes an active **Toontown toolchain migration path**. You can import world data, export custom content, and build scene tooling with a shared adapter architecture.

The goal is to support community-made worlds and tooling while keeping migration work practical and incremental. POTCO tools remain available, and Toontown tools can be launched now through the quick-start flow.

## 🚀 Features

### Core Tools
🗺️ **World Data Importer** - Import POTCO World Data files into Unity scenes  
🏴‍☠️ **World Data Exporter** - Export Unity scenes back to game-compatible files  
⛏️ **Procedural Cave Generator** - Create interconnected cave systems with presets  
🎨 **Advanced EGG File Importer** - Import Panda3D .egg models              
🖼️ **Native .RGB Importer** - Supports .rgb alpha transparency 

### Toontown Migration Tooling (Current)
- ✅ `Toontown/Quick Start` launcher
- ✅ Bundled sample world for first-run validation
- ✅ Toontown importer/exporter parse + round-trip scaffold
- ✅ `Toontown/World Data/DNA Scene Importer (MVP)` for `.dna -> Unity` hierarchy import
- ✅ `Toontown/Validation/Run Sample Smoke Test`
- ✅ CSV validation report export for mapping/tuning evidence
- ✅ `Toontown/Environment Switcher` for quick scene lighting, skybox, fog, audio, and effect preset passes

### Level Creation & Management
🏗️ **Level Editor (Prop Browser)** - Visual asset browser and placement tool  
🎯 **ObjectList Detection** - Automatic POTCO object classification and organization  
🖼️ **Custom Thumbnails** - Auto-generated preview images for all assets  
📂 **Smart Categorization** - Folder-based and ObjectList-based organization  

---

## 🛠️ Quick Start

### Requirements
- Unity 6000.1.11f1 (Most versions work)
- Universal Render Pipeline (URP)

Open the project in Unity 6000.1.11f1, then access tools via:
**Unity Menu Bar → POTCO → [Choose Tool]**

## 🧭 Contribution Workflow
- Contributor guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- Engineering workflow: [`docs/WORKFLOW.md`](docs/WORKFLOW.md)
- Toontown migration plan: [`docs/TOONTOWN_MIGRATION_PLAN.md`](docs/TOONTOWN_MIGRATION_PLAN.md)
- Toontown quick start (first runnable flow): [`docs/TOONTOWN_QUICKSTART.md`](docs/TOONTOWN_QUICKSTART.md)
- Current branch/project status: [`docs/REPO_STATUS.md`](docs/REPO_STATUS.md)
- Recommended next mainline update: [`docs/NEXT_MAINLINE_UPDATE.md`](docs/NEXT_MAINLINE_UPDATE.md)
- Required baseline check before commit:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/primary-checks.ps1`
- Optional local headless smoke check:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-toontown-smoke.ps1`
- Optional Toontown DNA resource setup:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/setup-toontown-resources.ps1`
- Optional Toontown DNA MVP batch demo:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-toontown-dna-demo.ps1`
- Windows `cmd` run action wrapper (launch + checks + demo):
  - `scripts\run-action.cmd help`
  - `scripts\run-action.cmd open`
  - `scripts\run-action.cmd primary-checks`
  - `scripts\run-action.cmd smoke`
  - `scripts\run-action.cmd dna-demo`

---

## 🎮 Tools Overview

### 🎨 EGG File Importer Manager
Advanced Panda3D `.egg` file processing with comprehensive filtering options:
- **Multi-Texture Support** - Specialized overlay system for tiling ground textures (known limitation; needs stabilization)
- **Advanced Filtering** - Skip footprints, LOD control, skeletal data filtering
- **Folder Exclusions** - Default exclusions for gui, effects, sea, sky, textureCards
- **Manual Import Control** - Disable auto-import to prevent Unity startup delays
- **Startup Import Prompt** - Modal dialog for selective batch importing

### 🗺️ World Data Importer
Import `.py` World Data files from POTCO into Unity scenes:
- Automatic object placement and hierarchy creation
- ObjectList data integration for proper object classification
- Collision and Holiday Props import options
- Prop color application toggle
- Coordinate system conversion (Panda3D ↔ Unity)
- Import speed optimization

### 🏴‍☠️ World Data Exporter  
Export Unity scenes back to POTCO-compatible format:
- Advanced ObjectList filtering and type detection
- Coordinate system conversion between Unity and Panda3D formats

### ⛏️ Procedural Cave Generator
Create interconnected cave systems using connector-based cave pieces:
- Connector-based assembly system (`cave_connector_*` transform markers)
- Weighted randomization for varied layouts
- Built-in theme presets (Padres Del Fuego Theme)
- Custom configuration saving/loading (JSON format)

### 🏗️ Level Editor (Prop Browser)
Visual asset management and level creation tool:
- **Thumbnail Grid View** - Visual browsing of all POTCO assets
- **Smart Categorization** - Multiple organization methods:
  - Folder-based categorization
  - ObjectList-based categorization (Buildings, Props, Lighting, etc.)
  - Uncategorized item handling
- **Quick Placement** - Drag-and-drop asset placement in scenes
- **Search and Filtering** - Find assets quickly across categories
---

## 📖 Basic Usage

### Import a World
1. Open `POTCO → World Data Importer`
2. Click "Select World .py File"
3. Choose your world file and click "Import"
4. Watch as your POTCO world appears in Unity!

### Use the Level Editor
1. Open `POTCO → Level Editor`
2. Browse assets using the thumbnail grid
3. Switch between categorization modes (Folder/ObjectList)
4. Drag assets directly into your scene
5. Assets are automatically categorized and ready for export
6. Select "🎯Surface" for any placed models either from importing or dragging out to contain collisions to be able to easily drag and drop props on top seemlessly will cause the world importer to take longer

### Toontown First Launch (Current Migration Flow)
1. Open `Toontown → Quick Start`
2. Click `Switch Active Game Flavor to Toontown`
3. Open importer/exporter from quick-start buttons
4. Use bundled sample actions to run parse + export loop
5. Run `Toontown → Validation → Run Sample Smoke Test`
6. Open `Toontown → World Data → DNA Scene Importer (MVP)` for OpenToontown `.dna` scene import


### Generate a Cave
1. Open `POTCO → Procedural Cave Generator`  
2. Load the "Padres Del Fuego Theme" preset
3. Set cave length (try 10-15 pieces)
4. Click "Generate Cave" and watch it build!

### Import EGG Files
1. Open `POTCO → EGG Importer Manager`
2. Configure filtering options (skip footprints, LOD settings, etc.)
3. Choose folders to exclude (gui, effects recommended)
4. Use "Import All EGG Files" or selective import
5. View statistics in the Statistics tab

### Export Your Scene
1. Add objects to your scene (use Level Editor for easy placement)
2. Open `POTCO → World Data Exporter`
3. Configure export settings and filters
4. Click "Export World Data" to create a `.py` file

**Important Notes:**
- All props must have a parent object to export properly
- Imported WorldData always spawns at (0,0,0)
- Use "Add ObjectListInfo to Selected Objects" for manual object classification

---

## 🖼️ Screenshots

### Level Editor in Action
https://github.com/user-attachments/assets/9ebcb461-3981-4f06-8db6-6236e9a3b744

### Cave Generation in Action

https://github.com/user-attachments/assets/39e63113-1cf6-457f-9515-f33dab15f0b0

### World Importer in Action
https://github.com/user-attachments/assets/b4187ae0-391a-410d-99f8-5706204fa792

### Custom Scenes
<img width="1607" height="1091" alt="image" src="https://github.com/user-attachments/assets/6a8616a4-4b24-401c-964a-fefe9b388d31" />

### POTCO Tortuga Tavern
<img width="3829" height="1890" alt="image" src="https://github.com/user-attachments/assets/e0e57ff1-6b74-4bf9-a862-c4084f12340f" />

### Tools Windows
<img width="3143" height="1356" alt="Unity_CTd6HhjjwX" src="https://github.com/user-attachments/assets/58e3240e-da14-47e3-8cd0-c2b454ea57b9" />

---

## ⚠️ Known Issues

- Some manually dragged objects may not receive ObjectListInfo components automatically - use "Add ObjectListInfo to Selected Objects" in the exporter
- Large cave systems may occasionally experience piece overlapping
- Complex EGG files with extensive bone data may require additional processing time



---

## 📚 Educational Use

This project is designed for **educational and research purposes**:

❌ **Original POTCO assets remain property of their owners**  

---

---

## 🏴‍☠️ Build and Iterate!

Transform Unity into a practical game-world toolkit for POTCO and the ongoing Toontown migration flow. Start with the quick-start path, run smoke checks, and iterate in small validated steps.

**Create, explore, and share your pirate adventures!** ⚓

---

*Built with passion for the POTCO community. Fair winds and following seas!*
