using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MangoFog
{
    public class MangoCameraController : MonoBehaviour
    {
        public Transform target;
        public MangoFogOrientation orientation;

        public int minZoom;
        public int maxZoom;
        public int zoomStep;
        public int cameraZDistance2D;

        public float cameraMoveSpeed3D = 12.0f;

        public Vector3 cameraTargetOffset3D;

        Camera cam;
        protected void Start()
        {
            cam = GetComponent<Camera>();
        }

        protected void Update()
        {
            if(orientation == MangoFogOrientation.Perspective3D)
			{
				if (Input.GetKey(KeyCode.W))
				{
                    transform.position += Vector3.forward * cameraMoveSpeed3D * Time.deltaTime;
				}
                if (Input.GetKey(KeyCode.S))
                {
                    transform.position -= Vector3.forward * cameraMoveSpeed3D * Time.deltaTime;
                }
                if (Input.GetKey(KeyCode.A))
                {
                    transform.position -= Vector3.right * cameraMoveSpeed3D * Time.deltaTime;
                }
                if (Input.GetKey(KeyCode.D))
                {
                    transform.position += Vector3.right * cameraMoveSpeed3D * Time.deltaTime;
                }
                if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                {
                    if (cam.transform.position.y > minZoom)
                    {
                        cam.transform.position -= new Vector3(0, zoomStep, 0);
                    }
                }
                else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                {
                    if (cam.transform.position.y < maxZoom)
                    {
                        cam.transform.position += new Vector3(0, zoomStep, 0);
                    }
                }
            }
			else
			{
                if(target)              
                    transform.position = new Vector3(target.position.x, target.position.y, cameraZDistance2D);
                if (Input.GetAxis("Mouse ScrollWheel") > 0f)
                {
                    if (cam.orthographicSize > minZoom)
                    {
                        cam.orthographicSize -= zoomStep;
                    }
                }
                else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
                {
                    if (cam.orthographicSize < maxZoom)
                    {
                        cam.orthographicSize += zoomStep;
                    }
                }
            }


        }
    }
}

