using UnityEngine;

/// <summary>
/// Handles the *visual* creation of the planet, moon, and overlays.
/// It is directed by PlanetDirector.
/// </summary>
public class PlanetView : MonoBehaviour
{
	[Header("Visual Settings")]
	[Tooltip("The base scale of the planet in Unity units.")]
	public float planetDisplayScale = 10.0f;
	[Tooltip("If true, creates a slightly larger sphere to test materials on.")]
	public bool showDebugPlanet = false;

	[Header("Materials")]
	public Material planetMaterial; // Base planet material
	public Material moonMaterial;   
	[Tooltip("The material to apply to the debug sphere.")]
	public Material debugPlanetMaterial;

	[Header("Moon Settings")]
	[Tooltip("Moon's orbit radius in local units relative to planet.")]
	public float moonOrbitRadius = 3.0f;
	[Tooltip("Moon's orbit speed in degrees per second.")]
	public float moonOrbitSpeed = 10.0f;

	[Header("Boundary Lines")]
	[Tooltip("Controls the thickness of the plate boundary lines drawn by the shader.")]
	[Range(0.5f, 5.0f)]
	public float boundaryLineThickness = 1.5f;
	[Tooltip("Controls the softness (anti-aliasing) of the boundary line edges.")]
	[Range(0.5f, 3.0f)]
	public float boundaryLineAA = 1.0f;
	[Tooltip("The color of the plate boundary lines.")]
	public Color boundaryLineColor = Color.white; 

	// Private object references
	private GameObject planet;
	private GameObject moon;
	private float moonOrbitAngle = 0f;

	/// <summary>
	/// Creates the planet, moon, and debug sphere GameObjects.
	/// </summary>
	public void BuildPlanetView(PlanetData data)
	{
		GeneratePlanet(data);
        
		if (showDebugPlanet)
		{
			GenerateDebugPlanet();
		}

		if (data.config.hasLargeMoon)
		{
			GenerateMoon();
		}
	}

	/// <summary>
	/// Applies all overlay materials (boundaries, heightmap) to the planet.
	/// </summary>
	public void ApplyAllOverlays(PlanetData data)
	{
		ApplyBoundaryOverlayMaterial(data);
		ApplyHeightmapOverlayMaterial(data);
	}

	void Update()
	{
		UpdateMoonOrbit();
	}

	void GeneratePlanet(PlanetData data)
	{
		planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		planet.name = "Planet";
		planet.transform.position = Vector3.zero; 

		float radius = data.PlanetRadius * planetDisplayScale;
		planet.transform.localScale = Vector3.one * radius;

		Material mat = planetMaterial != null ?
			new Material(planetMaterial) :
			CreateDefaultPlanetMaterial();
        
		mat.color = data.PlanetColor; 
        
		var renderer = planet.GetComponent<MeshRenderer>();
		renderer.material = mat;

		Destroy(planet.GetComponent<Collider>());
	}

	void GenerateDebugPlanet()
	{
		if (planet == null) return;

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

	void GenerateMoon()
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

	void UpdateMoonOrbit()
	{
		if (moon == null) return;
		moonOrbitAngle += moonOrbitSpeed * Time.deltaTime;
		float rad = moonOrbitAngle * Mathf.Deg2Rad;
		moon.transform.localPosition = new Vector3(Mathf.Cos(rad) * moonOrbitRadius, 0f, Mathf.Sin(rad) * moonOrbitRadius);
	}
    
	// --- Material Application ---

	Material CreateDefaultPlanetMaterial()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
		Material mat = new Material(shader);
		mat.color = new Color(0.5f, 0.5f, 0.5f, 1f);
		return mat;
	}

	void ApplyBoundaryOverlayMaterial(PlanetData data)
	{
		if (planet == null || data == null || data.PlateIDTexture == null) return;
		var rend = planet.GetComponent<MeshRenderer>();
		if (rend == null) return;
        
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
        
		var newMats = new Material[mats.Length + 1];
		mats.CopyTo(newMats, 0);
		newMats[newMats.Length - 1] = overlayMat;
		rend.materials = newMats;
	}
    
	void ApplyHeightmapOverlayMaterial(PlanetData data)
	{
		if (planet == null || data == null || data.Heightmap == null) return;
		var rend = planet.GetComponent<MeshRenderer>();
		if (rend == null) return;
        
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
        
		var newMats = new Material[mats.Length + 1];
		mats.CopyTo(newMats, 0);
		newMats[newMats.Length - 1] = overlayMat;
		rend.materials = newMats;
	}

	// Public getter
	public GameObject GetPlanet() => planet;
}
