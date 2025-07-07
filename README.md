# cátte — Enhanced Dynamics

UI Enhancements for VRChat Dynamics (viewport handles, physics preview)

![Physics Preview Screenshot](https://via.placeholder.com/800x400/2D3748/FFFFFF?text=Enhanced+Dynamics+Physics+Preview+Screenshot)

## Installation

### VRChat Creator Companion (Recommended)

1. Add the repository to VCC: `https://enhanced-dynamics.kittyn.cat/index.json`
2. Find "cátte — Enhanced Dynamics" in the package list
3. Click "Add" to install to your project

### Manual Installation

1. Download the latest release from [Releases](https://github.com/kittynXR/enhanced-dynamics/releases)
2. Extract the zip file to your Unity project's `Packages` folder

## Requirements

- Unity 2019.4.31f1 or later
- VRChat SDK 3.7.0 or later

## Features

### 🎮 Physics Preview System
- **Real-time Physics Testing** - Test PhysBone behavior without uploading to VRChat
- **Isolated Preview Environment** - Creates physics-only clone of your avatar
- **Safe Experimentation** - Automatic state restoration when exiting preview
- **Change Tracking** - Captures modifications and applies them back to original avatar

### 🔧 Enhanced Inspectors
- **PhysBone Inspector** - Added "Preview Physics" button for instant testing
- **PhysBoneCollider Inspector** - Enhanced with preview controls and viewport gizmos
- **Interactive Gizmo Toggles** - Enable/disable radius, height, position, and rotation handles
- **Real-time Visualization** - See collider changes instantly in scene view

### 🎯 Viewport Enhancements
- **Interactive Collider Handles** - Orange radius handles, yellow height handles
- **Position & Rotation Gizmos** - Move and rotate colliders with visual feedback
- **Proper Handle Scaling** - Handles scale appropriately with scene view zoom
- **Undo Support** - Full undo/redo integration for all gizmo operations

### ⚡ Workflow Optimization
- **No Upload Required** - Test physics behavior instantly in editor
- **Rapid Iteration** - Make changes and see results immediately
- **Build Pipeline Isolation** - Prevents unwanted component processing during preview
- **Memory-based Persistence** - Changes persist between preview sessions

## Usage

### Getting Started

1. **Access Menu**: Go to `Tools > ⚙️🎨 kittyn.cat 🐟 > Enhanced Dynamics`
2. **Enable Physics Preview**: Select your avatar and choose "🐟 Enter Physics Preview"
3. **Test & Iterate**: Modify PhysBone settings and see real-time results
4. **Save Changes**: Exit preview to apply changes back to your original avatar

### Common Workflows

#### Testing PhysBone Settings
```
1. Select your avatar root in hierarchy
2. Use "Enter Physics Preview" from menu or inspector button
3. Adjust PhysBone parameters (gravity, damping, elasticity)
4. See immediate physics simulation results
5. Exit preview to save changes
```

#### Adjusting PhysBone Colliders
```
1. Select a PhysBoneCollider component
2. Click "Preview Physics" button in inspector
3. Toggle viewport gizmos (radius, height, position)
4. Drag handles to adjust collider size and position
5. Test interaction with PhysBones in real-time
```

#### Rapid Prototyping Workflow
```
1. Create multiple PhysBone setups
2. Enter physics preview mode
3. Test different configurations quickly
4. Use undo/redo for quick comparisons
5. Save only the settings you're happy with
```

### Advanced Features

#### Physics Preview Controls
- **Floating UI Panel** - In-scene controls for save/exit operations
- **Avatar Translation** - Move entire avatar during preview with gizmo
- **Component Isolation** - Strips build-triggering components for clean testing
- **Context-aware Selection** - Automatically selects corresponding components in clone

#### Viewport Gizmo System
- **Color-coded Handles** - Orange for radius, yellow for height adjustments
- **Multi-handle Support** - Adjust multiple colliders simultaneously
- **Smooth Interaction** - Responsive handle movement with proper constraints
- **Visual Feedback** - Immediate visual response to parameter changes

#### Menu Options
- **🐟 Enter Physics Preview** - Start physics simulation mode
- **🐟 Exit Physics Preview** - End simulation and restore original state
- **🐟 Reinitialize Patches** - Refresh system patches if needed
- **🐟 Test Debug Output** - Verify system functionality

## Screenshots

![Inspector Enhancement](https://via.placeholder.com/600x400/2D3748/FFFFFF?text=Enhanced+PhysBone+Inspector+with+Preview+Button)
*Enhanced PhysBone Inspector with Preview Physics button*

![Viewport Gizmos](https://via.placeholder.com/600x400/2D3748/FFFFFF?text=Viewport+Gizmos+for+PhysBone+Colliders)
*Interactive viewport gizmos for PhysBone colliders*

![Physics Preview UI](https://via.placeholder.com/600x300/2D3748/FFFFFF?text=Physics+Preview+Floating+UI+Panel)
*Floating UI panel during physics preview mode*

## Why Enhanced Dynamics?

### Before Enhanced Dynamics:
- Upload avatar to VRChat to test physics
- Wait for upload and processing
- Join VRChat world to test
- Repeat cycle for each adjustment

### After Enhanced Dynamics:
- Test physics instantly in Unity editor
- Make rapid adjustments with immediate feedback
- Perfect your setup before uploading
- Save hours of iteration time

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- [Issues](https://github.com/kittynXR/enhanced-dynamics/issues)
- [Discussions](https://github.com/kittynXR/enhanced-dynamics/discussions)