using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Developer mode manager, used to manage the developer mode in the game.
// It can be used to enable or disable developer mode, and to manage the developer mode settings.

// MUST BE ATTACHED TO A PERSISTENT GAMEOBJECT THAT PERSISTS THROUGH THE ENTIRE GAME
// Toggle developer mode by pressing Shift + F1. When developer mode is enabled,
// the developer mode settings will be displayed in the inspector.

// HOW IT WORKS:

// DevModeManger is a persistent singleton, meaning one instance of it exists for the entire
// game sessin and never gets destroyed when scenes load. It sits in the background
// doing nothing until the user (YOU) toggles it on. Then a panel pops up and shows the hotkeys
// for testing

// SINGLETON:

// The Instance pattern means only one DevModeManager can ever exist (It is a singleton as described above).
// In Awake(), if one already exists somehow when a new one tries to spawn (in a scene reload for exmaple),
// it destroys the duplicate and keeps the orignal. DontDestroyOnLoad() makes sure it survives scene transitions

// THE TOGGLE:

// Every frame in update, it checks if you are still holding shift and if F1 is pressed. If yes, it flips,
// IsEnabled on and off. The entire hoykey and GUI system only runs when IsEnabled is true,
// so there is no overhead things when DevMode is off

// THE GUARDS:

// #if UNITY_EDITOR || DEV_MODE preprocessor directives are used to wrap any code that should only run in developer mode.
// This means that code will not even be compiled into the game when developer mode is disabled.

// COMPONENT CACHING:

// On Awake and every time a new scene is loaded, it calls CachePlayerComponents(), which finds the Player by tag
// and grabs references to PlayerController, StateCotroller, PlayerHealth, and Rigidbody2D. This is why is re-caches on load, since
// the player object is a new instance every time

// INVINCIBILITY:

// This is a two-part system. DevModeManager subscribes to EventHandler.OnPlayerDamage, just like StateController
// and PlayerHealth do. When damge fires, all three receive it at the same time. DevModeManager cannot cancel the event
// mid-flight, which is why there is a direct check inside the StateController.OnPlayerDamage method. If invincibility is on,
// StateController just returns early before it can transition to the HurtState. PlayerHealth.TakeDamage still fires though,
// so if we want the same check to be there too, it will prevent the health from being affected

// GOD MODE:
// God mode is a stronger version of invincibility. Invincibility only blocks the HurtState transition in the StateController,
// meaning health can still technically decrease. God mode adds a second check directly inside PlayerHealth.TakeDamage
// so that health is also completely unaffected

// TELEPORTATION:
// Teleport moves the player's Rigidbody2D position directily to a preset Vector2 coordinate that can be set in the inspector
// The velocity is also reset on teleport so the player does not carry momentum into the new position
// Teleport points can be saved as presets in the Inspector and selected from the panel once the game starts
// There is also a button to save the player's current position as a new preset at runtime, and those presets are saved to PlayerPrefs so they persist between play sessions.
// There is also a button to clear all saved presets from PlayerPrefs in case you want to start fresh
// The saved presets will only show when you are runninig the game in the editor, they will not be included in a build since they are only saved to PlayerPrefs on the actual machine
// You can also manually add them based on what the Debug.Log prints if you prefer

// THE DEBUG PANEL:

// ONGUI is a Unity method that draws immediate-mode UI every frame on top of everything else. It only renders when both IsEnabled
// and _showPanel are true. All the live info it displays is read directly from the cached component references every frame so it is always accurate

// HOTKEYS:

// Toggle ON/OFF : Shift + F1
// TOGGLE INVINCIBILITY : F2
// NEXT SCENE : F3
// RELOAD SCENE : F4
// TOGGLE SLOW MOTION : F5
// TOGGLE DEBUG PANEL : F6
// FULL HEAL : F7
// KILL PLAYER : F8
// FORCE HURT STATE : F9 (goes through EventHandler, so it triggers both StateController and PlayerHealth logic at the same time)
// FORCE IDLE STATE : F10
// TELEPORT TO PRESET : F11
// TOGGLE GOD MODE : F12
// UNLOCK ABILITIES : Digit 1 - 6 (Jump, Wall Jump, Dash, Stretch, Attack, Block respectively)

