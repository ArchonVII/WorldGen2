// [Canvas: PlanetController.cs]

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI; // <-- Make sure this is here!

/// <summary>
/// Main controller for planet generation and visualization.
/// This version keeps the planet stationary and rotates a Directional Light
/// to simulate a day/night cycle and orbit.
/// </summary>
public class PlanetController : MonoBehaviour
{
	[Header("Generation Config")]
	public PlanetGenerationConfig config = new PlanetGenerationConfig
	{
		randomizeOnStart = false,
		planetZone = HabitableZone.Habitable,
		planetRadius = 1.0f,
		planetAgeInBillions = 4.5f,
		waterAbundance = 0.7f,
		hasLargeMoon = true,
		numTectonicPlates = 12,
		
		// Set the new default values here
		oceanicPlateChance = 0.6f,
		plateMapResolutionX = 2048,
		plateMapResolutionY = 1024,
		
		// --- ADDED ---
		planetSeed = 0, // 0 = use fallback logic
		velocityModel = PlateVelocityModel.AxisFlows2, // Default to new model
		minPlateSpeed = 0.5f,
		maxPlateSpeed = 2.0f
	};
	
	[Header("Compute Shaders")]
	[Tooltip("The compute shader that generates the plate ID map.")]
	public ComputeShader plateIdShader;
	
	[Tooltip("The compute shader that generates primordial noise.")]
	public ComputeShader primordialNoiseShader;
	
	[Header("Noise Settings")]
	[Tooltip("The 'zoom' level of the base noise.")]
	public float noiseFrequency = 3.5f;
	[Tooltip("The strength of the base noise.")]
	public float noiseAmplitude = 1.0f;

	// --- MODIFIED SECTION ---
	[Header("Scene Objects")]
	[Tooltip("Assign your 'Sun Light' (Directional Light) from the scene here.")]
	public Light sunLight; // <-- NEW
	
	[Tooltip("The speed the 'sun' rotates around the planet (degrees per second).")]
	public float sunRotationSpeed = 5.0f; // <-- NEW

	public Material planetMaterial; // Base planet material
	public Material moonMaterial;   
	// public Material starMaterial; // <-- REMOVED
	// --- END MODIFIED SECTION ---

	[Header("Boundary Lines")]
	[Tooltip("Controls the thickness of the plate boundary lines drawn by the shader.")]
	[Range(0.5f, 5.0f)]
	public float boundaryLineThickness = 1.5f;

	[Tooltip("Controls the softness (anti-aliasing) of the boundary line edges.")]
	[Range(0.5f, 3.0f)]
	public float boundaryLineAA = 1.0f;

	[Tooltip("The color of the plate boundary lines.")]
	public Color boundaryLineColor = Color.white; 

	[Header("Display Settings")]
	[Tooltip("The base scale of the planet in Unity units.")]
	public float planetDisplayScale = 10.0f;

	// --- REMOVED planetOrbitRadius and planetOrbitSpeed ---

	[Tooltip("Moon's orbit radius in local units relative to planet.")]
	public float moonOrbitRadius = 3.0f;

	[Tooltip("Moon's orbit speed in degrees per second.")]
	public float moonOrbitSpeed = 10.0f;

	[Header("2D Debug")]
	[Tooltip("Assign the 'RawImage' component from your 'MapDisplayPanel' or 'TectonicMapImage' here.")]
	public RawImage debugMapDisplay;
	
	[Tooltip("Assign the 'RawImage' for the Primordial Noise Map.")]
	public RawImage primordialNoiseMapDisplay;
	
	// --- ADD THIS SECTION ---
	[Header("Debug")]
	[Tooltip("If true, creates a slightly larger sphere to test materials on.")]
	public bool showDebugPlanet = false;
	
	[Tooltip("The material to apply to the debug sphere.")]
	public Material debugPlanetMaterial;
	// --- END OF SECTION ---
	
	

	// Private variables
	private GameObject planet;
	private GameObject moon;
	// private GameObject star; // <-- REMOVED
	private float moonOrbitAngle = 0f;
	// private float planetOrbitAngle = 90f; // <-- REMOVED
	private Camera mainCamera;
	private Mouse mouse;
	private PlanetData currentPlanetData;
	// private float starRadius = 10.0f; // <-- REMOVED

