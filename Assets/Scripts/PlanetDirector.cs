using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main orchestrator for planet generation (Step 2 Refactor).
/// This component replaces the old PlanetController. It holds the configuration 
/// and directs the other components (View, Rigs, Pipeline) to do their jobs.
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
		planetSeed = 0,
		velocityModel = PlateVelocityModel.AxisFlows2,
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
    
	// --- MODIFIED: Removed the "Scene References" header and public fields ---
    
	[Header("UI")]
	[Tooltip("Assign the 'RawImage' for the Tectonic Plate ID Map.")]
	public RawImage debugMapDisplay;
    
	[Tooltip("Assign the 'RawImage' for the Primordial Noise Map.")]
	public RawImage primordialNoiseMapDisplay;
    
	// --- MODIFIED: These are now private and auto-assigned ---
	private PlanetView planetView;
	private OrbitalCameraRig cameraRig;
	private SunRig sunRig;
    
	// Private state
	private PlanetData currentPlanetData;
	private TectonicsComputePipeline computePipeline;

	// --- NEW METHOD ---
	/// <summary>
	/// Automatically find and assign sibling components.
	/// </summary>
	void Awake()
	{
		planetView = GetComponent<PlanetView>();
		cameraRig = GetComponent<OrbitalCameraRig>();
		sunRig = GetComponent<SunRig>();
        
		if (planetView == null || cameraRig == null || sunRig == null)
		{
			Debug.LogError("PlanetDirector is missing one of its required components (PlanetView, OrbitalCameraRig, or SunRig) on the same GameObject!", this);
		}
	}

	void Start()
	{
		// 1. Generate all the CPU data and create empty compute assets
		currentPlanetData = PlanetGenerator.Generate(config);

		// 2. Create the compute pipeline
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
			planetView.ApplyAllOverlays(currentPlanetData);
		}

		// 6. Frame the camera
		cameraRig.FramePlanet(planetView.GetPlanet());
        
		// 7. Update Debug UI
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
			// (The rest of the Start method is unchanged)
		}

		void OnDestroy()
		{
			if (currentPlanetData != null)
			{
				currentPlanetData.ReleaseComputeAssets();
			}
		}

		// Public getter for other systems
		public PlanetData GetPlanetData() => currentPlanetData;
	}