// Note: Digit 1-6 is just the number keys at the top of the keyboard. This is so Unity can distiguish between those numbers and NumPad numbers


public class DevModeManager : MonoBehaviour
{
  public static DevModeManager Instance { get; private set; }
  public static bool IsEnabled { get; private set; } = false;

  [Header("Settings")]
  [SerializeField] private bool requireShift = true;

  [Header("Player References)")]
  private PlayerController _playerController;
  private StateController _stateController;
  private PlayerHealth _playerHealth;
  private Rigidbody2D _rb;

  // Internal flags for developer mode settings
  private bool _showPanel = false;
  [SerializeField] private float slowMotionScale = 0.25f;
  private bool _isSlowMo = false;

  [SerializeField] private int forceDamageAmount = 1; // the amount of damage to force when using the Force Hurt State hotkey, can be adjusted as needed. Set to 1 by default to ensure the player is still alive when testing

  // Invincibility variables
  private bool _isInvincible = false;
  public bool Invincible => _isInvincible; // used by StateController invincibility check

  // God Mode variables. Again, God Mode is meant to block both the HurtState transition and Health loss
  private bool _isGodMode = false;
  public bool GodMode => _isGodMode; // used by PlayerHealth.TakeDamage check

  // Teleportation variables. A list of preset teleport positions that can be set in the Inspector
  // The selected index tracks which preset is currently selected in the panel
  // Everyone can add/edit positions directly in the inspector (NO TOUCHING THE CODE)
  [Header("Teleportation Presets")]
  [SerializeField] private Vector2[] teleportPresets = { Vector2.zero }; // add preset positions in inspector
  private int _selectedTeleportIndex = 0; // tracks which preset is currently selected in the panel


  private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CachePlayerComponents();
        LoadSavedPresets();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventHandler.OnPlayerDamage += OnPlayerDamage;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventHandler.OnPlayerDamage -= OnPlayerDamage;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEV_MODE
        bool modifier = !requireShift || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

        if (modifier && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            IsEnabled = !IsEnabled;
            _showPanel = IsEnabled; // Show the panel when developer mode is enabled
            Debug.Log($"[DevMode] {(IsEnabled ? "ENABLED" : "DISABLED")}");
        }

        if (IsEnabled)
        {
            HandleHotkeys();
        }
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CachePlayerComponents();
    }

    // Component caching for the player, called on scene load
    private void CachePlayerComponents()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerController = player.GetComponent<PlayerController>();
            _stateController = player.GetComponent<StateController>();
            _playerHealth = player.GetComponent<PlayerHealth>();
            _rb = player.GetComponent<Rigidbody2D>();

        }
    }

    // HOTKEYS FOR DEV MODE SETTINGS

#if UNITY_EDITOR || DEV_MODE
    private void HandleHotkeys()
    {
        if (Keyboard.current.f2Key.wasPressedThisFrame)  ToggleInvincibility();
        if (Keyboard.current.f3Key.wasPressedThisFrame)  LoadNextScene();
        if (Keyboard.current.f4Key.wasPressedThisFrame)  ReloadScene();
        if (Keyboard.current.f5Key.wasPressedThisFrame)  ToggleSlowMotion();
        if (Keyboard.current.f6Key.wasPressedThisFrame)  _showPanel = !_showPanel;
        if (Keyboard.current.f7Key.wasPressedThisFrame)  FullHeal();
        if (Keyboard.current.f8Key.wasPressedThisFrame)  KillPlayer();
        if (Keyboard.current.f9Key.wasPressedThisFrame)  ForceHurtState();
        if (Keyboard.current.f10Key.wasPressedThisFrame) ForceIdleState();
        if (Keyboard.current.f11Key.wasPressedThisFrame) TeleportToPreset(_selectedTeleportIndex);
        if (Keyboard.current.f12Key.wasPressedThisFrame) ToggleGodMode();

        // Ability unlocks via number keys
        if (Keyboard.current.digit1Key.wasPressedThisFrame) UnlockAbility(AbilityID.jump);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) UnlockAbility(AbilityID.wallJump);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) UnlockAbility(AbilityID.dash);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) UnlockAbility(AbilityID.stretch);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) UnlockAbility(AbilityID.attack);
        if (Keyboard.current.digit6Key.wasPressedThisFrame) UnlockAbility(AbilityID.block);
    }
