using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MangoFog
{
    public class MangoFogDebug : MonoBehaviour
    {
        public static MangoFogDebug Instance;
        public Transform chunkDebugBoxParent;
        public GameObject chunkDebugBoxPrefab;

        public Text totalRevealersText;

        protected void Awake()
        {
            Instance = this;
        }

		protected void Update()
		{
            totalRevealersText.text = "Total Revealers: " + MangoFogInstance.Instance.GetTotalRevealers().ToString();
        }

		public void CreateDebugBoxes(Dictionary<Vector3, MangoFogChunk> chunks)
		{
            foreach (KeyValuePair<Vector3, MangoFogChunk> chunk in chunks)
            {
                MangoFogDebugBox b = Instantiate(chunkDebugBoxPrefab, chunkDebugBoxParent).GetComponent<MangoFogDebugBox>();
                b.chunk = chunk.Value;
                b.gameObject.SetActive(true);
            }
        }

    }
}

