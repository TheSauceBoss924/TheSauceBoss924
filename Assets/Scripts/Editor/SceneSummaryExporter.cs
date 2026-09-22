// Editor-only. This file lives in an "Editor" folder, so Unity never includes it in a build.
// The #if is a second guard in case it ever gets moved out of that folder.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// SCENE SUMMARY EXPORTER
//
// PURPOSE:
// Writes a plain-text summary of every open scene, so the layout and setup of a scene can be shared and read
// outside of Unity (e.g. pasted into a chat to get help setting up the world streaming system).
//
// HOW TO USE:
// Open the scene(s) you want summarized, then use the menu at the top of the editor:
//   Tools -> Scene Summary -> Export (Compact)    the layout, components, colliders, and inspector references/text fields
//   Tools -> Scene Summary -> Export (Detailed)   the same, plus every number, bool and enum value in your scripts' inspectors
// The summary is copied to the clipboard AND saved to a "SceneSummaries" folder next to the Assets folder
// (outside Assets on purpose, so Unity doesn't import it). The folder opens automatically after exporting.
// If several scenes are open at once, they all go in the same summary.
//
// WHAT'S IN THE SUMMARY:
//   1. OVERVIEW: object counts, missing scripts, which objects are tagged Player, and how many of each component type there are
//   2. REFERENCES INTO PLAYER/CAMERA OBJECTS: every inspector field on a level object that points at the Player, the cameras,
//      the CameraTarget or the WorldStreamer. Those objects move to the Core scene when the level is split up, and Unity can't
//      save references between scenes, so every line in this section is something that will break and need changing
//   3. HIERARCHY: every object, indented like the Hierarchy window, with its position, tag, layer and components.
//      Colliders show their shape, size and whether they're triggers. Your own scripts show their inspector fields.
//
// It only reads the scene. Nothing in the scene is changed.
public static class SceneSummaryExporter
{
    // Arrays longer than this only show their first few elements, so one big list doesn't flood the summary.
    private const int MaxArrayElements = 10;

    // Objects with one of these components (or tagged "Player") are the ones that will live in the Core scene.
    // Matched by type name, so this file doesn't need a reference to Cinemachine or any of our own scripts.
    private static readonly HashSet<string> CoreComponentNames = new HashSet<string>
    {
        "Camera", "CinemachineCamera", "CinemachineVirtualCamera", "CinemachineBrain", "CinemachineConfiner2D",
        "CameraTarget", "WorldStreamer", "DevModeManager", "EventSystem"
    };

    [MenuItem("Tools/Scene Summary/Export (Compact)")]
    private static void ExportCompact() => Export(detailed: false);

    [MenuItem("Tools/Scene Summary/Export (Detailed)")]
    private static void ExportDetailed() => Export(detailed: true);