#endif

    // DEV ACTIONS

    // Logs when damaged is blocked. To prevent HurtState, you also need
    // to add a DevModeManager.Instance.Invincible check in StateController.OnPlayerDamage

    private void OnPlayerDamage(int damage)
    {
        if (_isInvincible || _isGodMode)
        {
            Debug.Log("[DevMode] Player damage blocked (Invincibility ON)");
        }
    }

    private void ToggleInvincibility()
    {
        // If God Mode is on, turn it off first before switching to regular invincivbility
        // so thaat only one damage-blocking mode is active at a time
        if (_isGodMode) ToggleGodMode();

        _isInvincible = !_isInvincible;
        Debug.Log($"[DevMode] Invincibility: {_isInvincible}");
    }

    private void ToggleGodMode()
    {
        // If invincibility is on, turn it off firstr before switching to God mode
        if (_isInvincible) _isInvincible = false;

        _isGodMode = !_isGodMode;
        Debug.Log($"[DevMode] God Mode: {_isGodMode}");
    }

    private void ToggleSlowMotion()
    {
        _isSlowMo = !_isSlowMo;
        Time.timeScale = _isSlowMo ? slowMotionScale : 1f;
        Debug.Log($"[DevMode] Slow Motion: {_isSlowMo}");
    }

    private void FullHeal()
    {
        if (_playerHealth == null) return;

        _playerHealth.Heal(999); // clamps to maxHealth internally
        Debug.Log("[DevMode] Player fully healed");
    }

    private void KillPlayer()
    {
        if (_playerHealth == null) return;

        _playerHealth.Death(); // kills the player
        Debug.Log("[DevMode] Player killed");
    }

    // Goes through EventHandler -> PlayerHealth.TakeDamage + StateController.OnPlayerDamage at the same time
    private void ForceHurtState()
    {
        EventHandler.InvokePlayerDamage(forceDamageAmount); // invokes damage, but with low damage to ensure player is still alive. Change value as wanted up above
        Debug.Log("[DevMode] Forced Hurt State through Event Handler");
    }

    private void ForceIdleState()
    {
        if (_stateController == null) return;

        _stateController.ChangeState(_stateController.idleState);
        Debug.Log("[DevMode] Forced Idle State");
    }

    // Teleports the player to the preset position at the given index.
    // Velocity is reset so the player's momentum does not carry over
    // Presets are set in the inspector
    private void TeleportToPreset(int index)
    {
        if (_rb == null) return;
        if (teleportPresets == null || teleportPresets.Length == 0)
        {
            Debug.LogWarning("[DevMode] No teleport presets set!");
            return;
        }

        // Clamp the index so it does not go out of range
        index = Mathf.Clamp(index, 0, teleportPresets.Length - 1);

        _rb.position = teleportPresets[index];
        _rb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms(); // forces the physics engine to accept the new position immediately
        Debug.Log($"[DevMode] Teleported to preset {index}: {teleportPresets[index]}");
    }

    // Saves the player's current position as a new teleport preset at runtime
    // Note: these are NOT saved between play sessions, add them to the Inspector array for permanence
    private void SaveCurrentPositionAsPreset()
    {
        if (_rb == null) return;

        Vector2 currentPos = _rb.position;

        // Add the current position to the presets array.
        // Since arrays are fixed size, we need to resize it first
        System.Array.Resize(ref teleportPresets, teleportPresets.Length + 1);
        teleportPresets[teleportPresets.Length - 1] = currentPos;
        _selectedTeleportIndex = teleportPresets.Length - 1;

        // Save to PlayerPrefs sp it persists after stopping play
        int index = teleportPresets.Length - 1;
        PlayerPrefs.SetFloat($"TeleportPreset_{index}_x", currentPos.x);
        PlayerPrefs.SetFloat($"TeleportPreset_{index}_y", currentPos.y);
        PlayerPrefs.SetInt("TeleportPresetCount", teleportPresets.Length);
        PlayerPrefs.Save();

        Debug.Log($"[DevMode] Saved current position as new preset {index}: {currentPos}");
    }

    private void LoadSavedPresets()
    {
        int count = PlayerPrefs.GetInt("TeleportPresetCount", 0);
        if (count == 0) return;

        teleportPresets = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float x = PlayerPrefs.GetFloat($"TeleportPreset_{i}_x", 0f);
            float y = PlayerPrefs.GetFloat($"TeleportPreset_{i}_y", 0f);
            teleportPresets[i] = new Vector2(x, y);
        }
        Debug.Log($"[DevMode] Loaded {count} teleport presets from PlayerPrefs");
    }

    private void ClearSavedPresets()
    {
        int count = PlayerPrefs.GetInt("TeleportPresetCount", 0);
        for (int i = 0; i < count; i++)
        {
            PlayerPrefs.DeleteKey($"TeleportPreset_{i}_x");
            PlayerPrefs.DeleteKey($"TeleportPreset_{i}_y");
        }
        PlayerPrefs.DeleteKey("TeleportPresetCount");
        PlayerPrefs.Save();
        teleportPresets = new Vector2[] { Vector2.zero};
        _selectedTeleportIndex = 0;
        Debug.Log("[DevMode] Cleared all saved teleport presets from PlayerPrefs");
    }

    private void UnlockAbility(AbilityID id)
    {
        if (_playerController == null) return;

        _playerController.UnlockAbility(id);
        Debug.Log($"[DevMode] Unlocked ability: {id}");
    }

    private void UnlockAllAbilities()
    {
        if (_playerController == null) return;

        foreach (AbilityID id in System.Enum.GetValues(typeof(AbilityID)))
        {
            if (id == AbilityID.NONE) continue; // skip NONE
            _playerController.UnlockAbility(id);
        }
        Debug.Log("[DevMode] Unlocked all abilities");
    }

    private void LoadNextScene()
    {
        int next = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
        SceneManager.LoadScene(next);
        Debug.Log($"[DevMode] Loaded next scene: {SceneManager.GetActiveScene().name}");
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Debug.Log($"[DevMode] Reloaded scene: {SceneManager.GetActiveScene().name}");
    }