	void Start()
	{
		// Get components
		mainCamera = Camera.main;
		mouse = Mouse.current;
		
		// --- MODIFIED ---
		// Check if Sun Light is assigned
		if (sunLight == null)
		{
			Debug.LogError("Sun Light is not assigned in the PlanetController inspector!");
		}

		// --- REMOVED GenerateStar() call ---

		// 1. Generate all the CPU data and create empty compute assets
		currentPlanetData = PlanetGenerator.Generate(config);

		// 2. Run the Compute Shader pipeline to fill the assets
		if (currentPlanetData.HasTectonics)
		{
			RunPlateIDShader(currentPlanetData);
			RunPrimordialNoiseShader(currentPlanetData);
		}
		
		// 3. Generate the 3D sphere
		GeneratePlanet(currentPlanetData); // This now generates at (0,0,0)
		
		// --- ADD THIS IF BLOCK ---
		if (showDebugPlanet)
		{
			GenerateDebugPlanet();
		}
		// --- END OF BLOCK ---

		// Create moon if configured
		if (currentPlanetData.config.hasLargeMoon)
		{
			GenerateMoon();
		}
		
		if (currentPlanetData.HasTectonics)
		{
			ApplyBoundaryOverlayMaterial(currentPlanetData);
			ApplyHeightmapOverlayMaterial(currentPlanetData);

			if (debugMapDisplay != null && currentPlanetData.PlateIDTexture != null)
			{
				debugMapDisplay.texture = currentPlanetData.PlateIDTexture;
				debugMapDisplay.color = Color.white;
			}
			
			if (primordialNoiseMapDisplay != null && currentPlanetData.Heightmap != null)
			{
				primordialNoiseMapDisplay.texture = currentPlanetData.Heightmap;
				primordialNoiseMapDisplay.color = Color.white;
			}
		}

		// Set camera to frame the planet (which is now at 0,0,0)
		FramePlanet();
	}

	void Update()
	{
		HandleCameraOrbitAndZoom();
		
		// --- MODIFIED ---
		RotateSunlight(); // Replaces UpdatePlanetOrbit()
		// --- END MODIFIED ---
		
		UpdateMoonOrbit();
	}
	
	// --- NEW METHOD ---
	/// <summary>
	/// Rotates the assigned Sun Light around the Y-axis to simulate orbit.
	/// </summary>
	void RotateSunlight()
	{
		if (sunLight != null)
		{
			// Rotate the light around the world's UP axis (Vector3.up)
			sunLight.transform.Rotate(Vector3.up, sunRotationSpeed * Time.deltaTime, Space.World);
		}
	}
	// --- END NEW METHOD ---

