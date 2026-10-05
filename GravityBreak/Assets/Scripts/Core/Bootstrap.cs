using UnityEngine;

namespace GravityBreak
{
    /// <summary>
    /// Starts the game in whatever scene is open: press Play in any scene (even an empty one) and this
    /// creates the GameManager. Alternatively add this component to a GameObject yourself.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (GameManager.Instance == null) gameObject.AddComponent<GameManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (GameManager.Instance != null) return;
            new GameObject("GravityBreak").AddComponent<Bootstrap>();
        }
    }
}
