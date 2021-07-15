using UnityEngine;
using System.Collections;

namespace GameWish.Game
{
    public class ShowFps : MonoBehaviour
    {
        public float f_UpdateInterval = 0.5F;

        private float f_LastInterval;

        private int i_Frames = 0;

        private float f_Fps;

        GUIStyle myStyle;

        private int m_HighQuaRate = 0;
        private int m_LowQuaRate = 0;

        private int m_QuaTag = 1;


        void Start()
        {
            //Application.targetFrameRate=60;

            f_LastInterval = Time.realtimeSinceStartup;

            i_Frames = 0;

            myStyle = new GUIStyle();
            myStyle.fontSize = 36;
            myStyle.normal.textColor = Color.black;
        }

        //#if UNITY_EDITOR
        void OnGUI()
        {
            GUI.Label(new Rect(Screen.width - 200, Screen.height - 36, 200, 200), "FPS:" + f_Fps.ToString("f2"), myStyle);
        }
        // #endif

        void Update()
        {
            ++i_Frames;

            if (Time.realtimeSinceStartup > f_LastInterval + f_UpdateInterval)
            {
                f_Fps = i_Frames / (Time.realtimeSinceStartup - f_LastInterval);

                i_Frames = 0;

                f_LastInterval = Time.realtimeSinceStartup;
            }
        }
    }
}
