using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main "Director" for the scene.
/// This script holds the configuration, references all other components,
/// and orchestrates the planet generation process.
/// [cite: GeminiUpload/Planet_Generator_Architecture_Review.md]
/// </summary>
[RequireComponent(typeof(PlanetView), typeof(OrbitalCameraRig), typeof(SunRig))]
public class PlanetDirector : MonoBehaviour
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

		oceanicPlateChance = 0.6f,
		plateMapResolutionX = 2048,
		plateMapResolutionY = 1024,

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

	// --- ADDED ---
	[Header("Visuals")]
	[Tooltip("The color of the plate boundary lines.")]
	public Color boundaryLineColor = Color.white;
	// --- END ---

	[Header("UI")]
	[Tooltip("Assign the 'RawImage' for the Tectonic Plate ID Map.")]
	public RawImage debugMapDisplay;

	[Tooltip("Assign the 'RawImage' for the Primordial Noise Map.")]
	public RawImage primordialNoiseMapDisplay;

	// --- Private References ---
	private PlanetView planetView;
	private OrbitalCameraRig cameraRig;
	private SunRig sunRig;

	private PlanetData currentPlanetData;
	private TectonicsComputePipeline computePipeline;

	/// <summary>
	/// Grab references to sibling components
	/// </summary>
	void Awake()
	{
		planetView = GetComponent<PlanetView>();
		cameraRig = GetComponent<OrbitalCameraRig>();
		sunRig = GetComponent<SunRig>();
	}

	void Start()
	{
		// 1. Generate all the CPU data and create empty compute assets
		currentPlanetData = PlanetGenerator.Generate(config);

		// 2. Initialize the compute pipeline
		computePipeline = new TectonicsComputePipeline(
			plateIdShader,
			primordialNoiseShader,
			noiseFrequency,
			noiseAmplitude
		);

		// 3. Run the Compute Shader pipeline to fill the assets
		if (currentPlanetData.HasTectonics)
		{
			computePipeline.Run(currentPlanetData);
		}
        
		// 4. Generate the 3D sphere and visual elements
		planetView.BuildPlanetView(currentPlanetData);

		// 5. Apply overlay materials
		if (currentPlanetData.HasTectonics)
		{
			// --- MODIFIED: Pass color and call the new public method ---
			planetView.boundaryLineColor = boundaryLineColor;
			planetView.ApplyAllOverlays(currentPlanetData);
			// --- END ---
		}

		// 6. Frame the camera
		cameraRig.FramePlanet(planetView.GetPlanet());

		// 7. Update UI
		if (currentPlanetData.HasTectonics)
		{
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
	}

	void OnDestroy()
	{
		if (currentPlanetData != null)
		{
			currentPlanetData.ReleaseComputeAssets();
		}
	}

	public PlanetData GetPlanetData() => currentPlanetData;
}

