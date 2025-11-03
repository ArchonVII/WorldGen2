using UnityEngine;


/// Handles the rotation of the sun (Directional Light).
public class SunRig : MonoBehaviour
{
	[Tooltip("Assign your 'Sun Light' (Directional Light) from the scene here.")]
	public Light sunLight; 
    
	[Tooltip("The speed the 'sun' rotates around the planet (degrees per second).")]
	public float sunRotationSpeed = 5.0f; 

	void Start()
	{
		if (sunLight == null)
		{
			Debug.LogError("Sun Light is not assigned in the SunRig inspector!", this);
		}
	}

	void Update()
	{
		RotateSunlight();
	}

	void RotateSunlight()
	{
		if (sunLight != null)
		{
			sunLight.transform.Rotate(Vector3.up, sunRotationSpeed * Time.deltaTime, Space.World);
		}
	}
}

