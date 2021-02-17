using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MangoFog
{
    public class MangoUnitController : MonoBehaviour
    {
        public static MangoUnitController Instance;
        public MangoUnit currentUnit;

        public void SetCurrentUnit(MangoUnit unit)
		{
            if (currentUnit)
                currentUnit.DeselectUnit();
            currentUnit = unit;
            currentUnit.SelectUnit();
            Camera.main.gameObject.GetComponent<MangoCameraController>().target = currentUnit.transform;
		}

		protected void Awake()
		{
            Instance = this;
		}

		protected void Start()
        {

        }

        protected void Update()
        {

        }
    }
}

