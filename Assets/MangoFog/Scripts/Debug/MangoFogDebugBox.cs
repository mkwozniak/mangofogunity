using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MangoFog
{
    public class MangoFogDebugBox : MonoBehaviour
    {
        public MangoFogChunk chunk;
        public Text threadID;
        public Text threadState;
        public Text changeState;
        public Text numRevealersText;

        protected void Update()
        {
			if (chunk)
			{
                threadID.text = "Chunk ID: " + chunk.GetChunkID().ToString();
                //threadState.text = "State: " + chunk.GetFogState().ToString();
                changeState.text = "Change: " + chunk.GetChangeState().ToString();
                numRevealersText.text = "Revealers: Unavailable" ;
			}

        }
    }
}

