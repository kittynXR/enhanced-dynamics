# Enhanced Dynamics for VRChat

A Unity tool that enhances the VRC PhysBone Collider component inspector with inline utility buttons for faster workflow.

## Features

- **Inline buttons** for quick adjustments to PhysBone collider properties:
  - **Radius**: Scale buttons (×0.5, ×0.8, ×1.2, ×2) and Reset
  - **Height** (Capsule only): Scale buttons and Reset
  - **Position**: Center, Snap to 0.01 grid, Flip X/Y/Z
  - **Rotation**: Reset, +90° rotations for each axis

- **Batch operations**:
  - Copy/Paste collider values
  - Paste to all selected GameObjects with PhysBone colliders

- **Smart UI**:
  - Height controls only show for Capsule colliders
  - Radius controls hidden for Plane colliders

## Requirements

- Unity 2022.3 or higher
- VRChat SDK (Avatars 3.0)
- Harmony 2.2

## Installation

1. Import the VRChat SDK into your Unity project
2. Copy the `EnhancedDynamics` folder into your project's Assets folder
3. Ensure you have Harmony 2.2 dll in your project (usually comes with certain Unity packages)

## Usage

The enhanced controls automatically appear at the bottom of any VRC PhysBone Collider component inspector. All operations support Unity's Undo system.

## Technical Details

This tool uses Harmony 2.2 to patch the VRChat PhysBone Collider inspector at runtime, adding additional UI elements without modifying the original SDK files.