using UnityEngine;
using UnityEditor;
using EnhancedDynamics.Editor;

// This is a temporary test script to verify the physics preview works
public class TestPhysicsPreview : MonoBehaviour
{
    [MenuItem("EnhancedDynamics/Test Physics Preview")]
    static void TestPreview()
    {
        Debug.Log("[Test] Starting physics preview test...");
        PhysicsPreviewManager.StartPreview();
        
        // The preview should now be active
        Debug.Log($"[Test] Preview active: {PhysicsPreviewManager.IsPreviewActive}");
    }
}