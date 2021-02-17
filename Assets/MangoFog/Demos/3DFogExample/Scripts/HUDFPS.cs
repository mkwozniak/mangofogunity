using UnityEngine;
using UnityEngine.UI;

namespace MangoFog
{
    public class HUDFPS : MonoBehaviour
    {
        float FramesPerSecond;
        public Text UITextFPSObject;

        public float updateInterval = 0.5f;

        private float accum = 0; // FPS accumulated over the interval
        private int frames = 0; // Frames drawn over the interval
        private float timeleft; // Left time for current interval

        void Start()
        {
            if (!UITextFPSObject)
            {
                Debug.Log("UtilityFramesPerSecond needs a Text component!");
                enabled = false;
                return;
            }
            timeleft = updateInterval;
        }

        void Update()
        {
            timeleft -= Time.deltaTime;
            accum += Time.timeScale / Time.deltaTime;
            ++frames;

            // Interval ended - update GUI text and start new interval
            if (timeleft <= 0.0)
            {
                // display two fractional digits (f2 format)
                FramesPerSecond = accum / frames;
                string format = System.String.Format("{0:F0} FPS", FramesPerSecond);
                UITextFPSObject.text = format;

                if (FramesPerSecond < 30)
                    UITextFPSObject.color = Color.yellow;
                else
                    if (FramesPerSecond < 10)
                    UITextFPSObject.color = Color.red;
                else
                    UITextFPSObject.color = Color.green;
                //	DebugConsole.Log(format,level);
                timeleft = updateInterval;
                accum = 0.0F;
                frames = 0;
            }
        }
    }
}
