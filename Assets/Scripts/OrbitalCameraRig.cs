using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all camera logic: orbiting, zooming, and initial framing.
/// </summary>
public class OrbitalCameraRig : MonoBehaviour
{
	private Camera mainCamera;
	private Mouse mouse;
	private GameObject planetToOrbit;

	void Start()
	{
		mainCamera = Camera.main;
		mouse = Mouse.current;
	}

	void Update()
	{
		HandleCameraOrbitAndZoom();
	}

	/// <summary>
	/// Points the camera at the planet and sets an initial distance.
	/// </summary>
	public void FramePlanet(GameObject planet)
	{
		if (mainCamera == null || planet == null) return;

		planetToOrbit = planet;

		float planetWorldRadius = planet.transform.lossyScale.x * 0.5f;
		float dist = planetWorldRadius * 6.5f;
		Vector3 target = planet.transform.position;
		Vector3 dir = (new Vector3(0, 0.50f, 1)).normalized; 
        
		mainCamera.transform.position = target + dir * dist;
		mainCamera.transform.LookAt(target, Vector3.up);
        
		if (mainCamera.orthographic == false)
			mainCamera.fieldOfView = 45f;
	}

	void HandleCameraOrbitAndZoom()
	{
		if (mainCamera == null || planetToOrbit == null) return;

		// Orbit with right mouse button
		if (mouse != null && mouse.rightButton.isPressed)
		{
			Vector2 delta = mouse.delta.ReadValue();
			float rotSpeed = 60f * Time.deltaTime;

			mainCamera.transform.RotateAround(planetToOrbit.transform.position, Vector3.up, -delta.x * rotSpeed);
			mainCamera.transform.RotateAround(planetToOrbit.transform.position, mainCamera.transform.right, -delta.y * rotSpeed);
			mainCamera.transform.LookAt(planetToOrbit.transform.position);
		}

		// Zoom with scroll
		float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
		if (Mathf.Abs(scroll) > 0.01f)
		{
			Vector3 zoomDir = mainCamera.transform.forward * (scroll > 0 ? 0.5f : -0.5f);
			mainCamera.transform.position += zoomDir;
		}
	}
}
