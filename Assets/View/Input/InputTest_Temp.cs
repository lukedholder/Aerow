using UnityEngine;

namespace Aerow.View.Input
{
    public class InputTest_Temp : MonoBehaviour
    {
        void Update() {
            if (GameInput.OnFoot.Move != Vector2.zero) Debug.Log($"Move {GameInput.OnFoot.Move}"); // WASD
            if (GameInput.OnFoot.JumpPressed) Debug.Log("Jump");                                     // Space
            if (GameInput.Global.ScreenshotPressed) Debug.Log("Screenshot");                         // F12
        }
    }
}