	void RunPlateIDShader(PlanetData data)
	{
		if (plateIdShader == null || data.TectonicPlatesBuffer == null)
		{
			Debug.LogError("Plate ID Shader or Plate Buffer is missing!");
			return;
		}
		
		int kernel = plateIdShader.FindKernel("CSMain");
		
		plateIdShader.SetBuffer(kernel, "_PlateDataBuffer", data.TectonicPlatesBuffer);
		plateIdShader.SetTexture(kernel, "_PlateIDTexture", data.PlateIDTexture);
		plateIdShader.SetInt("_NumPlates", data.TectonicPlates.Count);
		plateIdShader.SetInts("_Resolution", data.PlateIDTexture.width, data.PlateIDTexture.height);
		
		// Dispatch the shader
		int threadGroupsX = Mathf.CeilToInt(data.PlateIDTexture.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(data.PlateIDTexture.height / 8.0f);
		plateIdShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
		
		Debug.Log("Compute Shader: Generated Plate ID Texture.");
	}
	

	void RunPrimordialNoiseShader(PlanetData data)
	{
		if (primordialNoiseShader == null || data.Heightmap == null)
		{
			Debug.LogError("Primordial Noise Shader or Heightmap is missing!");
			return;
		}
		
		int kernel = primordialNoiseShader.FindKernel("CSMain");
		
		primordialNoiseShader.SetTexture(kernel, "_Heightmap", data.Heightmap);
		primordialNoiseShader.SetInts("_Resolution", data.Heightmap.width, data.Heightmap.height);
		primordialNoiseShader.SetFloat("_Frequency", noiseFrequency);
		primordialNoiseShader.SetFloat("_Amplitude", noiseAmplitude);
		
		// Dispatch the shader
		int threadGroupsX = Mathf.CeilToInt(data.Heightmap.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(data.Heightmap.height / 8.0f);
		primordialNoiseShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
		
		Debug.Log("Compute Shader: Generated Primordial Noise.");
	}


	void HandleCameraOrbitAndZoom()
	{
		if (mainCamera == null || planet == null) return;

		// Orbit with right mouse button
		if (mouse != null && mouse.rightButton.isPressed)
		{
			Vector2 delta = mouse.delta.ReadValue();
			float rotSpeed = 60f * Time.deltaTime;

			// 1. Horizontal rotation (Yaw)
			mainCamera.transform.RotateAround(planet.transform.position, Vector3.up, -delta.x * rotSpeed);

			// 2. Vertical rotation (Pitch)
			mainCamera.transform.RotateAround(planet.transform.position, mainCamera.transform.right, -delta.y * rotSpeed);
			
			// Force it to look back at the planet's center
			mainCamera.transform.LookAt(planet.transform.position);
		}

		// Zoom with scroll
		float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
		if (Mathf.Abs(scroll) > 0.01f)
		{
			// --- THE TYPO WAS HERE ---
			// I've removed the stray "_Speed_" text
			
			Vector3 zoomDir = mainCamera.transform.forward * (scroll > 0 ? 0.5f : -0.5f);
			mainCamera.transform.position += zoomDir;
		}
	}
	
	
	// --- REMOVED GenerateStar() ---

	// --- MODIFIED METHOD ---
	void GeneratePlanet(PlanetData data)
	{
		planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		planet.name = "Planet";
		
		// --- MODIFIED ---
		// Set position to the center of the scene
		planet.transform.position = Vector3.zero; 
		// --- END MODIFIED ---

		float radius = data.PlanetRadius * planetDisplayScale;
		planet.transform.localScale = Vector3.one * radius;

		// --- REMOVED localPosition set ---

		// Setup material
		Material mat = planetMaterial != null ?
			new Material(planetMaterial) :
			CreateDefaultPlanetMaterial();
		
		// Use the data-driven color
		mat.color = data.PlanetColor; 
		
		var renderer = planet.GetComponent<MeshRenderer>();
		renderer.material = mat;

		// Remove collider (not needed)
		Destroy(planet.GetComponent<Collider>());
	}
	// --- END MODIFIED METHOD ---

	void GenerateDebugPlanet()
	{
		if (planet == null)
		{
			Debug.LogError("Cannot create debug planet because main planet is null!");
			return;
		}

		GameObject debugPlanet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		debugPlanet.name = "DebugPlanet";
		
		// Parent it to the main planet so it inherits its position (0,0,0)
		debugPlanet.transform.SetParent(planet.transform, false);

		// Make it just slightly larger so it renders on top
		debugPlanet.transform.localScale = Vector3.one * 1.01f;

		// Assign the debug material
		var rend = debugPlanet.GetComponent<MeshRenderer>();
		if (debugPlanetMaterial != null)
		{
			rend.material = debugPlanetMaterial;
		}
		else
		{
			// Fallback if no material is assigned
			rend.material = CreateDefaultPlanetMaterial();
			rend.material.color = Color.magenta; // Magenta = "missing material"
		}

		// Clean up
		Destroy(debugPlanet.GetComponent<Collider>());
		
		Debug.Log("Debug Planet overlay created.");
	}

	void GenerateMoon()
	{
		moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		moon.name = "Moon";
		moon.transform.SetParent(planet.transform, false);

		// Put it on the +Z side at a reasonable distance (in planet-local units)
		moon.transform.localPosition = new Vector3(0, 0, moonOrbitRadius);

		// IMPORTANT: ratio, not radius. Keep ~0.27x the planet's diameter.
		moon.transform.localScale = Vector3.one * 0.27f;
		
		Material mat;
		if (moonMaterial != null)
		{
			mat = new Material(moonMaterial);
		}
		else
		{
			mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
			mat.color = new Color(0.8f, 0.8f, 0.8f, 1f);
		}

		moon.GetComponent<MeshRenderer>().material = mat;

		Destroy(moon.GetComponent<Collider>());
	}

	// --- REMOVED UpdatePlanetOrbit() ---

	void UpdateMoonOrbit()
	{
		if (moon == null) return;

		moonOrbitAngle += moonOrbitSpeed * Time.deltaTime;
		float rad = moonOrbitAngle * Mathf.Deg2Rad;

		moon.transform.localPosition = new Vector3(Mathf.Cos(rad) * moonOrbitRadius, 0f, Mathf.Sin(rad) * moonOrbitRadius);
	}

	void FramePlanet()
	{
		if (Camera.main == null || planet == null) return;

		float planetWorldRadius = planet.transform.lossyScale.x * 0.5f;

		float dist = planetWorldRadius * 6.5f;
		Vector3 target = planet.transform.position; // This is now (0,0,0)
		Vector3 dir = (new Vector3(0, 0.50f, 1)).normalized; 
		Camera.main.transform.position = target + dir * dist;
		Camera.main.transform.LookAt(target, Vector3.up);
		
		if (Camera.main.orthographic == false)
			Camera.main.fieldOfView = 45f;
	}

	// === Materials ===

	Material CreateDefaultPlanetMaterial()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
			Shader.Find("Standard");
		Material mat = new Material(shader);
		mat.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Default grey
		return mat;
	}

	
	void ApplyBoundaryOverlayMaterial(PlanetData data)
	{
		if (data == null || data.PlateIDTexture == null)
		{
			Debug.LogError("Overlay: missing PlanetData or PlateIDTexture");
			return;
		}

		var rend = planet.GetComponent<MeshRenderer>();
		if (rend == null)
		{
			Debug.LogError("Overlay: planet MeshRenderer not found");
			return;
		}
		
		var mats = rend.materials;

		var shader = Shader.Find("Unlit/PlateBoundariesAA_v2");
		if (shader == null)
		{
			Debug.LogError("Overlay shader not found: Unlit/PlateBoundariesAA_v2");
			return;
		}

		var overlayMat = new Material(shader);
		overlayMat.SetTexture("_PlateIDTex", data.PlateIDTexture);
		overlayMat.SetFloat("_Thickness", boundaryLineThickness); 
		overlayMat.SetFloat("_AA", boundaryLineAA);                 
		overlayMat.SetColor("_LineColor", boundaryLineColor);       
		overlayMat.SetFloat("_Opacity", 1.0f);
		
		overlayMat.renderQueue = 3100;
		overlayMat.SetInt("_ZWrite", 0);
		overlayMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		
		var newMats = new Material[mats.Length + 1];
		for (int i = 0; i < mats.Length; i++) newMats[i] = mats[i];
		newMats[newMats.Length - 1] = overlayMat;
		rend.materials = newMats;

		Debug.Log($"Overlay material applied on planet renderer. PlateID tex: {data.PlateIDTexture.width}x{data.PlateIDTexture.height}");
	}
	void ApplyHeightmapOverlayMaterial(PlanetData data)
	{
		var rend = planet.GetComponent<MeshRenderer>();
		var mats = rend.materials; 

		var shader = Shader.Find("Unlit/PrimordialNoiseOverlay");
		if (shader == null)
		{
			Debug.LogError("Overlay shader not found: Unlit/PrimordialNoiseOverlay");
			return;
		}

		var overlayMat = new Material(shader);
		overlayMat.SetTexture("_Heightmap", data.Heightmap);
		overlayMat.SetFloat("_Opacity", 0.5f); 

		overlayMat.renderQueue = 3101; 
		overlayMat.SetInt("_ZWrite", 0);
		overlayMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		
		var newMats = new Material[mats.Length + 1];
		for (int i = 0; i < mats.Length; i++) newMats[i] = mats[i];
		newMats[newMats.Length - 1] = overlayMat;
		rend.materials = newMats;
	}
	
	void OnDestroy()
	{
		if (currentPlanetData != null)
		{
			currentPlanetData.ReleaseComputeAssets();
		}
	}

	public GameObject GetPlanet() => planet;
	public PlanetData GetPlanetData() => currentPlanetData;
}