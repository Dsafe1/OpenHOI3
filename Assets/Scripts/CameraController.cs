using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    float minZoom = 0.1f;
    float maxZoom = 3;
    float zoomSpeed = 20f;
    Camera camera;
    float panSpeed = 5.0f;

    // Start is called before the first frame update
    void Start()
    {
        camera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        //Mouse Zoom
        if (Input.GetAxis("Mouse ScrollWheel") > 0)
            ZoomOrthoCamera(Camera.main.ScreenToWorldPoint(Input.mousePosition), zoomSpeed);
        else if (Input.GetAxis("Mouse ScrollWheel") < 0)
            ZoomOrthoCamera(Camera.main.ScreenToWorldPoint(Input.mousePosition), -zoomSpeed);

        //Disable borders when too far
        if (camera.orthographicSize > 0.75f)
            GameObject.Find("BorderMap").GetComponent<MeshRenderer>().enabled = false;
        else
            GameObject.Find("BorderMap").GetComponent<MeshRenderer>().enabled = true;

        //Mouse Drag
        if (Input.GetMouseButton(2))
        {
            //Speed based on zoom
            float tSpeed = Time.deltaTime * -Mathf.Lerp(panSpeed * 0.05f, panSpeed * 1.9f, camera.orthographicSize / Mathf.Abs(minZoom - maxZoom));
            Camera.main.transform.position += new Vector3(Input.GetAxis("Mouse X") * tSpeed, Input.GetAxis("Mouse Y") * tSpeed, 0);
        }

    }

    void ZoomOrthoCamera(Vector3 zoomTowards, float amount)
    {
        //Amount based on zoom
        amount = Time.deltaTime * Mathf.Lerp(amount * 0.3f, amount * 1.2f, camera.orthographicSize / Mathf.Abs(minZoom - maxZoom));

        //Block at limits
        if (camera.orthographicSize == minZoom && amount > 0)
            return;

        if (camera.orthographicSize == maxZoom && amount < 0)
            return;

        // Calculate how much we will have to move towards the zoomTowards position
        float multiplier = (1.0f / camera.orthographicSize * amount);

        // Move camera
        Camera.main.transform.position += (zoomTowards - Camera.main.transform.position) * multiplier;

        // Zoom camera
        camera.orthographicSize -= amount;

        // Limit zoom
        camera.orthographicSize = Mathf.Clamp(camera.orthographicSize, minZoom, maxZoom);
    }
}
