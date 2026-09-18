using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// Editor-only debug utility for logging and drawing gizmos.
//
// This class is safe to call from runtime scripts because every public debug method
// uses [Conditional("UNITY_EDITOR")]. Calls to these methods are removed from builds.
//
// Do not place this file inside an "Editor" folder if you want to call it directly
// from normal MonoBehaviour scripts.
//
// Example usage:
//
// private void Start()
// {
//     EditorDebug.Info("Game initialized", this);
//     EditorDebug.Value("Health", currentHealth);
// }
//
// private void OnDrawGizmos()
// {
//     EditorDebug.DrawWireSphere(transform.position, 2f, EditorDebug.DebugColor.Cyan);
//     EditorDebug.DrawArrowRay(transform.position, transform.forward * 3f, EditorDebug.DebugColor.Green);
//     EditorDebug.DrawNameLabel(transform, Vector3.up * 1.5f);
// }
public static class EditorDebug
{
    // Simple reusable color presets for logs, Debug.DrawLine, and Gizmos.
    public enum DebugColor
    {
        Default,
        White,
        Gray,
        Black,
        Red,
        Orange,
        Yellow,
        Green,
        Cyan,
        Blue,
        Purple,
        Pink
    }

    // ══════════════════════════════════════════════
    // Logging
    // ══════════════════════════════════════════════

