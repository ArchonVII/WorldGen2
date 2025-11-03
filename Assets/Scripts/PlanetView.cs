using UnityEngine;

/// <summary>
/// Handles the "View" aspect of the planet.
/// Creates the meshes (planet, moon, debug) and applies materials/overlays.
/// Does not contain any generation logic.
/// [cite: GeminiUpload/Planet_Generator_Architecture_Review.md]
/// </summary>
public class PlanetView : MonoBehaviour
{
	[Header("Base Materials")]
	public Material planetMaterial;
	public Material moonMaterial;
	public Material debugPlanetMaterial;

	[Header("Display Settings")]
	[Tooltip("The base scale of the planet in Unity units.")]
	public float planetDisplayScale = 10.0f;
	[Tooltip("Moon's orbit radius in local units relative to planet.")]
	public float moonOrbitRadius = 3.0f;
	[Tooltip("Moon's orbit speed in degrees per second.")]
	public float moonOrbitSpeed = 10.0f;
	[Tooltip("If true, creates a slightly larger sphere to test materials on.")]
	public bool showDebugPlanet = false;

	// --- NEW: Public field set by PlanetDirector ---
	[HideInInspector] // We don't need to see this in the PlanetView inspector
	public Color boundaryLineColor = Color.white;

	// --- Private ---
	private GameObject planet;
	private GameObject moon;
	private float moonOrbitAngle = 0f;
	private MeshRenderer planetRenderer;

	/// <summary>
	/// Creates the main planet GameObject
	/// </summary>
	public void BuildPlanetView(PlanetData data)
	{
		planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		planet.name = "Planet";
		planet.transform.position = Vector3.zero;
		planet.transform.SetParent(this.transform, false);

		float radius = data.PlanetRadius * planetDisplayScale;
		planet.transform.localScale = Vector3.one * radius;

		// Setup material
		Material mat = planetMaterial != null ?
			new Material(planetMaterial) :
			CreateDefaultPlanetMaterial();

		mat.color = data.PlanetColor;

		planetRenderer = planet.GetComponent<MeshRenderer>();
		planetRenderer.material = mat;
		Destroy(planet.GetComponent<Collider>());

		// --- Create debug planet if needed ---
		if (showDebugPlanet)
		{
			GenerateDebugPlanet();
		}

		// --- Create moon if configured ---
		if (data.config.hasLargeMoon)
		{
			GenerateMoon();
		}
	}
    
	// --- NEW: Public entry point to apply all overlays ---
	public void ApplyAllOverlays(PlanetData data)
	{
		if (planetRenderer == null) return;
        
		// Start with just the base material
		planetRenderer.materials = new Material[] { planetRenderer.material };

		// Apply overlays
		ApplyBoundaryOverlayMaterial(data);
		ApplyHeightmapOverlayMaterial(data);
	}

	void Update()
	{
		UpdateMoonOrbit();
	}
    
	// --- MODIFIED: Now uses BoundaryDeltaTexture ---
	private void ApplyBoundaryOverlayMaterial(PlanetData data)
	{
		if (data == null || data.BoundaryDeltaTexture == null)
		{
			Debug.LogError("Overlay: missing PlanetData or BoundaryDeltaTexture");
			return;
		}
		if (planetRenderer == null) return;

		var mats = planetRenderer.materials;

		var shader = Shader.Find("Unlit/PlateBoundariesAA_v2");
		if (shader == null)
		{
			Debug.LogError("Overlay shader not found: Unlit/PlateBoundariesAA_v2");
			return;
		}

		var overlayMat = new Material(shader);

		// --- MODIFIED: Set the new texture and properties ---
		overlayMat.SetTexture("_BoundaryDeltaTex", data.BoundaryDeltaTexture);
		overlayMat.SetFloat("_Threshold", 0.02f); // Default threshold
		overlayMat.SetFloat("_AA", 0.01f); // Default AA
		overlayMat.SetColor("_LineColor", boundaryLineColor); // Use color from director
		overlayMat.SetFloat("_Opacity", 1.0f);
		// --- END MODIFICATION ---

		overlayMat.renderQueue = 3100;
		overlayMat.SetInt("_ZWrite", 0);
		overlayMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);

		var newMats = new Material[mats.Length + 1];
		for (int i = 0; i < mats.Length; i++) newMats[i] = mats[i];
		newMats[newMats.Length - 1] = overlayMat;
		planetRenderer.materials = newMats;
	}

	private void ApplyHeightmapOverlayMaterial(PlanetData data)
	{
		if (data == null || data.Heightmap == null) return;
		if (planetRenderer == null) return;

		var mats = planetRenderer.materials;

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
		planetRenderer.materials = newMats;
	}


	private void GenerateDebugPlanet()
	{
		if (planet == null)
		{
			Debug.LogError("Cannot create debug planet because main planet is null!");
			return;
		}

		GameObject debugPlanet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		debugPlanet.name = "DebugPlanet";
		debugPlanet.transform.SetParent(planet.transform, false);
		debugPlanet.transform.localScale = Vector3.one * 1.01f;

		var rend = debugPlanet.GetComponent<MeshRenderer>();
		if (debugPlanetMaterial != null)
		{
			rend.material = debugPlanetMaterial;
		}
		else
		{
			rend.material = CreateDefaultPlanetMaterial();
			rend.material.color = Color.magenta;
		}
		Destroy(debugPlanet.GetComponent<Collider>());
	}

	private void GenerateMoon()
	{
		moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		moon.name = "Moon";
		moon.transform.SetParent(planet.transform, false);
		moon.transform.localPosition = new Vector3(0, 0, moonOrbitRadius);
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

	private void UpdateMoonOrbit()
	{
		if (moon == null) return;
		moonOrbitAngle += moonOrbitSpeed * Time.deltaTime;
		float rad = moonOrbitAngle * Mathf.Deg2Rad;
		moon.transform.localPosition = new Vector3(Mathf.Cos(rad) * moonOrbitRadius, 0f, Mathf.Sin(rad) * moonOrbitRadius);
	}

	Material CreateDefaultPlanetMaterial()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
			Shader.Find("Standard");
		Material mat = new Material(shader);
		mat.color = new Color(0.5f, 0.5f, 0.5f, 1f);
		return mat;
	}

	public GameObject GetPlanet() => planet;
}

