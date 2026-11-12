using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only save tools, found in the top menu bar under Tools -> Save System.
// They work whether or not the game is playing, so you can reset your save without pressing Play first.
//
//   Delete Save File       - deletes the save file. While playing it also clears what's held in memory
//                            (SaveManager and GameFlags) and reloads the scene, so you start fresh straight away.
//   Saving Enabled         - tick/untick to turn the whole save system on or off for testing. This is the same switch as
//                            Saving [ON]/[OFF] on the Dev Mode panel (SaveManager.SavingDisabled).
//   Open Save Folder       - opens the folder the save file lives in.
//   Log Save File Contents - prints the save file to the Console, so you can see exactly what was saved.
//
// This file must stay in a folder named Editor, so it is never included in a build.
public static class SaveTools
{
    private const string MenuRoot = "Tools/Save System/";
    private const string SavingEnabledMenu = MenuRoot + "Saving Enabled";

    // Must match PathToFile in SaveManager (that one is private, so it's repeated here)
    private static string SaveFilePath => Application.persistentDataPath + "/GameSaveFile.json";

    [MenuItem(MenuRoot + "Delete Save File", priority = 0)]
    private static void DeleteSaveFile()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Delete save file?",
            "This deletes all saved progress (checkpoint, flags, opened doors). It can't be undone.",
            "Delete",
            "Cancel");
        if (!confirmed) return;

        if (Application.isPlaying)
        {
            // Clear the copies held in memory too, otherwise the next checkpoint would write the old progress straight back,
            // then reload the scene so everything in it starts fresh
            SaveManager.DeleteSave();
            GameFlags.ClearAll();
            EditorSceneManager.LoadSceneInPlayMode(
                SceneManager.GetActiveScene().path,
                new LoadSceneParameters(LoadSceneMode.Single));
            Debug.Log("[SaveTools] Save deleted and scene reloaded");
            return;
        }

        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            Debug.Log($"[SaveTools] Deleted save file at {SaveFilePath}");
        }
        else
        {
            Debug.Log("[SaveTools] There was no save file to delete");
        }
    }

    [MenuItem(SavingEnabledMenu, priority = 20)]
    private static void ToggleSavingEnabled()
    {
        SaveManager.SavingDisabled = !SaveManager.SavingDisabled;
        Debug.Log($"[SaveTools] Saving: {(SaveManager.SavingDisabled ? "OFF" : "ON")}");
    }

    // Runs when the menu opens, to show the tick next to Saving Enabled
    [MenuItem(SavingEnabledMenu, true)]
    private static bool ToggleSavingEnabledValidate()
    {
        Menu.SetChecked(SavingEnabledMenu, !SaveManager.SavingDisabled);
        return true;
    }

    [MenuItem(MenuRoot + "Open Save Folder", priority = 40)]
    private static void OpenSaveFolder()
    {
        // Highlights the save file if there is one, otherwise just opens the folder
        EditorUtility.RevealInFinder(File.Exists(SaveFilePath) ? SaveFilePath : Application.persistentDataPath);
    }

    [MenuItem(MenuRoot + "Log Save File Contents", priority = 41)]
    private static void LogSaveFileContents()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.Log("[SaveTools] There is no save file yet");
            return;
        }

        Debug.Log($"[SaveTools] {SaveFilePath}\n{File.ReadAllText(SaveFilePath)}");
    }
}
