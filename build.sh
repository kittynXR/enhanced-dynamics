#!/bin/bash

# Build script for cátte — Enhanced Dynamics
# Creates VCC-compatible packages and GitHub releases

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
PACKAGE_NAME="cat.kittyn.enhanced-dynamics"
ROOT_DIR="cat.kittyn.enhanced-dynamics"
REPO_OWNER="kittynXR"
REPO_NAME="enhanced-dynamics"

# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check for uncommitted changes
if ! git diff-index --quiet HEAD --; then
    print_error "You have uncommitted changes. Please commit or stash them before building."
    exit 1
fi

# Function to save Unity version
save_unity_version() {
    echo "$1" > .unity-version
    print_success "Unity version set to: $1"
    print_info "This will be used for all future builds in this project."
    exit 0
}

# Parse arguments
if [ "$1" = "--set-unity-version" ]; then
    if [ -z "$2" ]; then
        print_error "Unity version required"
        echo "Usage: $0 --set-unity-version <version>"
        echo "Example: $0 --set-unity-version 2022.3.22f1"
        exit 1
    fi
    save_unity_version "$2"
fi

# Parse version argument
if [ -z "$1" ]; then
    print_error "Version argument required: major, minor, patch, or specific version (e.g., 1.2.3)"
    echo "Usage: $0 <major|minor|patch|x.y.z>"
    echo "       $0 --set-unity-version <unity-version>"
    exit 1
fi

VERSION_ARG=$1

# Read Unity version if configured
if [ -f ".unity-version" ]; then
    UNITY_VERSION=$(cat .unity-version)
else
    UNITY_VERSION="2019.4.31f1"  # Default VRChat version
fi

# Read current version from package.json
CURRENT_VERSION=$(cat "kittyncat_tools/$ROOT_DIR/package.json" | grep '"version"' | sed -E 's/.*"version": "([^"]+)".*/\1/')
print_info "Current version: $CURRENT_VERSION"

# Parse current version
IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_VERSION"
MAJOR="${VERSION_PARTS[0]}"
MINOR="${VERSION_PARTS[1]}"
PATCH="${VERSION_PARTS[2]}"

