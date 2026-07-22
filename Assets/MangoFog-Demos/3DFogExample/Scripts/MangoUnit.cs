using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MangoFog
{
    public class MangoUnit : MonoBehaviour
    {
        public bool selected;
        public GameObject selectionBox;
        public NavMeshAgent agent;

        public void SelectUnit()
        {
            selected = true;
        }

        public void DeselectUnit()
        {
            selected = false;
        }

        protected void Update()
        {
            if (Input.GetMouseButtonDown(1) && selected)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hitInfo;
                if (Physics.Raycast(ray, out hitInfo))
                {
                    agent.SetDestination(hitInfo.point);
                }
            }
            if (selected && !selectionBox.gameObject.activeSelf)
                selectionBox.gameObject.SetActive(true);
            else if(!selected && selectionBox.gameObject.activeSelf)
                selectionBox.gameObject.SetActive(false);
        }

		protected void OnMouseOver()
		{
            if (Input.GetMouseButtonDown(0))
            {
                MangoUnitController.Instance.SetCurrentUnit(this);
            }
        }

        protected void OnMouseExit()
        {
        }
    }
}

