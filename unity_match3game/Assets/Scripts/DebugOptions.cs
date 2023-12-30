using UnityEditor;
using UnityEngine;

// Davor
namespace DefaultNamespace
{
    public class DebugOptions : MonoBehaviour
    {
        // Set to "true" to show debug options and to "false" to hide them
        bool shouldDebugStuffBeShown = false;

        public GameObject debugGO;

        void Start()
        {
            debugGO.SetActive(shouldDebugStuffBeShown);
        }
    }
}