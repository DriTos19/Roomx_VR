using UnityEngine;

public class LocomotionBlocker : MonoBehaviour
{
    public Behaviour moveProvider;

    void Update()
    {
        if (moveProvider == null || WallPlacer_VR.Instance == null)
            return;

        moveProvider.enabled = !WallPlacer_VR.Instance.ShouldBlockMovement;
    }
}