/////////// DEBUG PANEL (can be expanded with more settings as needed)///////////
#if UNITY_EDITOR || DEV_MODE
    private void OnGUI()
    {
        if (!IsEnabled || !_showPanel) return;

        // Background of panel. Just colors and stuff, making it look nice
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(10, 10, 490, 600), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(18, 16, 474, 588));

        // DEVELOPER MODE HEADER
        GUIStyle header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        header.normal.textColor = new Color(0.4f, 0.8f, 1f);
        GUILayout.Label("DEVELOPER MODE", header);
        GUILayout.Space(4); // small space after header

        // LIVE STATE INFORMATION
        GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        info.normal.textColor = Color.white;

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(220));

        GUILayout.Label("--- State Info ---", info);
        if (_stateController != null)
        {
            string stateName = _stateController.GetCurrentState()?.GetType().Name ?? "null";
            GUILayout.Label($"State       : {stateName}",                          info);
            GUILayout.Label($"Grounded    : {_stateController.isGrounded}",        info);
            GUILayout.Label($"Wall Dir    : {_stateController.wallDirection}",      info);
            GUILayout.Label($"Coyote      : {_stateController._coyoteTimer:F2}s",  info);
            GUILayout.Label($"Can Coyote  : {_stateController.CanCoyoteJump}",     info);
            GUILayout.Label($"Wall Stick  : {_stateController.IsStickingToWall}",  info);
            GUILayout.Label($"Is Dashing  : {_stateController.IsDashing}",         info);
        }

        if (_rb != null)
        {
            GUILayout.Label($"Velocity    : {_rb.linearVelocity:F2}", info);
        }

        if (_playerController != null)
        {
            GUILayout.Label($"Move Locked : {_playerController.isMovementLocked}", info);
            GUILayout.Label($"Last Dir : {_playerController.LastMoveDirection}", info);
        }
        GUILayout.Label($"Position    : {(_rb != null ? _rb.position.ToString("F1") : "N/A")}", info);
        GUILayout.Label($"Scene : {SceneManager.GetActiveScene().name}", info);
        GUILayout.Label($"FPS {(int)(1f / Time.unscaledDeltaTime)}", info);
        GUILayout.Label($"Time Scale : {Time.timeScale}x", info);

        GUILayout.Space(8); // space before hotkey list

        // TOGGLEABLES
        GUILayout.Label("--- Toggles ---", info);
        if (GUILayout.Button(_isInvincible ? "Invincibility [ON] (F2)" : "Invincibility [OFF] (F2)")) ToggleInvincibility();
        if (GUILayout.Button(_isGodMode ? "God Mode [ON] (F12)" : "God Mode [OFF] (F12)")) ToggleGodMode();
        if (GUILayout.Button(_isSlowMo ? "Slow Motion [ON] (F5)" : "Slow Motion [OFF] (F5)")) ToggleSlowMotion();

        GUILayout.Space(4); // space before scene list

        // SCENE CONTROLS
        GUILayout.Label("--- Scene Controls ---", info);
        if (GUILayout.Button("Load Next Scene (F3)")) LoadNextScene();
        if (GUILayout.Button("Reload Scene (F4)")) ReloadScene();

        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical();

        // HEALTH / STATE CONTROLS
        GUILayout.Label("--- Health / State Controls ---", info);
        if (GUILayout.Button("Full Heal (F7)")) FullHeal();
        if (GUILayout.Button("Kill Player (F8)")) KillPlayer();
        if (GUILayout.Button("Force Hurt State (F9)")) ForceHurtState();
        if (GUILayout.Button("Force Idle State (F10)")) ForceIdleState();

        GUILayout.Space(4); // space

        // TELEPORTATION
        // Arrow buttons cycle through the preset list, telport buttons moves the player
        GUILayout.Label("--- Teleportation ---", info);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(30)))
            _selectedTeleportIndex = Mathf.Max(0, _selectedTeleportIndex - 1);

        // Show the currently sselected preset index and its position
        string presetLabel = (teleportPresets != null && teleportPresets.Length > 0)
            ? $"Preset {_selectedTeleportIndex}: {teleportPresets[_selectedTeleportIndex]:F1}"
            : "No presets set";
        GUILayout.Label(presetLabel, info);

        if (GUILayout.Button(">", GUILayout.Width(30)))
            _selectedTeleportIndex = Mathf.Min(teleportPresets.Length - 1, _selectedTeleportIndex + 1);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Teleport (F11)")) TeleportToPreset(_selectedTeleportIndex);
        if (GUILayout.Button("Save Current Position as Preset")) SaveCurrentPositionAsPreset();
        if (GUILayout.Button("Clear Saved Presets")) ClearSavedPresets();

        GUILayout.Space(4); // space before ability list

        // ABILITY UNLOCKS
        GUILayout.Label("--- Ability Unlocks ---", info);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Jump (1)")) UnlockAbility(AbilityID.jump);
        if (GUILayout.Button("Wall Jump (2)")) UnlockAbility(AbilityID.wallJump);
        if (GUILayout.Button("Dash (3)")) UnlockAbility(AbilityID.dash);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stretch (4)")) UnlockAbility(AbilityID.stretch);
        if (GUILayout.Button("Attack (5)")) UnlockAbility(AbilityID.attack);
        if (GUILayout.Button("Block (6)")) UnlockAbility(AbilityID.block);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Unlock All Abilities")) UnlockAllAbilities();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.Space(6); // space

        GUIStyle hint = new GUIStyle(GUI.skin.label) { fontSize = 10};
        hint.normal.textColor = Color.gray;
        GUILayout.Label("F6 hide panel | Shift+F1 toggle off", hint);

        GUILayout.EndArea();
    }
#endif
}