    // Logs a normal editor-only message.
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message, Object context = null)
    {
        WriteLog(LogType.Log, message, DebugColor.Default, null, context);
    }

    // Logs a colored editor-only message.
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message, DebugColor color, Object context = null)
    {
        WriteLog(LogType.Log, message, color, null, context);
    }

    // Logs a colored editor-only message with a custom tag.
    // Example: EditorDebug.Log("PLAYER", "Jumped", EditorDebug.DebugColor.Green);
    [Conditional("UNITY_EDITOR")]
    public static void Log(string tag, object message, DebugColor color = DebugColor.Default, Object context = null)
    {
        WriteLog(LogType.Log, message, color, tag, context);
    }

    // Logs an informational message using a cyan INFO tag.
    [Conditional("UNITY_EDITOR")]
    public static void Info(object message, Object context = null)
    {
        WriteLog(LogType.Log, message, DebugColor.Cyan, "INFO", context);
    }

    // Logs a success message using a green SUCCESS tag.
    [Conditional("UNITY_EDITOR")]
    public static void Success(object message, Object context = null)
    {
        WriteLog(LogType.Log, message, DebugColor.Green, "SUCCESS", context);
    }

    // Logs a warning message using Unity's warning log type.
    [Conditional("UNITY_EDITOR")]
    public static void Warning(object message, Object context = null)
    {
        WriteLog(LogType.Warning, message, DebugColor.Yellow, "WARNING", context);
    }

    // Logs an error message using Unity's error log type.
    [Conditional("UNITY_EDITOR")]
    public static void Error(object message, Object context = null)
    {
        WriteLog(LogType.Error, message, DebugColor.Red, "ERROR", context);
    }

    // Logs a named value, useful for quickly inspecting variables.
    // Example: EditorDebug.Value("Current HP", currentHp);
    [Conditional("UNITY_EDITOR")]
    public static void Value(string name, object value, DebugColor color = DebugColor.Purple, Object context = null)
    {
        WriteLog(LogType.Log, $"{name}: {value}", color, "VALUE", context);
    }

    // Prints a visual separator in the Console to make debug output easier to scan.
    [Conditional("UNITY_EDITOR")]
    public static void Separator(string title = null, DebugColor color = DebugColor.Gray)
    {
        string text = string.IsNullOrWhiteSpace(title)
            ? "────────────────────────────────────────"
            : $"──────────── {title} ────────────";

        WriteLog(LogType.Log, text, color, null, null);
    }

    // ══════════════════════════════════════════════
    // Unity Debug Draw
    // ══════════════════════════════════════════════
    // These use Debug.DrawLine / Debug.DrawRay.
    // They are useful from Update, FixedUpdate, or temporary runtime debugging.
    // Unlike Gizmos, they do not require OnDrawGizmos.

    // Draws a temporary editor-only debug line in the Scene view.
    // Useful for raycast checks, movement direction, or aiming lines.
    [Conditional("UNITY_EDITOR")]
    public static void DrawDebugLine(
        Vector3 start,
        Vector3 end,
        DebugColor color = DebugColor.Cyan,
        float duration = 0f,
        bool depthTest = true)
    {
        Debug.DrawLine(start, end, ToUnityColor(color), duration, depthTest);
    }

    // Draws a temporary editor-only debug ray in the Scene view.
    // Direction includes both direction and length.
    [Conditional("UNITY_EDITOR")]
    public static void DrawDebugRay(
        Vector3 start,
        Vector3 direction,
        DebugColor color = DebugColor.Cyan,
        float duration = 0f,
        bool depthTest = true)
    {
        Debug.DrawRay(start, direction, ToUnityColor(color), duration, depthTest);
    }

    // ══════════════════════════════════════════════
    // Gizmos - Basic
    // ══════════════════════════════════════════════
    // Call these from OnDrawGizmos or OnDrawGizmosSelected.
    // They are ideal for persistent editor visualization.

    // Draws a gizmo line between two world positions.
    [Conditional("UNITY_EDITOR")]
    public static void DrawLine(Vector3 start, Vector3 end, DebugColor color = DebugColor.White)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Gizmos.DrawLine(start, end);
        }
    }

    // Draws a gizmo ray from a start position using direction as offset.
    [Conditional("UNITY_EDITOR")]
    public static void DrawRay(Vector3 start, Vector3 direction, DebugColor color = DebugColor.White)
    {
        DrawLine(start, start + direction, color);
    }

    // Draws a small filled sphere as a point marker.
    // Useful for target positions, hit points, and waypoints.
    [Conditional("UNITY_EDITOR")]
    public static void DrawPoint(Vector3 position, float size = 0.1f, DebugColor color = DebugColor.Yellow)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Gizmos.DrawSphere(position, size);
        }
    }

    // Draws a filled sphere.
    // Useful for visualizing detection areas or overlap checks.
    [Conditional("UNITY_EDITOR")]
    public static void DrawSphere(Vector3 center, float radius, DebugColor color = DebugColor.Cyan)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Gizmos.DrawSphere(center, radius);
        }
    }

    // Draws a wire sphere.
    // Better than filled sphere when you do not want to block scene visibility.
    [Conditional("UNITY_EDITOR")]
    public static void DrawWireSphere(Vector3 center, float radius, DebugColor color = DebugColor.Cyan)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Gizmos.DrawWireSphere(center, radius);
        }
    }

    // Draws a filled cube using world position and world size.
    [Conditional("UNITY_EDITOR")]
    public static void DrawCube(Vector3 center, Vector3 size, DebugColor color = DebugColor.Cyan)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Gizmos.DrawCube(center, size);
        }
    }

    // Draws a wire cube using world position and world size.
    // Useful for trigger zones, bounds, and area visualization.
    [Conditional("UNITY_EDITOR")]
    public static void DrawWireCube(Vector3 center, Vector3 size, DebugColor color = DebugColor.Cyan)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Gizmos.DrawWireCube(center, size);
        }
    }

    // Draws Unity Bounds as a wire cube.
    [Conditional("UNITY_EDITOR")]
    public static void DrawBounds(Bounds bounds, DebugColor color = DebugColor.Green)
    {
        DrawWireCube(bounds.center, bounds.size, color);
    }

    // ══════════════════════════════════════════════
    // Gizmos - Rotated Shapes
    // ══════════════════════════════════════════════

    // Draws a filled rotated box.
    // Useful for visualizing oriented hitboxes or custom box checks.
    [Conditional("UNITY_EDITOR")]
    public static void DrawBox(
        Vector3 center,
        Vector3 size,
        Quaternion rotation,
        DebugColor color = DebugColor.Cyan)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        using (new GizmoMatrixScope(Matrix4x4.TRS(center, rotation, Vector3.one)))
        {
            Gizmos.DrawCube(Vector3.zero, size);
        }
    }

    // Draws a wire rotated box.
    // Usually the better choice for hitbox and area debugging.
    [Conditional("UNITY_EDITOR")]
    public static void DrawWireBox(
        Vector3 center,
        Vector3 size,
        Quaternion rotation,
        DebugColor color = DebugColor.Cyan)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        using (new GizmoMatrixScope(Matrix4x4.TRS(center, rotation, Vector3.one)))
        {
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }

    // Draws a camera-like frustum.
    // Useful for vision cones, camera preview areas, or detection volumes.
    [Conditional("UNITY_EDITOR")]
    public static void DrawFrustum(
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        float maxRange,
        float minRange,
        float aspect,
        DebugColor color = DebugColor.Yellow)
    {
        using (new GizmoColorScope(ToUnityColor(color)))
        using (new GizmoMatrixScope(Matrix4x4.TRS(position, rotation, Vector3.one)))
        {
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, maxRange, minRange, aspect);
        }
    }

    // ══════════════════════════════════════════════
    // Gizmos - Path / Lines
    // ══════════════════════════════════════════════

    // Draws connected lines through a list of points.
    // Useful for waypoint routes, patrol paths, or movement previews.
    [Conditional("UNITY_EDITOR")]
    public static void DrawPath(
        IReadOnlyList<Vector3> points,
        DebugColor color = DebugColor.White,
        bool loop = false)
    {
        if (points == null || points.Count < 2)
            return;

        using (new GizmoColorScope(ToUnityColor(color)))
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Gizmos.DrawLine(points[i], points[i + 1]);
            }

            if (loop)
            {
                Gizmos.DrawLine(points[^1], points[0]);
            }
        }
    }

    // Draws a path and marks every point with a small sphere.
    // More readable than DrawPath when debugging waypoint positions.
    [Conditional("UNITY_EDITOR")]
    public static void DrawPathWithPoints(
        IReadOnlyList<Vector3> points,
        DebugColor lineColor = DebugColor.White,
        DebugColor pointColor = DebugColor.Yellow,
        float pointSize = 0.1f,
        bool loop = false)
    {
        if (points == null || points.Count == 0)
            return;

        DrawPath(points, lineColor, loop);

        for (int i = 0; i < points.Count; i++)
        {
            DrawPoint(points[i], pointSize, pointColor);
        }
    }

    // Draws a normalized direction arrow from a position.
    // Useful for forward direction, velocity direction, or target direction.
    [Conditional("UNITY_EDITOR")]
    public static void DrawDirection(
        Vector3 position,
        Vector3 direction,
        float length = 1f,
        DebugColor color = DebugColor.Blue)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        DrawArrow(position, position + direction.normalized * length, color);
    }

    // ══════════════════════════════════════════════
    // Gizmos - Circle / Arc
    // ══════════════════════════════════════════════

    // Draws a horizontal circle using Vector3.up as the normal.
    // Good for radius checks on the XZ plane.
    [Conditional("UNITY_EDITOR")]
    public static void DrawCircle(
        Vector3 center,
        float radius,
        DebugColor color = DebugColor.Cyan,
        int segments = 48)
    {
        DrawCircle(center, Vector3.up, radius, color, segments);
    }

    // Draws a circle on any plane using a custom normal.
    // Increase segments for smoother circles.
    [Conditional("UNITY_EDITOR")]
    public static void DrawCircle(
        Vector3 center,
        Vector3 normal,
        float radius,
        DebugColor color = DebugColor.Cyan,
        int segments = 48)
    {
        if (radius <= 0f)
            return;

        if (segments < 3)
            segments = 3;

        normal = SafeNormal(normal, Vector3.up);

        Vector3 forward = Vector3.Slerp(normal, -normal, 0.5f);
        Vector3 right = Vector3.Cross(normal, forward).normalized;

        if (right.sqrMagnitude <= Mathf.Epsilon)
            right = Vector3.Cross(normal, Vector3.forward).normalized;

        forward = Vector3.Cross(right, normal).normalized;

        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Vector3 previousPoint = center + right * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;

                Vector3 nextPoint =
                    center +
                    (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;

                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }

    // Draws an arc around a center point.
    // Useful for angle limits, attack ranges, vision ranges, or rotation previews.
    [Conditional("UNITY_EDITOR")]
    public static void DrawArc(
        Vector3 center,
        Vector3 normal,
        Vector3 startDirection,
        float angle,
        float radius,
        DebugColor color = DebugColor.Yellow,
        int segments = 32)
    {
        if (radius <= 0f)
            return;

        if (segments < 1)
            segments = 1;

        normal = SafeNormal(normal, Vector3.up);
        startDirection = SafeNormal(startDirection, Vector3.forward);

        using (new GizmoColorScope(ToUnityColor(color)))
        {
            Vector3 previousPoint = center + startDirection.normalized * radius;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Quaternion rotation = Quaternion.AngleAxis(angle * t, normal);
                Vector3 nextDirection = rotation * startDirection.normalized;
                Vector3 nextPoint = center + nextDirection * radius;

                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }

    // Draws a simple cone shape on the XZ plane.
    // Useful for enemy field of view, attack cone, or detection angle.
    [Conditional("UNITY_EDITOR")]
    public static void DrawConeArc(
        Vector3 origin,
        Vector3 direction,
        float angle,
        float distance,
        DebugColor color = DebugColor.Yellow,
        int arcSegments = 32)
    {
        if (distance <= 0f)
            return;

        direction = SafeNormal(direction, Vector3.forward);

        Vector3 leftDirection = Quaternion.Euler(0f, -angle * 0.5f, 0f) * direction;
        Vector3 rightDirection = Quaternion.Euler(0f, angle * 0.5f, 0f) * direction;

        DrawLine(origin, origin + leftDirection * distance, color);
        DrawLine(origin, origin + rightDirection * distance, color);

        DrawArc(
            origin,
            Vector3.up,
            leftDirection,
            angle,
            distance,
            color,
            arcSegments);
    }

    // ══════════════════════════════════════════════
    // Gizmos - Arrow
    // ══════════════════════════════════════════════

    // Draws an arrow from start to end.
    // Useful for direction, velocity, force, and target debugging.
    [Conditional("UNITY_EDITOR")]
    public static void DrawArrow(
        Vector3 start,
        Vector3 end,
        DebugColor color = DebugColor.Green,
        float headLength = 0.25f,
        float headAngle = 25f)
    {
        Vector3 direction = end - start;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        DrawLine(start, end, color);

        Vector3 normalizedDirection = direction.normalized;

        // The head is spread around an axis perpendicular to the shaft. Rotating around world up instead
        // would put the head in the XZ plane, where an arrow drawn in the XY plane hides it edge-on and
        // only a straight up or down arrow still looks like an arrow.
        Vector3 headAxis = SafeNormal(Vector3.Cross(normalizedDirection, Vector3.up), Vector3.forward);

        Vector3 right = Quaternion.AngleAxis(headAngle, headAxis) * -normalizedDirection;
        Vector3 left = Quaternion.AngleAxis(-headAngle, headAxis) * -normalizedDirection;

        DrawLine(end, end + right * headLength, color);
        DrawLine(end, end + left * headLength, color);
    }

    // Draws an arrow using start position and direction offset.
    // Direction includes both direction and length.
    [Conditional("UNITY_EDITOR")]
    public static void DrawArrowRay(
        Vector3 start,
        Vector3 direction,
        DebugColor color = DebugColor.Green,
        float headLength = 0.25f,
        float headAngle = 25f)
    {
        DrawArrow(start, start + direction, color, headLength, headAngle);
    }

    // ══════════════════════════════════════════════
    // Gizmos - Transform Helpers
    // ══════════════════════════════════════════════

    // Draws local transform axes:
    // Red = Right, Green = Up, Blue = Forward.
    // Useful for checking object orientation.
    [Conditional("UNITY_EDITOR")]
    public static void DrawTransformAxes(Transform target, float length = 1f)
    {
        if (target == null)
            return;

        Vector3 position = target.position;

        DrawArrow(position, position + target.right * length, DebugColor.Red);
        DrawArrow(position, position + target.up * length, DebugColor.Green);
        DrawArrow(position, position + target.forward * length, DebugColor.Blue);
    }

    // Draws a local-space box using a Transform as reference.
    // Useful for custom local hitboxes or detection zones.
    [Conditional("UNITY_EDITOR")]
    public static void DrawLocalBounds(
        Transform target,
        Vector3 localCenter,
        Vector3 size,
        DebugColor color = DebugColor.Cyan)
    {
        if (target == null)
            return;

        Vector3 worldCenter = target.TransformPoint(localCenter);

        DrawWireBox(
            worldCenter,
            size,
            target.rotation,
            color);
    }

    // Draws a label using the target Transform's name.
    // Useful for identifying objects in crowded Scene views.
    [Conditional("UNITY_EDITOR")]
    public static void DrawNameLabel(
        Transform target,
        Vector3 offset = default,
        DebugColor color = DebugColor.White,
        int fontSize = 12)
    {
        if (target == null)
            return;

        DrawLabel(
            target.position + offset,
            target.name,
            color,
            fontSize);
    }

    // ══════════════════════════════════════════════
    // Gizmos - Label
    // ══════════════════════════════════════════════
    // Uses UnityEditor.Handles internally.
    // The UnityEditor reference is wrapped in #if UNITY_EDITOR,
    // so it will not break builds.

    // Draws a text label in the Scene view.
    // Useful for displaying object names, state values, or short debug notes.
    [Conditional("UNITY_EDITOR")]
    public static void DrawLabel(
        Vector3 position,
        string text,
        DebugColor color = DebugColor.White,
        int fontSize = 12)
    {
#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(text))
            return;

        UnityEditor.Handles.Label(
            position,
            text,
            CreateLabelStyle(ToUnityColor(color), fontSize));
#endif
    }

    // ══════════════════════════════════════════════
    // Internal Helpers
    // ══════════════════════════════════════════════

    // Centralized log writer so all log methods share the same formatting behavior.
    private static void WriteLog(
        LogType logType,
        object message,
        DebugColor color,
        string tag,
        Object context)
    {
        string text = message == null ? "null" : message.ToString();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            text = $"[{tag}] {text}";
        }

        text = ApplyRichTextColor(text, color);

        switch (logType)
        {
            case LogType.Warning:
                Debug.LogWarning(text, context);
                break;

            case LogType.Error:
            case LogType.Exception:
                Debug.LogError(text, context);
                break;

            default:
                Debug.Log(text, context);
                break;
        }
    }

    // Wraps console text with Unity rich-text color tags.
    private static string ApplyRichTextColor(string text, DebugColor color)
    {
        string hex = ToHexColor(color);

        if (string.IsNullOrEmpty(hex))
            return text;

        return $"<color={hex}>{text}</color>";
    }

    // Converts DebugColor into UnityEngine.Color for Gizmos and Debug.DrawLine.
    public static UnityEngine.Color ToUnityColor(DebugColor color)
    {
        return color switch
        {
            DebugColor.White => UnityEngine.Color.white,
            DebugColor.Gray => new UnityEngine.Color(0.6f, 0.6f, 0.6f),
            DebugColor.Black => UnityEngine.Color.black,
            DebugColor.Red => new UnityEngine.Color(1f, 0.25f, 0.25f),
            DebugColor.Orange => new UnityEngine.Color(1f, 0.55f, 0.1f),
            DebugColor.Yellow => new UnityEngine.Color(1f, 0.85f, 0.25f),
            DebugColor.Green => new UnityEngine.Color(0.35f, 1f, 0.45f),
            DebugColor.Cyan => new UnityEngine.Color(0.35f, 0.9f, 1f),
            DebugColor.Blue => new UnityEngine.Color(0.35f, 0.55f, 1f),
            DebugColor.Purple => new UnityEngine.Color(0.75f, 0.45f, 1f),
            DebugColor.Pink => new UnityEngine.Color(1f, 0.45f, 0.8f),
            _ => UnityEngine.Color.white
        };
    }

    // Converts DebugColor into hex string for Unity Console rich text.
    private static string ToHexColor(DebugColor color)
    {
        return color switch
        {
            DebugColor.White => "#FFFFFF",
            DebugColor.Gray => "#A0A0A0",
            DebugColor.Black => "#000000",
            DebugColor.Red => "#FF5C5C",
            DebugColor.Orange => "#FFA500",
            DebugColor.Yellow => "#FFD966",
            DebugColor.Green => "#6DFF7A",
            DebugColor.Cyan => "#5CE1FF",
            DebugColor.Blue => "#6FA8FF",
            DebugColor.Purple => "#C27CFF",
            DebugColor.Pink => "#FF8AD8",
            _ => null
        };
    }

    // Returns a normalized vector, or a fallback when the input is zero.
    // Prevents broken gizmo drawing caused by invalid directions.
    private static Vector3 SafeNormal(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude <= Mathf.Epsilon
            ? fallback.normalized
            : value.normalized;
    }

#if UNITY_EDITOR
    // Creates a simple Scene view label style.
    // Editor-only because it depends on UnityEditor.
    private static GUIStyle CreateLabelStyle(UnityEngine.Color color, int fontSize)
    {
        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
        {
            fontSize = fontSize
        };

        style.normal.textColor = color;
        return style;
    }
#endif

    // Temporarily changes Gizmos.color, then restores the previous color automatically.
    // This prevents one gizmo draw call from affecting the next one.
    private readonly struct GizmoColorScope : IDisposable
    {
        private readonly UnityEngine.Color previousColor;

        public GizmoColorScope(UnityEngine.Color color)
        {
            previousColor = Gizmos.color;
            Gizmos.color = color;
        }

        public void Dispose()
        {
            Gizmos.color = previousColor;
        }
    }

    // Temporarily changes Gizmos.matrix, then restores the previous matrix automatically.
    // This is needed for rotated gizmo shapes.
    private readonly struct GizmoMatrixScope : IDisposable
    {
        private readonly Matrix4x4 previousMatrix;

        public GizmoMatrixScope(Matrix4x4 matrix)
        {
            previousMatrix = Gizmos.matrix;
            Gizmos.matrix = matrix;
        }

        public void Dispose()
        {
            Gizmos.matrix = previousMatrix;
        }
    }
}