# Determine new version
case "$VERSION_ARG" in
    major)
        NEW_VERSION="$((MAJOR + 1)).0.0"
        ;;
    minor)
        NEW_VERSION="$MAJOR.$((MINOR + 1)).0"
        ;;
    patch)
        NEW_VERSION="$MAJOR.$MINOR.$((PATCH + 1))"
        ;;
    *)
        # Validate version format
        if [[ ! "$VERSION_ARG" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            print_error "Invalid version format. Use major, minor, patch, or x.y.z"
            exit 1
        fi
        NEW_VERSION="$VERSION_ARG"
        ;;
esac

print_info "New version: $NEW_VERSION"

# Update package.json version
print_info "Updating package.json..."
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s/\"version\": \"$CURRENT_VERSION\"/\"version\": \"$NEW_VERSION\"/" "kittyncat_tools/$ROOT_DIR/package.json"
else
    # Linux
    sed -i "s/\"version\": \"$CURRENT_VERSION\"/\"version\": \"$NEW_VERSION\"/" "kittyncat_tools/$ROOT_DIR/package.json"
fi

# Update download URL in package.json
DOWNLOAD_URL="https://github.com/$REPO_OWNER/$REPO_NAME/releases/download/v$NEW_VERSION/$PACKAGE_NAME-$NEW_VERSION.zip"
if [[ "$OSTYPE" == "darwin"* ]]; then
    sed -i '' "s|\"url\": \"[^\"]*\"|\"url\": \"$DOWNLOAD_URL\"|" "kittyncat_tools/$ROOT_DIR/package.json"
else
    sed -i "s|\"url\": \"[^\"]*\"|\"url\": \"$DOWNLOAD_URL\"|" "kittyncat_tools/$ROOT_DIR/package.json"
fi

# Create output directory for VPM-compatible package
OUTPUT_DIR="output"
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Copy package files directly (VPM expects package.json at root)
print_info "Copying package files..."
cp -r "kittyncat_tools/$ROOT_DIR"/* "$OUTPUT_DIR/"

# No assembly definition cleanup needed - using single .asmdef files for both packages

# Create versioned zip
ZIP_FILE="$PACKAGE_NAME-$NEW_VERSION.zip"
print_info "Creating $ZIP_FILE..."
cd "$OUTPUT_DIR"
zip -r "../$ZIP_FILE" *
cd ..


# Clean up
rm -rf "$OUTPUT_DIR"

# Create UnityPackage
UNITY_PACKAGE="$PACKAGE_NAME-$NEW_VERSION.unitypackage"
print_info "Creating $UNITY_PACKAGE..."

# Find Unity executable
if [ -n "$UNITY_PATH" ]; then
    # Use user-provided Unity path
    UNITY_EXEC="$UNITY_PATH"
    print_info "Using Unity from UNITY_PATH: $UNITY_EXEC"
elif command -v Unity &> /dev/null; then
    UNITY_EXEC="Unity"
elif [ -f "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity" ]; then
    # macOS Unity Hub
    UNITY_EXEC="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
elif [ -f "/opt/Unity/Editor/Unity" ]; then
    # Linux standard installation
    UNITY_EXEC="/opt/Unity/Editor/Unity"
elif [ -f "/mnt/c/Program Files/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity.exe" ]; then
    # WSL - Windows Unity Hub installation
    UNITY_EXEC="/mnt/c/Program Files/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity.exe"
elif [ -f "/mnt/c/Program Files/Unity/Editor/Unity.exe" ]; then
    # WSL - Windows Unity installation
    UNITY_EXEC="/mnt/c/Program Files/Unity/Editor/Unity.exe"
else
    print_warning "Unity $UNITY_VERSION not found. You can:"
    print_info "1. Set Unity version: ./build.sh --set-unity-version <version>"
    print_info "2. Set UNITY_PATH: export UNITY_PATH='/path/to/Unity.exe'"
    print_info "3. Install Unity $UNITY_VERSION via Unity Hub"
    print_info "Will try tar method for UnityPackage creation."
    UNITY_EXEC=""
fi

if [ -n "$UNITY_EXEC" ] && [ -f "ProjectSettings/ProjectVersion.txt" ]; then
    # We're in a Unity project, use Unity to export
    "$UNITY_EXEC" -batchmode -nographics -silent-crashes -quit \
        -projectPath "$(pwd)" \
        -exportPackage "Packages/$PACKAGE_NAME" "$UNITY_PACKAGE" || {
        print_warning "Failed to create UnityPackage with Unity. Trying tar method..."
        UNITY_PACKAGE=""
    }
fi

# If Unity export failed or we're not in a Unity project, create UnityPackage with tar
if [ ! -f "$UNITY_PACKAGE" ]; then
    print_info "Creating UnityPackage using tar method..."
    
    # Ensure we have the package filename
    if [ -z "$UNITY_PACKAGE" ]; then
        UNITY_PACKAGE="$PACKAGE_NAME-$NEW_VERSION.unitypackage"
    fi
    
    # Create temporary directory structure
    TEMP_UNITY_DIR="temp_unity_package_$$"
    mkdir -p "$TEMP_UNITY_DIR/Assets"
    
    # Copy the kittyncat_tools directory preserving structure
    cp -r "kittyncat_tools" "$TEMP_UNITY_DIR/Assets/"

    # Remove embedded localization provider code for UnityPackage to avoid duplicates
    # (Provider is shipped via ComfiHierarchy only for .unitypackage installs)
    rm -rf "$TEMP_UNITY_DIR/Assets/kittyncat_tools/cat.kittyn.enhanced-dynamics/Editor/Localization"
    rm -rf "$TEMP_UNITY_DIR/Assets/kittyncat_tools/cat.kittyn.enhanced-dynamics/Resources/Localization/Editor" 2>/dev/null || true
    
    # No assembly definition swapping needed - using single .asmdef files for both packages
    print_info "Using existing assembly definitions for UnityPackage..."
    
    # Create proper Unity package structure
    UNITY_PACKAGE_DIR="unity_package_structure_$$"
    mkdir -p "$UNITY_PACKAGE_DIR"
    
    # Process each file and its meta
    cd "$TEMP_UNITY_DIR"
    find Assets -type f -name "*.meta" | while read -r meta_file; do
        # Extract GUID from meta file
        guid=$(grep "guid:" "$meta_file" | awk '{print $2}' | tr -d '\r')
        
        # Skip if no GUID found
        if [ -z "$guid" ]; then
            continue
        fi
        
        # Get the actual asset file (remove .meta extension)
        asset_file="${meta_file%.meta}"
        
        # Skip if asset file doesn't exist (meta without asset)
        if [ ! -f "$asset_file" ]; then
            continue
        fi
        
        # Create GUID directory
        mkdir -p "../$UNITY_PACKAGE_DIR/$guid"
        
        # Copy files with Unity's expected names
        cp "$asset_file" "../$UNITY_PACKAGE_DIR/$guid/asset"
        cp "$meta_file" "../$UNITY_PACKAGE_DIR/$guid/asset.meta"
        
        # Create pathname file with Unix-style paths
        echo "$asset_file" | sed 's/\\/\//g' > "../$UNITY_PACKAGE_DIR/$guid/pathname"
    done
    
    # Also process directory meta files
    find Assets -type f -name "*.meta" | while read -r meta_file; do
        # Check if this meta file is for a directory
        dir_path="${meta_file%.meta}"
        if [ -d "$dir_path" ]; then
            # Extract GUID from meta file
            guid=$(grep "guid:" "$meta_file" | awk '{print $2}' | tr -d '\r')
            
            if [ -n "$guid" ]; then
                mkdir -p "../$UNITY_PACKAGE_DIR/$guid"
                cp "$meta_file" "../$UNITY_PACKAGE_DIR/$guid/asset.meta"
                # For directories, pathname is the directory path
                echo "$dir_path" | sed 's/\\/\//g' > "../$UNITY_PACKAGE_DIR/$guid/pathname"
            fi
        fi
    done
    
    cd ..
    
    # Create the unitypackage (tar.gz format)
    cd "$UNITY_PACKAGE_DIR"
    tar -czf "$SCRIPT_DIR/$UNITY_PACKAGE" *
    cd "$SCRIPT_DIR"
    
    # Clean up temp directories
    rm -rf "$TEMP_UNITY_DIR" "$UNITY_PACKAGE_DIR"
    
    if [ -f "$UNITY_PACKAGE" ]; then
        print_success "Created UnityPackage: $UNITY_PACKAGE"
    else
        print_warning "Failed to create UnityPackage"
        UNITY_PACKAGE=""
    fi
fi

# Clean up
rm -rf "$OUTPUT_DIR"

# Commit changes
print_info "Committing version bump..."
git add "kittyncat_tools/$ROOT_DIR/package.json"
git commit -m "Bump version to $NEW_VERSION"

# Create git tag
TAG="v$NEW_VERSION"
print_info "Creating tag $TAG..."
git tag "$TAG"

# Push changes
print_info "Pushing to GitHub..."
git push origin HEAD
git push origin "$TAG"

# Create GitHub release
print_info "Creating GitHub release..."

# Build release notes
RELEASE_NOTES="Release $NEW_VERSION

## What's Changed
- Version bump to $NEW_VERSION

## Installation

### VRChat Creator Companion (Recommended)
1. In VCC, click \"Manage Project\"
2. Add the repository: \`https://$REPO_NAME.kittyn.cat/index.json\`
3. Find \"cátte — Enhanced Dynamics\" in the package list and click \"Add\"
"

# Add Unity Package section if package was created
if [ -n "$UNITY_PACKAGE" ] && [ -f "$UNITY_PACKAGE" ]; then
    RELEASE_NOTES="$RELEASE_NOTES
### Unity Package
Download \`$UNITY_PACKAGE\` and import it into your Unity project.
"
fi

RELEASE_NOTES="$RELEASE_NOTES
### Manual Installation
Download \`$ZIP_FILE\` and extract to your Unity project's \`Packages\` folder."

# Build file list for release
RELEASE_FILES="$ZIP_FILE"
if [ -n "$UNITY_PACKAGE" ] && [ -f "$UNITY_PACKAGE" ]; then
    RELEASE_FILES="$RELEASE_FILES $UNITY_PACKAGE"
fi

gh release create "$TAG" \
    --title "cátte — Enhanced Dynamics $NEW_VERSION" \
    --notes "$RELEASE_NOTES" \
    $RELEASE_FILES

# Clean up local files
rm -f "$ZIP_FILE" "$UNITY_PACKAGE"

print_success "Release $NEW_VERSION created successfully!"
print_info "GitHub Release: https://github.com/$REPO_OWNER/$REPO_NAME/releases/tag/$TAG"
print_info "VPM Repository: https://$REPO_NAME.kittyn.cat/index.json"
