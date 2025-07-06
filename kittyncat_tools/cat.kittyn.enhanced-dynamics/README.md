# Enhanced Dynamics for VRChat
**Version 1.0.0**

A Unity Editor extension that provides instant physics preview for VRChat PhysBone systems, bypassing all build pipelines for instant switching between edit and preview modes.

## Overview

*Enhanced Dynamics was inspired by Avatar Dynamics Overhaul (ADO), but takes a completely different implementation approach. While ADO focuses on runtime optimization, Enhanced Dynamics prioritizes instant editor workflow through avatar hiding and memory-based persistence. Whether this approach is better or worse depends on your specific needs - ADO may be better for runtime performance, while Enhanced Dynamics excels at instant preview without build pipeline interference.*

## ⚠️ Important Warning

**During physics preview, Unity is in Play Mode!** The editor will show the play button as active. If you unhide your avatar during preview mode, it will trigger all build scripts (VRCFury, Modular Avatar, NDMF, etc.) attached to that avatar. Always use the "Exit Preview" button to safely return to edit mode.

## Features

- **Instant Physics Preview**: Switch between edit and preview modes without waiting for build pipelines
- **Avatar Hiding System**: Prevents VRCFury, Modular Avatar, and NDMF from triggering during preview
- **Memory-Based Save System**: Capture and apply physics changes without triggering build systems
- **Floating UI Panel**: Repositionable controls with save/exit functionality
- **Avatar Manipulation Gizmo**: Translation and rotation controls at avatar center
- **Component Selection**: Automatic hierarchy expansion and component focusing
- **Full VRChat Support**: Works with PhysBones, PhysBoneColliders, ContactSenders, and ContactReceivers
- **Inspector Enhancements**: Inline utility buttons for PhysBone collider properties

## Requirements

- Unity 2022.3 or higher
- VRChat SDK (Avatars 3.0)
- Harmony 2.2

## Installation

### VPM (Recommended)
1. Add the VPM package to your project
2. The tool will automatically initialize when Unity starts

### Manual Installation
1. Import the VRChat SDK into your Unity project
2. Copy the `EnhancedDynamics` folder into your project's Assets folder
3. Ensure you have Harmony 2.2 dll in your project (usually comes with certain Unity packages)

## Usage

### Physics Preview
1. **Start Preview**: Select a PhysBone or PhysBoneCollider component and click "Enter Physics Preview" in the inspector
2. **Manipulate Physics**: Use the floating UI panel and avatar gizmo to adjust physics in real-time
3. **Save Changes**: Click "Save Changes" in the floating panel to capture your modifications
4. **Exit Preview**: Click "Exit Preview" to return to edit mode and apply saved changes

### Inspector Enhancements
Enhanced controls automatically appear at the bottom of VRC PhysBone Collider component inspectors:
- **Radius**: Scale buttons (×0.5, ×0.8, ×1.2, ×2) and Reset
- **Height** (Capsule only): Scale buttons and Reset  
- **Position**: Center, Snap to 0.01 grid, Flip X/Y/Z
- **Rotation**: Reset, +90° rotations for each axis
- **Batch Operations**: Copy/Paste collider values, paste to multiple selected objects

### Contact Components
Contact Senders and Receivers get additional gizmo controls:
- **Radius Gizmo**: Visual radius adjustment handles
- **Height Gizmo**: For capsule contacts, adjust height with handles
- **Position/Rotation Gizmos**: Direct manipulation in scene view

## Keyboard Shortcuts

- **Enter Preview**: Available via menu `Tools > Enhanced Dynamics > Enter Physics Preview`
- **Exit Preview**: Use the floating UI panel or exit play mode
- **Save Changes**: Use the floating UI panel during preview

## Known Limitations

- Physics preview requires Unity Play Mode (this is intentional to use VRChat's physics system)
- Object references in physics components cannot be restored from saves (limitation of serialization)
- Some third-party components may not work correctly in the physics clone environment
- Avatar must have PhysBone components to create a physics preview

## Troubleshooting

**Preview doesn't start**: Ensure your avatar has PhysBone components and VRChat SDK is properly imported.

**Build systems trigger**: Make sure you're using the "Exit Preview" button, not manually unhiding avatars.

**Changes don't save**: Verify you clicked "Save Changes" before exiting preview mode.

**Performance issues**: Try reducing the number of PhysBone components or simplifying your avatar during preview.

## Technical Details

This tool uses Harmony 2.2 to patch VRChat component inspectors at runtime, adding additional UI elements without modifying the original SDK files. The physics preview system works by:

1. Hiding all VRC avatars to prevent build pipeline execution
2. Creating a physics-only clone of the selected avatar
3. Entering Unity Play Mode to initialize VRChat's physics system
4. Providing real-time manipulation through a floating UI and gizmos
5. Storing changes in memory and applying them when exiting preview

## Changelog

### v1.0.0 (2024)
- Initial release
- Instant physics preview system
- Memory-based save system
- Floating UI panel with repositioning
- Avatar manipulation gizmo
- Support for all VRChat physics components
- Inspector enhancements for PhysBone colliders