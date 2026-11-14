using UnityEngine;

public enum DeskViewTarget { None = -1, Stocks, Chart, Chat, TabletOne, TabletTwo }

/// <summary>Canvas 크기에서 정면 시점과 좌우 이동을 계산한다. 게임 상태를 변경하지 않는다.</summary>
public static class DeskViewFraming
{
    public static DeskViewTarget Navigate(DeskViewTarget current, int direction)
    {
        if (current >= DeskViewTarget.Stocks && current <= DeskViewTarget.Chat)
        { return (DeskViewTarget)Mathf.Clamp((int)current + direction, 0, 2); }
        if (current == DeskViewTarget.TabletOne || current == DeskViewTarget.TabletTwo)
        { return (DeskViewTarget)Mathf.Clamp((int)current + direction, 3, 4); }
        return current;
    }

    public static float Distance(float width, float height, float verticalFov, float aspect, float fill)
    {
        float tangent = Mathf.Tan(Mathf.Clamp(verticalFov, 1f, 120f) * .5f * Mathf.Deg2Rad);
        return Mathf.Max(height, width / Mathf.Max(.1f, aspect)) / (2f * tangent * Mathf.Clamp(fill, .1f, .98f));
    }

    public static bool TryPose(RectTransform screen, Camera camera, float fill, Vector3[] corners, out Pose pose, float fieldOfView = -1f)
    {
        pose = default;
        if (screen == null || camera == null)
        { return false; }
        screen.GetWorldCorners(corners);
        Vector3 right = corners[3] - corners[0], up = corners[1] - corners[0];
        if (right.sqrMagnitude < .000001f || up.sqrMagnitude < .000001f)
        { return false; }
        Vector3 forward = Vector3.Cross(right, up).normalized;
        Vector3 center = (corners[0] + corners[2]) * .5f;
        float distance = Mathf.Max(camera.nearClipPlane + .1f, Distance(right.magnitude, up.magnitude, fieldOfView > 0f ? fieldOfView : camera.fieldOfView, camera.aspect, fill));
        pose = new Pose(center - forward * distance, Quaternion.LookRotation(forward, up));
        return true;
    }
}