    private static void Export(bool detailed)
    {
        // Every scene currently open in the Hierarchy
        var scenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded) scenes.Add(scene);
        }
        if (scenes.Count == 0)
        {
            Debug.LogWarning("[SceneSummary] No scene is open.");
            return;
        }

        // Every GameObject in those scenes, including inactive ones and children
        var allObjects = new List<GameObject>();
        foreach (Scene scene in scenes)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    allObjects.Add(t.gameObject);
                }
            }
        }

        // With more than one scene open, object paths are prefixed with their scene name so it's clear where they live
        bool multiScene = scenes.Count > 1;
        HashSet<GameObject> coreObjects = FindCoreObjects(allObjects);

        var sb = new StringBuilder();
        WriteHeader(sb, scenes, detailed);
        WriteOverview(sb, allObjects, multiScene);
        WriteCoreReferences(sb, allObjects, coreObjects, multiScene);

        foreach (Scene scene in scenes)
        {
            sb.AppendLine();
            sb.AppendLine($"=== HIERARCHY: {scene.name} ===");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                WriteObject(sb, root, 0, detailed, multiScene);
            }
        }

        // Save next to the Assets folder (not inside it), copy to the clipboard, and open the folder
        string text = sb.ToString();
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SceneSummaries");
        Directory.CreateDirectory(folder);
        string fileName = $"{string.Join("+", scenes.Select(s => s.name))}_{(detailed ? "detailed" : "compact")}.txt";
        string path = Path.Combine(folder, fileName);
        File.WriteAllText(path, text);

        EditorGUIUtility.systemCopyBuffer = text;
        Debug.Log($"[SceneSummary] Exported {allObjects.Count} objects ({text.Length / 1024} KB) to {path}. It's also been copied to the clipboard.");
        EditorUtility.RevealInFinder(path);
    }

    // SECTIONS

    private static void WriteHeader(StringBuilder sb, List<Scene> scenes, bool detailed)
    {
        sb.AppendLine("SCENE SUMMARY");
        sb.AppendLine($"Scenes: {string.Join(", ", scenes.Select(s => s.name))}");
        sb.AppendLine($"Unity {Application.unityVersion}, {(detailed ? "detailed" : "compact")} export, {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("Positions are world positions. [inactive] = GameObject turned off, [disabled] = component turned off.");
    }

    private static void WriteOverview(StringBuilder sb, List<GameObject> allObjects, bool multiScene)
    {
        var componentCounts = new Dictionary<string, int>();
        int inactive = 0;
        int missingScripts = 0;

        foreach (GameObject go in allObjects)
        {
            if (!go.activeInHierarchy) inactive++;

            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null)
                {
                    missingScripts++;
                    continue;
                }
                if (c is Transform) continue;

                string typeName = c.GetType().Name;
                componentCounts[typeName] = componentCounts.TryGetValue(typeName, out int n) ? n + 1 : 1;
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== OVERVIEW ===");
        sb.AppendLine($"GameObjects: {allObjects.Count} ({inactive} inactive)");
        if (missingScripts > 0)
        {
            sb.AppendLine($"Missing scripts: {missingScripts}");
        }

        List<string> players = allObjects.Where(go => go.CompareTag("Player")).Select(go => GetPath(go, multiScene)).ToList();
        sb.AppendLine($"Tagged Player: {(players.Count > 0 ? string.Join(", ", players) : "none")}");

        sb.AppendLine("Components:");
        foreach (var pair in componentCounts.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
        {
            sb.AppendLine($"  {pair.Key}: {pair.Value}");
        }
    }

    // Lists inspector references from level objects to objects that will move to Core (player, cameras, etc.),
    // since those references break once they're in different scenes.
    private static void WriteCoreReferences(StringBuilder sb, List<GameObject> allObjects, HashSet<GameObject> coreObjects, bool multiScene)
    {
        sb.AppendLine();
        sb.AppendLine("=== REFERENCES INTO PLAYER/CAMERA OBJECTS ===");
        sb.AppendLine("(Inspector fields on level objects that point at things that will move to the Core scene. These break when the level is split.)");

        int found = 0;
        foreach (GameObject go in allObjects)
        {
            if (coreObjects.Contains(go)) continue;

            foreach (Component c in go.GetComponents<Component>())
            {
                // Only scripts hold references like this. Built-in components (tilemaps especially) have huge
                // internal data that would make this very slow to scan.
                if (!(c is MonoBehaviour)) continue;

                var serialized = new SerializedObject(c);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                    Object target = property.objectReferenceValue;
                    GameObject targetObject = target is Component targetComponent ? targetComponent.gameObject : target as GameObject;
                    if (targetObject == null || !coreObjects.Contains(targetObject)) continue;

                    sb.AppendLine($"  {GetPath(go, multiScene)} -> {c.GetType().Name}.{property.propertyPath} -> {DescribeReference(target, multiScene)}");
                    found++;
                }
            }
        }

        if (found == 0)
        {
            sb.AppendLine("  None found.");
        }
    }

    // Writes one object's line, its components, then all of its children (indented one level further)
    private static void WriteObject(StringBuilder sb, GameObject go, int depth, bool detailed, bool multiScene)
    {
        string indent = new string(' ', depth * 2);
        Transform t = go.transform;

        var line = new StringBuilder($"{indent}- {go.name}");
        if (!go.activeSelf) line.Append(" [inactive]");
        if (!go.CompareTag("Untagged")) line.Append($" tag={go.tag}");
        if (go.layer != 0) line.Append($" layer={LayerMask.LayerToName(go.layer)}");
        line.Append($" pos({t.position.x:0.##}, {t.position.y:0.##})");

        float rotation = t.eulerAngles.z;
        if (Mathf.Abs(Mathf.DeltaAngle(0f, rotation)) > 0.01f) line.Append($" rot={rotation:0.#}");

        Vector3 scale = t.localScale;
        if (scale != Vector3.one) line.Append($" scale({scale.x:0.##}, {scale.y:0.##})");

        if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
        {
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (prefab != null) line.Append($" prefab={prefab.name}");
        }
        sb.AppendLine(line.ToString());

        foreach (Component c in go.GetComponents<Component>())
        {
            if (c is Transform) continue;
            sb.AppendLine($"{indent}    * {DescribeComponent(c, detailed, multiScene)}");
        }

        foreach (Transform child in t)
        {
            WriteObject(sb, child.gameObject, depth + 1, detailed, multiScene);
        }
    }

    // COMPONENT DESCRIPTIONS

    private static string DescribeComponent(Component c, bool detailed, bool multiScene)
    {
        if (c == null) return "MISSING SCRIPT";

        string name = c.GetType().Name;
        bool disabled = (c is Behaviour behaviour && !behaviour.enabled) || (c is Renderer renderer && !renderer.enabled);

        // The details that matter most for each kind of component
        string details = c switch
        {
            BoxCollider2D box => $"{Trigger(box)}size({V(box.size)}) offset({V(box.offset)})",
            CircleCollider2D circle => $"{Trigger(circle)}radius={circle.radius:0.##}",
            CapsuleCollider2D capsule => $"{Trigger(capsule)}size({V(capsule.size)})",
            PolygonCollider2D polygon => $"{Trigger(polygon)}paths={polygon.pathCount} points={polygon.GetTotalPointCount()} worldSize({V(polygon.bounds.size)})",
            CompositeCollider2D composite => $"{Trigger(composite)}geometry={composite.geometryType} paths={composite.pathCount} worldSize({V(composite.bounds.size)})",
            Collider2D collider => Trigger(collider).TrimEnd(),
            Rigidbody2D body => $"bodyType={body.bodyType}{(body.simulated ? "" : " simulated=false")}",
            SpriteRenderer sprite => $"sprite={(sprite.sprite != null ? sprite.sprite.name : "None")} sortingLayer={sprite.sortingLayerName} order={sprite.sortingOrder}",
            TilemapRenderer tilemapRenderer => $"sortingLayer={tilemapRenderer.sortingLayerName} order={tilemapRenderer.sortingOrder}",
            Tilemap tilemap => $"tiles={tilemap.GetUsedTilesCount()} cellBounds={tilemap.cellBounds.size.x}x{tilemap.cellBounds.size.y}",
            Camera camera => camera.orthographic ? $"orthographic size={camera.orthographicSize:0.##}" : "perspective",
            MonoBehaviour script => DescribeFields(script, detailed, multiScene),
            _ => ""
        };

        string result = disabled ? $"{name} [disabled]" : name;
        return string.IsNullOrEmpty(details) ? result : $"{result} {details}";
    }

    // Lists a script's inspector fields. Compact shows references and text only, detailed shows everything.
    private static string DescribeFields(MonoBehaviour script, bool detailed, bool multiScene)
    {
        var parts = new List<string>();
        var serialized = new SerializedObject(script);
        SerializedProperty property = serialized.GetIterator();

        // NextVisible(true) the first time steps into the object's fields, then NextVisible(false)
        // moves across the top-level fields without diving into each one
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script") continue;

            string value = FormatValue(property, detailed, multiScene);
            if (value != null) parts.Add($"{property.name}={value}");
        }

        return string.Join(", ", parts);
    }

    // Turns one inspector field into text. Returns null when the field should be left out of the summary.
    private static string FormatValue(SerializedProperty property, bool detailed, bool multiScene)
    {
        // Arrays and lists (strings count as arrays of characters internally, so they're excluded here)
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            int count = property.arraySize;
            if (count == 0) return detailed ? "[]" : null;

            // Compact mode only shows arrays of references or text, the things that matter for scene setup
            SerializedPropertyType elementType = property.GetArrayElementAtIndex(0).propertyType;
            bool relevant = elementType == SerializedPropertyType.ObjectReference || elementType == SerializedPropertyType.String;
            if (!detailed && !relevant) return null;

            var items = new List<string>();
            for (int i = 0; i < Mathf.Min(count, MaxArrayElements); i++)
            {
                items.Add(FormatValue(property.GetArrayElementAtIndex(i), true, multiScene) ?? "?");
            }
            string more = count > MaxArrayElements ? $", ...{count - MaxArrayElements} more" : "";
            return $"[{string.Join(", ", items)}{more}]";
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                if (property.objectReferenceValue == null) return detailed ? "None" : null;
                return DescribeReference(property.objectReferenceValue, multiScene);

            case SerializedPropertyType.String:
                if (string.IsNullOrEmpty(property.stringValue)) return detailed ? "\"\"" : null;
                return $"\"{property.stringValue}\"";
        }

        // Everything below is only shown in the detailed export
        if (!detailed) return null;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Integer: return property.intValue.ToString();
            case SerializedPropertyType.Float: return property.floatValue.ToString("0.###");
            case SerializedPropertyType.Enum:
                string[] names = property.enumDisplayNames;
                int index = property.enumValueIndex;
                return index >= 0 && index < names.Length ? names[index] : property.intValue.ToString();
            case SerializedPropertyType.Vector2: return $"({V(property.vector2Value)})";
            case SerializedPropertyType.Vector3: return property.vector3Value.ToString("0.##");
            case SerializedPropertyType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(property.colorValue);
            case SerializedPropertyType.LayerMask: return property.intValue.ToString();
            case SerializedPropertyType.Generic: return "{...}";
            default: return property.propertyType.ToString();
        }
    }

    // Describes what a reference field points at: a component or object in the scene, or an asset in the project
    private static string DescribeReference(Object target, bool multiScene)
    {
        if (target is Component component && component.gameObject.scene.IsValid())
        {
            return $"{component.GetType().Name} on '{GetPath(component.gameObject, multiScene)}'";
        }
        if (target is GameObject go && go.scene.IsValid())
        {
            return $"'{GetPath(go, multiScene)}'";
        }
        return $"{target.GetType().Name} asset '{target.name}'";
    }

    // HELPERS

    // Finds the objects that will live in the Core scene (and all their children)
    private static HashSet<GameObject> FindCoreObjects(List<GameObject> allObjects)
    {
        var core = new HashSet<GameObject>();
        foreach (GameObject go in allObjects)
        {
            bool isCore = go.CompareTag("Player")
                || go.GetComponents<Component>().Any(c => c != null && CoreComponentNames.Contains(c.GetType().Name));
            if (!isCore) continue;

            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                core.Add(t.gameObject);
            }
        }
        return core;
    }

    // Full hierarchy path of an object, e.g. "Level/Rooms/Fire_01"
    private static string GetPath(GameObject go, bool multiScene)
    {
        var names = new List<string>();
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            names.Add(t.name);
        }
        names.Reverse();

        string path = string.Join("/", names);
        return multiScene ? $"{go.scene.name}:{path}" : path;
    }

    private static string Trigger(Collider2D collider) => collider.isTrigger ? "isTrigger " : "";

    private static string V(Vector2 v) => $"{v.x:0.##}, {v.y:0.##}";
}
#endif
