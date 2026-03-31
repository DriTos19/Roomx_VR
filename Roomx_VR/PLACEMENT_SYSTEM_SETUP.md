# VR Furniture Placement System - Setup Guide

## Problem Summary
The furniture placement system was disappearing objects because:
1. XRRayInteractor raycast wasn't detecting the ground properly
2. No fallback raycast logic when XRRayInteractor fails
3. Ghost object visibility wasn't guaranteed during placement

## Fixed Issues
✅ Added fallback Physics.Raycast when XRRayInteractor doesn't detect hits
✅ Improved debugging logs to trace raycast hits
✅ Fixed FinalizePlacement to keep object visible and apply standard material
✅ Added FurnitureInteractable component automatically on placement
✅ Fixed FurnitureInteractable to use proper XR raycasting (not OnMouseDown)

## Required Unity Setup

### 1. **Layer Setup**
Make sure these layers exist in your project:
- **"Ground"** - Layer for your floor/ground objects
- **"Furniture"** - Layer for placed furniture (will be auto-assigned)

To check/create layers:
- Window → Tags and Layers
- Add "Ground" and "Furniture" if missing

### 2. **PlacementManager Configuration**
Select the PlacementManager GameObject in your scene and verify these settings:

**VR Input (Right Hand):**
- XR Ray Interactor: Assign your right hand XR Ray Interactor
- Trigger Press: Assign XRI RightHand Interaction/Activate
- Rotate Action: Assign XRI RightHand Thumbstick
- Cancel Action: Assign XRI RightHand Grip (or any cancel button)

**Layers & Materials:**
- Ground Layer: Set to "Ground" layer (use layer mask dropdown)
- Valid Material: Green semi-transparent material for valid placement
- Invalid Material: Red semi-transparent material for invalid placement

**Settings:**
- Grid Size: 0.5 (adjust as needed)
- Enable Snapping: true
- Rotation Speed: 180

### 3. **Ground Setup**
Your ground/floor object should have:
- ✅ Collider component (BoxCollider, MeshCollider, etc.)
- ✅ Layer set to "Ground"
- ✅ "Is Trigger" unchecked (must be physics collider)

### 4. **Inventory Item Prefabs**
Each furniture prefab in your inventory should have:
- ✅ Collider component (will be disabled during placement)
- ✅ MeshRenderer component
- ✅ Proper materials assigned

### 5. **XRRayInteractor Configuration**
Your XR Ray Interactor needs:
- ✅ Line Model: Enabled
- ✅ Raycast Mask: Should include "Ground" layer (or use physics.OverlapCapsule)
- ✅ Max Raycast Distance: Sufficient (e.g., 100+)
- ✅ Hit Detection Type: Consider "Raycast" or "raycastComplex"

## Testing Workflow

1. **Inspect Ground in Console**
   - Open Console (Window → General → Console)
   - When pointing ray at ground while in placement mode, you should see:
     - "Physics Raycast Hit: GroundName on layer Ground" OR
     - "XRRayInteractor Hit: GroundName on layer Ground"

2. **Check Placement Feedback**
   - Object should turn GREEN when over valid ground
   - Object should turn RED when over invalid area or hovering in air

3. **Place Object**
   - Press XRI RightHand Interaction/Activate (trigger)
   - Object should stay visible with standard material
   - Object layer should auto-set to "Furniture"
   - Console should show: "Object placed successfully at [position]"

4. **Pick Up Object**
   - Double-click with trigger on placed furniture
   - Should enter placement mode again
   - Console should show: "Double-click detected on [object]"

## Troubleshooting

**Object not appearing on screen during placement:**
- Check if prefab has MeshRenderer and materials
- Check if layers are correctly assigned
- Check Console for error messages

**Object turns red but doesn't turn green:**
- Verify ground object is on "Ground" layer
- Check groundLayer LayerMask is correctly set
- Verify ground collider is not marked as "Is Trigger"

**Object disappears after clicking:**
- Check logs for "Object placed successfully" message
- Verify FurnitureInteractable component was added
- Check if "Furniture" layer exists

**Can't pick up placed furniture:**
- Verify FurnitureInteractable component exists on placed object
- Check if XRRayInteractor is configured correctly
- Try double-clicking faster (default DOUBLE_CLICK_TIME = 0.3s)

## Key Code Changes

### PlacementManager.HandlePositioning()
- Added fallback Physics.Raycast when XRRayInteractor.TryGetCurrent3DRaycastHit() fails
- Ensures raycast uses the groundLayer mask
- Added comprehensive debug logging

### PlacementManager.FinalizePlacement()
- Automatically adds FurnitureInteractable component
- Resets material to Standard shader for visibility
- Keeps object SetActive(true) to prevent disappearance

### FurnitureInteractable
- Switched from OnMouseDown() to XR raycasting
- Properly detects furniture selection in VR mode
- Uses PlacementManager's XRRayInteractor for consistency

