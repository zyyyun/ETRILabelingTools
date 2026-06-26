
# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the solution
dotnet build ETRILabelingTool.sln

# Build for x64 Debug (default target)
dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64

# Build for Release
dotnet build ETRILabelingTool.sln -c Release -p:Platform=x64

# Publish
dotnet publish ETRILabelingTool.sln

# Run the application
dotnet run --project WinFormsApp1/WinFormsApp1.csproj
```

## Project Overview

This is a **video labeling tool** (ETRI Labeling Tool) built with Windows Forms (.NET 8.0). It is used for annotating video frames with bounding boxes for persons, vehicles, and events. The tool exports annotations in a COCO-like JSON format.

## Architecture

### Main Components

- **Form1.cs** (~15,000+ lines): The main form containing all UI and core logic. This is a large monolithic file that handles:
  - Video playback (OpenCV via OpenCvSharp4)
  - Bounding box drawing and manipulation
  - YOLO detection integration (GPU-accelerated via ONNX Runtime)
  - Timeline/waypoint management
  - JSON import/export
  - Skeleton visualization (3D joint data)

- **PersonAttributesForm.cs**: Modal dialog for editing person attributes (clothing, accessories, body characteristics). Supports both single-select (ComboBox) and multi-select (CheckedListBox) attributes. Includes numpad shortcuts for quick attribute selection.

- **CudaEnvironmentHelper.cs**: Utility class that ensures CUDA DLLs are available in the process PATH for GPU-accelerated YOLO inference.

### Key Data Structures (in Form1.cs)

- `BoundingBox`: Represents a labeled region with frame index, rectangle, label type, person/vehicle/event IDs, skeleton data, and person attributes
- `WaypointMarker`: Tracks entry/exit frames for objects across the video timeline
- `PersonAttributeStore`: Manages person attributes with waypoint-scoped (per-segment) and global (per-video) storage
- `LabelingDataExtended`: COCO-format JSON structure for serialization

### Person Attributes System

Attributes are divided into two types:
- **Waypoint-scoped** (orange labels): Occlusion, BodyView, ActionType - change per waypoint segment
- **Global** (black labels): All other attributes (Age, Gender, clothing, etc.) - persist across waypoints

Attribute values are stored in English internally but displayed in Korean in the UI.

### Dependencies

- OpenCvSharp4 (video handling)
- YoloSharp.Gpu (YOLO detection)
- Microsoft.ML.OnnxRuntime.Gpu (GPU inference)
- FFMpegCore (video processing)
- Newtonsoft.Json (JSON serialization)

## Language

The UI and comments are primarily in Korean. JSON output uses English attribute values.
